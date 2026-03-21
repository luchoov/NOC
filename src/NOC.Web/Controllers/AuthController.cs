// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NOC.Shared.Infrastructure.Data;
using NOC.Web.Auth;
using System.Security.Cryptography;
using System.Text;

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

        if (agent is null)
        {
            logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var passwordVerified = TryVerifyPassword(request.Password, agent.PasswordHash, out var shouldUpgradeHash);
        if (!passwordVerified)
        {
            logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        if (!agent.IsActive)
        {
            return Unauthorized(new { message = "Account is disabled" });
        }

        if (shouldUpgradeHash)
        {
            agent.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(request.Password);
            agent.PasswordVersion++;
            agent.PasswordUpdatedAt = DateTimeOffset.UtcNow;
            agent.UpdatedAt = DateTimeOffset.UtcNow;
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

    private bool TryVerifyPassword(string password, string passwordHash, out bool shouldUpgradeHash)
    {
        shouldUpgradeHash = false;

        if (string.IsNullOrWhiteSpace(passwordHash))
            return false;

        if (TryVerifyWith(() => BCrypt.Net.BCrypt.EnhancedVerify(password, passwordHash)))
            return true;

        if (TryVerifyWith(() => BCrypt.Net.BCrypt.Verify(password, passwordHash)))
        {
            shouldUpgradeHash = true;
            return true;
        }

        var sha384Base64 = Convert.ToBase64String(SHA384.HashData(Encoding.UTF8.GetBytes(password)));
        if (TryVerifyWith(() => BCrypt.Net.BCrypt.Verify(sha384Base64, passwordHash)))
        {
            shouldUpgradeHash = true;
            return true;
        }

        return false;
    }

    private bool TryVerifyWith(Func<bool> verify)
    {
        try
        {
            return verify();
        }
        catch (BCrypt.Net.SaltParseException ex)
        {
            logger.LogWarning(ex, "Skipping incompatible password hash format.");
            return false;
        }
        catch (BCrypt.Net.HashInformationException ex)
        {
            logger.LogWarning(ex, "Skipping invalid password hash metadata.");
            return false;
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Skipping malformed password hash.");
            return false;
        }
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
