// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NOC.Shared.Domain.Entities;

namespace NOC.Shared.Infrastructure.Data.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.Property(e => e.Id).HasDefaultValueSql("uuidv7()");
        builder.Property(e => e.ExternalId).HasMaxLength(150);
        builder.Property(e => e.MediaMimeType).HasMaxLength(100);
        builder.Property(e => e.MediaFilename).HasMaxLength(255);
        builder.Property(e => e.TemplateName).HasMaxLength(100);
        builder.Property(e => e.TemplateParams).HasColumnType("jsonb");
        builder.Property(e => e.ProviderMetadata).HasColumnType("text").HasDefaultValueSql("'{}'");
        builder.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(e => e.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(e => e.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.SentByAgent)
            .WithMany()
            .HasForeignKey(e => e.SentByAgentId);

        // Keyset pagination index (the most important one)
        builder.HasIndex(e => new { e.ConversationId, e.CreatedAt, e.Id })
            .IsDescending(false, true, true);

        // Webhook deduplication
        builder.HasIndex(e => e.ExternalId)
            .IsUnique()
            .HasFilter("\"external_id\" IS NOT NULL");

        // Outbound delivery tracking
        builder.HasIndex(e => new { e.DeliveryStatus, e.CreatedAt })
            .IsDescending(false, true)
            .HasFilter("direction = 'OUTBOUND' AND delivery_status IN ('PENDING', 'QUEUED', 'RETRY_PENDING')");
    }
}
