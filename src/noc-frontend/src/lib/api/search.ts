// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api, qs } from './client';
import type { ContactResponse, MessageResponse, SearchMessagesParams } from '@/types/api';

export function searchContacts(q: string, tag?: string, limit?: number) {
  return api.get<{ query: string; count: number; results: ContactResponse[] }>(
    `/api/search/contacts${qs({ q, tag, limit })}`,
  );
}

export function searchMessages(params: SearchMessagesParams) {
  return api.get<{
    count: number;
    results: MessageResponse[];
    nextCursor: { createdAt: string; id: string } | null;
  }>(`/api/search/messages${qs(params)}`);
}
