// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useState } from 'react';
import { Loader2, Pencil, Trash2, Users } from 'lucide-react';
import { toast } from 'sonner';
import { deleteContactList } from '@/lib/api/contact-lists';
import type { ContactListResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { timeAgo } from '@/lib/utils/format-date';

interface ListCardProps {
  list: ContactListResponse;
  onDeleted?: (id: string) => void;
  onClick?: () => void;
  onEdit?: () => void;
}

export function ListCard({ list, onDeleted, onClick, onEdit }: ListCardProps) {
  const [deleting, setDeleting] = useState(false);

  async function handleDelete(e: React.MouseEvent) {
    e.stopPropagation();
    if (!confirm(`¿Eliminar la lista "${list.name}"?`)) return;

    setDeleting(true);
    try {
      await deleteContactList(list.id);
      toast.success('Lista eliminada');
      onDeleted?.(list.id);
    } catch (err: unknown) {
      const error = err as ApiError;
      toast.error(error.detail || 'Error al eliminar lista');
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div
      onClick={onClick}
      className="group cursor-pointer rounded-xl border border-zinc-800/60 bg-zinc-900/50 p-4 transition-colors hover:border-zinc-700 hover:bg-zinc-900"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <h3 className="truncate text-sm font-semibold text-zinc-100">{list.name}</h3>
          {list.description && (
            <p className="mt-1 truncate text-xs text-zinc-500">{list.description}</p>
          )}
        </div>

        <div className="flex shrink-0 items-center gap-1">
          {onEdit && (
            <button
              onClick={(e) => { e.stopPropagation(); onEdit(); }}
              className="grid h-7 w-7 place-items-center rounded-md text-zinc-600 opacity-0 transition-all hover:bg-zinc-800 hover:text-zinc-300 group-hover:opacity-100"
            >
              <Pencil className="h-3.5 w-3.5" />
            </button>
          )}
          <button
            onClick={handleDelete}
            disabled={deleting}
            className="grid h-7 w-7 place-items-center rounded-md text-zinc-600 opacity-0 transition-all hover:bg-red-500/10 hover:text-red-400 group-hover:opacity-100 disabled:opacity-50"
          >
            {deleting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
          </button>
        </div>
      </div>

      <div className="mt-3 flex items-center justify-between border-t border-zinc-800/60 pt-3 text-[11px] text-zinc-500">
        <span className="inline-flex items-center gap-1">
          <Users className="h-3 w-3" />
          {list.memberCount} contacto{list.memberCount === 1 ? '' : 's'}
        </span>
        <span>Actualizado {timeAgo(list.updatedAt)}</span>
      </div>
    </div>
  );
}
