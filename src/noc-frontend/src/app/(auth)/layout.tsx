// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

export default function AuthLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="grid min-h-screen place-items-center bg-zinc-950">
      <div className="w-full max-w-sm px-4">{children}</div>
    </div>
  );
}
