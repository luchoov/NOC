// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class CampaignRecipientConfiguration : IEntityTypeConfiguration<CampaignRecipient>
{
    public void Configure(EntityTypeBuilder<CampaignRecipient> builder)
    {
        builder.ToTable("campaign_recipients");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.Phone).HasMaxLength(20).IsRequired();
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("QUEUED");
        builder.Property(e => e.ClaimedBy).HasMaxLength(100);
        builder.Property(e => e.ExternalId).HasMaxLength(150);

        builder.HasOne(e => e.Campaign)
            .WithMany(c => c.Recipients)
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Contact)
            .WithMany()
            .HasForeignKey(e => e.ContactId);

        // Unique per campaign+contact
        builder.HasIndex(e => new { e.CampaignId, e.ContactId }).IsUnique();

        // SKIP LOCKED batch claiming index
        builder.HasIndex(e => new { e.CampaignId, e.Id })
            .HasFilter("status = 'QUEUED'");

        // Lease expiry recovery
        builder.HasIndex(e => e.LeaseExpiresAt)
            .HasFilter("status = 'CLAIMED'");

        // External ID lookup for delivery tracking
        builder.HasIndex(e => e.ExternalId)
            .HasFilter("external_id IS NOT NULL");
    }
}
