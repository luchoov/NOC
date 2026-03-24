// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Loader2, Search } from 'lucide-react';
import { listContacts } from '@/lib/api/contacts';
import type { ContactResponse } from '@/types/api';
import { formatPhone } from '@/lib/utils/format-phone';

interface ContactPickerProps {
  excludeIds?: Set<string>;
  onSelect: (contacts: ContactResponse[]) => void;
  onCancel: () => void;
}

export function ContactPicker({ excludeIds, onSelect, onCancel }: ContactPickerProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<ContactResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [selected, setSelected] = useState<Map<string, ContactResponse>>(new Map());
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const search = useCallback(
    async (q: string) => {
      setLoading(true);
      try {
        const contacts = await listContacts({ search: q || undefined, limit: 50 });
        setResults(excludeIds ? contacts.filter((c) => !excludeIds.has(c.id)) : contacts);
      } catch {
        /* ignore */
      } finally {
        setLoading(false);
      }
    },
    [excludeIds],
  );

  useEffect(() => {
    search('');
  }, [search]);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => search(query), 300);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [query, search]);

  function toggleContact(contact: ContactResponse) {
    setSelected((prev) => {
      const next = new Map(prev);
      if (next.has(contact.id)) {
        next.delete(contact.id);
      } else {
        next.set(contact.id, contact);
      }
      return next;
    });
  }

  return (
    <div className="space-y-3">
      <div className="relative">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-zinc-600" />
        <input
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Buscar contactos..."
          className="block w-full rounded-md border border-zinc-800 bg-zinc-950 py-2 pl-9 pr-3 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
        />
      </div>

      <div className="max-h-60 overflow-auto rounded-md border border-zinc-800/60">
        {loading ? (
          <div className="flex h-20 items-center justify-center">
            <Loader2 className="h-4 w-4 animate-spin text-zinc-600" />
          </div>
        ) : results.length === 0 ? (
          <p className="p-3 text-center text-xs text-zinc-600">Sin resultados</p>
        ) : (
          results.map((contact) => {
            const isSelected = selected.has(contact.id);
            const displayName = contact.name || formatPhone(contact.phone);
            return (
              <button
                key={contact.id}
                type="button"
                onClick={() => toggleContact(contact)}
                className={`flex w-full items-center gap-3 px-3 py-2 text-left transition-colors hover:bg-zinc-800/50 ${
                  isSelected ? 'bg-blue-500/10' : ''
                }`}
              >
                <div
                  className={`grid h-4 w-4 shrink-0 place-items-center rounded border ${
                    isSelected
                      ? 'border-blue-500 bg-blue-500 text-white'
                      : 'border-zinc-700 bg-zinc-950'
                  }`}
                >
                  {isSelected && (
                    <svg className="h-2.5 w-2.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                    </svg>
                  )}
                </div>
                <div className="min-w-0">
                  <p className="truncate text-sm text-zinc-200">{displayName}</p>
                  <p className="truncate text-[10px] text-zinc-500">{formatPhone(contact.phone)}</p>
                </div>
              </button>
            );
          })
        )}
      </div>

      <div className="flex items-center justify-between">
        <span className="text-[10px] text-zinc-500">
          {selected.size} seleccionado{selected.size === 1 ? '' : 's'}
        </span>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={onCancel}
            className="rounded-md border border-zinc-800 px-3 py-1.5 text-xs font-medium text-zinc-400 transition-colors hover:bg-zinc-800 hover:text-zinc-200"
          >
            Cancelar
          </button>
          <button
            type="button"
            onClick={() => onSelect(Array.from(selected.values()))}
            disabled={selected.size === 0}
            className="rounded-md bg-blue-600 px-3 py-1.5 text-xs font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
          >
            Agregar ({selected.size})
          </button>
        </div>
      </div>
    </div>
  );
}
