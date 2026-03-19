// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class InboxConfiguration : IEntityTypeConfiguration<Inbox>
{
    public void Configure(EntityTypeBuilder<Inbox> builder)
    {
        builder.ToTable("inboxes");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Config).HasColumnType("jsonb").HasDefaultValueSql("'{}'");
        builder.Property(e => e.EvolutionInstanceName).HasMaxLength(100);
        builder.Property(e => e.EvolutionSessionStatus).HasMaxLength(30);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.ProxyOutbound)
            .WithMany(p => p.Inboxes)
            .HasForeignKey(e => e.ProxyOutboundId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.ProxyOutboundId)
            .HasFilter("proxy_outbound_id IS NOT NULL");
    }
}
