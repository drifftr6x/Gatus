using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/escalation-policies")]
[Authorize]
public class EscalationPoliciesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EscalationPoliciesController> _logger;

    public EscalationPoliciesController(ApplicationDbContext context, ILogger<EscalationPoliciesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<List<EscalationPolicyDto>>> GetPolicies()
    {
        var policies = await _context.EscalationPolicies
            .Include(p => p.Steps)
                .ThenInclude(s => s.Channel)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return policies.Select(p => new EscalationPolicyDto(
            p.Id, p.Name, p.Description, p.IsEnabled, p.CreatedAt,
            p.Steps.OrderBy(s => s.Order).Select(s => new EscalationStepDto(
                s.Id, s.Order, s.DelayMinutes, s.ChannelId,
                s.Channel?.Name ?? "Unknown",
                s.EscalateSeverity?.ToString()
            )).ToList()
        )).ToList();
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<EscalationPolicyDto>> GetPolicy(Guid id)
    {
        var p = await _context.EscalationPolicies
            .Include(p => p.Steps)
                .ThenInclude(s => s.Channel)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (p == null) return NotFound();

        return new EscalationPolicyDto(
            p.Id, p.Name, p.Description, p.IsEnabled, p.CreatedAt,
            p.Steps.OrderBy(s => s.Order).Select(s => new EscalationStepDto(
                s.Id, s.Order, s.DelayMinutes, s.ChannelId,
                s.Channel?.Name ?? "Unknown",
                s.EscalateSeverity?.ToString()
            )).ToList()
        );
    }

    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<EscalationPolicyDto>> CreatePolicy([FromBody] CreateEscalationPolicyRequest request)
    {
        var policy = new EscalationPolicy
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow
        };

        if (request.Steps != null)
        {
            foreach (var stepReq in request.Steps)
            {
                var step = new EscalationStep
                {
                    Id = Guid.NewGuid(),
                    PolicyId = policy.Id,
                    Order = stepReq.Order,
                    DelayMinutes = stepReq.DelayMinutes,
                    ChannelId = stepReq.ChannelId,
                    EscalateSeverity = !string.IsNullOrEmpty(stepReq.EscalateSeverity)
                        && Enum.TryParse<AlertSeverity>(stepReq.EscalateSeverity, true, out var sev)
                        ? sev : null
                };
                policy.Steps.Add(step);
            }
        }

        _context.EscalationPolicies.Add(policy);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Escalation policy created: {PolicyId} '{Name}'", policy.Id, policy.Name);

        return CreatedAtAction(nameof(GetPolicy), new { id = policy.Id },
            new EscalationPolicyDto(policy.Id, policy.Name, policy.Description, policy.IsEnabled, policy.CreatedAt, []));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> UpdatePolicy(Guid id, [FromBody] CreateEscalationPolicyRequest request)
    {
        var policy = await _context.EscalationPolicies
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (policy == null) return NotFound();

        policy.Name = request.Name;
        policy.Description = request.Description;
        policy.IsEnabled = request.IsEnabled;

        // Replace steps
        _context.EscalationSteps.RemoveRange(policy.Steps);
        policy.Steps.Clear();

        if (request.Steps != null)
        {
            foreach (var stepReq in request.Steps)
            {
                policy.Steps.Add(new EscalationStep
                {
                    Id = Guid.NewGuid(),
                    PolicyId = policy.Id,
                    Order = stepReq.Order,
                    DelayMinutes = stepReq.DelayMinutes,
                    ChannelId = stepReq.ChannelId,
                    EscalateSeverity = !string.IsNullOrEmpty(stepReq.EscalateSeverity)
                        && Enum.TryParse<AlertSeverity>(stepReq.EscalateSeverity, true, out var sev)
                        ? sev : null
                });
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeletePolicy(Guid id)
    {
        var policy = await _context.EscalationPolicies.FindAsync(id);
        if (policy == null) return NotFound();

        _context.EscalationPolicies.Remove(policy);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Escalation policy deleted: {PolicyId}", id);
        return NoContent();
    }
}
