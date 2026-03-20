// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useState } from 'react';
import { Plus, Radio, Loader2, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { listInboxes } from '@/lib/api/inboxes';
import type { InboxResponse, CreateInboxResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { InboxCard } from '@/components/settings/inbox-card';
import { InboxCreateForm } from '@/components/settings/inbox-create-form';
import { EvolutionQrPanel } from '@/components/settings/evolution-qr-panel';

export default function InboxesSettingsPage() {
  const [inboxes, setInboxes] = useState<InboxResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [createOpen, setCreateOpen] = useState(false);
  const [qrInbox, setQrInbox] = useState<InboxResponse | null>(null);

  const fetchInboxes = useCallback(async () => {
    try {
      const data = await listInboxes();
      setInboxes(data);
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || 'Error al cargar bandejas');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchInboxes();
  }, [fetchInboxes]);

  function handleCreated(res: CreateInboxResponse) {
    setInboxes((prev) => [res.inbox, ...prev]);
    setCreateOpen(false);

    // If Evolution was provisioned and QR is pending, auto-open QR panel
    if (
      res.inbox.channelType === 'WHATSAPP_UNOFFICIAL' &&
      res.inbox.evolutionSessionStatus === 'QR_PENDING'
    ) {
      setQrInbox(res.inbox);
    }
  }

  function handleDeleted(id: string) {
    setInboxes((prev) => prev.filter((i) => i.id !== id));
  }

  function handleUpdated(updated: InboxResponse) {
    setInboxes((prev) => prev.map((i) => (i.id === updated.id ? updated : i)));
    if (qrInbox?.id === updated.id) {
      setQrInbox(updated);
    }
  }

  function handleConnect(inbox: InboxResponse) {
    setQrInbox(inbox);
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
      </div>
    );
  }

  return (
    <div className="p-6">
      {/* Header */}
      <div className="mb-5 flex items-center justify-between">
        <div>
          <h2 className="text-sm font-semibold text-zinc-200">Bandejas de entrada</h2>
          <p className="mt-0.5 text-xs text-zinc-500">
            Gestión de canales de WhatsApp vía Evolution API
          </p>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={() => { setLoading(true); fetchInboxes(); }}
            className="grid h-8 w-8 place-items-center rounded-md border border-zinc-800 text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
            title="Refrescar"
          >
            <RefreshCw className="h-3.5 w-3.5" />
          </button>
          <button
            onClick={() => setCreateOpen(true)}
            className="flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-blue-500"
          >
            <Plus className="h-3.5 w-3.5" />
            Nueva bandeja
          </button>
        </div>
      </div>

      {/* List */}
      {inboxes.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-zinc-800 py-16">
          <Radio className="h-8 w-8 text-zinc-700" />
          <p className="mt-3 text-sm text-zinc-500">No hay bandejas configuradas</p>
          <button
            onClick={() => setCreateOpen(true)}
            className="mt-3 flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-blue-500"
          >
            <Plus className="h-3.5 w-3.5" />
            Crear primera bandeja
          </button>
        </div>
      ) : (
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {inboxes.map((inbox) => (
            <InboxCard
              key={inbox.id}
              inbox={inbox}
              onConnect={handleConnect}
              onDeleted={handleDeleted}
              onUpdated={handleUpdated}
            />
          ))}
        </div>
      )}

      {/* Create dialog */}
      <InboxCreateForm
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={handleCreated}
      />

      {/* QR panel */}
      {qrInbox && (
        <EvolutionQrPanel
          inbox={qrInbox}
          onStatusChange={handleUpdated}
          onClose={() => setQrInbox(null)}
        />
      )}
    </div>
  );
}
