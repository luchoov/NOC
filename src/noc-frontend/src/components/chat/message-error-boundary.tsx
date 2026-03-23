// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { Component, type ReactNode } from 'react';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
}

/**
 * Prevents a single broken message (e.g. audio player crash) from
 * unmounting the entire message list.
 */
export class MessageErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error) {
    console.error('[MessageBubble] render error:', error);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="flex justify-center py-0.5">
          <span className="rounded-full bg-zinc-800/40 px-3 py-1 text-[10px] text-zinc-600">
            Error al mostrar mensaje
          </span>
        </div>
      );
    }
    return this.props.children;
  }
}
