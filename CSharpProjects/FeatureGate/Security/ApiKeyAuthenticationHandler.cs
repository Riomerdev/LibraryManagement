using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace FeatureGate.Security;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "FeatureGateApiKey";
    public const string HeaderName = "X-Admin-Api-Key";

    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expectedKey = _configuration["FeatureGate:AdminApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            !Request.Headers.TryGetValue(HeaderName, out var suppliedHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expected = Encoding.UTF8.GetBytes(expectedKey);
        var supplied = Encoding.UTF8.GetBytes(suppliedHeader.ToString());
        if (expected.Length != supplied.Length ||
            !CryptographicOperations.FixedTimeEquals(expected, supplied))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "featuregate-admin"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
