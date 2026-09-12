using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using PortOps.Agent;
using PortOps.Api;
using PortOps.Domain;

namespace PortOps.Tests;

public sealed class MonitoringTests
{
    private static HttpClient Client(DemoFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Theory]
    [InlineData("/api/monitoring")]
    [InlineData("/api/monitoring/export")]
    [InlineData("/api/monitoring/requests/nonexistent")]
    public async Task Monitoring_requires_reviewer_and_never_accepts_role_header(string route)
    {
        await using var factory = new DemoFactory();
        using var unauthenticated = factory.CreateClient();
        using var client = Client(factory, DemoFactory.Northstar);
        client.DefaultRequestHeaders.Add("X-Role", "Reviewer");
        Assert.Equal(HttpStatusCode.Unauthorized, (await unauthenticated.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public async Task Request_ids_match_headers_routes_are_redacted_and_monitoring_does_not_count_itself()
    {
        await using var factory = new DemoFactory();
        using var client = Client(factory, DemoFactory.Reviewer);
        using var response = await client.GetAsync("/api/vehicles/DEMO-003?private-note=never-store-this");
        var snapshot = (await client.GetFromJsonAsync<MonitoringSnapshot>("/api/monitoring"))!;
        var trace = Assert.Single(snapshot.Traces);
        Assert.Equal(response.Headers.GetValues("X-PortOps-Request-Id").Single(), trace.RequestId);
        Assert.Equal(response.Headers.GetValues("X-PortOps-Trace-Id").Single(), trace.TraceId);
        Assert.Equal("/api/vehicles/{id}", trace.Route);
        Assert.Equal(200, trace.StatusCode);
        Assert.True(trace.DurationMs >= 0);
        Assert.Equal("api.request", Assert.Single(trace.Steps).Operation);
        var next = (await client.GetFromJsonAsync<MonitoringSnapshot>("/api/monitoring"))!;
        Assert.Equal(1, next.Requests);
        using var exported = await client.GetAsync("/api/monitoring/export");
        Assert.Equal("application/json", exported.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", exported.Content.Headers.ContentDisposition!.DispositionType);
        var json = await exported.Content.ReadAsStringAsync();
        Assert.DoesNotContain("DEMO-003", json);
        Assert.DoesNotContain("never-store-this", json);
        Assert.DoesNotContain(DemoFactory.Reviewer, json);
        Assert.Contains("no-store", exported.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Same_external_trace_id_cannot_join_or_disclose_another_customer_request()
    {
        await using var factory = new DemoFactory();
        using var own = Client(factory, DemoFactory.Reviewer);
        using var other = Client(factory, DemoFactory.Harborline);
        const string parent = "00-11111111111111111111111111111111-2222222222222222-01";
        own.DefaultRequestHeaders.Add("traceparent", parent);
        other.DefaultRequestHeaders.Add("traceparent", parent);
        other.DefaultRequestHeaders.Add("baggage", "customer_id=northstar");
        var responses = await Task.WhenAll(own.GetAsync("/api/vehicles/DEMO-003"), other.GetAsync("/api/vehicles/DEMO-007"));
        var theirs = responses[1].Headers.GetValues("X-PortOps-Request-Id").Single();
        var snapshot = (await own.GetFromJsonAsync<MonitoringSnapshot>("/api/monitoring?customerId=harborline"))!;
        Assert.Single(snapshot.Traces);
        Assert.DoesNotContain(snapshot.Traces, t => t.RequestId == theirs);
        Assert.Equal(HttpStatusCode.NotFound, (await own.GetAsync("/api/monitoring/requests/" + theirs)).StatusCode);
    }

    [Fact]
    public async Task Missing_model_is_measured_as_unavailable_with_no_invented_usage()
    {
        await using var factory = new DemoFactory();
        using var client = Client(factory, DemoFactory.Reviewer);
        var response = await client.PostAsJsonAsync("/api/agent/investigate", new { message = "sensitive-question-should-not-be-logged" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var snapshot = (await client.GetFromJsonAsync<MonitoringSnapshot>("/api/monitoring"))!;
        Assert.Equal(1, snapshot.ServerErrors);
        Assert.Equal(0, snapshot.ModelCalls);
        Assert.Null(snapshot.InputTokens);
        var trace = Assert.Single(snapshot.Traces);
        var agent = Assert.Single(trace.Steps, s => s.Operation == "agent.investigate");
        Assert.Equal("error", agent.Status);
        Assert.Equal("not_configured", ((JsonElement)agent.Attributes["error.type"]).GetString());
        Assert.DoesNotContain("sensitive-question", JsonSerializer.Serialize(snapshot));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(trace.TraceId, body.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Proposal_trace_contains_nested_search_and_storage_and_records_failed_review()
    {
        await using var factory = new DemoFactory();
        using var client = Client(factory, DemoFactory.Reviewer);
        var response = await client.PostAsJsonAsync("/api/proposals", new { vehicleId = "DEMO-003" });
        var proposal = (await response.Content.ReadFromJsonAsync<ActionProposal>(AgentJson.Options))!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/proposals/{proposal.Id}/approve", new { version = 99, proposal.PayloadHash })).StatusCode);
        var snapshot = (await client.GetFromJsonAsync<MonitoringSnapshot>("/api/monitoring"))!;
        Assert.Equal(2, snapshot.Requests);
        Assert.Equal(1, snapshot.ClientErrors);
        var create = snapshot.Traces.Single(t => t.Route == "/api/proposals");
        var root = create.Steps.Single(s => s.Operation == "api.request");
        var action = create.Steps.Single(s => s.Operation == "proposal.create");
        Assert.Equal(root.SpanId, action.ParentSpanId);
        Assert.Contains(create.Steps, s => s.Operation == "proposal.persist" && s.ParentSpanId == action.SpanId);
        Assert.Contains(create.Steps, s => s.Operation == "procedure.search");
        var failed = snapshot.Traces.Single(t => t.StatusCode == 409);
        Assert.Contains(failed.Steps, s => s.Operation == "proposal.approve" && s.Status == "error");
        Assert.DoesNotContain(proposal.Body, JsonSerializer.Serialize(snapshot));
        Assert.DoesNotContain(proposal.Id, JsonSerializer.Serialize(snapshot));
    }

    [Fact]
    public async Task Real_adapter_with_stubbed_provider_emits_correlated_model_tool_and_token_steps()
    {
        var count = 0;
        var options = ScriptedModel.Options();
        using var provider = new HttpClient(new Handler(_ =>
        {
            var output = Interlocked.Increment(ref count) == 1
                ? """[{"type":"function_call","call_id":"c1","name":"get_vehicle","arguments":"{\"id\":\"DEMO-003\"}"}]"""
                : JsonSerializer.Serialize(new[] { new { type = "message", content = new[] { new { type = "output_text", text = ScriptedModel.Answer().FinalText } } } });
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"completed\",\"output\":" + output + ",\"usage\":{\"input_tokens\":7,\"output_tokens\":3}}") };
        }));
        await using var factory = new DemoFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<IAgentModel>(new ResponsesModel(provider, options));
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", DemoFactory.Reviewer);
        var response = await client.PostAsJsonAsync("/api/agent/investigate", new { message = "Why is DEMO-003 blocked?" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<AgentResult>(AgentJson.Options))!;
        var snapshot = (await client.GetFromJsonAsync<MonitoringSnapshot>("/api/monitoring"))!;
        Assert.Equal(2, snapshot.ModelCalls);
        Assert.Equal(1, snapshot.ToolCalls);
        Assert.Equal(14, snapshot.InputTokens);
        Assert.Equal(6, snapshot.OutputTokens);
        var trace = Assert.Single(snapshot.Traces);
        Assert.Equal(result.TraceId, trace.TraceId);
        var agent = trace.Steps.Single(s => s.Operation == "agent.investigate");
        Assert.All(trace.Steps.Where(s => s.Operation is "tool.execute" or "model.responses"), s => Assert.Equal(agent.SpanId, s.ParentSpanId));
    }

    [Theory]
    [InlineData(true, 502, "provider_error")]
    [InlineData(false, 504, "cancelled")]
    public async Task Provider_failures_and_timeouts_mark_model_spans_without_error_payloads(bool httpError, int expectedStatus, string expectedType)
    {
        var options = ScriptedModel.Options();
        using var provider = new HttpClient(new Handler(_ => httpError
            ? new(HttpStatusCode.Unauthorized) { Content = new StringContent("provider-sensitive-secret") }
            : throw new TaskCanceledException("provider-sensitive-secret")));
        await using var factory = new DemoFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<IAgentModel>(new ResponsesModel(provider, options));
        }));
        using var client = factory.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", DemoFactory.Reviewer);
        Assert.Equal(expectedStatus, (int)(await client.PostAsJsonAsync("/api/agent/investigate", new { message = "Investigate" })).StatusCode);
        var snapshot = (await client.GetFromJsonAsync<MonitoringSnapshot>("/api/monitoring"))!;
        var model = Assert.Single(Assert.Single(snapshot.Traces).Steps, s => s.Operation == "model.responses");
        Assert.Equal("error", model.Status);
        Assert.Equal(expectedType, ((JsonElement)model.Attributes["error.type"]).GetString());
        Assert.DoesNotContain("provider-sensitive-secret", JsonSerializer.Serialize(snapshot));
        Assert.Null(snapshot.InputTokens);
    }

    [Fact]
    public void Retention_capacity_span_bounds_and_attribute_allowlist_are_enforced()
    {
        var clock = new MutableClock(DateTimeOffset.UtcNow);
        using var monitor = new LocalMonitoring(clock, capacity: 2, maxSteps: 2);
        for (var i = 0; i < 3; i++)
        {
            var scope = monitor.Begin("northstar", "/api/test", "GET");
            for (var j = 0; j < 5; j++)
            {
                using var step = ResponsesModel.Activities.StartActivity("tool.execute");
                step!.SetTag("prompt", "secret-payload");
                step.SetTag("tool.name", "unknown-private-tool");
                step.SetTag("error.type", "sensitive-exception-message");
                step.SetStatus(ActivityStatusCode.Error, "secret-exception-description");
            }
            monitor.Complete(scope, 200);
        }
        var snapshot = monitor.Snapshot("northstar");
        Assert.Equal(2, snapshot.Requests);
        Assert.All(snapshot.Traces, t => { Assert.True(t.Truncated); Assert.Equal(2, t.Steps.Count); Assert.Contains(t.Steps, s => s.Operation == "api.request"); });
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(snapshot));
        Assert.DoesNotContain("unknown-private-tool", JsonSerializer.Serialize(snapshot));
        Assert.Empty(monitor.Snapshot("harborline").Traces);
        clock.Now += TimeSpan.FromMinutes(61);
        var expired = monitor.Snapshot("northstar");
        Assert.Empty(expired.Traces);
        Assert.Null(expired.AverageMs);
        Assert.Null(expired.P95Ms);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(response(request));
    }
    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
