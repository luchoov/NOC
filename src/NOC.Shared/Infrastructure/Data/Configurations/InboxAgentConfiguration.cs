// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class InboxAgentConfiguration : IEntityTypeConfiguration<InboxAgent>
{
    public void Configure(EntityTypeBuilder<InboxAgent> builder)
    {
        builder.ToTable("inbox_agents");

        builder.HasKey(e => new { e.InboxId, e.AgentId });

        builder.HasOne(e => e.Inbox)
            .WithMany(i => i.InboxAgents)
            .HasForeignKey(e => e.InboxId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Agent)
            .WithMany(a => a.InboxAgents)
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
