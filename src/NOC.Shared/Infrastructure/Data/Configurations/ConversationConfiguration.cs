// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;
using NOC.Shared.Domain.Enums;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.Subject).HasMaxLength(200);
        builder.Property(e => e.LastMessagePreview).HasMaxLength(200);
        builder.Property(e => e.LastMessageDirection).HasMaxLength(10);
        builder.Property(e => e.RowVersion).HasDefaultValue(0);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

        // Optimistic locking
        builder.Property(e => e.RowVersion).IsConcurrencyToken();

        builder.HasOne(e => e.Inbox)
            .WithMany(i => i.Conversations)
            .HasForeignKey(e => e.InboxId)
            .IsRequired();

        builder.HasOne(e => e.Contact)
            .WithMany(c => c.Conversations)
            .HasForeignKey(e => e.ContactId)
            .IsRequired();

        builder.HasOne(e => e.AssignedAgent)
            .WithMany()
            .HasForeignKey(e => e.AssignedTo);

        builder.HasOne(e => e.ClosedByAgent)
            .WithMany()
            .HasForeignKey(e => e.ClosedBy);

        // Inbox tray index (the most important one)
        builder.HasIndex(e => new { e.InboxId, e.Status, e.LastMessageAt })
            .IsDescending(false, false, true);

        // Agent-specific index
        builder.HasIndex(e => new { e.AssignedTo, e.Status, e.LastMessageAt })
            .IsDescending(false, false, true)
            .HasFilter("\"assigned_to\" IS NOT NULL");

        // Contact lookup
        builder.HasIndex(e => new { e.ContactId, e.Status });

        // Only one active conversation per contact+inbox
        builder.HasIndex(e => new { e.ContactId, e.InboxId })
            .IsUnique()
            .HasFilter("status NOT IN ('RESOLVED', 'ARCHIVED')");
    }
}
