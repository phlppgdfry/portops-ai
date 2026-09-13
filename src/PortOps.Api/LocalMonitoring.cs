using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Routing;

namespace PortOps.Api;

public sealed record TraceStep(string SpanId, string? ParentSpanId, string Operation,
    double OffsetMs, double DurationMs, string Status, IReadOnlyDictionary<string, object> Attributes);
public sealed record RequestTrace(string RequestId, string TraceId, string RootSpanId, string Route,
    string Method, int StatusCode, DateTimeOffset StartedAt, double DurationMs,
    IReadOnlyList<TraceStep> Steps, bool Truncated);
public sealed record MonitoringSnapshot(DateTimeOffset CapturedAt, DateTimeOffset WindowStart,
    int WindowMinutes, int Capacity, int Requests, int ClientErrors, int ServerErrors,
    double? AverageMs, double? P95Ms, int ModelCalls, int ToolCalls, long? InputTokens,
    long? OutputTokens, IReadOnlyList<RequestTrace> Traces);

/// <summary>Bounded per-customer diagnostics. No payload, baggage, raw URL, identity or exception text.</summary>
public sealed class LocalMonitoring : IDisposable
{
    public static readonly ActivitySource Activities = new("PortOps.Api");
    private const string ScopeKey = "PortOps.Internal.MonitoringScope";
    private static readonly HashSet<string> Operations = ["api.request", "agent.investigate", "model.responses",
        "tool.execute", "procedure.search", "planning.overview", "proposal.create", "proposal.refresh", "proposal.approve", "proposal.reject", "proposal.persist"];
    private static readonly HashSet<string> Codes = ["ok", "answered", "insufficient_evidence", "refused", "draft", "executed", "rejected",
        "invalid_request", "not_configured", "configuration", "busy", "timeout", "tool_limit", "turn_limit", "ungrounded_answer",
        "provider_error", "provider_protocol", "provider_incomplete", "provider_http_error", "invalid_arguments", "not_found",
        "unknown_tool", "unavailable", "capacity", "forbidden", "conflict", "expired", "stale", "cancelled", "unexpected"];
    private static readonly HashSet<string> Tools = ["get_attention", "get_planning", "get_vehicle", "get_vessel_call", "search_procedures", "propose_notification", "rejected_tool"];
    private readonly object gate = new();
    private readonly Dictionary<string, Queue<RequestTrace>> customers = new(StringComparer.Ordinal);
    private readonly ActivityListener listener;
    private readonly TimeProvider clock;
    private readonly DateTimeOffset started;
    private readonly int capacity;
    private readonly int maxSteps;
    private readonly TimeSpan retention = TimeSpan.FromHours(1);

    public LocalMonitoring(TimeProvider? clock = null, int capacity = 200, int maxSteps = 64)
    {
        if (capacity is < 1 or > 1000 || maxSteps is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.clock = clock ?? TimeProvider.System;
        this.capacity = capacity;
        this.maxSteps = maxSteps;
        started = this.clock.GetUtcNow();
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name is "PortOps.Api" or "PortOps.Agent" or "PortOps.Domain",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = CaptureStep
        };
        ActivitySource.AddActivityListener(listener);
    }

    internal sealed class RequestScope(LocalMonitoring owner, string customer, Activity root, string route, string method, DateTimeOffset at)
    {
        public LocalMonitoring Owner { get; } = owner;
        public string Customer { get; } = customer;
        public Activity Root { get; } = root;
        public string RequestId { get; } = Guid.NewGuid().ToString("N");
        public string Route { get; } = route;
        public string Method { get; } = method;
        public DateTimeOffset At { get; } = at;
        public List<TraceStep> Steps { get; } = [];
        public bool Truncated { get; set; }
        public bool Completed { get; set; }
    }

    internal RequestScope Begin(string customer, string route, string method)
    {
        var root = Activities.StartActivity("api.request", ActivityKind.Internal)!;
        var scope = new RequestScope(this, customer, root, route, method is "GET" or "POST" ? method : "OTHER", clock.GetUtcNow());
        root.SetCustomProperty(ScopeKey, scope);
        return scope;
    }

