// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Infrastructure.Data;

namespace NOC.Shared.Infrastructure;

public class AuditService(NocDbContext db, IHttpContextAccessor httpContext)
{
    public async Task LogAsync(
        string eventType,
        string? entityType = null,
        Guid? entityId = null,
        object? payload = null,
        string actorType = "AGENT")
    {
        var agentIdClaim = httpContext.HttpContext?.User.FindFirst("sub")?.Value
            ?? httpContext.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        IPAddress? ip = null;
        if (httpContext.HttpContext?.Connection.RemoteIpAddress is { } remoteIp)
        {
            ip = remoteIp;
        }

        db.AuditEvents.Add(new AuditEvent
        {
            ActorId = agentIdClaim is not null ? Guid.Parse(agentIdClaim) : null,
            ActorType = actorType,
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            Payload = payload is not null ? JsonSerializer.Serialize(payload) : "{}",
            IpAddress = ip,
        });

        await db.SaveChangesAsync();
    }
}
