// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { listMessages } from '@/lib/api/messages';
import { useConversationUpdates, useInboxUpdates } from '@/lib/signalr/hooks';
import type { ConversationResponse, MessageResponse } from '@/types/api';
import { ChatHeader } from './chat-header';
import { MessageBubble } from './message-bubble';
import { MessageErrorBoundary } from './message-error-boundary';
import { MessageInput } from './message-input';
import { DateSeparator } from './date-separator';

interface ChatViewProps {
  conversation: ConversationResponse;
  onToggleContactPanel: () => void;
  onConversationUpdated: (c: ConversationResponse) => void;
  onConversationDeleted?: (id: string) => void;
}

export function ChatView({ conversation, onToggleContactPanel, onConversationUpdated, onConversationDeleted }: ChatViewProps) {
  const [messages, setMessages] = useState<MessageResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(true);
  const [isTyping, setIsTyping] = useState(false);
  const typingTimeout = useRef<ReturnType<typeof setTimeout>>(null);
  const scrollRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const prevConvId = useRef<string | null>(null);
  const fetchToken = useRef(0);
  // Track whether the user is near the bottom so we only auto-scroll for new messages
  const isNearBottom = useRef(true);
  // Track whether we're prepending older messages (don't auto-scroll)
  const isPrepending = useRef(false);

  // Check if scroll is near the bottom
  function updateNearBottom() {
    const el = scrollRef.current;
    if (!el) return;
    isNearBottom.current = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
  }

  // Fetch messages on conversation change
  const fetchMessages = useCallback(
    async (conversationId: string, append: boolean, currentMessages: MessageResponse[]) => {
      const token = ++fetchToken.current;
      if (append) {
        setLoadingMore(true);
        isPrepending.current = true;
      } else {
        setLoading(true);
      }

      try {
        const params: Record<string, unknown> = { limit: 50, includePrivateNotes: true };
        if (append && currentMessages.length > 0) {
          const oldest = currentMessages[0];
          params.beforeCreatedAt = oldest.createdAt;
          params.beforeId = oldest.id;
        }

        const data = await listMessages(conversationId, params as never);
        if (token !== fetchToken.current) return;

        // API returns DESC (newest first) — reverse to get ASC (oldest first)
        const sorted = [...data].reverse();

        if (append) {
          // Preserve scroll position: save distance from bottom before prepending
          const el = scrollRef.current;
          const prevScrollHeight = el?.scrollHeight ?? 0;

          setMessages((prev) => [...sorted, ...prev]);

          // After DOM update, restore scroll position
          requestAnimationFrame(() => {
            if (el) {
              const newScrollHeight = el.scrollHeight;
              el.scrollTop += newScrollHeight - prevScrollHeight;
            }
            isPrepending.current = false;
          });
        } else {
          setMessages(sorted);
        }
        setHasMore(data.length === 50);
      } catch {
        isPrepending.current = false;
      } finally {
        if (token === fetchToken.current) {
          setLoading(false);
          setLoadingMore(false);
        }
      }
    },
    [],
  );

  // Reset and load on conversation change
  useEffect(() => {
    if (prevConvId.current !== conversation.id) {
      prevConvId.current = conversation.id;
      setMessages([]);
      setHasMore(true);
      setIsTyping(false);
      isNearBottom.current = true;
      fetchMessages(conversation.id, false, []);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversation.id]);

  // Presence updates (typing indicator)
  const inboxIds = conversation.inboxId ? [conversation.inboxId] : [];
  useInboxUpdates(inboxIds, {
    onPresenceUpdate: (payload) => {
      // Check if this presence update is for the current contact
      if (conversation.contactPhone && payload.phone === conversation.contactPhone.replace(/\D/g, '')) {
        if (payload.presence === 'composing') {
          setIsTyping(true);
          if (typingTimeout.current) clearTimeout(typingTimeout.current);
          typingTimeout.current = setTimeout(() => setIsTyping(false), 5000);
        } else {
          setIsTyping(false);
        }
      }
    },
  });

  // Auto-scroll to bottom ONLY for initial load and new messages (not history prepend)
  useEffect(() => {
    if (loading || isPrepending.current) return;
    if (isNearBottom.current) {
      bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages.length, loading]);

  // SignalR: real-time messages for this conversation
  // NOTE: The client is also in inbox groups, so MessageReceived fires for ALL
  // conversations in the inbox. We MUST filter by conversationId to avoid
  // showing other conversations' messages in this chat.
  useConversationUpdates(conversation.id, {
    onMessageReceived: (cId, message) => {
      if (cId !== conversation.id) return; // not for this chat
      setMessages((prev) => {
        if (prev.some((m) => m.id === message.id)) return prev;
        return [...prev, message];
      });
    },
    onMessageStatusUpdate: (cId, payload) => {
      if (cId !== conversation.id) return;
      setMessages((prev) =>
        prev.map((m) =>
          m.id === payload.messageId
            ? { ...m, deliveryStatus: payload.deliveryStatus }
            : m,
        ),
      );
    },
  });

  // Infinite scroll up — load older messages
  function handleScroll() {
    updateNearBottom();
    if (!scrollRef.current || loadingMore || !hasMore) return;
    if (scrollRef.current.scrollTop < 80) {
      // Pass current messages to avoid stale closure
      setMessages((current) => {
        fetchMessages(conversation.id, true, current);
        return current;
      });
    }
  }

  // Optimistic send — add message locally immediately
  function handleMessageSent(message: MessageResponse) {
    setMessages((prev) => {
      if (prev.some((m) => m.id === message.id)) return prev;
      return [...prev, message];
    });
    // Scroll to bottom after sending
    isNearBottom.current = true;
  }

  return (
    <div className="flex flex-1 flex-col overflow-hidden">
      <ChatHeader
        conversation={conversation}
        onToggleContactPanel={onToggleContactPanel}
        onConversationUpdated={onConversationUpdated}
        onConversationDeleted={onConversationDeleted}
      />

      {/* Messages */}
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="flex flex-1 flex-col overflow-auto px-4 py-3"
      >
        {loadingMore && (
          <div className="flex justify-center py-2">
            <Loader2 className="h-3.5 w-3.5 animate-spin text-zinc-600" />
          </div>
        )}

        {loading ? (
          <div className="flex flex-1 items-center justify-center">
            <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
          </div>
        ) : messages.length === 0 ? (
          <div className="flex flex-1 items-center justify-center">
            <p className="text-xs text-zinc-600">Sin mensajes aún</p>
          </div>
        ) : (
          <div className="mt-auto space-y-1">
            {messages.map((msg, i) => {
              const prevMsg = i > 0 ? messages[i - 1] : null;
              const showDate = shouldShowDate(prevMsg, msg);
              return (
                <div key={msg.id}>
                  {showDate && <DateSeparator date={msg.createdAt} />}
                  <MessageErrorBoundary>
                    <MessageBubble message={msg} />
                  </MessageErrorBoundary>
                </div>
              );
            })}
          </div>
        )}

        <div ref={bottomRef} />
      </div>

      {isTyping && (
        <div className="px-4 py-1">
          <span className="text-xs text-zinc-500 italic animate-pulse">escribiendo...</span>
        </div>
      )}

      <MessageInput conversationId={conversation.id} onSent={handleMessageSent} />
    </div>
  );
}

/** Show a date separator when the day changes between messages */
function shouldShowDate(prev: MessageResponse | null, curr: MessageResponse): boolean {
  if (!prev) return true;
  const prevDate = curr.createdAt.slice(0, 10);
  const currDate = prev.createdAt.slice(0, 10);
  return prevDate !== currDate;
}
