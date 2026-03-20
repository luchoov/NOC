// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect, useRef, useState, useCallback } from 'react';
import { Loader2, RefreshCw, CheckCircle2, XCircle, Wifi, WifiOff, QrCode } from 'lucide-react';
import { toast } from 'sonner';
import { connectEvolution, getEvolutionStatus } from '@/lib/api/inboxes';
import type { InboxResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { cn } from '@/lib/utils';

interface EvolutionQrPanelProps {
  inbox: InboxResponse;
  onStatusChange: (inbox: InboxResponse) => void;
  onClose: () => void;
}

type ConnectionPhase = 'idle' | 'connecting' | 'qr_pending' | 'connected' | 'error';

function extractQrCode(payload: Record<string, unknown>): string | null {
  // Evolution API returns QR in various formats depending on version
  // Try common paths: payload.base64, payload.qrcode.base64, payload.code
  if (typeof payload.base64 === 'string') return payload.base64;
  if (typeof payload.code === 'string') return payload.code;
  const qrcode = payload.qrcode as Record<string, unknown> | undefined;
  if (qrcode && typeof qrcode.base64 === 'string') return qrcode.base64;
  // Check nested instance
  const instance = payload.instance as Record<string, unknown> | undefined;
  if (instance && typeof instance.base64 === 'string') return instance.base64;

  // Fallback: look for any string that looks like a base64 QR image
  for (const value of Object.values(payload)) {
    if (typeof value === 'string' && value.startsWith('data:image')) return value;
  }
  return null;
}

function extractPairingCode(payload: Record<string, unknown>): string | null {
  if (typeof payload.pairingCode === 'string') return payload.pairingCode;
  const instance = payload.instance as Record<string, unknown> | undefined;
  if (instance && typeof instance.pairingCode === 'string') return instance.pairingCode;
  return null;
}

export function EvolutionQrPanel({ inbox, onStatusChange, onClose }: EvolutionQrPanelProps) {
  const [phase, setPhase] = useState<ConnectionPhase>('idle');
  const [qrData, setQrData] = useState<string | null>(null);
  const [pairingCode, setPairingCode] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [polling, setPolling] = useState(false);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, []);

  const stopPolling = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
    setPolling(false);
  }, []);

  const checkStatus = useCallback(async () => {
    try {
      const res = await getEvolutionStatus(inbox.id, true);
      if (!mountedRef.current) return;

      onStatusChange(res.inbox);

      const status = res.inbox.evolutionSessionStatus?.toLowerCase();
      if (status === 'open' || status === 'connected') {
        setPhase('connected');
        stopPolling();
        toast.success('WhatsApp conectado exitosamente');
      }
    } catch {
      // Silently continue polling
    }
  }, [inbox.id, onStatusChange, stopPolling]);

  const startPolling = useCallback(() => {
    stopPolling();
    setPolling(true);
    pollRef.current = setInterval(() => {
      checkStatus();
    }, 5000);
  }, [checkStatus, stopPolling]);

  async function handleConnect() {
    setPhase('connecting');
    setQrData(null);
    setPairingCode(null);
    setErrorMsg(null);

    try {
      const res = await connectEvolution(inbox.id);
      if (!mountedRef.current) return;

      onStatusChange(res.inbox);

      const qr = extractQrCode(res.payload);
      const code = extractPairingCode(res.payload);

      if (qr) {
        setQrData(qr);
        setPhase('qr_pending');
        startPolling();
      } else if (code) {
        setPairingCode(code);
        setPhase('qr_pending');
        startPolling();
      } else {
        // No QR returned — might already be connected or need provisioning
        setPhase('qr_pending');
        startPolling();
      }
    } catch (e: unknown) {
      if (!mountedRef.current) return;
      const err = e as ApiError;
      setErrorMsg(err.detail || err.message || 'Error al conectar');
      setPhase('error');
    }
  }

  async function handleRefreshStatus() {
    try {
      const res = await getEvolutionStatus(inbox.id, true);
      onStatusChange(res.inbox);

      const status = res.inbox.evolutionSessionStatus?.toLowerCase();
      if (status === 'open' || status === 'connected') {
        setPhase('connected');
        stopPolling();
        toast.success('WhatsApp conectado');
      }
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al consultar estado');
    }
  }

  const sessionStatus = inbox.evolutionSessionStatus?.toLowerCase();
  const isConnected = sessionStatus === 'open' || sessionStatus === 'connected';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="fixed inset-0 bg-black/60" onClick={onClose} />
      <div className="relative z-10 w-full max-w-sm rounded-lg border border-zinc-800 bg-zinc-900 shadow-2xl">
        <div className="flex items-center justify-between border-b border-zinc-800/60 px-5 py-3.5">
          <div className="flex items-center gap-2">
            <QrCode className="h-4 w-4 text-blue-400" />
            <h2 className="text-sm font-semibold text-zinc-200">Conectar WhatsApp</h2>
          </div>
          <button
            onClick={onClose}
            className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
          >
            ×
          </button>
        </div>

        <div className="p-5 space-y-4">
          {/* Inbox info */}
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-medium text-zinc-200">{inbox.name}</p>
              <p className="text-xs text-zinc-500">{inbox.phoneNumber}</p>
            </div>
            <span
              className={cn(
                'inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-medium',
                isConnected
                  ? 'bg-emerald-500/15 text-emerald-400'
                  : 'bg-zinc-500/15 text-zinc-400',
              )}
            >
              {isConnected ? (
                <Wifi className="h-3 w-3" />
              ) : (
                <WifiOff className="h-3 w-3" />
              )}
              {inbox.evolutionSessionStatus || 'Sin estado'}
            </span>
          </div>

          {inbox.evolutionInstanceName && (
            <p className="text-[11px] text-zinc-600 font-mono truncate">
              Instancia: {inbox.evolutionInstanceName}
            </p>
          )}

          {/* Phase: idle */}
          {phase === 'idle' && !isConnected && (
            <button
              onClick={handleConnect}
              className="flex w-full items-center justify-center gap-2 rounded-md bg-blue-600 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-blue-500"
            >
              <QrCode className="h-4 w-4" />
              Obtener código QR
            </button>
          )}

          {/* Phase: connecting */}
          {phase === 'connecting' && (
            <div className="flex flex-col items-center gap-3 py-6">
              <Loader2 className="h-8 w-8 animate-spin text-blue-400" />
              <p className="text-sm text-zinc-400">Conectando con Evolution API...</p>
            </div>
          )}

          {/* Phase: qr_pending — show QR code */}
          {phase === 'qr_pending' && (
            <div className="space-y-3">
              {qrData ? (
                <div className="flex flex-col items-center gap-3">
                  <div className="rounded-lg bg-white p-3">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                      src={qrData.startsWith('data:') ? qrData : `data:image/png;base64,${qrData}`}
                      alt="QR Code para vincular WhatsApp"
                      className="h-52 w-52"
                    />
                  </div>
                  <p className="text-center text-xs text-zinc-400">
                    Escaneá este código QR con WhatsApp en tu celular
                  </p>
                </div>
              ) : pairingCode ? (
                <div className="flex flex-col items-center gap-3 py-4">
                  <p className="text-xs text-zinc-400">Código de vinculación:</p>
                  <p className="font-mono text-2xl font-bold tracking-widest text-blue-400">
                    {pairingCode}
                  </p>
                  <p className="text-center text-xs text-zinc-500">
                    Ingresá este código en WhatsApp → Dispositivos vinculados
                  </p>
                </div>
              ) : (
                <div className="flex flex-col items-center gap-3 py-4">
                  <Loader2 className="h-6 w-6 animate-spin text-blue-400" />
                  <p className="text-xs text-zinc-400">Esperando código QR...</p>
                </div>
              )}

              {polling && (
                <div className="flex items-center justify-center gap-2 text-xs text-zinc-500">
                  <div className="h-1.5 w-1.5 animate-pulse rounded-full bg-blue-400" />
                  Verificando estado de conexión...
                </div>
              )}

              <div className="flex gap-2">
                <button
                  onClick={handleConnect}
                  className="flex flex-1 items-center justify-center gap-1.5 rounded-md border border-zinc-800 px-3 py-2 text-xs font-medium text-zinc-300 transition-colors hover:bg-zinc-800"
                >
                  <RefreshCw className="h-3 w-3" />
                  Nuevo QR
                </button>
                <button
                  onClick={handleRefreshStatus}
                  className="flex flex-1 items-center justify-center gap-1.5 rounded-md border border-zinc-800 px-3 py-2 text-xs font-medium text-zinc-300 transition-colors hover:bg-zinc-800"
                >
                  <RefreshCw className="h-3 w-3" />
                  Verificar estado
                </button>
              </div>
            </div>
          )}

          {/* Phase: connected */}
          {(phase === 'connected' || isConnected) && (
            <div className="flex flex-col items-center gap-3 py-6">
              <CheckCircle2 className="h-10 w-10 text-emerald-400" />
              <div className="text-center">
                <p className="text-sm font-medium text-zinc-200">WhatsApp conectado</p>
                <p className="text-xs text-zinc-500">La bandeja está lista para recibir mensajes</p>
              </div>
            </div>
          )}

          {/* Phase: error */}
          {phase === 'error' && (
            <div className="space-y-3">
              <div className="flex flex-col items-center gap-3 py-4">
                <XCircle className="h-10 w-10 text-red-400" />
                <div className="text-center">
                  <p className="text-sm font-medium text-zinc-200">Error de conexión</p>
                  <p className="text-xs text-red-400/80">{errorMsg}</p>
                </div>
              </div>
              <button
                onClick={handleConnect}
                className="flex w-full items-center justify-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500"
              >
                <RefreshCw className="h-4 w-4" />
                Reintentar
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
