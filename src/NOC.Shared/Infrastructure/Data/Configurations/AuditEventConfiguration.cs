// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.ActorType).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(80).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(50);
        builder.Property(e => e.Payload).HasColumnType("jsonb").HasDefaultValueSql("'{}'");
        builder.Property(e => e.OccurredAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.Actor)
            .WithMany()
            .HasForeignKey(e => e.ActorId);

        builder.HasIndex(e => new { e.ActorId, e.OccurredAt }).IsDescending(false, true);
        builder.HasIndex(e => new { e.EntityType, e.EntityId, e.OccurredAt }).IsDescending(false, false, true);
        builder.HasIndex(e => new { e.EventType, e.OccurredAt }).IsDescending(false, true);
    }
}
