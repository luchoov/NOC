// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2, Plus, X } from 'lucide-react';
import { toast } from 'sonner';
import { createContactList, updateContactList } from '@/lib/api/contact-lists';
import type { ApiError } from '@/lib/api/client';
import type { ContactListResponse } from '@/types/api';
import { createContactListSchema, type CreateContactListFormData } from '@/lib/validations/contact-list.schema';

interface ListCreateModalProps {
  open: boolean;
  onClose: () => void;
  onSaved: (list: ContactListResponse) => void;
  editList?: ContactListResponse | null;
}

export function ListCreateModal({ open, onClose, onSaved, editList }: ListCreateModalProps) {
  const [submitting, setSubmitting] = useState(false);
  const isEdit = !!editList;

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CreateContactListFormData>({
    resolver: zodResolver(createContactListSchema) as never,
    defaultValues: { name: '', description: '' },
  });

  useEffect(() => {
    if (open && editList) {
      reset({ name: editList.name, description: editList.description ?? '' });
    } else if (!open) {
      reset({ name: '', description: '' });
      setSubmitting(false);
    }
  }, [open, editList, reset]);

  useEffect(() => {
    if (!open) return;
    function handleEscape(e: KeyboardEvent) {
      if (e.key === 'Escape' && !submitting) onClose();
    }
    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [onClose, open, submitting]);

  async function onSubmit(data: CreateContactListFormData) {
    setSubmitting(true);
    try {
      const saved = isEdit
        ? await updateContactList(editList!.id, {
            name: data.name.trim(),
            description: data.description?.trim() || null,
          })
        : await createContactList({
            name: data.name.trim(),
            description: data.description?.trim() || undefined,
          });
      toast.success(isEdit ? 'Lista actualizada' : 'Lista creada');
      onSaved(saved);
      reset();
    } catch (err: unknown) {
      const error = err as ApiError;
      toast.error(error.detail || 'Error al guardar lista');
    } finally {
      setSubmitting(false);
    }
  }

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-zinc-950/80 px-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-2xl border border-zinc-800 bg-zinc-900 shadow-2xl">
        <div className="flex items-center justify-between border-b border-zinc-800/60 px-5 py-4">
          <div>
            <h2 className="text-sm font-semibold text-zinc-100">
              {isEdit ? 'Editar lista' : 'Nueva lista'}
            </h2>
            <p className="mt-0.5 text-xs text-zinc-500">
              Agrupa contactos manualmente para campanas o segmentacion.
            </p>
          </div>
          <button
            onClick={onClose}
            disabled={submitting}
            className="grid h-8 w-8 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300 disabled:opacity-40"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 p-5">
          <div className="space-y-1">
            <label className="block text-xs font-medium text-zinc-400">Nombre</label>
            <input
              {...register('name')}
              placeholder="Clientes VIP"
              className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
            />
            {errors.name && <p className="text-[10px] text-red-400">{errors.name.message}</p>}
          </div>

          <div className="space-y-1">
            <label className="block text-xs font-medium text-zinc-400">Descripcion</label>
            <textarea
              {...register('description')}
              placeholder="Opcional"
              rows={2}
              className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25 resize-none"
            />
            {errors.description && <p className="text-[10px] text-red-400">{errors.description.message}</p>}
          </div>

          <div className="flex items-center justify-end gap-2 border-t border-zinc-800/60 pt-4">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="rounded-md border border-zinc-800 px-3 py-2 text-xs font-medium text-zinc-400 transition-colors hover:bg-zinc-800 hover:text-zinc-200 disabled:opacity-40"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="flex items-center gap-2 rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
            >
              {submitting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Plus className="h-3.5 w-3.5" />}
              {isEdit ? 'Guardar' : 'Crear lista'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
