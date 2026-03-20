// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import {
  Wifi,
  WifiOff,
  QrCode,
  Trash2,
  RefreshCw,
  Power,
  MoreVertical,
  Loader2,
  Smartphone,
} from 'lucide-react';
import { useState, useRef, useEffect } from 'react';
import { toast } from 'sonner';
import type { InboxResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { deleteInbox, provisionEvolution, getEvolutionStatus } from '@/lib/api/inboxes';
import { BAN_STATUS } from '@/lib/utils/constants';
import { timeAgo } from '@/lib/utils/format-date';
import { cn } from '@/lib/utils';

interface InboxCardProps {
  inbox: InboxResponse;
  onConnect: (inbox: InboxResponse) => void;
  onDeleted: (id: string) => void;
  onUpdated: (inbox: InboxResponse) => void;
}

export function InboxCard({ inbox, onConnect, onDeleted, onUpdated }: InboxCardProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [provisioning, setProvisioning] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) {
        setMenuOpen(false);
      }
    }
    if (menuOpen) document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [menuOpen]);

  const sessionStatus = inbox.evolutionSessionStatus?.toLowerCase();
  const isConnected = sessionStatus === 'open' || sessionStatus === 'connected';
  const isUnofficial = inbox.channelType === 'WHATSAPP_UNOFFICIAL';
  const needsProvisioning = isUnofficial && !inbox.evolutionInstanceName;

  async function handleDelete() {
    if (!confirm('¿Eliminar esta bandeja? Esta acción no se puede deshacer.')) return;
    setDeleting(true);
    try {
      await deleteInbox(inbox.id);
      toast.success('Bandeja eliminada');
      onDeleted(inbox.id);
    } catch (e: unknown) {
      const err = e as ApiError;
      if (err.status === 409) {
        toast.error('No se puede eliminar: tiene conversaciones o campañas asociadas');
      } else {
        toast.error(err.detail || 'Error al eliminar');
      }
    } finally {
      setDeleting(false);
      setMenuOpen(false);
    }
  }

  async function handleProvision() {
    setProvisioning(true);
    try {
      const res = await provisionEvolution(inbox.id, { autoConnect: true });
      onUpdated(res.inbox);
      toast.success('Instancia Evolution provisionada');

      // If QR was returned, open the QR panel
      if (res.inbox.evolutionSessionStatus === 'QR_PENDING') {
        onConnect(res.inbox);
      }
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al provisionar');
    } finally {
      setProvisioning(false);
    }
  }

  async function handleRefreshStatus() {
    setRefreshing(true);
    try {
      const res = await getEvolutionStatus(inbox.id, true);
      onUpdated(res.inbox);
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al obtener estado');
    } finally {
      setRefreshing(false);
    }
  }

  const banConfig = BAN_STATUS[inbox.banStatus] ?? BAN_STATUS.OK;

  return (
    <div className="group rounded-md border border-zinc-800/60 bg-zinc-900/50 transition-colors hover:border-zinc-700/60">
      <div className="flex items-start gap-3 p-4">
        {/* Icon */}
        <div
          className={cn(
            'grid h-9 w-9 shrink-0 place-items-center rounded-md',
            isConnected ? 'bg-emerald-500/10 text-emerald-400' : 'bg-zinc-800 text-zinc-500',
          )}
        >
          <Smartphone className="h-4 w-4" />
        </div>

        {/* Info */}
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <h3 className="truncate text-sm font-medium text-zinc-200">{inbox.name}</h3>
            {!inbox.isActive && (
              <span className="rounded bg-zinc-700/50 px-1.5 py-0.5 text-[10px] font-medium text-zinc-500">
                INACTIVA
              </span>
            )}
          </div>

          <p className="mt-0.5 text-xs text-zinc-500">{inbox.phoneNumber}</p>

          <div className="mt-2 flex flex-wrap items-center gap-1.5">
            {/* Channel badge */}
            <span className="inline-flex items-center rounded-full bg-blue-500/10 px-2 py-0.5 text-[10px] font-medium text-blue-400">
              {isUnofficial ? 'Evolution API' : 'API Oficial'}
            </span>

            {/* Session status */}
            {isUnofficial && (
              <span
                className={cn(
                  'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-medium',
                  isConnected
                    ? 'bg-emerald-500/10 text-emerald-400'
                    : sessionStatus === 'qr_pending'
                      ? 'bg-sky-500/10 text-sky-400'
                      : 'bg-zinc-500/10 text-zinc-500',
                )}
              >
                {isConnected ? <Wifi className="h-2.5 w-2.5" /> : <WifiOff className="h-2.5 w-2.5" />}
                {inbox.evolutionSessionStatus || 'DISCONNECTED'}
              </span>
            )}

            {/* Ban status */}
            {inbox.banStatus !== 'OK' && (
              <span className={cn('rounded-full px-2 py-0.5 text-[10px] font-medium', banConfig.class)}>
                {banConfig.label}
              </span>
            )}
          </div>

          {/* Evolution instance name */}
          {isUnofficial && inbox.evolutionInstanceName && (
            <p className="mt-1.5 truncate text-[10px] font-mono text-zinc-600">
              {inbox.evolutionInstanceName}
            </p>
          )}

          {/* Last heartbeat */}
          {inbox.evolutionLastHeartbeat && (
            <p className="mt-0.5 text-[10px] text-zinc-600">
              Último heartbeat: {timeAgo(inbox.evolutionLastHeartbeat)}
            </p>
          )}
        </div>

        {/* Actions */}
        <div className="flex shrink-0 items-center gap-1">
          {/* Quick connect button for unofficial */}
          {isUnofficial && !isConnected && !needsProvisioning && (
            <button
              onClick={() => onConnect(inbox)}
              title="Conectar WhatsApp"
              className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-blue-500/10 hover:text-blue-400"
            >
              <QrCode className="h-3.5 w-3.5" />
            </button>
          )}

          {/* Provision button */}
          {needsProvisioning && (
            <button
              onClick={handleProvision}
              disabled={provisioning}
              title="Provisionar instancia"
              className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-blue-500/10 hover:text-blue-400 disabled:opacity-50"
            >
              {provisioning ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <Power className="h-3.5 w-3.5" />
              )}
            </button>
          )}

          {/* Refresh status */}
          {isUnofficial && inbox.evolutionInstanceName && (
            <button
              onClick={handleRefreshStatus}
              disabled={refreshing}
              title="Actualizar estado"
              className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300 disabled:opacity-50"
            >
              <RefreshCw className={cn('h-3.5 w-3.5', refreshing && 'animate-spin')} />
            </button>
          )}

          {/* More menu */}
          <div ref={menuRef} className="relative">
            <button
              onClick={() => setMenuOpen(!menuOpen)}
              className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
            >
              <MoreVertical className="h-3.5 w-3.5" />
            </button>

            {menuOpen && (
              <div className="absolute right-0 top-8 z-20 w-40 rounded-md border border-zinc-800 bg-zinc-900 py-1 shadow-xl">
                <button
                  onClick={handleDelete}
                  disabled={deleting}
                  className="flex w-full items-center gap-2 px-3 py-1.5 text-xs text-red-400 transition-colors hover:bg-zinc-800"
                >
                  {deleting ? (
                    <Loader2 className="h-3 w-3 animate-spin" />
                  ) : (
                    <Trash2 className="h-3 w-3" />
                  )}
                  Eliminar
                </button>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
