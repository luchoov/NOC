// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useState } from 'react';
import { Loader2, Plus, X, ChevronRight, ChevronLeft, Clock, Zap, AlertTriangle } from 'lucide-react';
import { toast } from 'sonner';
import { createCampaign } from '@/lib/api/campaigns';
import { listInboxes } from '@/lib/api/inboxes';
import { listContactLists } from '@/lib/api/contact-lists';
import { listSegments } from '@/lib/api/segments';
import type {
  CampaignResponse,
  InboxResponse,
  ContactListResponse,
  SegmentResponse,
  CreateCampaignRequest,
} from '@/types/api';

interface CampaignCreateModalProps {
  open: boolean;
  onClose: () => void;
  onSaved: (campaign: CampaignResponse) => void;
}

type AudienceType = 'list' | 'segment';
type Step = 'basics' | 'audience' | 'settings' | 'review';
type SendMode = 'now' | 'scheduled';

const STEPS: Step[] = ['basics', 'audience', 'settings', 'review'];
const STEP_LABELS: Record<Step, string> = {
  basics: 'Mensaje',
  audience: 'Audiencia',
  settings: 'Envio',
  review: 'Confirmar',
};

export function CampaignCreateModal({ open, onClose, onSaved }: CampaignCreateModalProps) {
  const [step, setStep] = useState<Step>('basics');
  const [submitting, setSubmitting] = useState(false);
  const [dataLoading, setDataLoading] = useState(true);

  // Data
  const [inboxes, setInboxes] = useState<InboxResponse[]>([]);
  const [lists, setLists] = useState<ContactListResponse[]>([]);
  const [segments, setSegments] = useState<SegmentResponse[]>([]);

  // Form fields
  const [name, setName] = useState('');
  const [inboxId, setInboxId] = useState('');
  const [messageTemplate, setMessageTemplate] = useState('');
  const [audienceType, setAudienceType] = useState<AudienceType>('list');
  const [selectedListId, setSelectedListId] = useState('');
  const [selectedSegmentId, setSelectedSegmentId] = useState('');
  const [sendMode, setSendMode] = useState<SendMode>('now');
  const [scheduledDate, setScheduledDate] = useState('');
  const [scheduledTime, setScheduledTime] = useState('');
  const [msgsPerMinute, setMsgsPerMinute] = useState(10);

  const loadData = useCallback(async () => {
    setDataLoading(true);
    try {
      const [inboxData, listData, segmentData] = await Promise.all([
        listInboxes({ isActive: true }),
        listContactLists(),
        listSegments(),
      ]);
      setInboxes(inboxData);
      setLists(listData);
      setSegments(segmentData);
      if (inboxData.length > 0) setInboxId(inboxData[0].id);
    } catch {
      toast.error('Error al cargar datos');
    } finally {
      setDataLoading(false);
    }
  }, []);

  useEffect(() => {
    if (open) {
      loadData();
      setStep('basics');
      setName('');
      setMessageTemplate('');
      setAudienceType('list');
      setSelectedListId('');
      setSelectedSegmentId('');
      setSendMode('now');
      setScheduledDate('');
      setScheduledTime('');
      setMsgsPerMinute(10);
    }
  }, [open, loadData]);

  if (!open) return null;

  const stepIdx = STEPS.indexOf(step);
  const canGoBack = stepIdx > 0;
  const canGoNext = stepIdx < STEPS.length - 1;

  function goBack() { if (canGoBack) setStep(STEPS[stepIdx - 1]); }
  function goNext() {
    if (step === 'basics') {
      if (!name.trim()) { toast.error('Nombre requerido'); return; }
      if (!inboxId) { toast.error('Selecciona una bandeja'); return; }
      if (!messageTemplate.trim()) { toast.error('Escribe el mensaje'); return; }
    }
    if (step === 'audience') {
      if (audienceType === 'list' && !selectedListId) { toast.error('Selecciona una lista'); return; }
      if (audienceType === 'segment' && !selectedSegmentId) { toast.error('Selecciona un segmento'); return; }
    }
    if (step === 'settings') {
      if (sendMode === 'scheduled' && (!scheduledDate || !scheduledTime)) {
        toast.error('Selecciona fecha y hora'); return;
      }
    }
    if (canGoNext) setStep(STEPS[stepIdx + 1]);
  }

  async function handleSubmit() {
    setSubmitting(true);
    try {
      const payload: CreateCampaignRequest = {
        inboxId,
        name: name.trim(),
        messageTemplate: messageTemplate.trim(),
        messagesPerMinute: msgsPerMinute,
        ...(audienceType === 'list' ? { contactListId: selectedListId } : { segmentId: selectedSegmentId }),
        ...(sendMode === 'scheduled' && scheduledDate && scheduledTime
          ? { scheduledAt: new Date(`${scheduledDate}T${scheduledTime}`).toISOString() }
          : {}),
      };
      const saved = await createCampaign(payload);
      toast.success('Campana creada');
      onSaved(saved);
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Error al crear campana';
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  }

  const selectedInbox = inboxes.find((i) => i.id === inboxId);
  const selectedList = lists.find((l) => l.id === selectedListId);
  const selectedSegment = segments.find((s) => s.id === selectedSegmentId);
  const recipientCount = audienceType === 'list'
    ? (selectedList?.memberCount ?? 0)
    : (selectedSegment?.matchingContactCount ?? 0);
  const audienceLabel = audienceType === 'list'
    ? selectedList ? `${selectedList.name} (${selectedList.memberCount})` : '-'
    : selectedSegment ? `${selectedSegment.name} (${selectedSegment.matchingContactCount})` : '-';
  const estimatedMinutes = recipientCount > 0 ? Math.ceil(recipientCount / msgsPerMinute) : 0;

  const inputClass =
    'block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25';
  const selectClass =
    'block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm" onClick={onClose}>
      <div className="flex max-h-[85vh] w-full max-w-lg flex-col rounded-2xl border border-zinc-800 bg-zinc-900 shadow-2xl" onClick={(e) => e.stopPropagation()}>
        {/* Header */}
        <div className="flex shrink-0 items-center justify-between border-b border-zinc-800/60 px-5 py-4">
          <div>
            <h2 className="text-sm font-semibold text-zinc-100">Nueva campana</h2>
            <p className="mt-0.5 text-xs text-zinc-500">Envia un mensaje masivo a tus contactos.</p>
          </div>
          <button onClick={onClose} className="grid h-8 w-8 place-items-center rounded-md text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300">
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Step indicator */}
        <div className="flex shrink-0 border-b border-zinc-800/40 px-5 py-2.5">
          {STEPS.map((s, i) => {
            const isPast = i < stepIdx;
            const isCurrent = step === s;
            return (
              <div key={s} className="flex items-center">
                {i > 0 && <ChevronRight className="mx-1.5 h-3 w-3 text-zinc-700" />}
                <span className={`text-xs font-medium ${isCurrent ? 'text-blue-400' : isPast ? 'text-zinc-400' : 'text-zinc-600'}`}>
                  {STEP_LABELS[s]}
                </span>
              </div>
            );
          })}
        </div>

        {/* Content */}
        <div className="flex-1 overflow-auto p-5">
          {dataLoading ? (
            <div className="flex h-40 items-center justify-center">
              <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
            </div>
          ) : (
            <>
              {step === 'basics' && (
                <div className="space-y-4">
                  <div className="space-y-1">
                    <label className="block text-xs font-medium text-zinc-400">Nombre de la campana</label>
                    <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Promo Marzo 2026" className={inputClass} />
                  </div>

                  <div className="space-y-1">
                    <label className="block text-xs font-medium text-zinc-400">Bandeja de envio</label>
                    <select value={inboxId} onChange={(e) => setInboxId(e.target.value)} className={selectClass}>
                      {inboxes.map((inbox) => (
                        <option key={inbox.id} value={inbox.id}>{inbox.name} ({inbox.phoneNumber})</option>
                      ))}
                    </select>
                  </div>

                  <div className="space-y-1">
                    <label className="block text-xs font-medium text-zinc-400">Mensaje</label>
                    <textarea
                      value={messageTemplate}
                      onChange={(e) => setMessageTemplate(e.target.value)}
                      placeholder="Hola {{nombre}}! Te escribimos para..."
                      rows={5}
                      className={inputClass + ' resize-none'}
                    />
                    <div className="flex items-center justify-between">
                      <p className={`text-[10px] ${messageTemplate.length > 4096 ? 'text-red-400' : 'text-zinc-600'}`}>
                        {messageTemplate.length} / 4096 caracteres
                      </p>
                      {messageTemplate.length > 4096 && (
                        <span className="flex items-center gap-1 text-[10px] text-red-400">
                          <AlertTriangle className="h-2.5 w-2.5" />
                          Excede el limite de WhatsApp
                        </span>
                      )}
                    </div>
                    <div className="flex flex-wrap items-center gap-1.5">
                      <span className="text-[10px] text-zinc-600">Variables:</span>
                      {['{{nombre}}', '{{telefono}}', '{{email}}', '{{localidad}}'].map((v) => (
                        <button
                          key={v}
                          type="button"
                          onClick={() => setMessageTemplate((prev) => prev + v)}
                          className="rounded bg-zinc-800 px-1.5 py-0.5 text-[10px] text-zinc-400 transition-colors hover:bg-zinc-700 hover:text-zinc-200"
                        >
                          {v}
                        </button>
                      ))}
                    </div>
                  </div>

                  {/* WhatsApp bubble preview */}
                  {messageTemplate.trim() && (
                    <div className="space-y-1">
                      <label className="block text-xs font-medium text-zinc-400">Vista previa</label>
                      <div className="rounded-lg bg-zinc-950/80 p-4">
                        <div className="ml-auto max-w-[80%] rounded-lg rounded-tr-none bg-[#005c4b] px-3 py-2">
                          <p className="whitespace-pre-wrap text-sm text-zinc-100">{messageTemplate}</p>
                          <p className="mt-1 text-right text-[10px] text-zinc-400">
                            {new Date().toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' })}
                          </p>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              )}

              {step === 'audience' && (
                <div className="space-y-4">
                  <div className="flex items-center gap-1 rounded-lg bg-zinc-950 p-1 border border-zinc-800/60">
                    <button
                      onClick={() => setAudienceType('list')}
                      className={`flex-1 rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${
                        audienceType === 'list' ? 'bg-zinc-800 text-zinc-100' : 'text-zinc-500 hover:text-zinc-300'
                      }`}
                    >
                      Lista ({lists.length})
                    </button>
                    <button
                      onClick={() => setAudienceType('segment')}
                      className={`flex-1 rounded-md px-3 py-1.5 text-xs font-medium transition-colors ${
                        audienceType === 'segment' ? 'bg-zinc-800 text-zinc-100' : 'text-zinc-500 hover:text-zinc-300'
                      }`}
                    >
                      Segmento ({segments.length})
                    </button>
                  </div>

                  {audienceType === 'list' && (
                    <div className="space-y-2">
                      {lists.length === 0 ? (
                        <p className="py-8 text-center text-xs text-zinc-500">No hay listas creadas. Crea una en Audiencias.</p>
                      ) : lists.map((list) => (
                        <label
                          key={list.id}
                          className={`flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors ${
                            selectedListId === list.id ? 'border-blue-500/50 bg-blue-500/5' : 'border-zinc-800/60 hover:border-zinc-700'
                          }`}
                        >
                          <input type="radio" name="list" value={list.id} checked={selectedListId === list.id}
                            onChange={() => setSelectedListId(list.id)} className="accent-blue-500" />
                          <div className="min-w-0 flex-1">
                            <p className="text-sm font-medium text-zinc-200">{list.name}</p>
                            {list.description && <p className="text-[10px] text-zinc-600">{list.description}</p>}
                          </div>
                          <span className="shrink-0 rounded-full bg-zinc-800 px-2 py-0.5 text-[10px] text-zinc-400">
                            {list.memberCount}
                          </span>
                        </label>
                      ))}
                    </div>
                  )}

                  {audienceType === 'segment' && (
                    <div className="space-y-2">
                      {segments.length === 0 ? (
                        <p className="py-8 text-center text-xs text-zinc-500">No hay segmentos creados. Crea uno en Audiencias.</p>
                      ) : segments.map((seg) => (
                        <label
                          key={seg.id}
                          className={`flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors ${
                            selectedSegmentId === seg.id ? 'border-blue-500/50 bg-blue-500/5' : 'border-zinc-800/60 hover:border-zinc-700'
                          }`}
                        >
                          <input type="radio" name="segment" value={seg.id} checked={selectedSegmentId === seg.id}
                            onChange={() => setSelectedSegmentId(seg.id)} className="accent-blue-500" />
                          <div className="min-w-0 flex-1">
                            <p className="text-sm font-medium text-zinc-200">{seg.name}</p>
                            <div className="mt-0.5 flex flex-wrap gap-1">
                              {seg.rules.map((r, i) => (
                                <span key={i} className="rounded bg-zinc-800 px-1.5 py-0.5 text-[9px] text-zinc-400">
                                  {r.field} {r.operator}
                                </span>
                              ))}
                            </div>
                          </div>
                          <span className="shrink-0 rounded-full bg-zinc-800 px-2 py-0.5 text-[10px] text-zinc-400">
                            {seg.matchingContactCount}
                          </span>
                        </label>
                      ))}
                    </div>
                  )}
                </div>
              )}

              {step === 'settings' && (
                <div className="space-y-5">
                  {/* Send mode */}
                  <div className="space-y-2">
                    <label className="block text-xs font-medium text-zinc-400">Cuando enviar</label>
                    <div className="grid grid-cols-2 gap-2">
                      <button
                        onClick={() => setSendMode('now')}
                        className={`flex items-center gap-2 rounded-lg border p-3 text-left transition-colors ${
                          sendMode === 'now' ? 'border-blue-500/50 bg-blue-500/5' : 'border-zinc-800/60 hover:border-zinc-700'
                        }`}
                      >
                        <Zap className={`h-4 w-4 ${sendMode === 'now' ? 'text-blue-400' : 'text-zinc-600'}`} />
                        <div>
                          <p className="text-xs font-medium text-zinc-200">Enviar ahora</p>
                          <p className="text-[10px] text-zinc-500">Iniciar manualmente</p>
                        </div>
                      </button>
                      <button
                        onClick={() => setSendMode('scheduled')}
                        className={`flex items-center gap-2 rounded-lg border p-3 text-left transition-colors ${
                          sendMode === 'scheduled' ? 'border-blue-500/50 bg-blue-500/5' : 'border-zinc-800/60 hover:border-zinc-700'
                        }`}
                      >
                        <Clock className={`h-4 w-4 ${sendMode === 'scheduled' ? 'text-blue-400' : 'text-zinc-600'}`} />
                        <div>
                          <p className="text-xs font-medium text-zinc-200">Programar</p>
                          <p className="text-[10px] text-zinc-500">Fecha y hora</p>
                        </div>
                      </button>
                    </div>
                  </div>

                  {sendMode === 'scheduled' && (
                    <div className="grid grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <label className="block text-xs font-medium text-zinc-400">Fecha</label>
                        <input type="date" value={scheduledDate} onChange={(e) => setScheduledDate(e.target.value)}
                          min={new Date().toISOString().split('T')[0]} className={inputClass} />
                      </div>
                      <div className="space-y-1">
                        <label className="block text-xs font-medium text-zinc-400">Hora</label>
                        <input type="time" value={scheduledTime} onChange={(e) => setScheduledTime(e.target.value)} className={inputClass} />
                      </div>
                    </div>
                  )}

                  {/* Throttle */}
                  <div className="space-y-2">
                    <label className="block text-xs font-medium text-zinc-400">Velocidad de envio</label>
                    <div className="flex items-center gap-3">
                      <input
                        type="range"
                        min={1}
                        max={30}
                        value={msgsPerMinute}
                        onChange={(e) => setMsgsPerMinute(Number(e.target.value))}
                        className="flex-1 accent-blue-500"
                      />
                      <span className="w-24 shrink-0 rounded-md border border-zinc-800 bg-zinc-950 px-2 py-1.5 text-center text-xs text-zinc-200">
                        {msgsPerMinute} msg/min
                      </span>
                    </div>
                    <p className="text-[10px] text-zinc-600">
                      Velocidades altas pueden causar bloqueos de WhatsApp. Recomendado: 5-15 msg/min.
                    </p>
                  </div>

                  {/* Estimation */}
                  {recipientCount > 0 && (
                    <div className="rounded-lg border border-zinc-800/60 bg-zinc-950/50 p-3">
                      <p className="text-xs text-zinc-400">
                        Tiempo estimado: <span className="font-medium text-zinc-200">~{estimatedMinutes} minuto{estimatedMinutes !== 1 ? 's' : ''}</span>
                        {' '}para {recipientCount} destinatario{recipientCount !== 1 ? 's' : ''}
                      </p>
                    </div>
                  )}
                </div>
              )}

              {step === 'review' && (
                <div className="space-y-4">
                  <div className="space-y-3 rounded-lg border border-zinc-800/60 bg-zinc-950/50 p-4">
                    <div className="flex justify-between">
                      <span className="text-xs text-zinc-500">Nombre</span>
                      <span className="text-xs font-medium text-zinc-200">{name}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-xs text-zinc-500">Bandeja</span>
                      <span className="text-xs text-zinc-200">{selectedInbox?.name ?? '-'}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-xs text-zinc-500">Audiencia</span>
                      <span className="text-xs text-zinc-200">{audienceLabel}</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-xs text-zinc-500">Envio</span>
                      <span className="text-xs text-zinc-200">
                        {sendMode === 'now' ? 'Manual' : `${scheduledDate} ${scheduledTime}`}
                      </span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-xs text-zinc-500">Velocidad</span>
                      <span className="text-xs text-zinc-200">{msgsPerMinute} msg/min</span>
                    </div>
                    {recipientCount > 0 && (
                      <div className="flex justify-between">
                        <span className="text-xs text-zinc-500">Tiempo estimado</span>
                        <span className="text-xs text-zinc-200">~{estimatedMinutes} min</span>
                      </div>
                    )}
                  </div>

                  {/* WhatsApp bubble preview */}
                  <div className="space-y-1">
                    <span className="text-xs text-zinc-500">Mensaje</span>
                    <div className="rounded-lg bg-zinc-950/80 p-4">
                      <div className="ml-auto max-w-[85%] rounded-lg rounded-tr-none bg-[#005c4b] px-3 py-2">
                        <p className="whitespace-pre-wrap text-sm text-zinc-100">{messageTemplate}</p>
                        <p className="mt-1 text-right text-[10px] text-zinc-400">
                          {new Date().toLocaleTimeString('es-AR', { hour: '2-digit', minute: '2-digit' })}
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </>
          )}
        </div>

        {/* Footer */}
        <div className="flex shrink-0 items-center justify-between border-t border-zinc-800/60 px-5 py-4">
          <div>
            {canGoBack && (
              <button onClick={goBack} className="flex items-center gap-1 text-xs text-zinc-400 transition-colors hover:text-zinc-200">
                <ChevronLeft className="h-3.5 w-3.5" />
                Anterior
              </button>
            )}
          </div>
          <div className="flex items-center gap-2">
            <button onClick={onClose} disabled={submitting}
              className="rounded-md border border-zinc-800 px-3 py-2 text-xs font-medium text-zinc-400 transition-colors hover:bg-zinc-800 hover:text-zinc-200 disabled:opacity-40">
              Cancelar
            </button>
            {canGoNext ? (
              <button onClick={goNext}
                className="flex items-center gap-1 rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-500">
                Siguiente
                <ChevronRight className="h-3.5 w-3.5" />
              </button>
            ) : (
              <button onClick={handleSubmit} disabled={submitting}
                className="flex items-center gap-2 rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-500 disabled:opacity-50">
                {submitting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Plus className="h-3.5 w-3.5" />}
                Crear campana
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
