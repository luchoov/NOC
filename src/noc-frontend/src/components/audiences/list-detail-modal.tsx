// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useState } from 'react';
import { Loader2, Plus, Trash2, Users, X } from 'lucide-react';
import { toast } from 'sonner';
import { addMembers, listContactListMembers, removeMembers } from '@/lib/api/contact-lists';
import type { ContactListResponse, ContactResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { formatPhone } from '@/lib/utils/format-phone';
import { ContactPicker } from './contact-picker';

interface ListDetailModalProps {
  list: ContactListResponse | null;
  onClose: () => void;
  onUpdated?: (list: ContactListResponse) => void;
}

export function ListDetailModal({ list, onClose, onUpdated }: ListDetailModalProps) {
  const [members, setMembers] = useState<ContactResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [showPicker, setShowPicker] = useState(false);
  const [removingId, setRemovingId] = useState<string | null>(null);

  const fetchMembers = useCallback(async () => {
    if (!list) return;
    setLoading(true);
    try {
      const data = await listContactListMembers(list.id, { limit: 500 });
      setMembers(data);
    } catch {
      /* ignore */
    } finally {
      setLoading(false);
    }
  }, [list]);

  useEffect(() => {
    if (list) {
      fetchMembers();
      setShowPicker(false);
    }
  }, [list, fetchMembers]);

  useEffect(() => {
    if (!list) return;
    function handleEscape(e: KeyboardEvent) {
      if (e.key === 'Escape') onClose();
    }
    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [list, onClose]);

  async function handleAddContacts(contacts: ContactResponse[]) {
    if (!list) return;
    try {
      const result = await addMembers(list.id, { contactIds: contacts.map((c) => c.id) });
      toast.success(`${result.added} contacto${result.added === 1 ? '' : 's'} agregado${result.added === 1 ? '' : 's'}`);
      setShowPicker(false);
      await fetchMembers();
      onUpdated?.({ ...list, memberCount: result.memberCount });
    } catch (err: unknown) {
      const error = err as ApiError;
      toast.error(error.detail || 'Error al agregar contactos');
    }
  }

  async function handleRemoveMember(contactId: string) {
    if (!list) return;
    setRemovingId(contactId);
    try {
      const result = await removeMembers(list.id, { contactIds: [contactId] });
      setMembers((prev) => prev.filter((m) => m.id !== contactId));
      onUpdated?.({ ...list, memberCount: result.memberCount });
      toast.success('Contacto removido de la lista');
    } catch (err: unknown) {
      const error = err as ApiError;
      toast.error(error.detail || 'Error al remover contacto');
    } finally {
      setRemovingId(null);
    }
  }

  if (!list) return null;

  const memberIds = new Set(members.map((m) => m.id));

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-zinc-950/80 px-4 backdrop-blur-sm">
      <div className="flex max-h-[80vh] w-full max-w-lg flex-col rounded-2xl border border-zinc-800 bg-zinc-900 shadow-2xl">
        <div className="flex items-center justify-between border-b border-zinc-800/60 px-5 py-4">
          <div className="min-w-0">
            <h2 className="truncate text-sm font-semibold text-zinc-100">{list.name}</h2>
            {list.description && (
              <p className="mt-0.5 truncate text-xs text-zinc-500">{list.description}</p>
            )}
          </div>
          <button
            onClick={onClose}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <div className="flex-1 overflow-auto p-5">
          {showPicker ? (
            <ContactPicker
              excludeIds={memberIds}
              onSelect={handleAddContacts}
              onCancel={() => setShowPicker(false)}
            />
          ) : (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <span className="inline-flex items-center gap-1 text-xs text-zinc-500">
                  <Users className="h-3 w-3" />
                  {members.length} miembro{members.length === 1 ? '' : 's'}
                </span>
                <button
                  type="button"
                  onClick={() => setShowPicker(true)}
                  className="flex items-center gap-1.5 rounded-md bg-zinc-800 px-2.5 py-1.5 text-xs font-medium text-zinc-200 transition-colors hover:bg-zinc-700"
                >
                  <Plus className="h-3 w-3" />
                  Agregar
                </button>
              </div>

              {loading ? (
                <div className="flex h-20 items-center justify-center">
                  <Loader2 className="h-4 w-4 animate-spin text-zinc-600" />
                </div>
              ) : members.length === 0 ? (
                <p className="py-6 text-center text-xs text-zinc-600">
                  Esta lista esta vacia. Agrega contactos para empezar.
                </p>
              ) : (
                <div className="space-y-1">
                  {members.map((contact) => {
                    const displayName = contact.name || formatPhone(contact.phone);
                    return (
                      <div
                        key={contact.id}
                        className="group flex items-center justify-between rounded-lg px-3 py-2 transition-colors hover:bg-zinc-800/50"
                      >
                        <div className="flex min-w-0 items-center gap-2.5">
                          <div className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-blue-500/10 text-xs font-semibold text-blue-400">
                            {displayName.charAt(0).toUpperCase()}
                          </div>
                          <div className="min-w-0">
                            <p className="truncate text-sm text-zinc-200">{displayName}</p>
                            <p className="truncate text-[10px] text-zinc-500">{formatPhone(contact.phone)}</p>
                          </div>
                        </div>
                        <button
                          type="button"
                          onClick={() => handleRemoveMember(contact.id)}
                          disabled={removingId === contact.id}
                          className="grid h-6 w-6 place-items-center rounded-md text-zinc-600 opacity-0 transition-all hover:bg-red-500/10 hover:text-red-400 group-hover:opacity-100 disabled:opacity-50"
                        >
                          {removingId === contact.id ? (
                            <Loader2 className="h-3 w-3 animate-spin" />
                          ) : (
                            <Trash2 className="h-3 w-3" />
                          )}
                        </button>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
