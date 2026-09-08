using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PortOps.Agent;

namespace PortOps.Tests;

public sealed class AgentApiTests
{
    [Fact]
    public async Task Agent_endpoint_uses_authenticated_customer_and_validates_citations()
    {
        var model = new ScriptedModel(ScriptedModel.Call(), ScriptedModel.Answer());
        await using var factory = new DemoFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(ScriptedModel.Options());
                services.AddSingleton<IAgentModel>(model);
            }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DemoFactory.Northstar);
        var response = await client.PostAsJsonAsync("/api/agent/investigate", new { message = "Why DEMO-003?" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AgentResult>(AgentJson.Options);
        Assert.Equal("e-h003", Assert.Single(result!.Evidence).Id);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Agent_requires_authentication()
    {
        await using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/agent/investigate", new { message = "Investigate" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/agent/status")).StatusCode);
    }

    [Fact]
    public async Task Unconfigured_model_is_explicit_and_empty_questions_are_invalid()
    {
        await using var factory = new DemoFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton(new AgentOptions())));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DemoFactory.Northstar);
        var response = await client.PostAsJsonAsync("/api/agent/investigate", new { message = "Investigate" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("not_configured", await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/agent/investigate", new { message = " " })).StatusCode);
    }

    [Fact]
    public async Task Oversized_request_is_rejected_before_model_execution()
    {
        await using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DemoFactory.Northstar);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PostAsJsonAsync("/api/agent/investigate", new { message = new string('x', 70_000) })).StatusCode);
    }

    [Fact]
    public async Task Rate_limit_bounds_repeated_requests()
    {
        await using var factory = new DemoFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton(new AgentOptions())));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DemoFactory.Northstar);
        for (var i = 0; i < 10; i++) await client.PostAsJsonAsync("/api/agent/investigate", new { message = "Check" });
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/agent/investigate", new { message = "Check" })).StatusCode);
    }
}
