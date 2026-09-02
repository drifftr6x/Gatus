using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    static CustomWebApplicationFactory()
    {
        // appsettings.json no longer carries a JWT secret; supply a test-only one
        // via environment variable (most reliable with minimal hosting model).
        Environment.SetEnvironmentVariable("Jwt__Secret", "integration-test-secret-key-do-not-use-in-prod");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "GatusKiosk");
        Environment.SetEnvironmentVariable("Jwt__Audience", "GatusKiosk");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove ALL EF Core related registrations (both options and the context itself)
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextOptionsConfiguration<>)))
                .ToList();

            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
        });

        builder.UseEnvironment("Testing");
    }
}
