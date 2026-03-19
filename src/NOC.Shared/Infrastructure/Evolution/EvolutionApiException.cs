// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;

namespace NOC.Shared.Infrastructure.Evolution;

public sealed class EvolutionApiException(
    string message,
    HttpStatusCode? statusCode = null,
    string? responseBody = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;
    public string? ResponseBody { get; } = responseBody;
}

