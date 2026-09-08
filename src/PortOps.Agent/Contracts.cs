using System.Text.Json;
using System.Text.Json.Serialization;
using PortOps.Domain;

namespace PortOps.Agent;

public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };
    public static JsonElement Element<T>(T value) => JsonSerializer.SerializeToElement(value, Options);
}

public sealed record ConversationTurn(string Question, string Answer);
public sealed record AgentRequest(string Message, IReadOnlyList<ConversationTurn>? History = null);
public sealed record Finding(string Text, IReadOnlyList<string> EvidenceIds);
public sealed record ModelAnswer(string Status, IReadOnlyList<Finding> Findings, IReadOnlyList<string> Unknowns);
public sealed record ToolCall(string Id, string Name, string Arguments);
public sealed record ToolExecution(string Name, string Status, long DurationMs);
public sealed record ToolResult(string Status, JsonElement Data, IReadOnlyList<Evidence> Evidence, ActionProposal? Proposal = null);
public sealed record TokenUsage(int InputTokens, int OutputTokens);
public sealed record ModelTurn(IReadOnlyList<JsonElement> OutputItems, IReadOnlyList<ToolCall> Calls,
    string? FinalText, TokenUsage? Usage, bool Refused = false);
public sealed record ModelInput(string Instructions, IReadOnlyList<JsonElement> Items, bool RequireTool);
public sealed record AgentResult(string Status, IReadOnlyList<Finding> Findings,
    IReadOnlyList<string> Unknowns, IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<ToolExecution> Tools, string TraceId, long DurationMs, TokenUsage? Usage,
    DateTimeOffset EvaluatedAt, string Provider, string Model, IReadOnlyList<ActionProposal>? Proposals = null);

public interface IAgentModel
{
    Task<ModelTurn> CompleteAsync(ModelInput input, CancellationToken cancellationToken);
}

public sealed class AgentFailure(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class AgentOptions
{
    public string Provider { get; set; } = "AzureOpenAI";
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxModelTurns { get; set; } = 6;
    public int MaxToolCalls { get; set; } = 8;
    public int MaxOutputTokens { get; set; } = 2500;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model)
        && (Provider == "OpenAI" || !string.IsNullOrWhiteSpace(Endpoint));

    public Uri ResponseUri()
    {
        if (Provider == "OpenAI") return new("https://api.openai.com/v1/responses");
        if (Provider != "AzureOpenAI" || !Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length > 0
            || uri.Query.Length > 0 || uri.Fragment.Length > 0
            || !(uri.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase))
            || (uri.AbsolutePath.TrimEnd('/') is not "" and not "/openai/v1"))
            throw new AgentFailure("configuration", "Configure an HTTPS Azure OpenAI resource endpoint (root or /openai/v1). No custom proxy endpoints are supported.");
        return new UriBuilder(uri) { Path = "/openai/v1/responses" }.Uri;
    }

    public void Validate()
    {
        if (Provider is not "AzureOpenAI" and not "OpenAI" || TimeoutSeconds is < 1 or > 120
            || MaxModelTurns is < 1 or > 8 || MaxToolCalls is < 1 or > 12 || MaxOutputTokens is < 256 or > 8000)
            throw new AgentFailure("configuration", "Invalid agent provider or request limits.");
        if (IsConfigured) _ = ResponseUri();
    }
}
