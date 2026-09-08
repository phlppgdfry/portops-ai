using System.Net;
using System.Text.Json;
using PortOps.Agent;

namespace PortOps.Tests;

public sealed class ResponsesModelTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }

    [Theory]
    [InlineData("AzureOpenAI", "https://example.openai.azure.com/")]
    [InlineData("AzureOpenAI", "https://example.services.ai.azure.com/openai/v1/")]
    [InlineData("OpenAI", "")]
    public async Task Sends_stateless_strict_tool_protocol_and_correct_auth(string provider, string endpoint)
    {
        var options = ScriptedModel.Options(); options.Provider = provider; options.Endpoint = endpoint;
        using var client = new HttpClient(new Handler(async request =>
        {
            Assert.EndsWith("/responses", request.RequestUri!.AbsolutePath);
            Assert.Equal(provider == "AzureOpenAI", request.Headers.Contains("api-key"));
            Assert.Equal(provider == "OpenAI", request.Headers.Authorization is not null);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.False(body.RootElement.GetProperty("store").GetBoolean());
            Assert.Equal("required", body.RootElement.GetProperty("tool_choice").GetString());
            Assert.Equal(3, body.RootElement.GetProperty("tools").GetArrayLength());
            Assert.True(body.RootElement.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
            Assert.Contains("reasoning.encrypted_content", body.RootElement.GetProperty("include").EnumerateArray().Select(x => x.GetString()));
            return new(HttpStatusCode.OK) { Content = new StringContent("""
              {"status":"completed","output":[{"type":"function_call","call_id":"c1","name":"get_vehicle","arguments":"{\"id\":\"DEMO-003\"}"}],"usage":{"input_tokens":7,"output_tokens":3}}
              """) };
        }));
        var result = await new ResponsesModel(client, options).CompleteAsync(new("instructions", [], true), default);
        Assert.Equal("get_vehicle", Assert.Single(result.Calls).Name);
        Assert.Equal(new TokenUsage(7, 3), result.Usage);
    }

    [Theory]
    [InlineData("{\"status\":\"incomplete\",\"output\":[]}", "provider_incomplete")]
    [InlineData("not json", "provider_protocol")]
    [InlineData("{\"status\":\"completed\"}", "provider_protocol")]
    public async Task Malformed_or_incomplete_responses_are_not_accepted(string body, string code)
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) })));
        Assert.Equal(code, (await Assert.ThrowsAsync<AgentFailure>(() =>
            new ResponsesModel(client, ScriptedModel.Options()).CompleteAsync(new("instructions", [], false), default))).Code);
    }

    [Fact]
    public async Task Provider_error_payload_is_not_exposed()
    {
        using var client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        { Content = new StringContent("secret-provider-diagnostic") })));
        var error = await Assert.ThrowsAsync<AgentFailure>(() =>
            new ResponsesModel(client, ScriptedModel.Options()).CompleteAsync(new("instructions", [], false), default));
        Assert.Equal("provider_error", error.Code);
        Assert.DoesNotContain("secret-provider-diagnostic", error.Message);
    }

    [Theory]
    [InlineData("http://example.openai.azure.com")]
    [InlineData("https://example.openai.azure.com.attacker.test")]
    [InlineData("https://example.openai.azure.com/other-path")]
    [InlineData("https://example.openai.azure.com?api-key=secret")]
    public void Invalid_azure_endpoints_are_rejected(string endpoint)
    {
        var options = ScriptedModel.Options(); options.Provider = "AzureOpenAI"; options.Endpoint = endpoint;
        Assert.Throws<AgentFailure>(() => options.Validate());
    }
}
