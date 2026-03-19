// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Domain.Entities;

public class InboxAgent
{
    public Guid InboxId { get; set; }
    public Guid AgentId { get; set; }

    // Navigation
    public Inbox Inbox { get; set; } = null!;
    public Agent Agent { get; set; } = null!;
}
