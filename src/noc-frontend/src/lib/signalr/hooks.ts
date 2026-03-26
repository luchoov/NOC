// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect, useState, useRef, useSyncExternalStore } from 'react';
import {
  getConnection,
  getReadyVersion,
  onReady,
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

/** Subscribe to the connection-ready version so hooks re-run when connection is established. */
function useHubReady(): number {
  return useSyncExternalStore(
    onReady,
    getReadyVersion,
    () => 0, // server snapshot
  );
}

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

export interface MessageStatusUpdatePayload {
  messageId: string;
  deliveryStatus: string;
}

export interface PresenceUpdatePayload {
  phone: string;
  presence: string; // "composing" | "paused" | "available" | "unavailable"
}

export function useInboxUpdates(
  inboxIds: string[],
  callbacks: {
    onMessageReceived?: (conversationId: string, message: MessageResponse) => void;
    onConversationAssigned?: (conversationId: string, agentId: string) => void;
    onConversationStatusChanged?: (conversationId: string, newStatus: string) => void;
    onInboxBanSuspected?: (inboxId: string, inboxName: string) => void;
    onSessionDisconnected?: (inboxId: string, instanceName: string) => void;
    onMessageStatusUpdate?: (conversationId: string, payload: MessageStatusUpdatePayload) => void;
    onPresenceUpdate?: (payload: PresenceUpdatePayload) => void;
  },
) {
  const cbRef = useRef(callbacks);
  cbRef.current = callbacks;

  // Stable key so useEffect re-runs only when the actual IDs change
  const idsKey = inboxIds.slice().sort().join(',');

  // Re-run when connection becomes ready (fixes race condition with useSignalR)
  const ready = useHubReady();

  useEffect(() => {
    if (inboxIds.length === 0) return;
    const conn = getConnection();
    if (!conn) return;

    // Join all provided inbox groups
    for (const id of inboxIds) {
      joinInbox(id).catch(() => {});
    }

    const onMsg = (cId: string, m: MessageResponse) => cbRef.current.onMessageReceived?.(cId, m);
    const onAssign = (cId: string, aId: string) => cbRef.current.onConversationAssigned?.(cId, aId);
    const onStatus = (cId: string, s: string) => cbRef.current.onConversationStatusChanged?.(cId, s);
    const onBan = (iId: string, n: string) => cbRef.current.onInboxBanSuspected?.(iId, n);
    const onDisc = (iId: string, n: string) => cbRef.current.onSessionDisconnected?.(iId, n);
    const onMsgStatus = (cId: string, p: MessageStatusUpdatePayload) => cbRef.current.onMessageStatusUpdate?.(cId, p);
    const onPresence = (p: PresenceUpdatePayload) => cbRef.current.onPresenceUpdate?.(p);

    conn.on('MessageReceived', onMsg);
    conn.on('ConversationAssigned', onAssign);
    conn.on('ConversationStatusChanged', onStatus);
    conn.on('InboxBanSuspected', onBan);
    conn.on('SessionDisconnected', onDisc);
    conn.on('MessageStatusUpdate', onMsgStatus);
    conn.on('PresenceUpdate', onPresence);

    return () => {
      for (const id of inboxIds) {
        leaveInbox(id).catch(() => {});
      }
      conn.off('MessageReceived', onMsg);
      conn.off('ConversationAssigned', onAssign);
      conn.off('ConversationStatusChanged', onStatus);
      conn.off('InboxBanSuspected', onBan);
      conn.off('SessionDisconnected', onDisc);
      conn.off('MessageStatusUpdate', onMsgStatus);
      conn.off('PresenceUpdate', onPresence);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [idsKey, ready]);
}

export interface CampaignProgressPayload {
  status: string;
  totalRecipients: number;
  sentCount: number;
  deliveredCount: number;
  readCount: number;
  failedCount: number;
}

export function useCampaignUpdates(
  callbacks: {
    onCampaignProgress?: (campaignId: string, payload: CampaignProgressPayload) => void;
  },
) {
  const cbRef = useRef(callbacks);
  cbRef.current = callbacks;

  const ready = useHubReady();

  useEffect(() => {
    const conn = getConnection();
    if (!conn) return;

    const onProgress = (campaignId: string, payload: CampaignProgressPayload) =>
      cbRef.current.onCampaignProgress?.(campaignId, payload);

    conn.on('CampaignProgress', onProgress);

    return () => {
      conn.off('CampaignProgress', onProgress);
    };
  }, [ready]);
}

export function useConversationUpdates(
  conversationId: string | null,
  callbacks: {
    onMessageReceived?: (conversationId: string, message: MessageResponse) => void;
    onMessageStatusUpdate?: (conversationId: string, payload: MessageStatusUpdatePayload) => void;
  },
) {
  const cbRef = useRef(callbacks);
  cbRef.current = callbacks;

  const ready = useHubReady();

  useEffect(() => {
    if (!conversationId) return;
    const conn = getConnection();
    if (!conn) return;

    joinConversation(conversationId).catch(() => {});

    const onMsg = (cId: string, m: MessageResponse) => cbRef.current.onMessageReceived?.(cId, m);
    const onMsgStatus = (cId: string, p: MessageStatusUpdatePayload) => cbRef.current.onMessageStatusUpdate?.(cId, p);
    conn.on('MessageReceived', onMsg);
    conn.on('MessageStatusUpdate', onMsgStatus);

    return () => {
      leaveConversation(conversationId).catch(() => {});
      conn.off('MessageReceived', onMsg);
      conn.off('MessageStatusUpdate', onMsgStatus);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversationId, ready]);
}
