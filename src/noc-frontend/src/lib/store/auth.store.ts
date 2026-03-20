// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { create } from 'zustand';
import type { Agent } from '@/types/api';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  expiresAt: string | null;
  agent: Agent | null;
  isAuthenticated: boolean;

  setTokens: (access: string, refresh: string, expires: string) => void;
  setAgent: (agent: Agent) => void;
  clearAuth: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  refreshToken: null,
  expiresAt: null,
  agent: null,
  isAuthenticated: false,

  setTokens: (accessToken, refreshToken, expiresAt) =>
    set({ accessToken, refreshToken, expiresAt, isAuthenticated: true }),

  setAgent: (agent) => set({ agent }),

  clearAuth: () =>
    set({
      accessToken: null,
      refreshToken: null,
      expiresAt: null,
      agent: null,
      isAuthenticated: false,
    }),
}));
