// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import Link from 'next/link';
import { ArrowUpRight, Mail, Phone, Sparkles } from 'lucide-react';
import type { ContactResponse } from '@/types/api';
import { formatPhone } from '@/lib/utils/format-phone';
import { timeAgo } from '@/lib/utils/format-date';

export function ContactListItem({
  contact,
}: {
  contact: ContactResponse;
}) {
  const displayName = contact.name || formatPhone(contact.phone);
  const customAttrCount = Object.keys(contact.customAttrs).length;

  return (
    <Link
      href={`/contacts/${contact.id}`}
      className="group rounded-xl border border-zinc-800/60 bg-zinc-900/50 p-4 transition-colors hover:border-zinc-700 hover:bg-zinc-900"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 items-start gap-3">
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
        </div>

        <ArrowUpRight className="mt-0.5 h-4 w-4 shrink-0 text-zinc-600 transition-colors group-hover:text-blue-400" />
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
    </Link>
  );
}
