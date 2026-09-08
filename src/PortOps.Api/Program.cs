using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using PortOps.Api;
using PortOps.Domain;
using PortOps.Agent;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 65_536);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(DemoAuthentication.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DemoAuthentication>(DemoAuthentication.SchemeName, _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<DemoCredentials>();
var snapshot = DemoTerminal.Create();
builder.Services.AddSingleton(snapshot);
builder.Services.AddSingleton<TimeProvider>(new ScenarioClock(snapshot.ScenarioTime));
builder.Services.AddSingleton<ReadinessPolicy>();
builder.Services.AddSingleton<OperationsService>();
builder.Services.AddSingleton(serviceProvider =>
{
    var settings = new AgentOptions();
    serviceProvider.GetRequiredService<IConfiguration>().GetSection("Agent").Bind(settings);
    settings.Validate();
    return settings;
});
builder.Services.AddHttpClient<IAgentModel, ResponsesModel>(client => client.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<OperationalTools>();
builder.Services.AddSingleton<AgentRunner>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("agent", context => RateLimitPartition.GetFixedWindowLimiter(
        Customer(context.User), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});

var app = builder.Build();
// Validate credentials before accepting traffic. No default credentials.
_ = app.Services.GetRequiredService<DemoCredentials>();
_ = app.Services.GetRequiredService<AgentOptions>();
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api")) context.Response.Headers.CacheControl = "no-store";
    if (context.Request.ContentLength > 65_536)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return;
    }
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "ok", mode = "synthetic-demo" }));

var api = app.MapGroup("/api").RequireAuthorization();
api.MapGet("/demo", (OperationsService operations, ClaimsPrincipal user) => Results.Ok(new
{
    name = "PortOps AI — Logistics Operations Agent",
    mode = "synthetic-demo",
    customerId = Customer(user),
    scenarioTime = operations.ScenarioTime,
    evaluatedAt = operations.Now,
    displayTimeZone = "Europe/Brussels",
    maximumObservationAgeHours = ReadinessPolicy.MaximumObservationAge.TotalHours,
    note = "Clock is fixed for repeatable scenarios. No live TOS is connected. Model configuration is reported by /api/agent/status."
}));
api.MapGet("/vehicles", (OperationsService operations, ClaimsPrincipal user) =>
    Results.Ok(operations.GetVehicles(Customer(user))));
api.MapGet("/vehicles/{id}", (string id, OperationsService operations, ClaimsPrincipal user) =>
    operations.GetVehicle(Customer(user), id) is { } vehicle ? Results.Ok(vehicle) : Results.NotFound());
api.MapGet("/bookings", (OperationsService operations, ClaimsPrincipal user) =>
    Results.Ok(operations.GetBookings(Customer(user))));
api.MapGet("/vessel-calls/{id}", (string id, OperationsService operations, ClaimsPrincipal user) =>
    operations.GetVesselCall(Customer(user), id) is { } call ? Results.Ok(call) : Results.NotFound());
api.MapGet("/operations/attention", (int? horizonHours, OperationsService operations, ClaimsPrincipal user) =>
{
    var horizon = horizonHours ?? 24;
    return horizon is < 1 or > 168
        ? Results.ValidationProblem(new Dictionary<string, string[]> { ["horizonHours"] = ["Must be between 1 and 168."] })
        : Results.Ok(operations.GetAttention(Customer(user), horizon));
});
api.MapGet("/agent/status", (AgentOptions settings) => Results.Ok(new
{
    configured = settings.IsConfigured, provider = settings.Provider,
    model = settings.IsConfigured ? settings.Model : null,
    mode = "read-only", maxModelTurns = settings.MaxModelTurns,
    maxToolCalls = settings.MaxToolCalls, timeoutSeconds = settings.TimeoutSeconds,
    citationValidation = "retrieved-record-membership; factual support requires evaluation"
}));
api.MapPost("/agent/investigate", async (AgentRequest request, AgentRunner agent,
    ClaimsPrincipal user, HttpContext context, CancellationToken token) =>
{
    try { return Results.Ok(await agent.RunAsync(Customer(user), request, token)); }
    catch (AgentFailure failure)
    {
        var status = failure.Code switch
        {
            "invalid_request" => 400,
            "not_configured" or "configuration" => 503,
            "busy" => 429,
            "timeout" => 504,
            _ => 502
        };
        return Results.Problem(statusCode: status, title: "Investigation unavailable", detail: failure.Message,
            extensions: new Dictionary<string, object?> { ["code"] = failure.Code, ["traceId"] = context.TraceIdentifier });
    }
}).RequireRateLimiting("agent");

app.Run();

static string Customer(ClaimsPrincipal user) => user.FindFirstValue("customer_id")
    ?? throw new InvalidOperationException("Authenticated customer scope is missing.");

public partial class Program;