    private void CaptureStep(Activity activity)
    {
        if (!Operations.Contains(activity.OperationName)) return;
        RequestScope? scope = null;
        for (var current = activity; current is not null; current = current.Parent)
            if (current.GetCustomProperty(ScopeKey) is RequestScope found) { scope = found; break; }
        if (scope is null || scope.Owner != this) return;
        var attributes = new Dictionary<string, object>();
        foreach (var (key, value) in activity.TagObjects)
        {
            if (key is "gen_ai.usage.input_tokens" or "gen_ai.usage.output_tokens" or "procedure.result_count")
            {
                if (value is int i && i >= 0) attributes[key] = i;
                else if (value is long l && l >= 0) attributes[key] = l;
            }
            else if (key is "error.type" or "tool.status" or "proposal.outcome" or "agent.outcome")
            {
                if (value is string code && Codes.Contains(code)) attributes[key] = code;
            }
            else if (key == "tool.name" && value is string name && Tools.Contains(name)) attributes[key] = name;
            else if (key == "gen_ai.provider.name" && value is "OpenAI" or "AzureOpenAI") attributes[key] = value;
        }
        var step = new TraceStep(activity.SpanId.ToString(), activity == scope.Root ? null : activity.ParentSpanId.ToString(),
            activity.OperationName, Math.Max(0, (activity.StartTimeUtc - scope.Root.StartTimeUtc).TotalMilliseconds),
            Math.Max(0, activity.Duration.TotalMilliseconds), activity.Status == ActivityStatusCode.Error ? "error" : "ok", attributes);
        lock (scope.Steps)
        {
            if (scope.Completed) return;
            if (scope.Steps.Count < maxSteps) scope.Steps.Add(step);
            else
            {
                scope.Truncated = true;
                if (activity == scope.Root) scope.Steps[^1] = step;
            }
        }
    }

    internal void Complete(RequestScope scope, int statusCode)
    {
        if (statusCode >= 400) scope.Root.SetStatus(ActivityStatusCode.Error);
        scope.Root.Stop();
        RequestTrace trace;
        lock (scope.Steps)
        {
            scope.Completed = true;
            trace = new(scope.RequestId, scope.Root.TraceId.ToString(), scope.Root.SpanId.ToString(), scope.Route,
                scope.Method, statusCode, scope.At, scope.Root.Duration.TotalMilliseconds,
                scope.Steps.OrderBy(s => s.OffsetMs).ThenByDescending(s => s.DurationMs).ToArray(), scope.Truncated);
        }
        lock (gate)
        {
            if (!customers.TryGetValue(scope.Customer, out var queue)) customers[scope.Customer] = queue = new();
            queue.Enqueue(trace);
            Trim(queue);
        }
        scope.Root.Dispose();
    }

    public MonitoringSnapshot Snapshot(string customer)
    {
        RequestTrace[] traces;
        lock (gate)
        {
            if (customers.TryGetValue(customer, out var queue)) { Trim(queue); traces = queue.Reverse().ToArray(); }
            else traces = [];
        }
        var durations = traces.Select(t => t.DurationMs).Order().ToArray();
        var steps = traces.SelectMany(t => t.Steps).ToArray();
        var models = steps.Where(s => s.Operation == "model.responses").ToArray();
        long? Tokens(string key) => traces.Any(t => t.Truncated) || models.Length == 0 || models.Any(m => !m.Attributes.ContainsKey(key))
            ? null : models.Sum(m => Convert.ToInt64(m.Attributes[key]));
        var cutoff = clock.GetUtcNow() - retention;
        return new(clock.GetUtcNow(), cutoff > started ? cutoff : started, (int)retention.TotalMinutes, capacity,
            traces.Length, traces.Count(t => t.StatusCode is >= 400 and < 500), traces.Count(t => t.StatusCode >= 500),
            durations.Length == 0 ? null : durations.Average(),
            durations.Length == 0 ? null : durations[(int)Math.Ceiling(durations.Length * .95) - 1],
            models.Length, steps.Count(s => s.Operation == "tool.execute"), Tokens("gen_ai.usage.input_tokens"), Tokens("gen_ai.usage.output_tokens"), traces);
    }

    public RequestTrace? Find(string customer, string requestId) => Snapshot(customer).Traces.SingleOrDefault(t => t.RequestId == requestId);
    private void Trim(Queue<RequestTrace> queue)
    {
        var cutoff = clock.GetUtcNow() - retention;
        // Completion order may differ from start order for concurrent long-running requests.
        var retained = queue.Where(t => t.StartedAt >= cutoff).TakeLast(capacity).ToArray();
        queue.Clear();
        foreach (var item in retained) queue.Enqueue(item);
    }
    public void Dispose() => listener.Dispose();
}

public sealed class MonitoringMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, LocalMonitoring monitor)
    {
        var customer = context.User.FindFirstValue("customer_id");
        var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
        if (customer is null || route is null || !route.StartsWith("/api/", StringComparison.Ordinal)
            || route.StartsWith("/api/monitoring", StringComparison.Ordinal))
        { await next(context); return; }
        var scope = monitor.Begin(customer, route, context.Request.Method);
        context.Response.Headers["X-PortOps-Trace-Id"] = scope.Root.TraceId.ToString();
        context.Response.Headers["X-PortOps-Request-Id"] = scope.RequestId;
        int? failedStatus = null;
        try { await next(context); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { failedStatus = 499; throw; }
        catch { failedStatus = 500; throw; }
        finally { monitor.Complete(scope, failedStatus ?? context.Response.StatusCode); }
    }
}
