using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Buggy.API.Extensions;

public static class AuthExtensions
{
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
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = $"https://{domain}/",
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("SingleUser", policy =>
                policy.RequireAssertion(context =>
                {
                    var sub = context.User.FindFirst("sub")?.Value;
                    return sub == allowedUserId;
                }));
        });

        return services;
    }
}
