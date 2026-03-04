using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Buggy.API.Extensions;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string HeaderName { get; set; } = "X-Api-Key";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration) : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValue))
            return Task.FromResult(AuthenticateResult.NoResult());

        var apiKey = headerValue.ToString();
        var configuredKey = _configuration["ApiKey:Key"];

        if (string.IsNullOrEmpty(configuredKey))
            return Task.FromResult(AuthenticateResult.Fail("API key not configured on server"));

        if (!string.Equals(apiKey, configuredKey, StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var allowedUserId = _configuration["Auth0:AllowedUserId"] ?? "apikey-user";
        var claims = new[]
        {
            new Claim("sub", allowedUserId),
            new Claim(ClaimTypes.AuthenticationMethod, "apikey")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
