// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Inbox, Loader2, Filter, ChevronDown } from 'lucide-react';
import { listConversations } from '@/lib/api/conversations';
import { listInboxes } from '@/lib/api/inboxes';
import { useUIStore } from '@/lib/store/ui.store';
import { useAuthStore } from '@/lib/store/auth.store';
import { useInboxUpdates } from '@/lib/signalr/hooks';
import type { ConversationResponse, InboxResponse, ConversationStatus } from '@/types/api';
import { ConversationListItem } from '@/components/inbox/conversation-list-item';
import { ChatView } from '@/components/chat/chat-view';
import { ContactPanel } from '@/components/inbox/contact-panel';
import { CONVERSATION_STATUS } from '@/lib/utils/constants';
import { cn } from '@/lib/utils';

export default function InboxPage() {
  const agent = useAuthStore((s) => s.agent);
  const {
    selectedInboxId,
    setSelectedInboxId,
    conversationFilter,
    setConversationFilter,
    statusFilter,
    setStatusFilter,
  } = useUIStore();

  const [inboxes, setInboxes] = useState<InboxResponse[]>([]);
  const [conversations, setConversations] = useState<ConversationResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(true);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [contactPanelOpen, setContactPanelOpen] = useState(false);
  const [filterOpen, setFilterOpen] = useState(false);
  const [inboxDropdownOpen, setInboxDropdownOpen] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);
  const fetchRef = useRef(0);

  const selectedConversation = useMemo(
    () => conversations.find((c) => c.id === selectedId) ?? null,
    [conversations, selectedId],
  );

  // Load inboxes once
  useEffect(() => {
    listInboxes({ isActive: true }).then(setInboxes).catch(() => {});
  }, []);

  // Fetch conversations
  const fetchConversations = useCallback(
    async (append = false) => {
      const token = ++fetchRef.current;
      if (append) setLoadingMore(true);
      else setLoading(true);

      try {
        const params: Record<string, unknown> = { limit: 50 };

        if (selectedInboxId) params.inboxId = selectedInboxId;
        if (statusFilter) params.status = statusFilter;
        if (conversationFilter === 'mine' && agent?.id) params.assignedTo = agent.id;

        if (append && conversations.length > 0) {
          const last = conversations[conversations.length - 1];
          if (last.lastMessageAt) params.beforeLastMessageAt = last.lastMessageAt;
          params.beforeId = last.id;
        }

        const data = await listConversations(params as never);
        if (token !== fetchRef.current) return; // stale

        if (append) {
          setConversations((prev) => [...prev, ...data]);
        } else {
          setConversations(data);
        }
        setHasMore(data.length === 50);
      } catch {
        // silent
      } finally {
        if (token === fetchRef.current) {
          setLoading(false);
          setLoadingMore(false);
        }
      }
    },
    [selectedInboxId, statusFilter, conversationFilter, agent?.id, conversations],
  );

  // Re-fetch on filter change
  useEffect(() => {
    fetchConversations(false);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedInboxId, statusFilter, conversationFilter]);

  // SignalR inbox updates
  useInboxUpdates(selectedInboxId, {
    onMessageReceived: (conversationId, message) => {
      setConversations((prev) => {
        const idx = prev.findIndex((c) => c.id === conversationId);
        if (idx === -1) return prev; // unknown conversation, ignore
        const updated = { ...prev[idx] };
        updated.lastMessageAt = message.createdAt;
        updated.lastMessagePreview = message.content ?? '';
        updated.lastMessageDirection = message.direction;
        if (message.direction === 'INBOUND' && conversationId !== selectedId) {
          updated.unreadCount = (updated.unreadCount ?? 0) + 1;
        }
        return [updated, ...prev.filter((_, i) => i !== idx)];
      });
    },
    onConversationAssigned: (conversationId, agentId) => {
      setConversations((prev) =>
        prev.map((c) =>
          c.id === conversationId ? { ...c, assignedTo: agentId, status: 'ASSIGNED' as ConversationStatus } : c,
        ),
      );
    },
    onConversationStatusChanged: (conversationId, newStatus) => {
      setConversations((prev) =>
        prev.map((c) =>
          c.id === conversationId ? { ...c, status: newStatus as ConversationStatus } : c,
        ),
      );
    },
  });

  // Infinite scroll
  function handleListScroll() {
    if (!listRef.current || loadingMore || !hasMore) return;
    const el = listRef.current;
    if (el.scrollTop + el.clientHeight >= el.scrollHeight - 100) {
      fetchConversations(true);
    }
  }

  const selectedInbox = inboxes.find((i) => i.id === selectedInboxId);

  return (
    <div className="flex h-full">
      {/* Left: conversation list */}
      <div className="flex w-80 shrink-0 flex-col border-r border-zinc-800/60">
        {/* Filters */}
        <div className="border-b border-zinc-800/60 p-2.5 space-y-2">
          <div className="relative">
            <button
              onClick={() => setInboxDropdownOpen(!inboxDropdownOpen)}
              className="flex w-full items-center justify-between rounded-md border border-zinc-800 bg-zinc-950 px-2.5 py-1.5 text-xs text-zinc-300 transition-colors hover:border-zinc-700"
            >
              <span className="truncate">{selectedInbox ? selectedInbox.name : 'Todas las bandejas'}</span>
              <ChevronDown className="h-3 w-3 text-zinc-500" />
            </button>
            {inboxDropdownOpen && (
              <DropdownList
                onClose={() => setInboxDropdownOpen(false)}
                items={[
                  { id: null, label: 'Todas las bandejas' },
                  ...inboxes.map((i) => ({ id: i.id, label: i.name })),
                ]}
                selected={selectedInboxId}
                onSelect={(id) => { setSelectedInboxId(id); setInboxDropdownOpen(false); }}
              />
            )}
          </div>

          <div className="flex gap-1">
            {(['all', 'mine', 'unassigned'] as const).map((f) => (
              <button
                key={f}
                onClick={() => setConversationFilter(f)}
                className={cn(
                  'rounded-md px-2 py-1 text-[11px] font-medium transition-colors',
                  conversationFilter === f ? 'bg-blue-500/15 text-blue-400' : 'text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300',
                )}
              >
                {f === 'all' ? 'Todas' : f === 'mine' ? 'Mías' : 'Sin asignar'}
              </button>
            ))}
            <div className="relative ml-auto">
              <button
                onClick={() => setFilterOpen(!filterOpen)}
                className={cn(
                  'grid h-6 w-6 place-items-center rounded-md transition-colors',
                  statusFilter ? 'bg-blue-500/15 text-blue-400' : 'text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300',
                )}
              >
                <Filter className="h-3 w-3" />
              </button>
              {filterOpen && (
                <StatusDropdown
                  selected={statusFilter}
                  onSelect={(s) => { setStatusFilter(s); setFilterOpen(false); }}
                  onClose={() => setFilterOpen(false)}
                />
              )}
            </div>
          </div>
        </div>

        {/* List */}
        <div ref={listRef} onScroll={handleListScroll} className="flex-1 overflow-auto">
          {loading ? (
            <div className="flex h-32 items-center justify-center">
              <Loader2 className="h-4 w-4 animate-spin text-zinc-600" />
            </div>
          ) : conversations.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16">
              <Inbox className="h-6 w-6 text-zinc-700" />
              <p className="mt-2 text-xs text-zinc-500">Sin conversaciones</p>
            </div>
          ) : (
            <>
              {conversations.map((c) => (
                <ConversationListItem
                  key={c.id}
                  conversation={c}
                  active={c.id === selectedId}
                  onClick={() => setSelectedId(c.id)}
                />
              ))}
              {loadingMore && (
                <div className="flex justify-center py-3">
                  <Loader2 className="h-3.5 w-3.5 animate-spin text-zinc-600" />
                </div>
              )}
            </>
          )}
        </div>
      </div>

      {/* Center: chat */}
      <div className="flex flex-1 flex-col overflow-hidden">
        {selectedConversation ? (
          <ChatView
            conversation={selectedConversation}
            onToggleContactPanel={() => setContactPanelOpen(!contactPanelOpen)}
            onConversationUpdated={(updated) => {
              setConversations((prev) => prev.map((c) => (c.id === updated.id ? updated : c)));
            }}
          />
        ) : (
          <div className="flex flex-1 items-center justify-center">
            <div className="text-center">
              <Inbox className="mx-auto h-8 w-8 text-zinc-700" />
              <p className="mt-2 text-sm text-zinc-500">Seleccioná una conversación</p>
            </div>
          </div>
        )}
      </div>

      {/* Right: contact panel */}
      {contactPanelOpen && selectedConversation && (
        <ContactPanel conversation={selectedConversation} onClose={() => setContactPanelOpen(false)} />
      )}
    </div>
  );
}

