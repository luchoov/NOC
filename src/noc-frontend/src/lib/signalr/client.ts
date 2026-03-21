// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

import * as signalR from '@microsoft/signalr';
import { useAuthStore } from '@/lib/store/auth.store';

// SignalR needs direct connection (WebSocket can't go through Next.js rewrites)
const HUB_URL = process.env.NEXT_PUBLIC_SIGNALR_URL || 'http://localhost:8080';

let connection: signalR.HubConnection | null = null;

function buildConnection(): signalR.HubConnection {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${HUB_URL}/hubs/noc`, {
      accessTokenFactory: () => useAuthStore.getState().accessToken || '',
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (ctx) =>
        Math.min(1000 * Math.pow(2, ctx.previousRetryCount), 30_000),
    })
    .configureLogging(signalR.LogLevel.None)
    .build();
}

export function getConnection(): signalR.HubConnection | null {
  return connection;
}

export async function startHub(): Promise<void> {
  // Always create a fresh connection to pick up current token
  if (connection) {
    try {
      await connection.stop();
    } catch {
      /* ignore */
    }
  }
  connection = buildConnection();
  try {
    await connection.start();
  } catch {
    // Hub may not be available yet — app works without real-time updates
    console.warn('[SignalR] Hub not available, real-time updates disabled');
    connection = null;
  }
}

export async function stopHub(): Promise<void> {
  if (connection) {
    try {
      await connection.stop();
    } catch {
      /* ignore */
    }
    connection = null;
  }
}

export const joinInbox = (id: string) => getConnection()?.invoke('JoinInbox', id) ?? Promise.resolve();
export const leaveInbox = (id: string) => getConnection()?.invoke('LeaveInbox', id) ?? Promise.resolve();
export const joinConversation = (id: string) => getConnection()?.invoke('JoinConversation', id) ?? Promise.resolve();
export const leaveConversation = (id: string) => getConnection()?.invoke('LeaveConversation', id) ?? Promise.resolve();
