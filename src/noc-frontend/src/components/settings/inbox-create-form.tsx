// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Loader2, Plus, X } from 'lucide-react';
import { toast } from 'sonner';
import { createInboxSchema, type CreateInboxFormData } from '@/lib/validations/inbox.schema';
import { createInbox } from '@/lib/api/inboxes';
import type { ApiError } from '@/lib/api/client';
import type { CreateInboxResponse } from '@/types/api';
import { cn } from '@/lib/utils';

interface InboxCreateFormProps {
  open: boolean;
  onClose: () => void;
  onCreated: (res: CreateInboxResponse) => void;
}

export function InboxCreateForm({ open, onClose, onCreated }: InboxCreateFormProps) {
  const [loading, setLoading] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<CreateInboxFormData>({
    resolver: zodResolver(createInboxSchema) as never,
    defaultValues: {
      channelType: 'WHATSAPP_UNOFFICIAL',
      autoProvisionEvolution: true,
      autoConnectEvolution: true,
    },
  });

  const channelType = watch('channelType');

  async function onSubmit(data: CreateInboxFormData) {
    setLoading(true);
    try {
      const res = await createInbox({
        name: data.name,
        channelType: data.channelType,
        phoneNumber: data.phoneNumber,
        evolutionInstanceName: data.evolutionInstanceName || undefined,
        autoProvisionEvolution: data.autoProvisionEvolution,
        autoConnectEvolution: data.autoConnectEvolution,
      });

      toast.success('Bandeja creada');
      reset();
      onCreated(res);
    } catch (e: unknown) {
      const err = e as ApiError;
      toast.error(err.detail || err.message || 'Error al crear bandeja');
    } finally {
      setLoading(false);
    }
  }

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="fixed inset-0 bg-black/60" onClick={onClose} />
      <div className="relative z-10 w-full max-w-md rounded-lg border border-zinc-800 bg-zinc-900 shadow-2xl">
        <div className="flex items-center justify-between border-b border-zinc-800/60 px-5 py-3.5">
          <h2 className="text-sm font-semibold text-zinc-200">Nueva bandeja</h2>
          <button
            onClick={onClose}
            className="grid h-7 w-7 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 p-5">
          {/* Name */}
          <div className="space-y-1.5">
            <label className="block text-xs font-medium text-zinc-400">Nombre</label>
            <input
              {...register('name')}
              placeholder="Ventas WhatsApp"
              className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
            />
            {errors.name && <p className="text-xs text-red-400">{errors.name.message}</p>}
          </div>

          {/* Channel Type */}
          <div className="space-y-1.5">
            <label className="block text-xs font-medium text-zinc-400">Canal</label>
            <select
              {...register('channelType')}
              className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
            >
              <option value="WHATSAPP_UNOFFICIAL">WhatsApp (Evolution API)</option>
              <option value="WHATSAPP_OFFICIAL">WhatsApp (API Oficial)</option>
            </select>
          </div>

          {/* Phone Number */}
          <div className="space-y-1.5">
            <label className="block text-xs font-medium text-zinc-400">Número de teléfono</label>
            <input
              {...register('phoneNumber')}
              placeholder="+5491155554444"
              className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
            />
            {errors.phoneNumber && (
              <p className="text-xs text-red-400">{errors.phoneNumber.message}</p>
            )}
          </div>

          {/* Evolution-specific fields */}
          {channelType === 'WHATSAPP_UNOFFICIAL' && (
            <>
              <div className="space-y-1.5">
                <label className="block text-xs font-medium text-zinc-400">
                  Nombre instancia Evolution{' '}
                  <span className="text-zinc-600">(opcional, se genera automáticamente)</span>
                </label>
                <input
                  {...register('evolutionInstanceName')}
                  placeholder="noc-5491155554444-a1b2c3d4"
                  className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
                />
              </div>

              <div className="space-y-2.5 rounded-md border border-zinc-800/60 bg-zinc-950/50 p-3">
                <label className="flex items-center gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    {...register('autoProvisionEvolution')}
                    className="h-3.5 w-3.5 rounded border-zinc-700 bg-zinc-900 text-blue-500 focus:ring-blue-500/25 focus:ring-offset-0"
                  />
                  <span className="text-xs text-zinc-300">Provisionar instancia automáticamente</span>
                </label>
                <label className="flex items-center gap-2.5 cursor-pointer">
                  <input
                    type="checkbox"
                    {...register('autoConnectEvolution')}
                    className="h-3.5 w-3.5 rounded border-zinc-700 bg-zinc-900 text-blue-500 focus:ring-blue-500/25 focus:ring-offset-0"
                  />
                  <span className="text-xs text-zinc-300">Conectar y obtener QR automáticamente</span>
                </label>
              </div>
            </>
          )}

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md px-3.5 py-2 text-sm font-medium text-zinc-400 transition-colors hover:bg-zinc-800 hover:text-zinc-300"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={loading}
              className={cn(
                'flex items-center gap-2 rounded-md bg-blue-600 px-3.5 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50',
              )}
            >
              {loading ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <Plus className="h-3.5 w-3.5" />
              )}
              Crear bandeja
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
