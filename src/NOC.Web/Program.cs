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
        await context.Response.WriteAsJsonAsync(new { error = ex?.GetType().Name, message = ex?.Message, stack = ex?.StackTrace?[..Math.Min(ex.StackTrace.Length, 2000)] });
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
