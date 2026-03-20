// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

export function formatPhone(phone: string): string {
  const d = phone.replace(/\D/g, '');
  if (d.length > 10) {
    const cc = d.slice(0, d.length - 10);
    const r = d.slice(-10);
    return `+${cc} ${r.slice(0, 3)} ${r.slice(3, 6)} ${r.slice(6)}`;
  }
  return phone;
}
