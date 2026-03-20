// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';
import { useEffect, useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  ArrowLeft,
  Calendar,
  Loader2,
  Mail,
  Phone,
  Plus,
  Save,
  Trash2,
  X,
} from 'lucide-react';
import { toast } from 'sonner';
import { addTag, deleteContact, getContact, removeTag, updateContact } from '@/lib/api/contacts';
import type { ApiError } from '@/lib/api/client';
import { parseCustomAttrsInput, parseTagInput, stringifyCustomAttrs } from '@/lib/utils/contact-form';
import { formatFull } from '@/lib/utils/format-date';
import { formatPhone } from '@/lib/utils/format-phone';
import { updateContactSchema, type UpdateContactFormData } from '@/lib/validations/contact.schema';
import type { ContactResponse } from '@/types/api';

export default function ContactDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const contactId = Array.isArray(params.id) ? params.id[0] : params.id;

  const [contact, setContact] = useState<ContactResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [tagDraft, setTagDraft] = useState('');
  const [tagBusy, setTagBusy] = useState<string | null>(null);
  const [customAttrsText, setCustomAttrsText] = useState('{}');
  const [customAttrsError, setCustomAttrsError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<UpdateContactFormData>({
    resolver: zodResolver(updateContactSchema) as never,
    defaultValues: {
      name: '',
      email: '',
      avatarUrl: '',
    },
  });

  useEffect(() => {
    if (!contactId) {
      return;
    }

    setLoading(true);
    getContact(contactId)
      .then((data) => {
        setContact(data);
        reset({
          name: data.name ?? '',
          email: data.email ?? '',
          avatarUrl: data.avatarUrl ?? '',
        });
        setCustomAttrsText(stringifyCustomAttrs(data.customAttrs));
        setCustomAttrsError(null);
      })
      .catch((error: unknown) => {
        const err = error as ApiError;
        toast.error(err.detail || 'No pudimos cargar el contacto');
        router.push('/contacts');
      })
      .finally(() => setLoading(false));
  }, [contactId, reset, router]);

  async function onSubmit(values: UpdateContactFormData) {
    if (!contact) {
      return;
    }

    let customAttrs: Record<string, unknown>;
    try {
      customAttrs = parseCustomAttrsInput(customAttrsText);
      setCustomAttrsError(null);
    } catch (error: unknown) {
      const message = error instanceof Error ? error.message : 'JSON invalido';
      setCustomAttrsError(message);
      toast.error(message);
      return;
    }

    setSaving(true);
    try {
      const updated = await updateContact(contact.id, {
        name: values.name?.trim() || null,
        email: values.email?.trim() || null,
        avatarUrl: values.avatarUrl?.trim() || null,
        customAttrs,
      });

      setContact(updated);
      reset({
        name: updated.name ?? '',
        email: updated.email ?? '',
        avatarUrl: updated.avatarUrl ?? '',
      });
      setCustomAttrsText(stringifyCustomAttrs(updated.customAttrs));
      toast.success('Contacto actualizado');
    } catch (error: unknown) {
      const err = error as ApiError;
      toast.error(err.detail || 'Error al guardar el contacto');
    } finally {
      setSaving(false);
    }
  }

  async function handleAddTag() {
    if (!contact) {
      return;
    }

    const [tag] = parseTagInput(tagDraft);
    if (!tag) {
      return;
    }

    setTagBusy('__add__');
    try {
      const updated = await addTag(contact.id, { tag });
      setContact(updated);
      setTagDraft('');
      toast.success('Tag agregada');
    } catch (error: unknown) {
      const err = error as ApiError;
      toast.error(err.detail || 'Error al agregar la tag');
    } finally {
      setTagBusy(null);
    }
  }

  async function handleRemoveTag(tag: string) {
    if (!contact) {
      return;
    }

    setTagBusy(tag);
    try {
      const updated = await removeTag(contact.id, tag);
      setContact(updated);
      toast.success('Tag removida');
    } catch (error: unknown) {
      const err = error as ApiError;
      toast.error(err.detail || 'Error al remover la tag');
    } finally {
      setTagBusy(null);
    }
  }

  async function handleDelete() {
    if (!contact) {
      return;
    }

    if (!confirm(`Eliminar a ${contact.name || formatPhone(contact.phone)}?`)) {
      return;
    }

    setDeleting(true);
    try {
      await deleteContact(contact.id);
      toast.success('Contacto eliminado');
      router.push('/contacts');
    } catch (error: unknown) {
      const err = error as ApiError;
      toast.error(err.detail || 'No se pudo eliminar el contacto');
    } finally {
      setDeleting(false);
    }
  }

  if (loading) {
    return (
      <div className="flex h-full items-center justify-center">
        <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
      </div>
    );
  }

  if (!contact) {
    return null;
  }

  const displayName = contact.name || formatPhone(contact.phone);
  const customAttrCount = Object.keys(contact.customAttrs).length;
  const jsonDirty = customAttrsText.trim() !== stringifyCustomAttrs(contact.customAttrs).trim();
  const canSave = isDirty || jsonDirty;

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="min-h-full p-6">
      <div className="mb-6 flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
        <div className="min-w-0">
          <Link
            href="/contacts"
            className="inline-flex items-center gap-2 text-xs text-zinc-500 transition-colors hover:text-zinc-300"
          >
            <ArrowLeft className="h-3.5 w-3.5" />
            Volver a contactos
          </Link>

          <div className="mt-4 flex items-start gap-4">
            <div className="grid h-14 w-14 shrink-0 place-items-center rounded-2xl bg-blue-500/10 text-lg font-semibold text-blue-400">
              {displayName.charAt(0).toUpperCase()}
            </div>
            <div className="min-w-0">
              <h1 className="truncate text-xl font-semibold text-zinc-100">{displayName}</h1>
              <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-zinc-500">
                <span className="inline-flex items-center gap-1">
                  <Phone className="h-3.5 w-3.5" />
                  {formatPhone(contact.phone)}
                </span>
                {contact.email && (
                  <span className="inline-flex items-center gap-1">
                    <Mail className="h-3.5 w-3.5" />
                    {contact.email}
                  </span>
                )}
              </div>
            </div>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            onClick={handleDelete}
            disabled={deleting}
            className="flex items-center gap-2 rounded-md border border-red-500/20 bg-red-500/10 px-3 py-2 text-xs font-medium text-red-300 transition-colors hover:bg-red-500/15 disabled:opacity-50"
          >
            {deleting ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <Trash2 className="h-3.5 w-3.5" />
            )}
            Eliminar
          </button>

          <button
            type="submit"
            disabled={saving || !canSave}
            className="flex items-center gap-2 rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50"
          >
            {saving ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <Save className="h-3.5 w-3.5" />
            )}
            Guardar cambios
          </button>
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
        <div className="space-y-6">
          <section className="rounded-2xl border border-zinc-800/60 bg-zinc-900/50 p-5">
            <div className="mb-4">
              <h2 className="text-sm font-semibold text-zinc-100">Perfil</h2>
              <p className="mt-1 text-xs text-zinc-500">
                El telefono es la clave primaria del contacto y no se edita desde esta pantalla.
              </p>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Telefono">
                <input
                  value={formatPhone(contact.phone)}
                  disabled
                  className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-500 outline-none"
                />
              </Field>

              <Field label="Nombre">
                <input
                  {...register('name')}
                  placeholder="Maria Gomez"
                  className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
                />
                {errors.name && <p className="mt-1 text-[10px] text-red-400">{errors.name.message}</p>}
              </Field>

              <Field label="Email">
                <input
                  {...register('email')}
                  placeholder="contacto@empresa.com"
                  className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
                />
                {errors.email && <p className="mt-1 text-[10px] text-red-400">{errors.email.message}</p>}
              </Field>

              <Field label="Avatar URL">
                <input
                  {...register('avatarUrl')}
                  placeholder="https://..."
                  className="block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
                />
                {errors.avatarUrl && <p className="mt-1 text-[10px] text-red-400">{errors.avatarUrl.message}</p>}
              </Field>
            </div>
          </section>

          <section className="rounded-2xl border border-zinc-800/60 bg-zinc-900/50 p-5">
            <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
              <div>
                <h2 className="text-sm font-semibold text-zinc-100">Etiquetas</h2>
                <p className="mt-1 text-xs text-zinc-500">
                  Usa una etiqueta por vez para organizar segmentos o prioridades.
                </p>
              </div>

              <div className="flex w-full gap-2 sm:max-w-sm">
                <input
                  value={tagDraft}
                  onChange={(event) => setTagDraft(event.target.value)}
                  placeholder="vip"
                  className="block flex-1 rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
                />
                <button
                  type="button"
                  onClick={handleAddTag}
                  disabled={tagBusy === '__add__'}
                  className="flex shrink-0 items-center gap-2 rounded-md bg-zinc-800 px-3 py-2 text-xs font-medium text-zinc-200 transition-colors hover:bg-zinc-700 disabled:opacity-50"
                >
                  {tagBusy === '__add__' ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Plus className="h-3.5 w-3.5" />
                  )}
                  Agregar
                </button>
              </div>
            </div>

            {contact.tags.length === 0 ? (
              <p className="text-xs text-zinc-600">Este contacto todavia no tiene tags.</p>
            ) : (
              <div className="flex flex-wrap gap-2">
                {contact.tags.map((tag) => (
                  <span
                    key={tag}
                    className="inline-flex items-center gap-1 rounded-full bg-blue-500/10 px-2.5 py-1 text-[11px] font-medium text-blue-400"
                  >
                    {tag}
                    <button
                      type="button"
                      onClick={() => void handleRemoveTag(tag)}
                      disabled={tagBusy === tag}
                      className="rounded-full p-0.5 transition-colors hover:bg-blue-500/20 disabled:opacity-40"
                      title="Remover tag"
                    >
                      {tagBusy === tag ? (
                        <Loader2 className="h-3 w-3 animate-spin" />
                      ) : (
                        <X className="h-3 w-3" />
                      )}
                    </button>
                  </span>
                ))}
              </div>
            )}
          </section>

          <section className="rounded-2xl border border-zinc-800/60 bg-zinc-900/50 p-5">
            <div className="mb-4">
              <h2 className="text-sm font-semibold text-zinc-100">Atributos custom</h2>
              <p className="mt-1 text-xs text-zinc-500">
                Se guardan como objeto JSON y sirven para enriquecer reglas futuras.
              </p>
            </div>

            <textarea
              value={customAttrsText}
              onChange={(event) => {
                setCustomAttrsText(event.target.value);
                if (customAttrsError) {
                  setCustomAttrsError(null);
                }
              }}
              spellCheck={false}
              className="min-h-72 w-full rounded-xl border border-zinc-800 bg-zinc-950 px-3 py-3 font-mono text-[13px] leading-6 text-zinc-200 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25"
            />
            {customAttrsError && <p className="mt-2 text-xs text-red-400">{customAttrsError}</p>}
          </section>
        </div>

        <aside className="space-y-6">
          <section className="rounded-2xl border border-zinc-800/60 bg-zinc-900/50 p-5">
            <h2 className="text-sm font-semibold text-zinc-100">Resumen</h2>
            <div className="mt-4 space-y-3">
              <MetaRow label="ID" value={contact.id} />
              <MetaRow label="Tags" value={String(contact.tags.length)} />
              <MetaRow label="Atributos" value={String(customAttrCount)} />
              <MetaRow label="Creado" value={formatFull(contact.createdAt)} />
              <MetaRow label="Actualizado" value={formatFull(contact.updatedAt)} />
            </div>
          </section>

          <section className="rounded-2xl border border-zinc-800/60 bg-zinc-900/50 p-5">
            <h2 className="text-sm font-semibold text-zinc-100">Contexto</h2>
            <div className="mt-4 space-y-3">
              <InfoRow
                icon={<Phone className="h-3.5 w-3.5 text-zinc-600" />}
                label="Telefono principal"
                value={formatPhone(contact.phone)}
              />
              <InfoRow
                icon={<Mail className="h-3.5 w-3.5 text-zinc-600" />}
                label="Email"
                value={contact.email || 'Sin email'}
              />
              <InfoRow
                icon={<Calendar className="h-3.5 w-3.5 text-zinc-600" />}
                label="Listo para auditar"
                value="Alta, edicion y tags ya generan eventos de auditoria."
              />
            </div>
          </section>
        </aside>
      </div>
    </form>
  );
}

function Field({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-medium text-zinc-400">{label}</span>
      {children}
    </label>
  );
}

function MetaRow({
  label,
  value,
}: {
  label: string;
  value: string;
}) {
  return (
    <div className="flex flex-col gap-1 border-b border-zinc-800/60 pb-3 last:border-b-0 last:pb-0">
      <span className="text-[11px] uppercase tracking-[0.12em] text-zinc-600">{label}</span>
      <span className="break-all text-sm text-zinc-300">{value}</span>
    </div>
  );
}

function InfoRow({
  icon,
  label,
  value,
}: {
  icon: ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="flex items-start gap-2">
      <span className="mt-0.5">{icon}</span>
      <div>
        <p className="text-[11px] uppercase tracking-[0.12em] text-zinc-600">{label}</p>
        <p className="mt-1 text-sm text-zinc-300">{value}</p>
      </div>
    </div>
  );
}
