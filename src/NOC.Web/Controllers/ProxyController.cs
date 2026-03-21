// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Crypto;
using NOC.Shared.Infrastructure.Data;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/proxies")]
[Authorize(Roles = "ADMIN,SUPERVISOR")]
public class ProxyController(
    NocDbContext db,
    AesGcmEncryptor encryptor,
    AuditService auditService,
    ILogger<ProxyController> logger) : ControllerBase
{
    private const string ProxyTestUrl = "https://httpbin.org/ip";

    // ── DTOs ─────────────────────────────────────────────────────────────

    public sealed record CreateProxyRequest(
        string Alias, string Host, int Port,
        ProxyProtocol Protocol = ProxyProtocol.HTTP,
        string? Username = null, string? Password = null);

    public sealed record ProxyResponse(
        Guid Id, string Alias, string Host, int Port, ProxyProtocol Protocol,
        bool HasCredentials, ProxyStatus Status, DateTimeOffset? LastTestedAt,
        bool? LastTestOk, int? LastTestLatencyMs, string? LastError,
        int AssignedInboxCount, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    public sealed record ProxyTestResult(bool Ok, int? LatencyMs, string? Error);

    // ── Mapping ──────────────────────────────────────────────────────────

    private static ProxyResponse ToResponse(ProxyOutbound p) => new(
        p.Id, p.Alias, p.Host, p.Port, p.Protocol,
        p.Username is not null,
        p.Status, p.LastTestedAt, p.LastTestOk, p.LastTestLatencyMs, p.LastError,
        p.Inboxes.Count,
        p.CreatedAt, p.UpdatedAt);

    // ── List ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int limit = 100)
    {
        limit = Math.Clamp(limit, 1, 200);

        var proxies = await db.ProxyOutbounds
            .AsNoTracking()
            .Include(p => p.Inboxes)
            .OrderBy(p => p.Alias)
            .Take(limit)
            .ToListAsync();

        return Ok(proxies.Select(ToResponse));
    }

    // ── Get ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var proxy = await db.ProxyOutbounds
            .AsNoTracking()
            .Include(p => p.Inboxes)
            .FirstOrDefaultAsync(p => p.Id == id);

        return proxy is null ? NotFound() : Ok(ToResponse(proxy));
    }

    // ── Create ───────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProxyRequest req)
    {
        var rawAgentId = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(rawAgentId, out var agentId))
            return Unauthorized();

        var proxy = new ProxyOutbound
        {
            Alias = req.Alias.Trim(),
            Host = req.Host.Trim(),
            Port = req.Port,
            Protocol = req.Protocol,
            Username = req.Username?.Trim(),
            EncryptedPassword = req.Password is not null ? encryptor.Encrypt(req.Password) : null,
            Status = ProxyStatus.ACTIVE,
            CreatedBy = agentId,
        };

        db.ProxyOutbounds.Add(proxy);
        await db.SaveChangesAsync();

        await auditService.LogAsync("PROXY_CREATED", "Proxy", proxy.Id,
            new { proxy.Alias, proxy.Host, proxy.Port, proxy.Protocol });

        // Reload with includes for response
        var saved = await db.ProxyOutbounds
            .AsNoTracking()
            .Include(p => p.Inboxes)
            .FirstAsync(p => p.Id == proxy.Id);

        return Created($"/api/proxies/{proxy.Id}", ToResponse(saved));
    }

    // ── Delete ───────────────────────────────────────────────────────────

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var proxy = await db.ProxyOutbounds
            .Include(p => p.Inboxes)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proxy is null) return NotFound();

        if (proxy.Inboxes.Count > 0)
            return Conflict(new { detail = $"Proxy is assigned to {proxy.Inboxes.Count} inbox(es). Unassign first." });

        db.ProxyOutbounds.Remove(proxy);
        await db.SaveChangesAsync();

        await auditService.LogAsync("PROXY_DELETED", "Proxy", id,
            new { proxy.Alias });

        return NoContent();
    }

    // ── Test connectivity ────────────────────────────────────────────────

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id)
    {
        var proxy = await db.ProxyOutbounds
            .Include(p => p.Inboxes)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (proxy is null) return NotFound();

        var sw = Stopwatch.StartNew();
        try
        {
            var webProxy = BuildWebProxy(proxy);
            using var handler = new HttpClientHandler
            {
                Proxy = webProxy,
                UseProxy = true,
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(20),
            };

            using var response = await client.GetAsync(ProxyTestUrl);
            sw.Stop();

            proxy.LastTestedAt = DateTimeOffset.UtcNow;
            proxy.LastTestOk = response.IsSuccessStatusCode;
            proxy.LastTestLatencyMs = (int)sw.ElapsedMilliseconds;
            proxy.LastError = response.IsSuccessStatusCode ? null : $"{(int)response.StatusCode} {response.ReasonPhrase}";
            if (response.IsSuccessStatusCode)
            {
                proxy.Status = proxy.Inboxes.Count > 0 ? ProxyStatus.ASSIGNED : ProxyStatus.ACTIVE;
            }
            else if (proxy.Status == ProxyStatus.ACTIVE)
            {
                proxy.Status = ProxyStatus.FAILING;
            }
            await db.SaveChangesAsync();

            return Ok(new ProxyTestResult(response.IsSuccessStatusCode, (int)sw.ElapsedMilliseconds, proxy.LastError));
        }
        catch (Exception ex)
        {
            sw.Stop();
            proxy.LastTestedAt = DateTimeOffset.UtcNow;
            proxy.LastTestOk = false;
            proxy.LastTestLatencyMs = null;
            proxy.LastError = ex.Message;
            if (proxy.Status == ProxyStatus.ACTIVE)
                proxy.Status = ProxyStatus.FAILING;
            await db.SaveChangesAsync();

            logger.LogWarning(ex, "Proxy test failed for {Alias} ({Host}:{Port})", proxy.Alias, proxy.Host, proxy.Port);
            return Ok(new ProxyTestResult(false, null, ex.Message));
        }
    }

    // ── Assign to inbox ──────────────────────────────────────────────────

    [HttpPost("{id:guid}/assign/{inboxId:guid}")]
    public async Task<IActionResult> Assign(Guid id, Guid inboxId)
    {
        var proxy = await db.ProxyOutbounds
            .Include(p => p.Inboxes)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (proxy is null) return NotFound(new { detail = "Proxy not found" });

        var inbox = await db.Inboxes.FindAsync(inboxId);
        if (inbox is null) return NotFound(new { detail = "Inbox not found" });

        if (proxy.Inboxes.Any(i => i.Id == inboxId))
            return Conflict(new { detail = "Already assigned" });

        inbox.ProxyOutboundId = proxy.Id;
        if (proxy.Status is ProxyStatus.ACTIVE or ProxyStatus.FAILING)
            proxy.Status = ProxyStatus.ASSIGNED;
        await db.SaveChangesAsync();

        await auditService.LogAsync("PROXY_ASSIGNED", "Proxy", id,
            new { InboxId = inboxId, InboxName = inbox.Name });

        return Ok(new { message = "Assigned" });
    }

    // ── Unassign from inbox ──────────────────────────────────────────────

    [HttpDelete("{id:guid}/assign/{inboxId:guid}")]
    public async Task<IActionResult> Unassign(Guid id, Guid inboxId)
    {
        var proxy = await db.ProxyOutbounds
            .Include(p => p.Inboxes)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (proxy is null) return NotFound(new { detail = "Proxy not found" });

        var inbox = proxy.Inboxes.FirstOrDefault(i => i.Id == inboxId);
        if (inbox is null) return NotFound(new { detail = "Not assigned" });

        inbox.ProxyOutboundId = null;
        if (!proxy.Inboxes.Any(i => i.Id != inboxId) && proxy.Status == ProxyStatus.ASSIGNED)
            proxy.Status = ProxyStatus.ACTIVE;
        await db.SaveChangesAsync();

        await auditService.LogAsync("PROXY_UNASSIGNED", "Proxy", id,
            new { InboxId = inboxId });

        return Ok(new { message = "Unassigned" });
    }

    private IWebProxy BuildWebProxy(ProxyOutbound proxy)
    {
        var scheme = proxy.Protocol switch
        {
            ProxyProtocol.HTTPS => "https",
            ProxyProtocol.SOCKS5 => "socks5",
            _ => "http",
        };

        var webProxy = new WebProxy($"{scheme}://{proxy.Host}:{proxy.Port}");
        if (!string.IsNullOrWhiteSpace(proxy.Username))
        {
            var password = proxy.EncryptedPassword is null
                ? null
                : encryptor.Decrypt(proxy.EncryptedPassword);
            webProxy.Credentials = new NetworkCredential(proxy.Username, password);
        }

        return webProxy;
    }
}
