// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Domain.Entities;

public class ProxyOutbound
{
    public Guid Id { get; set; }
    public required string Alias { get; set; }
    public required string Host { get; set; }
    public int Port { get; set; }
    public ProxyProtocol Protocol { get; set; } = ProxyProtocol.HTTP;
    public string? Username { get; set; }
    public string? EncryptedPassword { get; set; }

    public ProxyStatus Status { get; set; } = ProxyStatus.ACTIVE;
    public DateTimeOffset? LastTestedAt { get; set; }
    public bool? LastTestOk { get; set; }
    public int? LastTestLatencyMs { get; set; }
    public string? LastError { get; set; }

    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Agent? CreatedByAgent { get; set; }
    public ICollection<Inbox> Inboxes { get; set; } = [];
}
