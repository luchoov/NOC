// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2, Plus, X } from 'lucide-react';
import { toast } from 'sonner';
import { createContact } from '@/lib/api/contacts';
import type { ApiError } from '@/lib/api/client';
import type { ContactResponse } from '@/types/api';
import { createContactSchema, type CreateContactFormData } from '@/lib/validations/contact.schema';
import { parseTagInput } from '@/lib/utils/contact-form';

interface ContactCreateModalProps {
  open: boolean;
  onClose: () => void;
  onCreated: (contact: ContactResponse) => void;
}

export function ContactCreateModal({
  open,
  onClose,
  onCreated,
}: ContactCreateModalProps) {
  const [submitting, setSubmitting] = useState(false);
  const [tagsInput, setTagsInput] = useState('');

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CreateContactFormData>({
    resolver: zodResolver(createContactSchema) as never,
    defaultValues: {
      phone: '',
      name: '',
      email: '',
      avatarUrl: '',
      tags: [],
    },
  });

  useEffect(() => {
    if (!open) {
      reset();
      setTagsInput('');
      setSubmitting(false);
      return;
    }

    function handleEscape(event: KeyboardEvent) {
      if (event.key === 'Escape' && !submitting) {
        onClose();
      }
    }

    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [onClose, open, reset, submitting]);

  async function onSubmit(data: CreateContactFormData) {
    setSubmitting(true);
    try {
      const created = await createContact({
        phone: data.phone.trim(),
        name: data.name?.trim() || undefined,
        email: data.email?.trim() || undefined,
        avatarUrl: data.avatarUrl?.trim() || undefined,
        tags: parseTagInput(tagsInput),
      });
      toast.success('Contacto creado');
      onCreated(created);
      reset();
      setTagsInput('');
    } catch (error: unknown) {
      const err = error as ApiError;
      toast.error(err.detail || 'Error al crear contacto');
    } finally {
      setSubmitting(false);
    }
  }

  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-zinc-950/80 px-4 backdrop-blur-sm">
      <div className="w-full max-w-xl rounded-2xl border border-zinc-800 bg-zinc-900 shadow-2xl">
        <div className="flex items-center justify-between border-b border-zinc-800/60 px-5 py-4">
          <div>
            <h2 className="text-sm font-semibold text-zinc-100">Nuevo contacto</h2>
            <p className="mt-0.5 text-xs text-zinc-500">
              Alta manual para usarlo luego en conversaciones y campanas.
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
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1">
              <label className="block text-xs font-medium text-zinc-400">Telefono</label>
              <input
                {...register('phone')}
                placeholder="+54 11 5555 5555"
                className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
              />
              {errors.phone && <p className="text-[10px] text-red-400">{errors.phone.message}</p>}
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-zinc-400">Nombre</label>
              <input
                {...register('name')}
                placeholder="Maria Gomez"
                className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
              />
              {errors.name && <p className="text-[10px] text-red-400">{errors.name.message}</p>}
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1">
              <label className="block text-xs font-medium text-zinc-400">Email</label>
              <input
                {...register('email')}
                placeholder="contacto@empresa.com"
                className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
              />
              {errors.email && <p className="text-[10px] text-red-400">{errors.email.message}</p>}
            </div>

            <div className="space-y-1">
              <label className="block text-xs font-medium text-zinc-400">Avatar URL</label>
              <input
                {...register('avatarUrl')}
                placeholder="https://..."
                className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
              />
              {errors.avatarUrl && <p className="text-[10px] text-red-400">{errors.avatarUrl.message}</p>}
            </div>
          </div>

          <div className="space-y-1">
            <label className="block text-xs font-medium text-zinc-400">Tags</label>
            <input
              value={tagsInput}
              onChange={(event) => setTagsInput(event.target.value)}
              placeholder="vip, ecommerce, soporte"
              className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
            />
            <p className="text-[10px] text-zinc-600">
              Separa varias etiquetas con coma. Se guardan normalizadas en minusculas.
            </p>
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
              {submitting ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <Plus className="h-3.5 w-3.5" />
              )}
              Crear contacto
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
