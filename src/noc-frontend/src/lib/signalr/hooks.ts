// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect, useState, useRef } from 'react';
import {
  getConnection,
  startHub,
  stopHub,
  joinInbox,
  leaveInbox,
  joinConversation,
  leaveConversation,
} from './client';
import { useAuthStore } from '@/lib/store/auth.store';
import type { MessageResponse } from '@/types/api';

export type ConnectionStatus = 'connected' | 'disconnected' | 'reconnecting';

export function useSignalR() {
  const [status, setStatus] = useState<ConnectionStatus>('disconnected');
  const isAuth = useAuthStore((s) => s.isAuthenticated);

  useEffect(() => {
    if (!isAuth) return;

    startHub()
      .then(() => {
        const conn = getConnection();
        if (!conn) return;
        conn.onreconnecting(() => setStatus('reconnecting'));
        conn.onreconnected(() => setStatus('connected'));
        conn.onclose(() => setStatus('disconnected'));
        setStatus('connected');
      })
      .catch(() => setStatus('disconnected'));

    return () => {
      stopHub();
    };
  }, [isAuth]);

  return status;
}

export function useInboxUpdates(
  inboxId: string | null,
  callbacks: {
    onMessageReceived?: (conversationId: string, message: MessageResponse) => void;
    onConversationAssigned?: (conversationId: string, agentId: string) => void;
    onConversationStatusChanged?: (conversationId: string, newStatus: string) => void;
    onInboxBanSuspected?: (inboxId: string, inboxName: string) => void;
    onSessionDisconnected?: (inboxId: string, instanceName: string) => void;
  },
) {
  const cbRef = useRef(callbacks);
  cbRef.current = callbacks;

  useEffect(() => {
    if (!inboxId) return;
    const conn = getConnection();
    if (!conn) return;

    joinInbox(inboxId).catch(() => {});

    const onMsg = (cId: string, m: MessageResponse) => cbRef.current.onMessageReceived?.(cId, m);
    const onAssign = (cId: string, aId: string) => cbRef.current.onConversationAssigned?.(cId, aId);
    const onStatus = (cId: string, s: string) => cbRef.current.onConversationStatusChanged?.(cId, s);
    const onBan = (iId: string, n: string) => cbRef.current.onInboxBanSuspected?.(iId, n);
    const onDisc = (iId: string, n: string) => cbRef.current.onSessionDisconnected?.(iId, n);

    conn.on('MessageReceived', onMsg);
    conn.on('ConversationAssigned', onAssign);
    conn.on('ConversationStatusChanged', onStatus);
    conn.on('InboxBanSuspected', onBan);
    conn.on('SessionDisconnected', onDisc);

    return () => {
      leaveInbox(inboxId).catch(() => {});
      conn.off('MessageReceived', onMsg);
      conn.off('ConversationAssigned', onAssign);
      conn.off('ConversationStatusChanged', onStatus);
      conn.off('InboxBanSuspected', onBan);
      conn.off('SessionDisconnected', onDisc);
    };
  }, [inboxId]);
}

export function useConversationUpdates(
  conversationId: string | null,
  callbacks: {
    onMessageReceived?: (conversationId: string, message: MessageResponse) => void;
  },
) {
  const cbRef = useRef(callbacks);
  cbRef.current = callbacks;

  useEffect(() => {
    if (!conversationId) return;
    const conn = getConnection();
    if (!conn) return;

    joinConversation(conversationId).catch(() => {});

    const onMsg = (cId: string, m: MessageResponse) => cbRef.current.onMessageReceived?.(cId, m);
    conn.on('MessageReceived', onMsg);

    return () => {
      leaveConversation(conversationId).catch(() => {});
      conn.off('MessageReceived', onMsg);
    };
  }, [conversationId]);
}
