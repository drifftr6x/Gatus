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
[Route("api/[controller]")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(ApplicationDbContext context, ILogger<AlertsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<AlertListResponse>> GetAlerts(
        [FromQuery] string? severity = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? deviceId = null,
        [FromQuery] int limit = 100)
    {
        var query = _context.Alerts
            .Include(a => a.Device)
            .Include(a => a.AcknowledgedBy)
            .AsQueryable();

        if (!string.IsNullOrEmpty(severity) && Enum.TryParse<AlertSeverity>(severity, true, out var sev))
            query = query.Where(a => a.Severity == sev);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<AlertStatus>(status, true, out var st))
            query = query.Where(a => a.Status == st);

        if (deviceId.HasValue)
            query = query.Where(a => a.DeviceId == deviceId.Value);

        var total = await query.CountAsync();
        var activeCount = await _context.Alerts.CountAsync(a => a.Status == AlertStatus.Active);

        var alerts = await query
            .OrderByDescending(a => a.RaisedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .Select(a => new AlertDto(
                a.Id, a.DeviceId, a.Device.Name, a.Severity.ToString(), a.Title, a.Message,
                a.Status.ToString(), a.RaisedAt, a.AcknowledgedAt,
                a.AcknowledgedBy != null ? a.AcknowledgedBy.FirstName + " " + a.AcknowledgedBy.LastName : null,
                a.ResolvedAt, a.AutoResolved, a.EscalationStep))
            .ToListAsync();

        return Ok(new AlertListResponse(alerts, total, activeCount));
    }

    [HttpGet("count")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<object>> GetCount()
    {
        var active = await _context.Alerts.CountAsync(a => a.Status == AlertStatus.Active);
        var critical = await _context.Alerts.CountAsync(a => a.Status == AlertStatus.Active && a.Severity == AlertSeverity.Critical);
        return Ok(new { active, critical });
    }

    [HttpPost("{id:guid}/acknowledge")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> Acknowledge(Guid id)
    {
        var alert = await _context.Alerts.FindAsync(id);
        if (alert == null) return NotFound(new { error = "Alert not found" });

        if (alert.Status == AlertStatus.Resolved)
            return Conflict(new { error = "Alert is already resolved" });

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        Guid? userId = userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uid) ? uid : null;

        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedAt = DateTime.UtcNow;
        alert.AcknowledgedById = userId;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<IActionResult> Resolve(Guid id)
    {
        var alert = await _context.Alerts.FindAsync(id);
        if (alert == null) return NotFound(new { error = "Alert not found" });

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTime.UtcNow;
        alert.AutoResolved = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // ─── Alert Rules ───

    [HttpGet("rules")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<IEnumerable<AlertRuleDto>>> GetRules()
    {
        var rules = await _context.AlertRules
            .Include(r => r.EscalationPolicy)
            .OrderBy(r => r.Name)
            .Select(r => new AlertRuleDto(r.Id, r.Name, r.Metric, r.Operator, r.Threshold, r.Severity.ToString(), r.IsEnabled, r.CooldownMinutes, r.EscalationPolicyId, r.EscalationPolicy != null ? r.EscalationPolicy.Name : null, r.CreatedAt))
            .ToListAsync();
        return Ok(rules);
    }

    [HttpPost("rules")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<AlertRuleDto>> CreateRule([FromBody] CreateAlertRuleRequest request)
    {
        if (!Enum.TryParse<AlertSeverity>(request.Severity, true, out var severity))
            return BadRequest(new { error = "Invalid severity" });

        var rule = new AlertRule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Metric = request.Metric.ToLowerInvariant(),
            Operator = request.Operator.ToLowerInvariant(),
            Threshold = request.Threshold,
            Severity = severity,
            IsEnabled = request.IsEnabled,
            CooldownMinutes = request.CooldownMinutes > 0 ? request.CooldownMinutes : 15,
            EscalationPolicyId = request.EscalationPolicyId,
            CreatedAt = DateTime.UtcNow
        };

        _context.AlertRules.Add(rule);
        await _context.SaveChangesAsync();

        return Ok(new AlertRuleDto(rule.Id, rule.Name, rule.Metric, rule.Operator, rule.Threshold, rule.Severity.ToString(), rule.IsEnabled, rule.CooldownMinutes, rule.EscalationPolicyId, null, rule.CreatedAt));
        }

        [HttpPut("rules/{id:guid}")]
        [Authorize(Policy = "RequireEditor")]
        public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateAlertRuleRequest request)
        {
        var rule = await _context.AlertRules.FindAsync(id);
        if (rule == null) return NotFound(new { error = "Rule not found" });

        if (!Enum.TryParse<AlertSeverity>(request.Severity, true, out var severity))
            return BadRequest(new { error = "Invalid severity" });

        rule.Name = request.Name;
        rule.Threshold = request.Threshold;
        rule.Severity = severity;
        rule.IsEnabled = request.IsEnabled;
        rule.CooldownMinutes = request.CooldownMinutes > 0 ? request.CooldownMinutes : 15;
        rule.EscalationPolicyId = request.EscalationPolicyId;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("rules/{id:guid}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        var rule = await _context.AlertRules.FindAsync(id);
        if (rule == null) return NotFound(new { error = "Rule not found" });

        _context.AlertRules.Remove(rule);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
