// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect, useState } from 'react';
import { X, Phone, Mail, MapPin, Tag, Calendar, Loader2 } from 'lucide-react';
import { getContact } from '@/lib/api/contacts';
import type { ConversationResponse, ContactResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { formatFull, timeAgo } from '@/lib/utils/format-date';
import { CONVERSATION_STATUS } from '@/lib/utils/constants';
import { cn } from '@/lib/utils';

interface ContactPanelProps {
  conversation: ConversationResponse;
  onClose: () => void;
}

export function ContactPanel({ conversation: c, onClose }: ContactPanelProps) {
  const [contact, setContact] = useState<ContactResponse | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    getContact(c.contactId)
      .then(setContact)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [c.contactId]);

  const displayName = c.contactName || c.contactPhone;
  const statusCfg = CONVERSATION_STATUS[c.status] ?? CONVERSATION_STATUS.OPEN;

  return (
    <div className="w-72 shrink-0 border-l border-zinc-800/60 overflow-auto">
      <div className="flex items-center justify-between border-b border-zinc-800/60 px-4 py-3">
        <h3 className="text-xs font-semibold text-zinc-300">Contacto</h3>
        <button
          onClick={onClose}
          className="grid h-6 w-6 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
        >
          <X className="h-3.5 w-3.5" />
        </button>
      </div>

      {loading ? (
        <div className="flex h-32 items-center justify-center">
          <Loader2 className="h-4 w-4 animate-spin text-zinc-600" />
        </div>
      ) : (
        <div className="p-4 space-y-4">
          {/* Avatar + name */}
          <div className="flex flex-col items-center text-center">
            <div className="grid h-14 w-14 place-items-center rounded-full bg-zinc-800 text-lg font-medium text-zinc-400">
              {displayName.charAt(0).toUpperCase()}
            </div>
            <p className="mt-2 text-sm font-medium text-zinc-200">{displayName}</p>
            {contact?.email && (
              <p className="text-xs text-zinc-500">{contact.email}</p>
            )}
          </div>

          {/* Contact details */}
          <div className="space-y-2">
            <InfoRow icon={Phone} label="Teléfono" value={c.contactPhone} />
            {contact?.email && <InfoRow icon={Mail} label="Email" value={contact.email} />}
            {contact?.locality && <InfoRow icon={MapPin} label="Localidad" value={contact.locality} />}
            <InfoRow icon={Calendar} label="Creado" value={contact ? formatFull(contact.createdAt) : '—'} />
          </div>

          {/* Tags */}
          {contact && contact.tags.length > 0 && (
            <div>
              <p className="mb-1.5 flex items-center gap-1 text-[10px] font-medium uppercase tracking-wider text-zinc-500">
                <Tag className="h-2.5 w-2.5" />
                Etiquetas
              </p>
              <div className="flex flex-wrap gap-1">
                {contact.tags.map((tag) => (
                  <span
                    key={tag}
                    className="rounded-full bg-blue-500/10 px-2 py-0.5 text-[10px] font-medium text-blue-400"
                  >
                    {tag}
                  </span>
                ))}
              </div>
            </div>
          )}

          {/* Conversation info */}
          <div className="border-t border-zinc-800/60 pt-3 space-y-2">
            <p className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">Conversación</p>

            <div className="flex items-center justify-between">
              <span className="text-xs text-zinc-500">Estado</span>
              <span className={cn('rounded-full px-1.5 py-0.5 text-[9px] font-medium', statusCfg.class)}>
                {statusCfg.label}
              </span>
            </div>

            {c.assignedTo && (
              <div className="flex items-center justify-between">
                <span className="text-xs text-zinc-500">Asignada a</span>
                <span className="text-xs text-zinc-300 font-mono">{c.assignedTo.slice(0, 8)}...</span>
              </div>
            )}

            {c.lastMessageAt && (
              <div className="flex items-center justify-between">
                <span className="text-xs text-zinc-500">Último mensaje</span>
                <span className="text-xs text-zinc-400">{timeAgo(c.lastMessageAt)}</span>
              </div>
            )}

            <div className="flex items-center justify-between">
              <span className="text-xs text-zinc-500">No leídos</span>
              <span className="text-xs text-zinc-300">{c.unreadCount}</span>
            </div>

            {c.firstResponseAt && (
              <div className="flex items-center justify-between">
                <span className="text-xs text-zinc-500">Primera respuesta</span>
                <span className="text-xs text-zinc-400">{timeAgo(c.firstResponseAt)}</span>
              </div>
            )}
          </div>

          {/* Custom attrs */}
          {contact && Object.keys(contact.customAttrs).length > 0 && (
            <div className="border-t border-zinc-800/60 pt-3 space-y-2">
              <p className="text-[10px] font-medium uppercase tracking-wider text-zinc-500">Atributos</p>
              {Object.entries(contact.customAttrs).map(([key, val]) => (
                <div key={key} className="flex items-center justify-between">
                  <span className="text-xs text-zinc-500">{key}</span>
                  <span className="text-xs text-zinc-300 truncate max-w-[120px]">{String(val)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function InfoRow({ icon: Icon, label, value }: { icon: React.ComponentType<{ className?: string }>; label: string; value: string }) {
  return (
    <div className="flex items-center gap-2">
      <Icon className="h-3 w-3 shrink-0 text-zinc-600" />
      <div className="min-w-0">
        <p className="text-[10px] text-zinc-600">{label}</p>
        <p className="truncate text-xs text-zinc-300">{value}</p>
      </div>
    </div>
  );
}
