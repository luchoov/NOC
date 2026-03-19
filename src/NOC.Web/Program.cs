// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NOC.Shared.Infrastructure;
using NOC.Shared.Infrastructure.Crypto;
using NOC.Shared.Infrastructure.Data;
using NOC.Shared.Infrastructure.Outbox;
using NOC.Web.Auth;
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
        }));

    // Redis + Outbox
    var redisConnection = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    builder.Services.AddOutbox(redisConnection);

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
        });
    builder.Services.AddAuthorization();

    // Services
    builder.Services.AddSingleton<TokenService>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<AuditService>();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    // Middleware pipeline
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "noc-web" }));

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
