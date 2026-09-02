using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Platform.Security.Services;
using System.Text;

namespace Platform.Security;

public static class SecurityServiceCollectionExtensions
{
    private const string KnownPlaceholderSecret = "your-super-secret-key-change-in-production-min-32-chars";

    public static IServiceCollection AddPlatformSecurity(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        var jwtSecret = configuration["Jwt:Secret"];

        if (string.IsNullOrEmpty(jwtSecret))
            throw new InvalidOperationException(
                "JWT Secret not configured. Set Jwt__Secret environment variable or Jwt:Secret in configuration.");

        if (jwtSecret == KnownPlaceholderSecret && !isDevelopment)
            throw new InvalidOperationException(
                "JWT Secret is the known placeholder value. Generate a real secret: openssl rand -base64 48");

        if (jwtSecret.Length < 32 && !isDevelopment)
            throw new InvalidOperationException(
                $"JWT Secret is too short ({jwtSecret.Length} chars, minimum 32). Generate one: openssl rand -base64 48");

        if (isDevelopment && (jwtSecret == KnownPlaceholderSecret || jwtSecret.Length < 32))
            Console.WriteLine("WARNING: Using development JWT secret — do NOT use outside local development.");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "GatusKiosk";
        var jwtAudience = configuration["Jwt:Audience"] ?? "GatusKiosk";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtIssuer,
                ValidAudience = jwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "display_name"
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin", "SuperAdmin"));
            options.AddPolicy("RequireEditor", policy => policy.RequireRole("Editor", "Admin", "SuperAdmin"));
            options.AddPolicy("RequireViewer", policy => policy.RequireRole("Viewer", "Editor", "Admin", "SuperAdmin"));
        });

        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}
