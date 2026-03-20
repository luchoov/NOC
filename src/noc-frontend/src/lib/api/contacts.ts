// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api, qs } from './client';
import type {
  ContactResponse,
  CreateContactRequest,
  UpdateContactRequest,
  AddTagRequest,
  CsvImportResult,
} from '@/types/api';

export function listContacts(params: { search?: string; tag?: string; limit?: number }) {
  return api.get<ContactResponse[]>(`/api/contacts${qs(params)}`);
}

export function getContact(id: string) {
  return api.get<ContactResponse>(`/api/contacts/${id}`);
}

export function createContact(data: CreateContactRequest) {
  return api.post<ContactResponse>('/api/contacts', data);
}

export function updateContact(id: string, data: UpdateContactRequest) {
  return api.put<ContactResponse>(`/api/contacts/${id}`, data);
}

export function deleteContact(id: string) {
  return api.del<void>(`/api/contacts/${id}`);
}

export function addTag(id: string, data: AddTagRequest) {
  return api.post<ContactResponse>(`/api/contacts/${id}/tags`, data);
}

export function removeTag(id: string, tag: string) {
  return api.del<ContactResponse>(`/api/contacts/${id}/tags/${encodeURIComponent(tag)}`);
}

export function importCsv(file: File) {
  return api.upload<CsvImportResult>('/api/contacts/import/csv', file);
}
