// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_events");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.Stream).HasMaxLength(100).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(80).IsRequired();
        builder.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(e => e.CreatedAt)
            .HasFilter("published = false")
            .IsDescending(false);
    }
}
