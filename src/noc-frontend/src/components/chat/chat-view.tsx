// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { listMessages } from '@/lib/api/messages';
import { useConversationUpdates } from '@/lib/signalr/hooks';
import type { ConversationResponse, MessageResponse } from '@/types/api';
import { ChatHeader } from './chat-header';
import { MessageBubble } from './message-bubble';
import { MessageInput } from './message-input';

interface ChatViewProps {
  conversation: ConversationResponse;
  onToggleContactPanel: () => void;
  onConversationUpdated: (c: ConversationResponse) => void;
}

export function ChatView({ conversation, onToggleContactPanel, onConversationUpdated }: ChatViewProps) {
  const [messages, setMessages] = useState<MessageResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(true);
  const scrollRef = useRef<HTMLDivElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const prevConvId = useRef<string | null>(null);
  const fetchToken = useRef(0);

  // Fetch messages on conversation change
  const fetchMessages = useCallback(
    async (append = false) => {
      const token = ++fetchToken.current;
      if (append) setLoadingMore(true);
      else setLoading(true);

      try {
        const params: Record<string, unknown> = { limit: 50, includePrivateNotes: true };
        if (append && messages.length > 0) {
          const oldest = messages[0];
          params.beforeCreatedAt = oldest.createdAt;
          params.beforeId = oldest.id;
        }

        const data = await listMessages(conversation.id, params as never);
        if (token !== fetchToken.current) return;

        if (append) {
          setMessages((prev) => [...data, ...prev]);
        } else {
          setMessages(data.reverse());
        }
        setHasMore(data.length === 50);
      } catch {
        // silent
      } finally {
        if (token === fetchToken.current) {
          setLoading(false);
          setLoadingMore(false);
        }
      }
    },
    [conversation.id, messages],
  );

  useEffect(() => {
    if (prevConvId.current !== conversation.id) {
      prevConvId.current = conversation.id;
      setMessages([]);
      setHasMore(true);
      fetchMessages(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversation.id]);

  // Auto-scroll on new messages
  useEffect(() => {
    if (!loading) {
      bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
    }
  }, [messages.length, loading]);

  // SignalR: real-time messages
  useConversationUpdates(conversation.id, {
    onMessageReceived: (_cId, message) => {
      setMessages((prev) => {
        if (prev.some((m) => m.id === message.id)) return prev;
        return [...prev, message];
      });
    },
  });

  // Infinite scroll up
  function handleScroll() {
    if (!scrollRef.current || loadingMore || !hasMore) return;
    if (scrollRef.current.scrollTop < 80) {
      fetchMessages(true);
    }
  }

  // Optimistic send
  function handleMessageSent(message: MessageResponse) {
    setMessages((prev) => {
      if (prev.some((m) => m.id === message.id)) return prev;
      return [...prev, message];
    });
  }

  return (
    <div className="flex flex-1 flex-col overflow-hidden">
      <ChatHeader
        conversation={conversation}
        onToggleContactPanel={onToggleContactPanel}
        onConversationUpdated={onConversationUpdated}
      />

      {/* Messages */}
      <div
        ref={scrollRef}
        onScroll={handleScroll}
        className="flex-1 overflow-auto px-4 py-3"
      >
        {loadingMore && (
          <div className="flex justify-center py-2">
            <Loader2 className="h-3.5 w-3.5 animate-spin text-zinc-600" />
          </div>
        )}

        {loading ? (
          <div className="flex h-full items-center justify-center">
            <Loader2 className="h-5 w-5 animate-spin text-zinc-600" />
          </div>
        ) : messages.length === 0 ? (
          <div className="flex h-full items-center justify-center">
            <p className="text-xs text-zinc-600">Sin mensajes aún</p>
          </div>
        ) : (
          <div className="space-y-1">
            {messages.map((msg) => (
              <MessageBubble key={msg.id} message={msg} />
            ))}
          </div>
        )}

        <div ref={bottomRef} />
      </div>

      <MessageInput conversationId={conversation.id} onSent={handleMessageSent} />
    </div>
  );
}
