// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class ProxyOutboundConfiguration : IEntityTypeConfiguration<ProxyOutbound>
{
    public void Configure(EntityTypeBuilder<ProxyOutbound> builder)
    {
        builder.ToTable("proxy_outbounds");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.Alias).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Host).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Username).HasMaxLength(200);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.CreatedByAgent)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy);
    }
}
