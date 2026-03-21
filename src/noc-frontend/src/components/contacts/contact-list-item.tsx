// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useState } from 'react';
import Link from 'next/link';
import { ArrowUpRight, Loader2, Mail, Phone, Sparkles, Trash2, UserX } from 'lucide-react';
import { toast } from 'sonner';
import { deleteContact } from '@/lib/api/contacts';
import type { ContactResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { formatPhone } from '@/lib/utils/format-phone';
import { timeAgo } from '@/lib/utils/format-date';

interface ContactListItemProps {
  contact: ContactResponse;
  onDeleted?: (id: string) => void;
}

export function ContactListItem({ contact, onDeleted }: ContactListItemProps) {
  const [deleting, setDeleting] = useState(false);
  const displayName = contact.name || formatPhone(contact.phone);
  const customAttrCount = Object.keys(contact.customAttrs).length;

  async function handleDelete(force: boolean) {
    const msg = force
      ? `¿Eliminar "${displayName}" y TODAS sus conversaciones y mensajes? No se puede deshacer.`
      : `¿Eliminar "${displayName}"? Si tiene conversaciones se bloqueará.`;
    if (!confirm(msg)) return;

    setDeleting(true);
    try {
      await deleteContact(contact.id, force);
      toast.success(`Contacto "${displayName}" eliminado`);
      onDeleted?.(contact.id);
    } catch (e: unknown) {
      const err = e as ApiError;
      if (err.status === 409 && !force) {
        // Has conversations — offer force delete
        if (confirm('Este contacto tiene conversaciones. ¿Eliminar contacto junto con todas sus conversaciones y mensajes?')) {
          await handleDelete(true);
        }
      } else {
        toast.error(err.detail || 'Error al eliminar contacto');
      }
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div className="group rounded-xl border border-zinc-800/60 bg-zinc-900/50 p-4 transition-colors hover:border-zinc-700 hover:bg-zinc-900">
      <div className="flex items-start justify-between gap-3">
        <Link href={`/contacts/${contact.id}`} className="flex min-w-0 flex-1 items-start gap-3">
          <div className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-blue-500/10 text-sm font-semibold text-blue-400">
            {displayName.charAt(0).toUpperCase()}
          </div>

          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <h3 className="truncate text-sm font-semibold text-zinc-100">{displayName}</h3>
              <span className="rounded-full bg-zinc-800 px-2 py-0.5 text-[10px] text-zinc-500">
                {contact.tags.length} tag{contact.tags.length === 1 ? '' : 's'}
              </span>
            </div>

            <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-zinc-500">
              <span className="inline-flex items-center gap-1">
                <Phone className="h-3 w-3" />
                {formatPhone(contact.phone)}
              </span>
              {contact.email && (
                <span className="inline-flex items-center gap-1">
                  <Mail className="h-3 w-3" />
                  {contact.email}
                </span>
              )}
            </div>
          </div>
        </Link>

        <div className="flex shrink-0 items-center gap-1">
          {/* Delete contact only */}
          <button
            onClick={() => handleDelete(false)}
            disabled={deleting}
            title="Eliminar contacto"
            className="grid h-7 w-7 place-items-center rounded-md text-zinc-600 opacity-0 transition-all hover:bg-red-500/10 hover:text-red-400 group-hover:opacity-100 disabled:opacity-50"
          >
            {deleting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
          </button>
          {/* Force delete: contact + conversations + messages */}
          <button
            onClick={() => handleDelete(true)}
            disabled={deleting}
            title="Eliminar contacto + conversaciones + mensajes"
            className="grid h-7 w-7 place-items-center rounded-md text-zinc-600 opacity-0 transition-all hover:bg-red-500/10 hover:text-red-400 group-hover:opacity-100 disabled:opacity-50"
          >
            <UserX className="h-3.5 w-3.5" />
          </button>
          <Link href={`/contacts/${contact.id}`} className="grid h-7 w-7 place-items-center">
            <ArrowUpRight className="h-4 w-4 text-zinc-600 transition-colors group-hover:text-blue-400" />
          </Link>
        </div>
      </div>

      {contact.tags.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-1.5">
          {contact.tags.map((tag) => (
            <span
              key={tag}
              className="rounded-full bg-blue-500/10 px-2 py-0.5 text-[10px] font-medium text-blue-400"
            >
              {tag}
            </span>
          ))}
        </div>
      )}

      <div className="mt-3 flex items-center justify-between gap-3 border-t border-zinc-800/60 pt-3 text-[11px] text-zinc-500">
        <span className="inline-flex items-center gap-1">
          <Sparkles className="h-3 w-3" />
          {customAttrCount === 0 ? 'Sin atributos' : `${customAttrCount} atributo${customAttrCount === 1 ? '' : 's'}`}
        </span>
        <span>Actualizado {timeAgo(contact.updatedAt)}</span>
      </div>
    </div>
  );
}
