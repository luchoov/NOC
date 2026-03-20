// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace NOC.Shared.Domain.Enums;

public enum ConversationStatus
{
    OPEN,
    ASSIGNED,
    BOT_HANDLING,
    PENDING_CUSTOMER,
    PENDING_INTERNAL,
    SNOOZED,
    RESOLVED,
    ARCHIVED
}
