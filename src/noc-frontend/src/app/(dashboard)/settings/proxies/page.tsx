// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  Plus,
  Loader2,
  Trash2,
  Zap,
  Globe,
  CheckCircle2,
  XCircle,
  Clock,
  Link2,
  Unlink2,
} from 'lucide-react';
import { toast } from 'sonner';
import { createProxySchema, type CreateProxyFormData } from '@/lib/validations/proxy.schema';
import {
  listProxies,
  createProxy,
  deleteProxy,
  testProxy,
  assignToInbox,
  unassignFromInbox,
} from '@/lib/api/proxies';
import { listInboxes } from '@/lib/api/inboxes';
import type { ProxyResponse, InboxResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { PROXY_STATUS } from '@/lib/utils/constants';
import { timeAgo } from '@/lib/utils/format-date';
import { cn } from '@/lib/utils';

export default function ProxiesSettingsPage() {
  const [proxies, setProxies] = useState<ProxyResponse[]>([]);
  const [inboxes, setInboxes] = useState<InboxResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [testingId, setTestingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [assigningId, setAssigningId] = useState<string | null>(null);
  const [testResults, setTestResults] = useState<Record<string, { ok: boolean; latencyMs: number | null; error: string | null }>>({});

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CreateProxyFormData>({
    resolver: zodResolver(createProxySchema) as never,
    defaultValues: { protocol: 'HTTP', port: 8080 },
  });

  const fetchData = useCallback(async () => {
    try {
      const [proxyData, inboxData] = await Promise.all([listProxies(), listInboxes()]);
      setProxies(proxyData);
      setInboxes(inboxData);
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al cargar datos');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  async function onCreateSubmit(data: CreateProxyFormData) {
    setCreating(true);
    try {
      const res = await createProxy({
        alias: data.alias,
        host: data.host,
        port: data.port,
        protocol: data.protocol,
        username: data.username || undefined,
        password: data.password || undefined,
      });
      setProxies((prev) => [res, ...prev]);
      reset();
      toast.success('Proxy registrado');
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al crear proxy');
    } finally {
      setCreating(false);
    }
  }

  async function handleTest(id: string) {
    setTestingId(id);
    try {
      const result = await testProxy(id);
      setTestResults((prev) => ({ ...prev, [id]: result }));

      // Refresh the proxy list to get updated status
      const updated = await listProxies();
      setProxies(updated);

      if (result.ok) {
        toast.success(`Proxy OK — ${result.latencyMs}ms`);
      } else {
        toast.error(`Test fallido: ${result.error}`);
      }
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al testear proxy');
    } finally {
      setTestingId(null);
    }
  }

  async function handleDelete(id: string) {
    const proxy = proxies.find((p) => p.id === id);
    if (proxy && proxy.assignedInboxCount > 0) {
      toast.error('No se puede eliminar un proxy asignado a bandejas');
      return;
    }
    if (!confirm('¿Eliminar este proxy?')) return;

    setDeletingId(id);
    try {
      await deleteProxy(id);
      setProxies((prev) => prev.filter((p) => p.id !== id));
      toast.success('Proxy eliminado');
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al eliminar proxy');
    } finally {
      setDeletingId(null);
    }
  }

  async function handleAssign(proxyId: string, inboxId: string) {
    setAssigningId(proxyId);
    try {
      await assignToInbox(proxyId, inboxId);
      toast.success('Proxy asignado');
      await fetchData();
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al asignar proxy');
    } finally {
      setAssigningId(null);
    }
  }

  async function handleUnassign(proxyId: string, inboxId: string) {
    setAssigningId(proxyId);
    try {
      await unassignFromInbox(proxyId, inboxId);
      toast.success('Proxy desasignado');
      await fetchData();
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al desasignar proxy');
    } finally {
      setAssigningId(null);
    }
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
      </div>
    );
  }

  // Find inboxes that use each proxy
  const inboxesByProxy = new Map<string, InboxResponse[]>();
  for (const inbox of inboxes) {
    if (inbox.proxyOutboundId) {
      const existing = inboxesByProxy.get(inbox.proxyOutboundId) || [];
      existing.push(inbox);
      inboxesByProxy.set(inbox.proxyOutboundId, existing);
    }
  }

  // Available inboxes without proxies
  const unassignedInboxes = inboxes.filter((i) => !i.proxyOutboundId && i.channelType === 'WHATSAPP_UNOFFICIAL');

  return (
    <div className="flex h-full">
      {/* Left: Create form */}
      <div className="w-80 shrink-0 border-r border-zinc-800/60 p-5">
        <h3 className="text-sm font-semibold text-zinc-200">Nuevo proxy</h3>
        <p className="mt-0.5 text-xs text-zinc-500">Registrar un proxy de salida para Evolution API</p>

        <form onSubmit={handleSubmit(onCreateSubmit)} className="mt-4 space-y-3">
          <div className="space-y-1">
            <label className="block text-xs font-medium text-zinc-400">Alias</label>
            <input
              {...register('alias')}
              placeholder="proxy-ar-01"
              className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-1.5 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
            />
            {errors.alias && <p className="text-[10px] text-red-400">{errors.alias.message}</p>}
          </div>

          <div className="grid grid-cols-3 gap-2">
            <div className="col-span-2 space-y-1">
              <label className="block text-xs font-medium text-zinc-400">Host</label>
              <input
                {...register('host')}
                placeholder="proxy.example.com"
                className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-1.5 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
              />
              {errors.host && <p className="text-[10px] text-red-400">{errors.host.message}</p>}
            </div>
            <div className="space-y-1">
              <label className="block text-xs font-medium text-zinc-400">Puerto</label>
              <input
                {...register('port')}
                type="number"
                placeholder="8080"
                className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-1.5 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
              />
              {errors.port && <p className="text-[10px] text-red-400">{errors.port.message}</p>}
            </div>
          </div>

          <div className="space-y-1">
            <label className="block text-xs font-medium text-zinc-400">Protocolo</label>
            <select
              {...register('protocol')}
              className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-1.5 text-sm text-zinc-200 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
            >
              <option value="HTTP">HTTP</option>
              <option value="HTTPS">HTTPS</option>
              <option value="SOCKS5">SOCKS5</option>
            </select>
          </div>

          <div className="space-y-2 rounded-md border border-zinc-800/60 bg-zinc-950/50 p-3">
            <p className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">
              Credenciales (opcional)
            </p>
            <div className="space-y-1">
              <input
                {...register('username')}
                placeholder="Usuario"
                autoComplete="off"
                className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-1.5 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
              />
            </div>
            <div className="space-y-1">
              <input
                {...register('password')}
                type="password"
                placeholder="Contraseña"
                autoComplete="new-password"
                className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-1.5 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={creating}
            className="flex w-full items-center justify-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
          >
            {creating ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <Plus className="h-3.5 w-3.5" />
            )}
            Registrar proxy
          </button>
        </form>
      </div>

      {/* Right: Proxy list */}
      <div className="flex-1 overflow-auto p-5">
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h3 className="text-sm font-semibold text-zinc-200">Proxies registrados</h3>
            <p className="mt-0.5 text-xs text-zinc-500">
              {proxies.length} {proxies.length === 1 ? 'proxy' : 'proxies'}
            </p>
          </div>
        </div>

        {proxies.length === 0 ? (
          <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-zinc-800 py-16">
            <Globe className="h-8 w-8 text-zinc-700" />
            <p className="mt-3 text-sm text-zinc-500">No hay proxies registrados</p>
            <p className="text-xs text-zinc-600">Creá uno usando el formulario de la izquierda</p>
          </div>
        ) : (
          <div className="space-y-2">
            {proxies.map((proxy) => {
              const statusConfig = PROXY_STATUS[proxy.status] ?? PROXY_STATUS.ACTIVE;
              const result = testResults[proxy.id];
              const assignedInboxes = inboxesByProxy.get(proxy.id) || [];

              return (
                <div
                  key={proxy.id}
                  className="rounded-md border border-zinc-800/60 bg-zinc-900/50 transition-colors hover:border-zinc-700/60"
                >
                  <div className="flex items-start gap-3 p-4">
                    {/* Icon */}
                    <div
                      className={cn(
                        'grid h-9 w-9 shrink-0 place-items-center rounded-md',
                        proxy.lastTestOk === true
                          ? 'bg-emerald-500/10 text-emerald-400'
                          : proxy.lastTestOk === false
                            ? 'bg-red-500/10 text-red-400'
                            : 'bg-zinc-800 text-zinc-500',
                      )}
                    >
                      <Globe className="h-4 w-4" />
                    </div>

                    {/* Info */}
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2">
                        <h4 className="truncate text-sm font-medium text-zinc-200">{proxy.alias}</h4>
                        <span className={cn('rounded-full px-2 py-0.5 text-[10px] font-medium', statusConfig.class)}>
                          {statusConfig.label}
                        </span>
                      </div>

                      <p className="mt-0.5 font-mono text-xs text-zinc-500">
                        {proxy.protocol.toLowerCase()}://{proxy.host}:{proxy.port}
                        {proxy.hasCredentials && ' •  auth'}
                      </p>

                      {/* Test result */}
                      <div className="mt-2 flex flex-wrap items-center gap-3">
                        {proxy.lastTestedAt && (
                          <span className="inline-flex items-center gap-1 text-[10px] text-zinc-500">
                            <Clock className="h-2.5 w-2.5" />
                            Testeado {timeAgo(proxy.lastTestedAt)}
                          </span>
                        )}
                        {proxy.lastTestOk === true && proxy.lastTestLatencyMs != null && (
                          <span className="inline-flex items-center gap-1 text-[10px] text-emerald-400">
                            <CheckCircle2 className="h-2.5 w-2.5" />
                            {proxy.lastTestLatencyMs}ms
                          </span>
                        )}
                        {proxy.lastTestOk === false && proxy.lastError && (
                          <span className="inline-flex items-center gap-1 text-[10px] text-red-400">
                            <XCircle className="h-2.5 w-2.5" />
                            {proxy.lastError}
                          </span>
                        )}
                      </div>

                      {/* Inline test result */}
                      {result && (
                        <div
                          className={cn(
                            'mt-2 inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[10px] font-medium',
                            result.ok
                              ? 'bg-emerald-500/10 text-emerald-400'
                              : 'bg-red-500/10 text-red-400',
                          )}
                        >
                          {result.ok ? (
                            <>
                              <CheckCircle2 className="h-3 w-3" />
                              OK — {result.latencyMs}ms
                            </>
                          ) : (
                            <>
                              <XCircle className="h-3 w-3" />
                              {result.error}
                            </>
                          )}
                        </div>
                      )}

                      {/* Assigned inboxes */}
                      {assignedInboxes.length > 0 && (
                        <div className="mt-2 flex flex-wrap gap-1">
                          {assignedInboxes.map((inbox) => (
                            <span
                              key={inbox.id}
                              className="inline-flex items-center gap-1 rounded-full bg-blue-500/10 px-2 py-0.5 text-[10px] text-blue-400"
                            >
                              <Link2 className="h-2.5 w-2.5" />
                              {inbox.name}
                              <button
                                onClick={() => handleUnassign(proxy.id, inbox.id)}
                                disabled={assigningId === proxy.id}
                                className="ml-0.5 rounded-full p-0.5 transition-colors hover:bg-blue-500/20"
                                title="Desasignar"
                              >
                                <Unlink2 className="h-2.5 w-2.5" />
                              </button>
                            </span>
                          ))}
                        </div>
                      )}
                    </div>

                    {/* Actions */}
                    <div className="flex shrink-0 items-center gap-1">
                      {/* Assign dropdown */}
                      {unassignedInboxes.length > 0 && (
                        <AssignDropdown
                          proxyId={proxy.id}
                          inboxes={unassignedInboxes}
                          disabled={assigningId === proxy.id}
                          onAssign={handleAssign}
                        />
                      )}

                      <button
                        onClick={() => handleTest(proxy.id)}
                        disabled={testingId === proxy.id}
                        title="Testear proxy"
                        className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-blue-500/10 hover:text-blue-400 disabled:opacity-50"
                      >
                        {testingId === proxy.id ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Zap className="h-3.5 w-3.5" />
                        )}
                      </button>

                      <button
                        onClick={() => handleDelete(proxy.id)}
                        disabled={deletingId === proxy.id || proxy.assignedInboxCount > 0}
                        title={proxy.assignedInboxCount > 0 ? 'Desasignar primero' : 'Eliminar proxy'}
                        className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-red-500/10 hover:text-red-400 disabled:opacity-30"
                      >
                        {deletingId === proxy.id ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Trash2 className="h-3.5 w-3.5" />
                        )}
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

// ── Assign Dropdown ────────────────────────────────────────────────────────

function AssignDropdown({
  proxyId,
  inboxes,
  disabled,
  onAssign,
}: {
  proxyId: string;
  inboxes: InboxResponse[];
  disabled: boolean;
  onAssign: (proxyId: string, inboxId: string) => void;
}) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false);
      }
    }
    if (open) document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [open]);

  return (
    <div ref={ref} className="relative">
      <button
        onClick={() => setOpen(!open)}
        disabled={disabled}
        title="Asignar a bandeja"
        className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-blue-500/10 hover:text-blue-400 disabled:opacity-50"
      >
        <Link2 className="h-3.5 w-3.5" />
      </button>
      {open && (
        <div className="absolute right-0 top-8 z-20 w-48 rounded-md border border-zinc-800 bg-zinc-900 py-1 shadow-xl">
          <p className="px-3 py-1 text-[10px] font-medium uppercase tracking-wider text-zinc-500">
            Asignar a bandeja
          </p>
          {inboxes.map((inbox) => (
            <button
              key={inbox.id}
              onClick={() => {
                onAssign(proxyId, inbox.id);
                setOpen(false);
              }}
              className="flex w-full items-center gap-2 px-3 py-1.5 text-xs text-zinc-300 transition-colors hover:bg-zinc-800"
            >
              {inbox.name}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
