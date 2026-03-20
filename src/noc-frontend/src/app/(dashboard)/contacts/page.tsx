// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { FilterX, Loader2, Plus, RefreshCw, Search, Users } from 'lucide-react';
import { toast } from 'sonner';
import { ContactCreateModal } from '@/components/contacts/contact-create-modal';
import { ContactListItem } from '@/components/contacts/contact-list-item';
import { listContacts } from '@/lib/api/contacts';
import type { ApiError } from '@/lib/api/client';
import { cn } from '@/lib/utils';
import type { ContactResponse } from '@/types/api';

export default function ContactsPage() {
  const [contacts, setContacts] = useState<ContactResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [selectedTag, setSelectedTag] = useState<string | null>(null);
  const fetchToken = useRef(0);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setSearch(searchInput.trim());
    }, 300);

    return () => window.clearTimeout(timeout);
  }, [searchInput]);

  const fetchContacts = useCallback(async (mode: 'filter' | 'refresh' = 'filter') => {
    const token = ++fetchToken.current;

    if (mode === 'refresh') {
      setRefreshing(true);
    } else {
      setLoading(true);
    }

    try {
      const data = await listContacts({
        search: search || undefined,
        tag: selectedTag || undefined,
        limit: 200,
      });

      if (token !== fetchToken.current) {
        return;
      }

      setContacts(data);
    } catch (error: unknown) {
      const err = error as ApiError;
      toast.error(err.detail || 'Error al cargar contactos');
    } finally {
      if (token === fetchToken.current) {
        setLoading(false);
        setRefreshing(false);
      }
    }
  }, [search, selectedTag]);

  useEffect(() => {
    void fetchContacts();
  }, [fetchContacts]);

  const knownTags = Array.from(
    new Set([
      ...contacts.flatMap((contact) => contact.tags),
      ...(selectedTag ? [selectedTag] : []),
    ]),
  ).sort((left, right) => left.localeCompare(right));

  function clearFilters() {
    setSearchInput('');
    setSearch('');
    setSelectedTag(null);
  }

  function handleCreated() {
    setCreateOpen(false);
    if (search || selectedTag) {
      clearFilters();
      return;
    }

    void fetchContacts('refresh');
  }

  return (
    <>
      <div className="flex h-full flex-col">
        <div className="border-b border-zinc-800/60 px-6 py-5">
          <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
            <div>
              <h2 className="text-sm font-semibold text-zinc-100">Contactos</h2>
              <p className="mt-1 text-xs text-zinc-500">
                Base de contactos para conversaciones, segmentacion y futuras campanas.
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <button
                onClick={() => void fetchContacts('refresh')}
                className="grid h-9 w-9 place-items-center rounded-md border border-zinc-800 text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
                title="Refrescar"
              >
                <RefreshCw className={cn('h-4 w-4', refreshing && 'animate-spin')} />
              </button>
              <button
                onClick={() => setCreateOpen(true)}
                className="flex items-center gap-2 rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-500"
              >
                <Plus className="h-3.5 w-3.5" />
                Nuevo contacto
              </button>
            </div>
          </div>

          <div className="mt-4 space-y-3">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-zinc-600" />
              <input
                value={searchInput}
                onChange={(event) => setSearchInput(event.target.value)}
                placeholder="Buscar por nombre, telefono o email..."
                className="block w-full rounded-md border border-zinc-800 bg-zinc-950 py-2 pl-9 pr-3 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
              />
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <span className="text-[11px] font-medium uppercase tracking-[0.12em] text-zinc-600">
                Tags
              </span>

              {knownTags.length === 0 ? (
                <span className="text-xs text-zinc-600">Todavia no hay etiquetas cargadas.</span>
              ) : (
                knownTags.map((tag) => (
                  <button
                    key={tag}
                    onClick={() => setSelectedTag((current) => (current === tag ? null : tag))}
                    className={cn(
                      'rounded-full px-2.5 py-1 text-[11px] font-medium transition-colors',
                      selectedTag === tag
                        ? 'bg-blue-500/15 text-blue-400'
                        : 'bg-zinc-900 text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300',
                    )}
                  >
                    {tag}
                  </button>
                ))
              )}

              {(selectedTag || search) && (
                <button
                  onClick={clearFilters}
                  className="ml-auto inline-flex items-center gap-1 rounded-md px-2 py-1 text-[11px] text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
                >
                  <FilterX className="h-3.5 w-3.5" />
                  Limpiar filtros
                </button>
              )}
            </div>
          </div>
        </div>

        <div className="flex-1 overflow-auto px-6 py-5">
          {loading ? (
            <div className="flex h-64 items-center justify-center">
              <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
            </div>
          ) : contacts.length === 0 ? (
            <div className="flex h-full min-h-80 flex-col items-center justify-center rounded-2xl border border-dashed border-zinc-800 bg-zinc-950/40 px-6 text-center">
              <Users className="h-10 w-10 text-zinc-700" />
              <h3 className="mt-4 text-sm font-semibold text-zinc-200">
                {search || selectedTag ? 'No encontramos contactos' : 'Todavia no hay contactos'}
              </h3>
              <p className="mt-2 max-w-md text-xs leading-5 text-zinc-500">
                {search || selectedTag
                  ? 'Proba con otro termino o removiendo filtros. Tambien podes dar de alta un contacto nuevo manualmente.'
                  : 'Crea el primer contacto para empezar a poblar la bandeja y preparar futuras segmentaciones.'}
              </p>
              <button
                onClick={() => setCreateOpen(true)}
                className="mt-4 flex items-center gap-2 rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-500"
              >
                <Plus className="h-3.5 w-3.5" />
                Crear contacto
              </button>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="flex items-center justify-between gap-3 text-xs text-zinc-500">
                <p>
                  {contacts.length} contacto{contacts.length === 1 ? '' : 's'}
                  {selectedTag ? ` con tag "${selectedTag}"` : ''}
                </p>
                <p>Maximo 200 resultados por consulta</p>
              </div>

              <div className="grid gap-3 xl:grid-cols-2">
                {contacts.map((contact) => (
                  <ContactListItem key={contact.id} contact={contact} />
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      <ContactCreateModal
        open={createOpen}
        onClose={() => setCreateOpen(false)}
        onCreated={handleCreated}
      />
    </>
  );
}
