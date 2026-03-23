// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Loader2, Search, X, MessageSquarePlus } from 'lucide-react';
import { listContacts } from '@/lib/api/contacts';
import { createConversation } from '@/lib/api/conversations';
import type { ContactResponse, InboxResponse, ConversationResponse } from '@/types/api';
import { cn } from '@/lib/utils';

interface Props {
  inboxes: InboxResponse[];
  selectedInboxId: string | null;
  onCreated: (conversation: ConversationResponse) => void;
  onClose: () => void;
}

export function NewConversationModal({ inboxes, selectedInboxId, onCreated, onClose }: Props) {
  const [search, setSearch] = useState('');
  const [contacts, setContacts] = useState<ContactResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedContact, setSelectedContact] = useState<ContactResponse | null>(null);
  const [inboxId, setInboxId] = useState<string>(selectedInboxId ?? inboxes[0]?.id ?? '');
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>();
  const backdropRef = useRef<HTMLDivElement>(null);

  // Focus search on mount
  useEffect(() => { searchRef.current?.focus(); }, []);

  // Search contacts with debounce
  const searchContacts = useCallback(async (q: string) => {
    setLoading(true);
    try {
      const data = await listContacts({ search: q, limit: 20 });
      setContacts(data);
    } catch {
      setContacts([]);
    } finally {
      setLoading(false);
    }
  }, []);

  // Load initial contacts
  useEffect(() => { searchContacts(''); }, [searchContacts]);

  function handleSearchChange(value: string) {
    setSearch(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => searchContacts(value), 300);
  }

  async function handleCreate() {
    if (!selectedContact || !inboxId) return;
    setCreating(true);
    setError(null);
    try {
      const conv = await createConversation({ contactId: selectedContact.id, inboxId });
      onCreated(conv);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error al crear conversacion';
      setError(msg);
    } finally {
      setCreating(false);
    }
  }

  function handleBackdropClick(e: React.MouseEvent) {
    if (e.target === backdropRef.current) onClose();
  }

  return (
    <div
      ref={backdropRef}
      onClick={handleBackdropClick}
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm"
    >
      <div className="w-full max-w-md rounded-lg border border-zinc-800 bg-zinc-900 shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-zinc-800 px-4 py-3">
          <div className="flex items-center gap-2">
            <MessageSquarePlus className="h-4 w-4 text-blue-400" />
            <h2 className="text-sm font-medium text-zinc-200">Nueva conversacion</h2>
          </div>
          <button onClick={onClose} className="text-zinc-500 hover:text-zinc-300 transition-colors">
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="p-4 space-y-4">
          {/* Inbox selector */}
          {inboxes.length > 1 && (
            <div>
              <label className="block text-[11px] font-medium text-zinc-500 mb-1">Bandeja</label>
              <select
                value={inboxId}
                onChange={(e) => setInboxId(e.target.value)}
                className="w-full rounded-md border border-zinc-800 bg-zinc-950 px-2.5 py-1.5 text-xs text-zinc-300 outline-none focus:border-blue-500/50"
              >
                {inboxes.map((i) => (
                  <option key={i.id} value={i.id}>{i.name} ({i.phoneNumber})</option>
                ))}
              </select>
            </div>
          )}

          {/* Contact search */}
          <div>
            <label className="block text-[11px] font-medium text-zinc-500 mb-1">Contacto</label>
            <div className="relative">
              <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-zinc-600" />
              <input
                ref={searchRef}
                type="text"
                value={search}
                onChange={(e) => handleSearchChange(e.target.value)}
                placeholder="Buscar por nombre o telefono..."
                className="w-full rounded-md border border-zinc-800 bg-zinc-950 py-1.5 pl-8 pr-3 text-xs text-zinc-300 placeholder:text-zinc-600 outline-none focus:border-blue-500/50"
              />
            </div>
          </div>

          {/* Contact list */}
          <div className="max-h-56 overflow-auto rounded-md border border-zinc-800/60">
            {loading ? (
              <div className="flex justify-center py-6">
                <Loader2 className="h-4 w-4 animate-spin text-zinc-600" />
              </div>
            ) : contacts.length === 0 ? (
              <p className="py-6 text-center text-xs text-zinc-600">Sin resultados</p>
            ) : (
              contacts.map((c) => (
                <button
                  key={c.id}
                  onClick={() => setSelectedContact(c)}
                  className={cn(
                    'flex w-full items-center gap-3 px-3 py-2 text-left transition-colors hover:bg-zinc-800/50',
                    selectedContact?.id === c.id && 'bg-blue-500/10 border-l-2 border-blue-500',
                  )}
                >
                  <div className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-zinc-800 text-[11px] font-medium text-zinc-400">
                    {(c.name ?? c.phone)?.[0]?.toUpperCase() ?? '?'}
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-xs font-medium text-zinc-300">
                      {c.name ?? 'Sin nombre'}
                    </p>
                    <p className="truncate text-[11px] text-zinc-500">{c.phone}</p>
                  </div>
                </button>
              ))
            )}
          </div>

          {/* Error */}
          {error && <p className="text-xs text-red-400">{error}</p>}

          {/* Actions */}
          <div className="flex justify-end gap-2 pt-1">
            <button
              onClick={onClose}
              className="rounded-md px-3 py-1.5 text-xs text-zinc-400 hover:bg-zinc-800 transition-colors"
            >
              Cancelar
            </button>
            <button
              onClick={handleCreate}
              disabled={!selectedContact || !inboxId || creating}
              className="flex items-center gap-1.5 rounded-md bg-blue-600 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {creating && <Loader2 className="h-3 w-3 animate-spin" />}
              Iniciar conversacion
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
