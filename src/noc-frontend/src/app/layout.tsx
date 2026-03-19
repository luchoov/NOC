// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

export const metadata = {
  title: "NOC — Neuryn Omnichannel",
  description: "Plataforma de mensajería omnicanal",
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="es">
      <body>{children}</body>
    </html>
  );
}
