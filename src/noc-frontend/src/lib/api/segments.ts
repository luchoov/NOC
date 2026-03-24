// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api, qs } from './client';
import type {
  SegmentResponse,
  CreateSegmentRequest,
  UpdateSegmentRequest,
  ContactResponse,
} from '@/types/api';

export function listSegments() {
  return api.get<SegmentResponse[]>('/api/segments');
}

export function getSegment(id: string) {
  return api.get<SegmentResponse>(`/api/segments/${id}`);
}

export function createSegment(data: CreateSegmentRequest) {
  return api.post<SegmentResponse>('/api/segments', data);
}

export function updateSegment(id: string, data: UpdateSegmentRequest) {
  return api.put<SegmentResponse>(`/api/segments/${id}`, data);
}

export function deleteSegment(id: string) {
  return api.del<void>(`/api/segments/${id}`);
}

export function previewSegmentContacts(id: string, params?: { limit?: number }) {
  return api.get<ContactResponse[]>(`/api/segments/${id}/contacts${qs(params ?? {})}`);
}
