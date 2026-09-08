using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PortOps.Agent;
using PortOps.Domain;

namespace PortOps.Tests;

public sealed class ProposalApiTests
{
    private static HttpClient Client(DemoFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Complete_human_review_flow_enforces_role_scope_and_version()
    {
        await using var factory = new DemoFactory();
        using var writer = Client(factory, DemoFactory.Northstar);
        using var reviewer = Client(factory, DemoFactory.Reviewer);
        using var other = Client(factory, DemoFactory.Harborline);
        var created = await writer.PostAsJsonAsync("/api/proposals", new { vehicleId = "DEMO-003" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var draft = (await created.Content.ReadFromJsonAsync<ActionProposal>(AgentJson.Options))!;
        var decision = new { draft.Version, draft.PayloadHash };
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.PostAsJsonAsync($"/api/proposals/{draft.Id}/approve", decision)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/proposals/{draft.Id}/refresh", new { version = 1 })).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<ActionProposal[]>("/api/proposals", AgentJson.Options))!);
        Assert.Equal(HttpStatusCode.Conflict, (await reviewer.PostAsJsonAsync($"/api/proposals/{draft.Id}/approve", new { version = 9, draft.PayloadHash })).StatusCode);
        var approved = await reviewer.PostAsJsonAsync($"/api/proposals/{draft.Id}/approve", decision);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var first = (await approved.Content.ReadFromJsonAsync<ActionProposal>(AgentJson.Options))!;
        var retry = await reviewer.PostAsJsonAsync($"/api/proposals/{draft.Id}/approve", decision);
        var second = (await retry.Content.ReadFromJsonAsync<ActionProposal>(AgentJson.Options))!;
        Assert.Equal("executed", first.Status);
        Assert.Equal(first.Delivery!.Id, second.Delivery!.Id);
        Assert.Contains("no-store", approved.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Client_cannot_inject_destination_customer_or_approval_into_a_draft()
    {
        await using var factory = new DemoFactory();
        using var writer = Client(factory, DemoFactory.Northstar);
        Assert.Equal(HttpStatusCode.BadRequest, (await writer.PostAsJsonAsync("/api/proposals",
            new { vehicleId = "DEMO-003", customerId = "harborline", destination = "external@example.com", approved = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await writer.PostAsJsonAsync("/api/proposals", new { vehicleId = "DEMO-007" })).StatusCode);
    }

    [Theory]
    [InlineData("/api/procedures?query=rfp")]
    [InlineData("/api/vehicles/DEMO-003/procedures")]
    [InlineData("/api/proposals")]
    public async Task New_read_routes_require_authentication(string route)
    {
        await using var factory = new DemoFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
    }

    [Fact]
    public void Retrieval_filters_customer_before_matching_and_preserves_versions()
    {
        var catalog = new ProcedureCatalog();
        Assert.Empty(catalog.Search("northstar", "harborline"));
        var own = Assert.Single(catalog.Search("harborline", "harborline"));
        Assert.Equal("proc-harborline-v1", own.Evidence.Id);
        Assert.Contains(catalog.Search("northstar", "rfp"), p => p.Text.Contains("geen vrijgave"));
        Assert.Empty(catalog.Search("northstar", "nonexistentkeyword"));
    }
}
