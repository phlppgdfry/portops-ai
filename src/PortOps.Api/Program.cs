using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using PortOps.Api;
using PortOps.Domain;

var builder = WebApplication.CreateBuilder(args);
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

var app = builder.Build();
// Validate credentials before accepting traffic. No default credentials.
_ = app.Services.GetRequiredService<DemoCredentials>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
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
    note = "Clock is fixed for repeatable scenarios. No live TOS or AI model is connected."
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

app.Run();

static string Customer(ClaimsPrincipal user) => user.FindFirstValue("customer_id")
    ?? throw new InvalidOperationException("Authenticated customer scope is missing.");

public partial class Program;
