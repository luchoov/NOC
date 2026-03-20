// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import { create } from 'zustand';
import type { ConversationStatus } from '@/types/api';

interface UIState {
  sidebarCollapsed: boolean;
  selectedInboxId: string | null;
  conversationFilter: 'all' | 'unassigned' | 'mine';
  statusFilter: ConversationStatus | null;

  toggleSidebar: () => void;
  setSelectedInboxId: (id: string | null) => void;
  setConversationFilter: (f: 'all' | 'unassigned' | 'mine') => void;
  setStatusFilter: (s: ConversationStatus | null) => void;
}

export const useUIStore = create<UIState>((set) => ({
  sidebarCollapsed: false,
  selectedInboxId: null,
  conversationFilter: 'all',
  statusFilter: null,

  toggleSidebar: () => set((s) => ({ sidebarCollapsed: !s.sidebarCollapsed })),
  setSelectedInboxId: (id) => set({ selectedInboxId: id }),
  setConversationFilter: (f) => set({ conversationFilter: f }),
  setStatusFilter: (s) => set({ statusFilter: s }),
}));
