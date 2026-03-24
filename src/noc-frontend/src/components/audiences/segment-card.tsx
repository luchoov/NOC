// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useState } from 'react';
import { Filter, Loader2, Pencil, Trash2, Users } from 'lucide-react';
import { toast } from 'sonner';
import { deleteSegment } from '@/lib/api/segments';
import type { SegmentResponse } from '@/types/api';
import type { ApiError } from '@/lib/api/client';
import { timeAgo } from '@/lib/utils/format-date';

const FIELD_LABELS: Record<string, string> = {
  locality: 'Localidad',
  tags: 'Tags',
  email: 'Email',
};

const OP_LABELS: Record<string, string> = {
  equals: '=',
  contains: 'contiene',
  has_any_of: 'alguna de',
  has_all_of: 'todas',
  is_present: 'tiene',
  is_absent: 'no tiene',
};

interface SegmentCardProps {
  segment: SegmentResponse;
  onDeleted?: (id: string) => void;
  onEdit?: () => void;
}

export function SegmentCard({ segment, onDeleted, onEdit }: SegmentCardProps) {
  const [deleting, setDeleting] = useState(false);

  async function handleDelete(e: React.MouseEvent) {
    e.stopPropagation();
    if (!confirm(`¿Eliminar el segmento "${segment.name}"?`)) return;

    setDeleting(true);
    try {
      await deleteSegment(segment.id);
      toast.success('Segmento eliminado');
      onDeleted?.(segment.id);
    } catch (err: unknown) {
      const error = err as ApiError;
      toast.error(error.detail || 'Error al eliminar segmento');
    } finally {
      setDeleting(false);
    }
  }

  return (
    <div className="group rounded-xl border border-zinc-800/60 bg-zinc-900/50 p-4 transition-colors hover:border-zinc-700 hover:bg-zinc-900">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <h3 className="truncate text-sm font-semibold text-zinc-100">{segment.name}</h3>
          {segment.description && (
            <p className="mt-1 truncate text-xs text-zinc-500">{segment.description}</p>
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

      {segment.rules.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-1.5">
          {segment.rules.map((rule, i) => (
            <span
              key={i}
              className="inline-flex items-center gap-1 rounded-full bg-zinc-800 px-2 py-0.5 text-[10px] text-zinc-400"
            >
              <Filter className="h-2.5 w-2.5" />
              {FIELD_LABELS[rule.field] || rule.field} {OP_LABELS[rule.operator] || rule.operator}
              {rule.value && (
                <span className="text-zinc-300">
                  {Array.isArray(rule.value) ? rule.value.join(', ') : rule.value}
                </span>
              )}
            </span>
          ))}
        </div>
      )}

      <div className="mt-3 flex items-center justify-between border-t border-zinc-800/60 pt-3 text-[11px] text-zinc-500">
        <span className="inline-flex items-center gap-1">
          <Users className="h-3 w-3" />
          {segment.matchingContactCount} contacto{segment.matchingContactCount === 1 ? '' : 's'}
        </span>
        <span>Actualizado {timeAgo(segment.updatedAt)}</span>
      </div>
    </div>
  );
}
