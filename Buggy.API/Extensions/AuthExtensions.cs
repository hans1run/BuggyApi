using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Buggy.API.Extensions;

public static class AuthExtensions
{
    public const string ApiKeyScheme = "ApiKey";

    public static IServiceCollection AddAuth0Authentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var domain = configuration["Auth0:Domain"]!;
        var audience = configuration["Auth0:Audience"]!;
        var allowedUserId = configuration["Auth0:AllowedUserId"]!;

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://{domain}/";
                options.Audience = audience;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://{domain}/",
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    NameClaimType = "sub"
                };
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyScheme, _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("SingleUser", policy =>
                policy.RequireAssertion(context =>
                {
                    var sub = context.User.FindFirst("sub")?.Value;
                    return sub == allowedUserId;
                }));

            // Allows either Auth0 JWT (SingleUser) or API key
            options.AddPolicy("ApiKeyOrSingleUser", policy =>
            {
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyScheme);
                policy.RequireAssertion(context =>
                {
                    var sub = context.User.FindFirst("sub")?.Value;
                    return sub == allowedUserId;
                });
            });
        });

        return services;
    }
}
