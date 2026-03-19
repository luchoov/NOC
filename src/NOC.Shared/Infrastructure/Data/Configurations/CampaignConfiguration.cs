// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.MessageTemplate).IsRequired();
        builder.Property(e => e.SendWindowTz).HasMaxLength(50);
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.Inbox)
            .WithMany(i => i.Campaigns)
            .HasForeignKey(e => e.InboxId)
            .IsRequired();

        builder.HasOne(e => e.CreatedByAgent)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy);
    }
}
