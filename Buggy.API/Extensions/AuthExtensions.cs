using System.Security.Claims;
using System.Threading.RateLimiting;
using Buggy.API.Data;
using Buggy.API.Models;
using Buggy.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace Buggy.API.Extensions;

public static class AuthExtensions
{
    public const string ApiKeyScheme = "ApiKey";
    public const string ApiKeyOrUserPolicy = "ApiKeyOrUser";
    public const string AdminPolicy = "Admin";
    public const string LoginRateLimit = "login";

    public const string SubClaim = "sub";
    public const string NameClaim = "name";
    public const string EmailClaim = "email";
    public const string RoleClaim = "role";
    public const string StampClaim = "stamp";
    public const string AdminRole = "admin";

    public static IServiceCollection AddBuggyAuthentication(
        this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        // Persist cookie-encryption keys in Postgres so sessions survive container restarts.
        services.AddDataProtection()
            .SetApplicationName("Buggy.API")
            .PersistKeysToDbContext<BuggyDbContext>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "buggy.session";
                options.Cookie.HttpOnly = true;
                // SPA and API share one origin (Caddy / Vite proxy), so Strict costs nothing and blocks CSRF.
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;

                // An API answers with status codes, not redirects to a login page.
                options.Events.OnRedirectToLogin = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = ValidateSessionAsync;
            })
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyScheme, _ => { });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ApiKeyOrUserPolicy, policy =>
            {
                policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, ApiKeyScheme);
                policy.RequireAuthenticatedUser();
            });
            options.AddPolicy(AdminPolicy, policy =>
                policy.RequireAuthenticatedUser().RequireClaim(RoleClaim, AdminRole));
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(LoginRateLimit, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        var claims = new List<Claim>
        {
            new(SubClaim, user.Id.ToString()),
            new(NameClaim, user.DisplayName),
            new(EmailClaim, user.Email),
            new(StampClaim, user.SecurityStamp)
        };
        if (user.IsAdmin)
            claims.Add(new Claim(RoleClaim, AdminRole));

        var identity = new ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme, NameClaim, RoleClaim);
        return new ClaimsPrincipal(identity);
    }

    public static Guid? GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirst(SubClaim)?.Value, out var id) ? id : null;

    /// <summary>Name to record as the author of items and comments.</summary>
    public static string GetDisplayName(this ClaimsPrincipal principal) =>
        principal.FindFirst(NameClaim)?.Value ?? principal.FindFirst(SubClaim)?.Value ?? "unknown";

    /// <summary>
    /// Rejects sessions for deleted users or rotated security stamps (password or role
    /// change), and refreshes the display name if it changed.
    /// </summary>
    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.GetUserId();
        var stamp = context.Principal?.FindFirst(StampClaim)?.Value;
        var users = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
        var user = userId == null ? null : await users.GetEntityAsync(userId.Value);

        if (user == null || user.SecurityStamp != stamp)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (context.Principal!.FindFirst(NameClaim)?.Value != user.DisplayName)
        {
            context.ReplacePrincipal(CreatePrincipal(user));
            context.ShouldRenew = true;
        }
    }
}
