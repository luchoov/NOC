// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class MessageStatusEventConfiguration : IEntityTypeConfiguration<MessageStatusEvent>
{
    public void Configure(EntityTypeBuilder<MessageStatusEvent> builder)
    {
        builder.ToTable("message_status_events");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.ProviderCode).HasMaxLength(50);
        builder.Property(e => e.OccurredAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.Message)
            .WithMany(m => m.StatusEvents)
            .HasForeignKey(e => e.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.MessageId, e.OccurredAt })
            .IsDescending(false, true);
    }
}
