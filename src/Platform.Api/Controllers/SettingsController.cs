using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Contracts.Responses;
using Platform.Domain.Entities;
using Platform.Infrastructure.Persistence;

namespace Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController : ControllerBase
{
    public const string ExpectedDomainKey = "domain.expected";
    public const string MismatchMetric = "domain_mismatch";
    public const string TrustMetric = "domain_trust";

    private readonly ApplicationDbContext _context;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ApplicationDbContext context, ILogger<SettingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("domain-health")]
    [Authorize(Policy = "RequireViewer")]
    public async Task<ActionResult<DomainHealthSettingsDto>> GetDomainHealth()
    {
        await EnsureDomainRulesAsync();
        var expected = await _context.PlatformSettings
            .Where(s => s.Key == ExpectedDomainKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        var mismatch = await _context.AlertRules.FirstAsync(r => r.Metric == MismatchMetric);
        var trust = await _context.AlertRules.FirstAsync(r => r.Metric == TrustMetric);

        return Ok(new DomainHealthSettingsDto(expected, mismatch.IsEnabled, trust.IsEnabled));
    }

    [HttpPut("domain-health")]
    [Authorize(Policy = "RequireEditor")]
    public async Task<ActionResult<DomainHealthSettingsDto>> PutDomainHealth([FromBody] DomainHealthSettingsDto request)
    {
        await EnsureDomainRulesAsync();

        var expected = string.IsNullOrWhiteSpace(request.ExpectedDomain)
            ? null
            : request.ExpectedDomain.Trim().TrimEnd('.');

        var setting = await _context.PlatformSettings.FindAsync(ExpectedDomainKey);
        if (setting == null)
        {
            _context.PlatformSettings.Add(new PlatformSetting
            {
                Key = ExpectedDomainKey,
                Value = expected,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = expected;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        var mismatch = await _context.AlertRules.FirstAsync(r => r.Metric == MismatchMetric);
        var trust = await _context.AlertRules.FirstAsync(r => r.Metric == TrustMetric);
        mismatch.IsEnabled = request.AlertOnMismatch;
        trust.IsEnabled = request.AlertOnTrustBroken;

        await _context.SaveChangesAsync();
        _logger.LogInformation(
            "Domain health settings updated: expected={Expected}, mismatch={Mismatch}, trust={Trust}",
            expected, request.AlertOnMismatch, request.AlertOnTrustBroken);

        return Ok(new DomainHealthSettingsDto(expected, mismatch.IsEnabled, trust.IsEnabled));
    }

    private async Task EnsureDomainRulesAsync()
    {
        var now = DateTime.UtcNow;
        if (!await _context.AlertRules.AnyAsync(r => r.Metric == MismatchMetric))
        {
            _context.AlertRules.Add(new AlertRule
            {
                Id = Guid.NewGuid(),
                Name = "Domain mismatch",
                Metric = MismatchMetric,
                Operator = "eq",
                Threshold = 0,
                Severity = AlertSeverity.Warning,
                IsEnabled = false,
                CreatedAt = now
            });
        }

        if (!await _context.AlertRules.AnyAsync(r => r.Metric == TrustMetric))
        {
            _context.AlertRules.Add(new AlertRule
            {
                Id = Guid.NewGuid(),
                Name = "Domain trust broken",
                Metric = TrustMetric,
                Operator = "eq",
                Threshold = 0,
                Severity = AlertSeverity.Critical,
                IsEnabled = false,
                CreatedAt = now
            });
        }

        await _context.SaveChangesAsync();
    }
}
