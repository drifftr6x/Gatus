using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Platform.Contracts.Responses;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/product")]
[Authorize(Policy = "RequireViewer")]
public sealed class ProductController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public ProductController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet]
    public ActionResult<ProductConfigurationDto> Get()
    {
        var features = _configuration.GetSection("Product:Features");
        return Ok(new ProductConfigurationDto(
            _configuration["Product:Name"] ?? "EdgeWatch Lite",
            _configuration["Product:Edition"] ?? "Lite",
            _configuration["Product:Version"] ?? "0.1.0",
            new ProductFeatureFlags(
                GetFeature(features, "Groups", true),
                GetFeature(features, "Schedules", true),
                GetFeature(features, "Content", true),
                GetFeature(features, "Alerts", true),
                GetFeature(features, "Analytics", true),
                GetFeature(features, "Notifications", true),
                GetFeature(features, "Logs", true),
                GetFeature(features, "AdvancedReports", false))));
    }

    private static bool GetFeature(IConfigurationSection section, string key, bool fallback)
    {
        return bool.TryParse(section[key], out var value) ? value : fallback;
    }
}
