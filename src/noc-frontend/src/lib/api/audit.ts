// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api, qs } from './client';
import type { AuditEvent, ListAuditParams } from '@/types/api';

export function listAuditEvents(params: ListAuditParams) {
  return api.get<AuditEvent[]>(`/api/audit${qs(params)}`);
}
