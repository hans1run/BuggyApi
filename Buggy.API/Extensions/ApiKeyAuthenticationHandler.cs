using System.Security.Claims;
using System.Text.Encodings.Web;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Buggy.API.Extensions;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string HeaderName { get; set; } = "X-Api-Key";
}

/// <summary>
/// Authenticates the X-Api-Key header against named keys in the database, or the
/// legacy single key from configuration (ApiKey:Key, recorded as author "API").
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IApiKeyService _apiKeys;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration,
        IApiKeyService apiKeys) : base(options, logger, encoder)
    {
        _configuration = configuration;
        _apiKeys = apiKeys;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var headerValue))
            return AuthenticateResult.NoResult();

        var secret = headerValue.ToString();
        if (string.IsNullOrEmpty(secret))
            return AuthenticateResult.Fail("Empty API key");

        var key = await _apiKeys.ValidateAsync(secret);
        if (key != null)
            return Success($"apikey:{key.Id}", key.Name);

        var legacyKey = _configuration["ApiKey:Key"];
        if (!string.IsNullOrEmpty(legacyKey) && string.Equals(secret, legacyKey, StringComparison.Ordinal))
            return Success("api-key", "API");

        return AuthenticateResult.Fail("Invalid API key");
    }

    private AuthenticateResult Success(string sub, string name)
    {
        var claims = new[]
        {
            new Claim(AuthExtensions.SubClaim, sub),
            new Claim(AuthExtensions.NameClaim, name),
            new Claim(ClaimTypes.AuthenticationMethod, "apikey")
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name, AuthExtensions.NameClaim, AuthExtensions.RoleClaim);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }
}
