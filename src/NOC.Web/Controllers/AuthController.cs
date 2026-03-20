// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Infrastructure.Data;
using NOC.Web.Auth;

namespace NOC.Web.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(NocDbContext db, TokenService tokenService, ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(a => a.Email == request.Email);

        if (agent is null || !BCrypt.Net.BCrypt.EnhancedVerify(request.Password, agent.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        if (!agent.IsActive)
        {
            return Unauthorized(new { message = "Account is disabled" });
        }

        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(agent);
        var refreshToken = TokenService.GenerateRefreshToken();

        // Store hashed refresh token
        agent.RefreshTokenHash = TokenService.HashRefreshToken(refreshToken);
        agent.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
        agent.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        logger.LogInformation("Agent {AgentId} logged in", agent.Id);

        return Ok(new LoginResponse(accessToken, refreshToken, expiresAt));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var hashedToken = TokenService.HashRefreshToken(request.RefreshToken);

        var agent = await db.Agents.FirstOrDefaultAsync(a =>
            a.RefreshTokenHash == hashedToken &&
            a.RefreshTokenExpiresAt > DateTimeOffset.UtcNow &&
            a.IsActive);

        if (agent is null)
        {
            return Unauthorized(new { message = "Invalid or expired refresh token" });
        }

        // Rotate: generate new token pair
        var (accessToken, expiresAt) = tokenService.GenerateAccessToken(agent);
        var newRefreshToken = TokenService.GenerateRefreshToken();

        agent.RefreshTokenHash = TokenService.HashRefreshToken(newRefreshToken);
        agent.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(7);
        await db.SaveChangesAsync();

        return Ok(new LoginResponse(accessToken, newRefreshToken, expiresAt));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var agentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (agentId is null) return Unauthorized();

        var agent = await db.Agents.FindAsync(Guid.Parse(agentId));
        if (agent is not null)
        {
            agent.RefreshTokenHash = null;
            agent.RefreshTokenExpiresAt = null;
            await db.SaveChangesAsync();
        }

        return Ok(new { message = "Logged out" });
    }
}
