// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { api } from './client';
import type { LoginRequest, LoginResponse, RefreshRequest } from '@/types/api';

export function login(data: LoginRequest) {
  return api.post<LoginResponse>('/api/auth/login', data);
}

export function refresh(data: RefreshRequest) {
  return api.post<LoginResponse>('/api/auth/refresh', data);
}

export function logout() {
  return api.post<void>('/api/auth/logout');
}
