using System;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace NOC.Web.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_version = table.Column<short>(type: "smallint", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    disabled_reason = table.Column<string>(type: "text", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    password_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    refresh_token_hash = table.Column<string>(type: "text", nullable: true),
                    refresh_token_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "contacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    avatar_url = table.Column<string>(type: "text", nullable: true),
                    custom_attrs = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('spanish', coalesce(name, '') || ' ' || phone || ' ' || coalesce(email, ''))", stored: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    stream = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    event_version = table.Column<short>(type: "smallint", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published = table.Column<bool>(type: "boolean", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retry_count = table.Column<short>(type: "smallint", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    ip_address = table.Column<IPAddress>(type: "inet", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_events_agents_actor_id",
                        column: x => x.actor_id,
                        principalTable: "agents",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "proxy_outbounds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    alias = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    protocol = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    encrypted_password = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    last_tested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_test_ok = table.Column<bool>(type: "boolean", nullable: true),
                    last_test_latency_ms = table.Column<int>(type: "integer", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_proxy_outbounds", x => x.id);
                    table.ForeignKey(
                        name: "fk_proxy_outbounds_agents_created_by",
                        column: x => x.created_by,
                        principalTable: "agents",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "contact_tags",
                columns: table => new
                {
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tagged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contact_tags", x => new { x.contact_id, x.tag });
                    table.ForeignKey(
                        name: "fk_contact_tags_agents_tagged_by",
                        column: x => x.tagged_by,
                        principalTable: "agents",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_contact_tags_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inboxes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    config_schema_ver = table.Column<short>(type: "smallint", nullable: false),
                    encrypted_access_token = table.Column<string>(type: "text", nullable: true),
                    encrypted_refresh_token = table.Column<string>(type: "text", nullable: true),
                    secret_version = table.Column<short>(type: "smallint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    ban_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    banned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ban_reason = table.Column<string>(type: "text", nullable: true),
                    evolution_instance_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    evolution_session_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    evolution_last_heartbeat = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    proxy_outbound_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inboxes", x => x.id);
                    table.ForeignKey(
                        name: "fk_inboxes_proxy_outbounds_proxy_outbound_id",
                        column: x => x.proxy_outbound_id,
                        principalTable: "proxy_outbounds",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    inbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message_template = table.Column<string>(type: "text", nullable: false),
                    media_url = table.Column<string>(type: "text", nullable: true),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    paused_reason = table.Column<string>(type: "text", nullable: true),
                    messages_per_minute = table.Column<int>(type: "integer", nullable: false),
                    delay_min_ms = table.Column<int>(type: "integer", nullable: false),
                    delay_max_ms = table.Column<int>(type: "integer", nullable: false),
                    send_window_start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    send_window_end = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    send_window_tz = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    total_recipients = table.Column<int>(type: "integer", nullable: false),
                    sent_count = table.Column<int>(type: "integer", nullable: false),
                    delivered_count = table.Column<int>(type: "integer", nullable: false),
                    read_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campaigns", x => x.id);
                    table.ForeignKey(
                        name: "fk_campaigns_agents_created_by",
                        column: x => x.created_by,
                        principalTable: "agents",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_campaigns_inboxes_inbox_id",
                        column: x => x.inbox_id,
                        principalTable: "inboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    inbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_to = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_message_preview = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_message_direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    last_inbound_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_outbound_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    unread_count = table.Column<int>(type: "integer", nullable: false),
                    first_response_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    snoozed_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reopened_count = table.Column<short>(type: "smallint", nullable: false),
                    row_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ai_handled = table.Column<bool>(type: "boolean", nullable: false),
                    ai_escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversations_agents_assigned_to",
                        column: x => x.assigned_to,
                        principalTable: "agents",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_conversations_agents_closed_by",
                        column: x => x.closed_by,
                        principalTable: "agents",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_conversations_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conversations_inboxes_inbox_id",
                        column: x => x.inbox_id,
                        principalTable: "inboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "inbox_agents",
                columns: table => new
                {
                    inbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_agents", x => new { x.inbox_id, x.agent_id });
                    table.ForeignKey(
                        name: "fk_inbox_agents_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inbox_agents_inboxes_inbox_id",
                        column: x => x.inbox_id,
                        principalTable: "inboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_recipients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "QUEUED"),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claimed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    lease_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    external_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_campaign_recipients", x => x.id);
                    table.ForeignKey(
                        name: "fk_campaign_recipients_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalTable: "campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_campaign_recipients_contacts_contact_id",
                        column: x => x.contact_id,
                        principalTable: "contacts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    media_url = table.Column<string>(type: "text", nullable: true),
                    media_mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    media_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    media_filename = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    template_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    template_params = table.Column<string>(type: "jsonb", nullable: true),
                    delivery_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    delivery_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent_by_agent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_by_ai = table.Column<bool>(type: "boolean", nullable: false),
                    is_private_note = table.Column<bool>(type: "boolean", nullable: false),
                    provider_metadata = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_messages_agents_sent_by_agent_id",
                        column: x => x.sent_by_agent_id,
                        principalTable: "agents",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "message_status_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provider_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    detail = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message_status_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_message_status_events_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agents_email",
                table: "agents",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_actor_id_occurred_at",
                table: "audit_events",
                columns: new[] { "actor_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_entity_type_entity_id_occurred_at",
                table: "audit_events",
                columns: new[] { "entity_type", "entity_id", "occurred_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_event_type_occurred_at",
                table: "audit_events",
                columns: new[] { "event_type", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipients_campaign_id_contact_id",
                table: "campaign_recipients",
                columns: new[] { "campaign_id", "contact_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipients_campaign_id_id",
                table: "campaign_recipients",
                columns: new[] { "campaign_id", "id" },
                filter: "status = 'QUEUED'");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipients_contact_id",
                table: "campaign_recipients",
                column: "contact_id");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipients_external_id",
                table: "campaign_recipients",
                column: "external_id",
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_campaign_recipients_lease_expires_at",
                table: "campaign_recipients",
                column: "lease_expires_at",
                filter: "status = 'CLAIMED'");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_created_by",
                table: "campaigns",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_campaigns_inbox_id",
                table: "campaigns",
                column: "inbox_id");

            migrationBuilder.CreateIndex(
                name: "ix_contact_tags_tag",
                table: "contact_tags",
                column: "tag");

            migrationBuilder.CreateIndex(
                name: "ix_contact_tags_tagged_by",
                table: "contact_tags",
                column: "tagged_by");

            migrationBuilder.CreateIndex(
                name: "ix_contacts_phone",
                table: "contacts",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_contacts_search_vector",
                table: "contacts",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_assigned_to_status_last_message_at",
                table: "conversations",
                columns: new[] { "assigned_to", "status", "last_message_at" },
                descending: new[] { false, false, true },
                filter: "\"assigned_to\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_closed_by",
                table: "conversations",
                column: "closed_by");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_contact_id_inbox_id",
                table: "conversations",
                columns: new[] { "contact_id", "inbox_id" },
                unique: true,
                filter: "status NOT IN ('RESOLVED', 'ARCHIVED')");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_contact_id_status",
                table: "conversations",
                columns: new[] { "contact_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_conversations_inbox_id_status_last_message_at",
                table: "conversations",
                columns: new[] { "inbox_id", "status", "last_message_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_agents_agent_id",
                table: "inbox_agents",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_inboxes_proxy_outbound_id",
                table: "inboxes",
                column: "proxy_outbound_id",
                filter: "proxy_outbound_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_message_status_events_message_id_occurred_at",
                table: "message_status_events",
                columns: new[] { "message_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_messages_conversation_id_created_at_id",
                table: "messages",
                columns: new[] { "conversation_id", "created_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_messages_delivery_status_created_at",
                table: "messages",
                columns: new[] { "delivery_status", "created_at" },
                descending: new[] { false, true },
                filter: "direction = 'OUTBOUND' AND delivery_status IN ('PENDING', 'QUEUED', 'RETRY_PENDING')");

            migrationBuilder.CreateIndex(
                name: "ix_messages_external_id",
                table: "messages",
                column: "external_id",
                unique: true,
                filter: "\"external_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_messages_sent_by_agent_id",
                table: "messages",
                column: "sent_by_agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_events_created_at",
                table: "outbox_events",
                column: "created_at",
                filter: "published = false");

            migrationBuilder.CreateIndex(
                name: "ix_proxy_outbounds_created_by",
                table: "proxy_outbounds",
                column: "created_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "campaign_recipients");

            migrationBuilder.DropTable(
                name: "contact_tags");

            migrationBuilder.DropTable(
                name: "inbox_agents");

            migrationBuilder.DropTable(
                name: "message_status_events");

            migrationBuilder.DropTable(
                name: "outbox_events");

            migrationBuilder.DropTable(
                name: "campaigns");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "contacts");

            migrationBuilder.DropTable(
                name: "inboxes");

            migrationBuilder.DropTable(
                name: "proxy_outbounds");

            migrationBuilder.DropTable(
                name: "agents");
        }
    }
}
