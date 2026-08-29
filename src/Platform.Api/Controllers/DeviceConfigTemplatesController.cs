using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Contracts.Requests;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/device-config-templates")]
[Authorize]
public class DeviceConfigTemplatesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DeviceConfigTemplatesController> _logger;

    public DeviceConfigTemplatesController(ApplicationDbContext context, ILogger<DeviceConfigTemplatesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<List<DeviceConfigTemplateResponse>>> GetTemplates()
    {
        var templates = await _context.DeviceConfigTemplates
            .OrderBy(t => t.Name)
            .ToListAsync();

        return templates.Select(t => new DeviceConfigTemplateResponse(
            t.Id, t.Name, t.Description, t.ConfigJson, t.CreatedAt, t.UpdatedAt
        )).ToList();
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<DeviceConfigTemplateResponse>> GetTemplate(Guid id)
    {
        var template = await _context.DeviceConfigTemplates.FindAsync(id);
        if (template is null) return NotFound();

        return new DeviceConfigTemplateResponse(
            template.Id, template.Name, template.Description, template.ConfigJson,
            template.CreatedAt, template.UpdatedAt);
    }

    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<DeviceConfigTemplateResponse>> CreateTemplate([FromBody] CreateDeviceConfigTemplateRequest request)
    {
        var exists = await _context.DeviceConfigTemplates.AnyAsync(t => t.Name == request.Name);
        if (exists)
            return Conflict(new { error = "A template with this name already exists" });

        var template = new DeviceConfigTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            ConfigJson = request.ConfigJson
        };

        _context.DeviceConfigTemplates.Add(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Config template created: {TemplateId} '{Name}'", template.Id, template.Name);

        return CreatedAtAction(nameof(GetTemplate), new { id = template.Id },
            new DeviceConfigTemplateResponse(template.Id, template.Name, template.Description,
                template.ConfigJson, template.CreatedAt, template.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<DeviceConfigTemplateResponse>> UpdateTemplate(Guid id, [FromBody] UpdateDeviceConfigTemplateRequest request)
    {
        var template = await _context.DeviceConfigTemplates.FindAsync(id);
        if (template is null) return NotFound();

        var nameTaken = await _context.DeviceConfigTemplates.AnyAsync(t => t.Name == request.Name && t.Id != id);
        if (nameTaken)
            return Conflict(new { error = "A template with this name already exists" });

        template.Name = request.Name;
        template.Description = request.Description;
        template.ConfigJson = request.ConfigJson;
        await _context.SaveChangesAsync();

        return new DeviceConfigTemplateResponse(
            template.Id, template.Name, template.Description, template.ConfigJson,
            template.CreatedAt, template.UpdatedAt);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        var template = await _context.DeviceConfigTemplates.FindAsync(id);
        if (template is null) return NotFound();

        _context.DeviceConfigTemplates.Remove(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Config template deleted: {TemplateId}", id);
        return NoContent();
    }
}
