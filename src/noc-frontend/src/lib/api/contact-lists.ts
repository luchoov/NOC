// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api, qs } from './client';
import type {
  ContactListResponse,
  CreateContactListRequest,
  UpdateContactListRequest,
  AddMembersRequest,
  RemoveMembersRequest,
  ContactResponse,
} from '@/types/api';

export function listContactLists() {
  return api.get<ContactListResponse[]>('/api/contact-lists');
}

export function getContactList(id: string) {
  return api.get<ContactListResponse>(`/api/contact-lists/${id}`);
}

export function createContactList(data: CreateContactListRequest) {
  return api.post<ContactListResponse>('/api/contact-lists', data);
}

export function updateContactList(id: string, data: UpdateContactListRequest) {
  return api.put<ContactListResponse>(`/api/contact-lists/${id}`, data);
}

export function deleteContactList(id: string) {
  return api.del<void>(`/api/contact-lists/${id}`);
}

export function listContactListMembers(id: string, params?: { limit?: number }) {
  return api.get<ContactResponse[]>(`/api/contact-lists/${id}/members${qs(params ?? {})}`);
}

export function addMembers(id: string, data: AddMembersRequest) {
  return api.post<{ added: number; memberCount: number }>(`/api/contact-lists/${id}/members`, data);
}

export function removeMembers(id: string, data: RemoveMembersRequest) {
  return api.post<{ removed: number; memberCount: number }>(`/api/contact-lists/${id}/members/remove`, data);
}
