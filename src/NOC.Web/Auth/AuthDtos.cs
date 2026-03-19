// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Web.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

public record RefreshRequest(string RefreshToken);
