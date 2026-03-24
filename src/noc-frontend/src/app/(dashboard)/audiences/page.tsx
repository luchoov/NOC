// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useState } from 'react';
import { Loader2, Plus, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { listContactLists } from '@/lib/api/contact-lists';
import { listSegments } from '@/lib/api/segments';
import type { ContactListResponse, SegmentResponse } from '@/types/api';
import { ListCard } from '@/components/audiences/list-card';
import { ListCreateModal } from '@/components/audiences/list-create-modal';
import { ListDetailModal } from '@/components/audiences/list-detail-modal';
import { SegmentCard } from '@/components/audiences/segment-card';
import { SegmentCreateModal } from '@/components/audiences/segment-create-modal';
import { cn } from '@/lib/utils';

type Tab = 'lists' | 'segments';

export default function AudiencesPage() {
  const [tab, setTab] = useState<Tab>('lists');

  // Lists state
  const [lists, setLists] = useState<ContactListResponse[]>([]);
  const [listsLoading, setListsLoading] = useState(true);
  const [showListCreate, setShowListCreate] = useState(false);
  const [editList, setEditList] = useState<ContactListResponse | null>(null);
  const [detailList, setDetailList] = useState<ContactListResponse | null>(null);

  // Segments state
  const [segments, setSegments] = useState<SegmentResponse[]>([]);
  const [segmentsLoading, setSegmentsLoading] = useState(true);
  const [showSegmentCreate, setShowSegmentCreate] = useState(false);
  const [editSegment, setEditSegment] = useState<SegmentResponse | null>(null);

  const fetchLists = useCallback(async () => {
    setListsLoading(true);
    try {
      const data = await listContactLists();
      setLists(data);
    } catch {
      toast.error('Error al cargar listas');
    } finally {
      setListsLoading(false);
    }
  }, []);

  const fetchSegments = useCallback(async () => {
    setSegmentsLoading(true);
    try {
      const data = await listSegments();
      setSegments(data);
    } catch {
      toast.error('Error al cargar segmentos');
    } finally {
      setSegmentsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchLists();
    fetchSegments();
  }, [fetchLists, fetchSegments]);

  function handleListSaved(saved: ContactListResponse) {
    if (editList) {
      setLists((prev) => prev.map((l) => (l.id === saved.id ? saved : l)));
      setEditList(null);
    } else {
      setLists((prev) => [saved, ...prev]);
      setShowListCreate(false);
    }
  }

  function handleSegmentSaved(saved: SegmentResponse) {
    if (editSegment) {
      setSegments((prev) => prev.map((s) => (s.id === saved.id ? saved : s)));
      setEditSegment(null);
    } else {
      setSegments((prev) => [saved, ...prev]);
      setShowSegmentCreate(false);
    }
  }

  function handleDetailUpdated(updated: ContactListResponse) {
    setLists((prev) => prev.map((l) => (l.id === updated.id ? updated : l)));
    setDetailList(updated);
  }

  const isLoading = tab === 'lists' ? listsLoading : segmentsLoading;
  const handleRefresh = tab === 'lists' ? fetchLists : fetchSegments;

  const tabClass = (t: Tab) =>
    cn(
      'rounded-md px-3 py-1.5 text-xs font-medium transition-colors',
      tab === t
        ? 'bg-zinc-800 text-zinc-100'
        : 'text-zinc-500 hover:text-zinc-300',
    );

  return (
    <div className="min-h-full p-6">
      {/* Header */}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-lg font-semibold text-zinc-100">Audiencias</h1>
          <p className="mt-1 text-xs text-zinc-500">
            Organiza contactos en listas manuales o segmentos dinamicos.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={handleRefresh}
            disabled={isLoading}
            className="grid h-8 w-8 place-items-center rounded-md border border-zinc-800 text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-300 disabled:opacity-40"
          >
            <RefreshCw className={cn('h-3.5 w-3.5', isLoading && 'animate-spin')} />
          </button>
          <button
            onClick={() => (tab === 'lists' ? setShowListCreate(true) : setShowSegmentCreate(true))}
            className="flex items-center gap-2 rounded-md bg-blue-600 px-3 py-2 text-xs font-medium text-white transition-colors hover:bg-blue-500"
          >
            <Plus className="h-3.5 w-3.5" />
            {tab === 'lists' ? 'Nueva lista' : 'Nuevo segmento'}
          </button>
        </div>
      </div>

      {/* Tabs */}
      <div className="mb-5 flex items-center gap-1 rounded-lg bg-zinc-900/50 p-1 w-fit border border-zinc-800/60">
        <button className={tabClass('lists')} onClick={() => setTab('lists')}>
          Listas ({lists.length})
        </button>
        <button className={tabClass('segments')} onClick={() => setTab('segments')}>
          Segmentos ({segments.length})
        </button>
      </div>

      {/* Content */}
      {tab === 'lists' && (
        <>
          {listsLoading ? (
            <div className="flex h-40 items-center justify-center">
              <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
            </div>
          ) : lists.length === 0 ? (
            <div className="flex h-40 flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-zinc-800 text-center">
              <p className="text-sm text-zinc-500">No hay listas todavia</p>
              <button
                onClick={() => setShowListCreate(true)}
                className="flex items-center gap-1.5 text-xs text-blue-400 transition-colors hover:text-blue-300"
              >
                <Plus className="h-3 w-3" />
                Crear primera lista
              </button>
            </div>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {lists.map((list) => (
                <ListCard
                  key={list.id}
                  list={list}
                  onClick={() => setDetailList(list)}
                  onEdit={() => setEditList(list)}
                  onDeleted={(id) => setLists((prev) => prev.filter((l) => l.id !== id))}
                />
              ))}
            </div>
          )}
        </>
      )}

      {tab === 'segments' && (
        <>
          {segmentsLoading ? (
            <div className="flex h-40 items-center justify-center">
              <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
            </div>
          ) : segments.length === 0 ? (
            <div className="flex h-40 flex-col items-center justify-center gap-2 rounded-2xl border border-dashed border-zinc-800 text-center">
              <p className="text-sm text-zinc-500">No hay segmentos todavia</p>
              <button
                onClick={() => setShowSegmentCreate(true)}
                className="flex items-center gap-1.5 text-xs text-blue-400 transition-colors hover:text-blue-300"
              >
                <Plus className="h-3 w-3" />
                Crear primer segmento
              </button>
            </div>
          ) : (
            <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              {segments.map((segment) => (
                <SegmentCard
                  key={segment.id}
                  segment={segment}
                  onEdit={() => setEditSegment(segment)}
                  onDeleted={(id) => setSegments((prev) => prev.filter((s) => s.id !== id))}
                />
              ))}
            </div>
          )}
        </>
      )}

      {/* Modals */}
      <ListCreateModal
        open={showListCreate || !!editList}
        onClose={() => { setShowListCreate(false); setEditList(null); }}
        onSaved={handleListSaved}
        editList={editList}
      />

      <ListDetailModal
        list={detailList}
        onClose={() => setDetailList(null)}
        onUpdated={handleDetailUpdated}
      />

      <SegmentCreateModal
        open={showSegmentCreate || !!editSegment}
        onClose={() => { setShowSegmentCreate(false); setEditSegment(null); }}
        onSaved={handleSegmentSaved}
        editSegment={editSegment}
      />
    </div>
  );
}