// ── Reusable dropdowns ─────────────────────────────────────────────────────

function DropdownList({
  items,
  selected,
  onSelect,
  onClose,
}: {
  items: { id: string | null; label: string }[];
  selected: string | null;
  onSelect: (id: string | null) => void;
  onClose: () => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const handle = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) onClose(); };
    document.addEventListener('mousedown', handle);
    return () => document.removeEventListener('mousedown', handle);
  }, [onClose]);

  return (
    <div ref={ref} className="absolute left-0 top-full z-20 mt-1 w-full rounded-md border border-zinc-800 bg-zinc-900 py-1 shadow-xl max-h-60 overflow-auto">
      {items.map((item) => (
        <button
          key={item.id ?? '__all'}
          onClick={() => onSelect(item.id)}
          className={cn('flex w-full px-3 py-1.5 text-xs transition-colors hover:bg-zinc-800', selected === item.id ? 'text-blue-400' : 'text-zinc-300')}
        >
          {item.label}
        </button>
      ))}
    </div>
  );
}

function StatusDropdown({
  selected,
  onSelect,
  onClose,
}: {
  selected: ConversationStatus | null;
  onSelect: (s: ConversationStatus | null) => void;
  onClose: () => void;
}) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const handle = (e: MouseEvent) => { if (ref.current && !ref.current.contains(e.target as Node)) onClose(); };
    document.addEventListener('mousedown', handle);
    return () => document.removeEventListener('mousedown', handle);
  }, [onClose]);

  return (
    <div ref={ref} className="absolute right-0 top-full z-20 mt-1 w-40 rounded-md border border-zinc-800 bg-zinc-900 py-1 shadow-xl">
      <button
        onClick={() => onSelect(null)}
        className={cn('flex w-full px-3 py-1.5 text-xs transition-colors hover:bg-zinc-800', !selected ? 'text-blue-400' : 'text-zinc-300')}
      >
        Todos
      </button>
      {(Object.entries(CONVERSATION_STATUS) as [ConversationStatus, { label: string; class: string }][]).map(([key, cfg]) => (
        <button
          key={key}
          onClick={() => onSelect(key)}
          className={cn('flex w-full items-center gap-2 px-3 py-1.5 text-xs transition-colors hover:bg-zinc-800', selected === key ? 'text-blue-400' : 'text-zinc-300')}
        >
          <span className={cn('h-1.5 w-1.5 rounded-full', cfg.class.split(' ')[0])} />
          {cfg.label}
        </button>
      ))}
    </div>
  );
}
