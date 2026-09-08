using System.Security.Claims;
using System.Text.Json.Serialization;
using PortOps.Domain;

namespace PortOps.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DraftRequest(string VehicleId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RevisionRequest(int Version);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DecisionRequest(int Version, string PayloadHash);

public static class ProposalEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/procedures", (string? query, ClaimsPrincipal user, ProcedureCatalog catalog) =>
            string.IsNullOrWhiteSpace(query) || query.Length > 200
                ? Results.Problem(statusCode: 400, detail: "Use a procedure query of 1–200 characters.")
                : Results.Ok(catalog.Search(Customer(user), query)));
        api.MapGet("/vehicles/{id}/procedures", (string id, ClaimsPrincipal user, OperationsService operations, ProcedureCatalog catalog) =>
            operations.GetVehicle(Customer(user), id) is { } vehicle
                ? Results.Ok(catalog.ForVehicle(Customer(user), vehicle)) : Results.NotFound());
        api.MapGet("/proposals", (ClaimsPrincipal user, ProposalService service) => Results.Ok(service.List(Customer(user))));
        api.MapPost("/proposals", (DraftRequest request, ClaimsPrincipal user, ProposalService service) =>
            string.IsNullOrWhiteSpace(request.VehicleId) || request.VehicleId.Length > 64
                ? Results.Problem(statusCode: 400, detail: "A vehicle identifier is required.")
                : Execute(() => service.Create(Actor(user), request.VehicleId))).RequireRateLimiting("agent");
        api.MapPost("/proposals/{id}/refresh", (string id, RevisionRequest request, ClaimsPrincipal user, ProposalService service) =>
            Execute(() => service.Refresh(Actor(user), id, request.Version))).RequireRateLimiting("agent");
        api.MapPost("/proposals/{id}/approve", (string id, DecisionRequest request, ClaimsPrincipal user, ProposalService service) =>
            Execute(() => service.Decide(Actor(user), id, request.Version, request.PayloadHash, true))).RequireAuthorization("Reviewer");
        api.MapPost("/proposals/{id}/reject", (string id, DecisionRequest request, ClaimsPrincipal user, ProposalService service) =>
            Execute(() => service.Decide(Actor(user), id, request.Version, request.PayloadHash, false))).RequireAuthorization("Reviewer");
    }

    private static IResult Execute(Func<ActionProposal> action)
    {
        try { return Results.Ok(action()); }
        catch (ProposalFailure failure)
        {
            var status = failure.Code switch { "not_found" => 404, "forbidden" => 403, "capacity" => 429, _ => 409 };
            return Results.Problem(statusCode: status, title: "Proposal unavailable", detail: failure.Message,
                extensions: new Dictionary<string, object?> { ["code"] = failure.Code });
        }
    }
    private static string Customer(ClaimsPrincipal user) => user.FindFirstValue("customer_id")!;
    private static ProposalActor Actor(ClaimsPrincipal user) => new(user.FindFirstValue(ClaimTypes.NameIdentifier)!, Customer(user), user.IsInRole("Reviewer"));
}
