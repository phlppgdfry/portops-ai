using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PortOps.Api;

/// <summary>Local demo identity only. Replace with OIDC before external deployment.</summary>
public sealed class DemoCredentials
{
    private readonly IReadOnlyList<(string Customer, byte[] Hash)> entries;

    public DemoCredentials(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            throw new InvalidOperationException("Demo identity is available only in Development or Testing.");
        var configured = new[] { "northstar", "harborline" }
            .Select(customer => (Customer: customer, Token: configuration[$"DemoAuth:Keys:{customer}"]))
            .Where(x => !string.IsNullOrWhiteSpace(x.Token)).ToArray();
        if (configured.Length == 0 || configured.Any(x => x.Token!.Length < 24 || x.Token.Length > 1024 || x.Token.Any(char.IsWhiteSpace)))
            throw new InvalidOperationException("Set DemoAuth__Keys__northstar and/or DemoAuth__Keys__harborline to distinct demo tokens of 24–1024 characters without whitespace.");
        if (configured.Select(x => x.Token).Distinct(StringComparer.Ordinal).Count() != configured.Length)
            throw new InvalidOperationException("Each demo customer must have a distinct token.");
        entries = configured.Select(x => (x.Customer, Hash(x.Token!))).ToArray();
    }

    public string? Resolve(string token)
    {
        if (token.Length is < 24 or > 1024) return null;
        var hash = Hash(token);
        string? customer = null;
        foreach (var entry in entries)
            if (CryptographicOperations.FixedTimeEquals(hash, entry.Hash)) customer = entry.Customer;
        return customer;
    }

    private static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}

public sealed class DemoAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, DemoCredentials credentials)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DemoBearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(header)) return Task.FromResult(AuthenticateResult.NoResult());
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.Fail("Bearer credentials required."));
        var customer = credentials.Resolve(header[7..]);
        if (customer is null) return Task.FromResult(AuthenticateResult.Fail("Invalid demo credentials."));
        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, $"demo:{customer}"), new Claim("customer_id", customer)
        ], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
