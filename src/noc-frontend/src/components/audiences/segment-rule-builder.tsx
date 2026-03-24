// Copyright (c) Neuryn Software
// SPDX-License-Identifier: AGPL-3.0-or-later

'use client';

import { Plus, Trash2, X } from 'lucide-react';
import type { SegmentRule, SegmentRuleField, SegmentRuleOperator } from '@/types/api';

const FIELD_OPTIONS: { value: SegmentRuleField; label: string; description: string }[] = [
  { value: 'locality', label: 'Localidad', description: 'Filtra por ciudad o zona del contacto' },
  { value: 'tags', label: 'Tags', description: 'Filtra por etiquetas asignadas' },
  { value: 'email', label: 'Email', description: 'Filtra por presencia de email' },
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

      if (partial.field && partial.field !== rule.field) {
        const newField = partial.field;
        merged.operator = OPERATORS_BY_FIELD[newField][0].value;
        merged.value = newField === 'email' ? undefined : newField === 'tags' ? [] : '';
      }

      if (merged.field === 'email') {
        merged.value = undefined;
      }

      return merged;
    });
    onChange(updated);
  }

  function addTag(ruleIndex: number, tag: string) {
    const rule = rules[ruleIndex];
    const currentTags = Array.isArray(rule.value) ? rule.value : [];
    const normalized = tag.trim().toLowerCase();
    if (!normalized || currentTags.includes(normalized)) return;
    updateRule(ruleIndex, { value: [...currentTags, normalized] });
  }

  function removeTag(ruleIndex: number, tag: string) {
    const rule = rules[ruleIndex];
    const currentTags = Array.isArray(rule.value) ? rule.value : [];
    updateRule(ruleIndex, { value: currentTags.filter((t) => t !== tag) });
  }

  const selectClass =
    'block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25';
  const inputClass =
    'block w-full rounded-md border border-zinc-800 bg-zinc-950 px-3 py-2 text-sm text-zinc-200 placeholder:text-zinc-600 outline-none transition-colors focus:border-blue-500/50 focus:ring-1 focus:ring-blue-500/25';

  return (
    <div className="space-y-3">
      {rules.length === 0 && (
        <div className="rounded-lg border border-dashed border-zinc-800 p-4 text-center">
          <p className="text-xs text-zinc-500">Sin reglas. El segmento incluira todos los contactos.</p>
          <p className="mt-1 text-[10px] text-zinc-600">Agrega reglas para filtrar contactos automaticamente.</p>
        </div>
      )}

      {rules.map((rule, i) => {
        const operators = OPERATORS_BY_FIELD[rule.field] ?? [];
        const isTagField = rule.field === 'tags';
        const isEmailField = rule.field === 'email';
        const tags = isTagField && Array.isArray(rule.value) ? rule.value : [];

        return (
          <div key={i} className="relative rounded-lg border border-zinc-800/60 bg-zinc-950/50 p-3">
            {/* Connector */}
            {i > 0 && (
              <div className="absolute -top-3 left-4 rounded bg-zinc-800 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider text-zinc-400">
                y
              </div>
            )}

            {/* Delete button */}
            <button
              type="button"
              onClick={() => removeRule(i)}
              className="absolute right-2 top-2 grid h-6 w-6 place-items-center rounded-md text-zinc-600 transition-colors hover:bg-red-500/10 hover:text-red-400"
            >
              <Trash2 className="h-3 w-3" />
            </button>

            {/* Field + Operator row */}
            <div className="grid grid-cols-2 gap-2 pr-8">
              <div>
                <label className="mb-1 block text-[10px] font-medium uppercase tracking-wider text-zinc-500">Campo</label>
                <select
                  value={rule.field}
                  onChange={(e) => updateRule(i, { field: e.target.value as SegmentRuleField })}
                  className={selectClass}
                >
                  {FIELD_OPTIONS.map((f) => (
                    <option key={f.value} value={f.value}>
                      {f.label}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="mb-1 block text-[10px] font-medium uppercase tracking-wider text-zinc-500">Condicion</label>
                <select
                  value={rule.operator}
                  onChange={(e) => updateRule(i, { operator: e.target.value as SegmentRuleOperator })}
                  className={selectClass}
                >
                  {operators.map((op) => (
                    <option key={op.value} value={op.value}>
                      {op.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            {/* Value input */}
            {!isEmailField && (
              <div className="mt-2">
                <label className="mb-1 block text-[10px] font-medium uppercase tracking-wider text-zinc-500">Valor</label>

                {isTagField ? (
                  <div>
                    {/* Tag chips */}
                    {tags.length > 0 && (
                      <div className="mb-2 flex flex-wrap gap-1.5">
                        {tags.map((tag) => (
                          <span
                            key={tag}
                            className="inline-flex items-center gap-1 rounded-full bg-blue-500/10 px-2.5 py-1 text-xs text-blue-300"
                          >
                            {tag}
                            <button
                              type="button"
                              onClick={() => removeTag(i, tag)}
                              className="ml-0.5 rounded-full p-0.5 transition-colors hover:bg-blue-500/20"
                            >
                              <X className="h-2.5 w-2.5" />
                            </button>
                          </span>
                        ))}
                      </div>
                    )}
                    {/* Tag input */}
                    <input
                      placeholder="Escribe un tag y presiona Enter..."
                      className={inputClass}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ',') {
                          e.preventDefault();
                          const input = e.currentTarget;
                          addTag(i, input.value);
                          input.value = '';
                        }
                      }}
                      onBlur={(e) => {
                        if (e.target.value.trim()) {
                          addTag(i, e.target.value);
                          e.target.value = '';
                        }
                      }}
                    />
                    <p className="mt-1 text-[10px] text-zinc-600">Presiona Enter o coma para agregar cada tag</p>
                  </div>
                ) : (
                  <input
                    value={(rule.value as string) ?? ''}
                    onChange={(e) => updateRule(i, { value: e.target.value })}
                    placeholder="Buenos Aires"
                    className={inputClass}
                  />
                )}
              </div>
            )}

            {isEmailField && (
              <p className="mt-2 text-[10px] text-zinc-500">
                {rule.operator === 'is_present'
                  ? 'Incluye contactos que tienen email registrado'
                  : 'Incluye contactos sin email registrado'}
              </p>
            )}
          </div>
        );
      })}

      <button
        type="button"
        onClick={addRule}
        className="flex w-full items-center justify-center gap-1.5 rounded-lg border border-dashed border-zinc-700 py-2.5 text-xs text-zinc-500 transition-colors hover:border-zinc-500 hover:bg-zinc-900/50 hover:text-zinc-300"
      >
        <Plus className="h-3.5 w-3.5" />
        Agregar regla
      </button>
    </div>
  );
}
