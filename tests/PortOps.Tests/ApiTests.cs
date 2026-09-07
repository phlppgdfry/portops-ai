using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace PortOps.Tests;

public sealed class DemoFactory : WebApplicationFactory<Program>
{
    public const string Northstar = "test-only-northstar-token-00000001";
    public const string Harborline = "test-only-harborline-token-00000002";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DemoAuth:Keys:northstar"] = Northstar,
            ["DemoAuth:Keys:harborline"] = Harborline
        }));
    }
}

public sealed class ApiTests(DemoFactory factory) : IClassFixture<DemoFactory>
{
    private HttpClient Client(string? token = DemoFactory.Northstar)
    {
        var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Theory]
    [InlineData("/api/demo")]
    [InlineData("/api/vehicles")]
    [InlineData("/api/vehicles/DEMO-001")]
    [InlineData("/api/bookings")]
    [InlineData("/api/vessel-calls/CALL-01")]
    [InlineData("/api/operations/attention")]
    public async Task Operational_routes_require_authentication(string path)
    {
        using var client = Client(null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Invalid_token_is_rejected()
    {
        using var client = Client("invalid-token-with-sufficient-length");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/vehicles")).StatusCode);
    }

    [Theory]
    [InlineData("/api/vehicles/DEMO-007")]
    [InlineData("/api/vessel-calls/CALL-04")]
    [InlineData("/api/vehicles/nonexistent")]
    public async Task Inaccessible_records_are_not_found(string path)
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Caller_cannot_override_scope_in_query_or_header()
    {
        using var client = Client();
        client.DefaultRequestHeaders.Add("X-Customer-Id", "harborline");
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/vehicles?customerId=harborline"));
        Assert.Equal(6, json.RootElement.GetArrayLength());
        Assert.All(json.RootElement.EnumerateArray(), row =>
            Assert.Equal("northstar", row.GetProperty("vehicle").GetProperty("customerId").GetString()));
    }

    [Fact]
    public async Task Second_customer_sees_only_its_own_vehicle()
    {
        using var client = Client(DemoFactory.Harborline);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/vehicles"));
        Assert.Equal(1, json.RootElement.GetArrayLength());
        Assert.Equal("DEMO-007", json.RootElement[0].GetProperty("vehicle").GetProperty("id").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/vehicles/DEMO-001")).StatusCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("169")]
    [InlineData("wrong")]
    public async Task Invalid_horizon_returns_bad_request(string horizon)
    {
        using var client = Client();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/operations/attention?horizonHours={horizon}")).StatusCode);
    }

    [Fact]
    public async Task Overview_serializes_expected_priority_and_evidence()
    {
        using var client = Client();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/operations/attention"));
        var first = json.RootElement.GetProperty("items")[0];
        Assert.Equal("DEMO-003", first.GetProperty("vehicleId").GetString());
        Assert.Equal("Critical", first.GetProperty("priority").GetString());
        Assert.Contains(first.GetProperty("evidence").EnumerateArray(), e => e.GetProperty("id").GetString() == "e-h003");
    }

    [Fact]
    public async Task Demo_metadata_makes_fixed_clock_explicit()
    {
        using var client = Client();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/api/demo"));
        Assert.Equal("synthetic-demo", json.RootElement.GetProperty("mode").GetString());
        Assert.Equal("2026-09-07T08:00:00+00:00", json.RootElement.GetProperty("scenarioTime").GetString());
    }
}
