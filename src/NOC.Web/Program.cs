// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Crypto;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Evolution;
using NOC.Shared.Infrastructure.Outbox;
using NOC.Shared.Infrastructure.Storage;
using NOC.Web.Auth;
using NOC.Web.Hubs;
using NOC.Web.Middleware;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Service", "NOC.Web")
        .WriteTo.Console());

    // Database
    builder.Services.AddDbContext<NocDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"), npgsql =>
        {
            npgsql.MigrationsAssembly("NOC.Web");
        }).UseSnakeCaseNamingConvention());

    // Redis + Outbox
    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddOutbox(redisConnection);
    builder.Services.AddEvolutionApiClient(builder.Configuration);
    builder.Services.AddMediaStorage(builder.Configuration);

    // Encryption
    var masterKeyBase64 = builder.Configuration["Encryption:MasterKey"];
    if (!string.IsNullOrEmpty(masterKeyBase64))
    {
        var masterKey = Convert.FromBase64String(masterKeyBase64);
        builder.Services.AddSingleton(new AesGcmEncryptor(masterKey));
    }

    // JWT Authentication
    var jwtSecret = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("Jwt:Secret is required");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "noc-api",
                ValidateAudience = true,
                ValidAudience = builder.Configuration["Jwt:Audience"] ?? "noc-clients",
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                ClockSkew = TimeSpan.Zero,
            };

            // SignalR sends JWT via query string (WebSocket can't use headers)
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) &&
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
            };
        });
    builder.Services.AddAuthorization();

    // Services
    builder.Services.AddSingleton<TokenService>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<AuditService>();
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddSignalR()
        .AddJsonProtocol(options =>
        {
            options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddHostedService<SignalRBridge>();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    var app = builder.Build();

    // Global exception handler — return details in non-production
    app.UseExceptionHandler(err => err.Run(async context =>
    {
        var ex = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var stack = ex?.StackTrace;
        await context.Response.WriteAsJsonAsync(new { error = ex?.GetType().Name, message = ex?.Message, inner = ex?.InnerException?.Message, stack = stack != null ? stack[..Math.Min(stack.Length, 2000)] : null });
    }));

    // Middleware pipeline
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<NocHub>("/hubs/noc");
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "noc-web" }));

    // Ensure MinIO bucket exists
    try
    {
        var mediaStorage = app.Services.GetRequiredService<IMediaStorageService>();
        await mediaStorage.EnsureBucketAsync();
    }
    catch (Exception bucketEx)
    {
        Log.Warning(bucketEx, "Failed to ensure MinIO bucket on startup");
    }

    // Ensure campaign tables exist (created outside EF migrations)
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NocDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS campaigns (
                id uuid PRIMARY KEY DEFAULT uuidv7(),
                inbox_id uuid NOT NULL REFERENCES inboxes(id) ON DELETE CASCADE,
                name character varying(200) NOT NULL,
                status character varying(20) NOT NULL,
                message_template text NOT NULL,
                media_url text,
                scheduled_at timestamptz,
                started_at timestamptz,
                completed_at timestamptz,
                paused_at timestamptz,
                paused_reason text,
                messages_per_minute integer NOT NULL DEFAULT 10,
                delay_min_ms integer NOT NULL DEFAULT 2000,
                delay_max_ms integer NOT NULL DEFAULT 8000,
                send_window_start time,
                send_window_end time,
                send_window_tz character varying(50),
                total_recipients integer NOT NULL DEFAULT 0,
                sent_count integer NOT NULL DEFAULT 0,
                delivered_count integer NOT NULL DEFAULT 0,
                read_count integer NOT NULL DEFAULT 0,
                failed_count integer NOT NULL DEFAULT 0,
                created_by uuid REFERENCES agents(id),
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE INDEX IF NOT EXISTS ix_campaigns_inbox_id ON campaigns(inbox_id);
            CREATE INDEX IF NOT EXISTS ix_campaigns_created_by ON campaigns(created_by);

            CREATE TABLE IF NOT EXISTS campaign_recipients (
                id uuid PRIMARY KEY DEFAULT uuidv7(),
                campaign_id uuid NOT NULL REFERENCES campaigns(id) ON DELETE CASCADE,
                contact_id uuid NOT NULL REFERENCES contacts(id) ON DELETE CASCADE,
                phone character varying(20) NOT NULL,
                status character varying(20) NOT NULL DEFAULT 'QUEUED',
                claimed_at timestamptz,
                claimed_by character varying(100),
                lease_expires_at timestamptz,
                external_id character varying(150),
                sent_at timestamptz,
                delivered_at timestamptz,
                read_at timestamptz,
                failed_at timestamptz,
                failure_reason text,
                retry_count smallint NOT NULL DEFAULT 0
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_campaign_recipients_campaign_id_contact_id ON campaign_recipients(campaign_id, contact_id);
            CREATE INDEX IF NOT EXISTS ix_campaign_recipients_campaign_id_id ON campaign_recipients(campaign_id, id) WHERE status = 'QUEUED';
            CREATE INDEX IF NOT EXISTS ix_campaign_recipients_lease_expires_at ON campaign_recipients(lease_expires_at) WHERE status = 'CLAIMED';
            CREATE INDEX IF NOT EXISTS ix_campaign_recipients_external_id ON campaign_recipients(external_id) WHERE external_id IS NOT NULL;
            CREATE INDEX IF NOT EXISTS ix_campaign_recipients_contact_id ON campaign_recipients(contact_id);
            """);
        Log.Information("Campaign tables ensured");
    }
    catch (Exception campaignEx)
    {
        Log.Warning(campaignEx, "Failed to ensure campaign tables on startup");
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
