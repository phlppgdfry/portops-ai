using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PortOps.Agent;

/// <summary>Stateless Responses API adapter. Keys and raw provider errors never leave this boundary.</summary>
public sealed class ResponsesModel(HttpClient client, AgentOptions options) : IAgentModel
{
    public static readonly ActivitySource Activities = new("PortOps.Agent");
    public static readonly JsonElement AnswerSchema = JsonDocument.Parse("""
    {"type":"object","properties":{
      "status":{"type":"string","enum":["answered","insufficient_evidence","refused"]},
      "findings":{"type":"array","items":{"type":"object","properties":{"text":{"type":"string"},"evidenceIds":{"type":"array","items":{"type":"string"}}},"required":["text","evidenceIds"],"additionalProperties":false}},
      "unknowns":{"type":"array","items":{"type":"string"}}
    },"required":["status","findings","unknowns"],"additionalProperties":false}
    """).RootElement.Clone();

    public async Task<ModelTurn> CompleteAsync(ModelInput input, CancellationToken cancellationToken)
    {
        if (!options.IsConfigured) throw new AgentFailure("not_configured", "No model connection is configured.");
        using var activity = Activities.StartActivity("model.responses");
        activity?.SetTag("gen_ai.provider.name", options.Provider);
        activity?.SetTag("gen_ai.request.model", options.Model);
        using var request = new HttpRequestMessage(HttpMethod.Post, options.ResponseUri());
        if (options.Provider == "AzureOpenAI") request.Headers.Add("api-key", options.ApiKey);
        else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            model = options.Model, instructions = input.Instructions, input = input.Items,
            tools = OperationalTools.Definitions, tool_choice = input.RequireTool ? "required" : "auto",
            parallel_tool_calls = false, store = false,
            include = new[] { "reasoning.encrypted_content" },
            max_output_tokens = options.MaxOutputTokens,
            text = new { format = new { type = "json_schema", name = "portops_answer", strict = true, schema = AnswerSchema } }
        });
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "provider_http_error");
                throw new AgentFailure("provider_error", $"Model provider returned HTTP {(int)response.StatusCode}. Check server configuration or retry later.");
            }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int length;
            while ((length = await stream.ReadAsync(chunk, cancellationToken)) != 0)
            {
                if (buffer.Length + length > 1_048_576) throw new AgentFailure("provider_protocol", "Model response exceeded its size limit.");
                buffer.Write(chunk, 0, length);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            var root = document.RootElement;
            if (!root.TryGetProperty("status", out var status) || status.GetString() != "completed")
                throw new AgentFailure("provider_incomplete", "The model did not complete its response. No partial answer was accepted.");
            var outputs = root.GetProperty("output").EnumerateArray().Select(x => x.Clone()).ToArray();
            var calls = new List<ToolCall>();
            var texts = new List<string>();
            var refused = false;
            foreach (var output in outputs)
            {
                var type = output.GetProperty("type").GetString();
                if (type == "function_call") calls.Add(new(output.GetProperty("call_id").GetString()!,
                    output.GetProperty("name").GetString()!, output.GetProperty("arguments").GetString()!));
                if (type != "message") continue;
                foreach (var content in output.GetProperty("content").EnumerateArray())
                {
                    if (content.GetProperty("type").GetString() == "output_text") texts.Add(content.GetProperty("text").GetString()!);
                    if (content.GetProperty("type").GetString() == "refusal") refused = true;
                }
            }
            TokenUsage? usage = null;
            if (root.TryGetProperty("usage", out var tokens) && tokens.ValueKind == JsonValueKind.Object
                && tokens.TryGetProperty("input_tokens", out var inputTokens) && inputTokens.TryGetInt32(out var i)
                && tokens.TryGetProperty("output_tokens", out var outputTokens) && outputTokens.TryGetInt32(out var o) && i >= 0 && o >= 0)
                usage = new(i, o);
            activity?.SetTag("gen_ai.usage.input_tokens", usage?.InputTokens);
            activity?.SetTag("gen_ai.usage.output_tokens", usage?.OutputTokens);
            return new(outputs, calls, texts.Count > 0 ? string.Join("", texts) : null, usage, refused);
        }
        catch (HttpRequestException) { throw new AgentFailure("provider_error", "The model provider is unreachable. No answer was generated."); }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new AgentFailure("provider_protocol", "The model provider returned an unsupported response."); }
    }
}
