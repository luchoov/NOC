// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { Plus, Trash2 } from 'lucide-react';
import type { SegmentRule, SegmentRuleField, SegmentRuleOperator } from '@/types/api';

const FIELD_OPTIONS: { value: SegmentRuleField; label: string }[] = [
  { value: 'locality', label: 'Localidad' },
  { value: 'tags', label: 'Tags' },
  { value: 'email', label: 'Email' },
];

const OPERATORS_BY_FIELD: Record<SegmentRuleField, { value: SegmentRuleOperator; label: string }[]> = {
  locality: [
    { value: 'equals', label: 'es igual a' },
    { value: 'contains', label: 'contiene' },
  ],
  tags: [
    { value: 'has_any_of', label: 'tiene alguna de' },
    { value: 'has_all_of', label: 'tiene todas' },
  ],
  email: [
    { value: 'is_present', label: 'tiene email' },
    { value: 'is_absent', label: 'no tiene email' },
  ],
};

interface SegmentRuleBuilderProps {
  rules: SegmentRule[];
  onChange: (rules: SegmentRule[]) => void;
}

export function SegmentRuleBuilder({ rules, onChange }: SegmentRuleBuilderProps) {
  function addRule() {
    onChange([...rules, { field: 'locality', operator: 'equals', value: '' }]);
  }

  function removeRule(index: number) {
    onChange(rules.filter((_, i) => i !== index));
  }

  function updateRule(index: number, partial: Partial<SegmentRule>) {
    const updated = rules.map((rule, i) => {
      if (i !== index) return rule;
      const merged = { ...rule, ...partial };

      // Reset operator and value when field changes
      if (partial.field && partial.field !== rule.field) {
        const newField = partial.field;
        const defaultOp = OPERATORS_BY_FIELD[newField][0].value;
        merged.operator = defaultOp;
        merged.value = newField === 'email' ? undefined : '';
      }

      // Clear value for email operators
      if (merged.field === 'email') {
        merged.value = undefined;
      }

      return merged;
    });
    onChange(updated);
  }

  const inputClass =
    'block w-full rounded-md border border-zinc-800 bg-zinc-950 px-2.5 py-1.5 text-xs text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25';
  const selectClass =
    'block w-full rounded-md border border-zinc-800 bg-zinc-950 px-2.5 py-1.5 text-xs text-zinc-200 outline-none transition-colors focus:border-blue-500/50';

  return (
    <div className="space-y-2">
      {rules.length === 0 && (
        <p className="text-xs text-zinc-600">Sin reglas. El segmento incluira todos los contactos.</p>
      )}

      {rules.map((rule, i) => {
        const operators = OPERATORS_BY_FIELD[rule.field] ?? [];
        const needsValue = rule.field !== 'email';
        const isTagField = rule.field === 'tags';

        return (
          <div key={i} className="flex items-center gap-2">
            {i > 0 && <span className="shrink-0 text-[10px] font-medium text-zinc-600">Y</span>}

            <select
              value={rule.field}
              onChange={(e) => updateRule(i, { field: e.target.value as SegmentRuleField })}
              className={selectClass + ' w-28 shrink-0'}
            >
              {FIELD_OPTIONS.map((f) => (
                <option key={f.value} value={f.value}>
                  {f.label}
                </option>
              ))}
            </select>

            <select
              value={rule.operator}
              onChange={(e) => updateRule(i, { operator: e.target.value as SegmentRuleOperator })}
              className={selectClass + ' w-36 shrink-0'}
            >
              {operators.map((op) => (
                <option key={op.value} value={op.value}>
                  {op.label}
                </option>
              ))}
            </select>

            {needsValue && (
              <input
                value={
                  isTagField && Array.isArray(rule.value) ? rule.value.join(', ') : (rule.value as string) ?? ''
                }
                onChange={(e) => {
                  const raw = e.target.value;
                  const val = isTagField
                    ? raw.split(',').map((t) => t.trim().toLowerCase()).filter(Boolean)
                    : raw;
                  updateRule(i, { value: val });
                }}
                placeholder={isTagField ? 'vip, ecommerce' : 'Buenos Aires'}
                className={inputClass + ' min-w-0 flex-1'}
              />
            )}

            <button
              type="button"
              onClick={() => removeRule(i)}
              className="grid h-7 w-7 shrink-0 place-items-center rounded-md text-zinc-600 transition-colors hover:bg-red-500/10 hover:text-red-400"
            >
              <Trash2 className="h-3 w-3" />
            </button>
          </div>
        );
      })}

      <button
        type="button"
        onClick={addRule}
        className="flex items-center gap-1.5 rounded-md border border-dashed border-zinc-700 px-2.5 py-1.5 text-xs text-zinc-500 transition-colors hover:border-zinc-600 hover:text-zinc-300"
      >
        <Plus className="h-3 w-3" />
        Agregar regla
      </button>
    </div>
  );
}
