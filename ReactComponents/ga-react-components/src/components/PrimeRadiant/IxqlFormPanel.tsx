// src/components/PrimeRadiant/IxqlFormPanel.tsx
// IXQL Form renderer — renders CREATE FORM specs as interactive form panels.
// Supports hexavalent enum fields (T/P/U/D/F/C), sliders, text, number, toggle.
// GOVERNED BY badge, SUBMIT COMMAND, ON_SUCCESS REFRESH via DashboardSignalBus.

import React, { useState, useCallback, useMemo } from 'react';
import type { FormSpec } from './IxqlWidgetSpec';
import type { FormFieldDef } from './IxqlControlParser';
import { signalBus, useSignals } from './DashboardSignalBus';
import { generateFormProof, publishRenderProof } from './RenderProof';
import { isValidTransition, publishTransition, type HexavalentValue } from './HexavalentTemporal';

// ---------------------------------------------------------------------------
// Props
// ---------------------------------------------------------------------------

export interface IxqlFormPanelProps {
  spec: FormSpec;
}

// ---------------------------------------------------------------------------
// Hexavalent colors
// ---------------------------------------------------------------------------

const HEXAVALENT_COLORS: Record<string, string> = {
  T: '#22c55e',
  P: '#a3e635',
  U: '#6b7280',
  D: '#f97316',
  F: '#ef4444',
  C: '#d946ef',
};

const HEXAVALENT_LABELS: Record<string, string> = {
  T: 'True',
  P: 'Probable',
  U: 'Unknown',
  D: 'Doubtful',
  F: 'False',
  C: 'Contradictory',
};

// ---------------------------------------------------------------------------
// Governed-by badge (same pattern as IxqlGridPanel)
// ---------------------------------------------------------------------------

const GovernedByBadge: React.FC<{ articles: number[] }> = ({ articles }) => {
  if (articles.length === 0) return null;
  return (
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: 3,
        fontSize: 10,
        opacity: 0.7,
      }}
      title={`Governed by Article${articles.length > 1 ? 's' : ''} ${articles.join(', ')}`}
    >
      {articles.map(a => (
        <span
          key={a}
          style={{
            background: 'rgba(255,215,0,0.15)',
            color: '#ffd700',
            padding: '1px 6px',
            borderRadius: 8,
            fontSize: 9,
            fontWeight: 'bold',
          }}
        >
          Art.{a}
        </span>
      ))}
    </span>
  );
};

// ---------------------------------------------------------------------------
// Field renderers
// ---------------------------------------------------------------------------

const EnumField: React.FC<{
  field: FormFieldDef;
  value: string;
  onChange: (v: string) => void;
  hexavalent: boolean;
  panelId?: string;
}> = ({ field, value, onChange, hexavalent, panelId }) => {
  const options = field.options || [];
  const isHexavalentEnum = hexavalent && options.every(o => HEXAVALENT_COLORS[o] !== undefined);

  // Hexavalent transition constraints: only adjacent values are clickable
  const handleClick = useCallback((opt: string) => {
    if (isHexavalentEnum && value) {
      const from = value.toUpperCase() as HexavalentValue;
      const to = opt.toUpperCase() as HexavalentValue;
      if (!isValidTransition(from, to)) return; // blocked
      // Publish transition event for audit trail
      publishTransition({
        field: field.name,
        panelId: panelId ?? 'form',
        fromValue: from,
        toValue: to,
        timestamp: Date.now(),
        actor: 'user',
      });
    }
    onChange(opt);
  }, [isHexavalentEnum, value, field.name, panelId, onChange]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      <label style={{ color: '#8b949e', fontSize: 11, fontWeight: 'bold' }}>
        {field.label || field.name}
      </label>
      <div style={{ display: 'flex', gap: 4, flexWrap: 'wrap' }}>
        {options.map(opt => {
          const isSelected = value === opt;
          const color = isHexavalentEnum ? HEXAVALENT_COLORS[opt] : '#ffd700';
          // Determine if this option is reachable via one-step transition
          const isAllowed = !isHexavalentEnum || isSelected ||
            isValidTransition(
              (value || 'U').toUpperCase() as HexavalentValue,
              opt.toUpperCase() as HexavalentValue,
            );
          // CSS temporal class for hexavalent buttons
          const hexClass = isHexavalentEnum ? (
            opt === 'C' ? 'hex-cell--contradictory' :
            opt === 'P' ? 'hex-cell--probable' :
            opt === 'D' ? 'hex-cell--doubtful' : ''
          ) : '';
          return (
            <button
              key={opt}
              className={isSelected ? hexClass : ''}
              onClick={() => handleClick(opt)}
              disabled={!isAllowed}
              title={
                isHexavalentEnum
                  ? (HEXAVALENT_LABELS[opt] || opt) + (isAllowed ? '' : ' (transition not allowed)')
                  : opt
              }
              style={{
                background: isSelected ? color : 'rgba(255,255,255,0.05)',
                color: isSelected ? '#000' : (isHexavalentEnum ? color : '#e6edf3'),
                border: `1px solid ${isSelected ? color : 'rgba(255,255,255,0.1)'}`,
                borderRadius: 4,
                padding: '6px 14px',
                cursor: isAllowed ? 'pointer' : 'not-allowed',
                fontFamily: "'JetBrains Mono', monospace",
                fontSize: 13,
                fontWeight: 'bold',
                transition: 'all 0.15s',
                minWidth: isHexavalentEnum ? 40 : undefined,
                textAlign: 'center',
                opacity: isAllowed ? 1 : 0.3,
              }}
            >
              {opt}
            </button>
          );
        })}
      </div>
      {isHexavalentEnum && (
        <div style={{ fontSize: 9, color: '#6b7280', marginTop: 2 }}>
          Transition: one step at a time (T-P-U-D-F). C reachable from any.
        </div>
      )}
    </div>
  );
};

const SliderField: React.FC<{
  field: FormFieldDef;
  value: number;
  onChange: (v: number) => void;
}> = ({ field, value, onChange }) => {
  const min = field.min ?? 0;
  const max = field.max ?? 1;
  const step = max <= 1 ? 0.01 : 1;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
      <label style={{ color: '#8b949e', fontSize: 11, fontWeight: 'bold' }}>
        {field.label || field.name}
      </label>
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <input
          type="range"
          min={min}
          max={max}
          step={step}
          value={value}
          onChange={e => onChange(parseFloat(e.target.value))}
          style={{
            flex: 1,
            accentColor: '#ffd700',
            cursor: 'pointer',
          }}
        />
        <span style={{
          color: '#e6edf3',
          fontSize: 13,
          fontFamily: "'JetBrains Mono', monospace",
          fontWeight: 'bold',
          minWidth: 48,
          textAlign: 'right',
        }}>
          {max <= 1 ? (value * 100).toFixed(0) + '%' : value.toFixed(0)}
        </span>
      </div>
    </div>
  );
};

const TextField: React.FC<{
  field: FormFieldDef;
  value: string;
  onChange: (v: string) => void;
}> = ({ field, value, onChange }) => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
    <label style={{ color: '#8b949e', fontSize: 11, fontWeight: 'bold' }}>
      {field.label || field.name}
    </label>
    <textarea
      value={value}
      onChange={e => onChange(e.target.value)}
      rows={3}
      style={{
        background: '#161b22',
        border: '1px solid rgba(255,255,255,0.1)',
        borderRadius: 4,
        color: '#e6edf3',
        fontFamily: "'JetBrains Mono', monospace",
        fontSize: 12,
        padding: '8px 10px',
        resize: 'vertical',
        outline: 'none',
        lineHeight: 1.5,
      }}
    />
  </div>
);

const NumberField: React.FC<{
  field: FormFieldDef;
  value: number;
  onChange: (v: number) => void;
}> = ({ field, value, onChange }) => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
    <label style={{ color: '#8b949e', fontSize: 11, fontWeight: 'bold' }}>
      {field.label || field.name}
    </label>
    <input
      type="number"
      min={field.min}
      max={field.max}
      value={value}
      onChange={e => onChange(parseFloat(e.target.value) || 0)}
      style={{
        background: '#161b22',
        border: '1px solid rgba(255,255,255,0.1)',
        borderRadius: 4,
        color: '#e6edf3',
        fontFamily: "'JetBrains Mono', monospace",
        fontSize: 12,
        padding: '8px 10px',
        width: 120,
        outline: 'none',
      }}
    />
  </div>
);

const ToggleField: React.FC<{
  field: FormFieldDef;
  value: boolean;
  onChange: (v: boolean) => void;
}> = ({ field, value, onChange }) => (
  <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
    <input
      type="checkbox"
      checked={value}
      onChange={e => onChange(e.target.checked)}
      style={{ accentColor: '#ffd700', width: 16, height: 16, cursor: 'pointer' }}
    />
    <label style={{ color: '#8b949e', fontSize: 11, fontWeight: 'bold', cursor: 'pointer' }}
      onClick={() => onChange(!value)}>
      {field.label || field.name}
    </label>
  </div>
);

// ---------------------------------------------------------------------------
// Main form panel component
// ---------------------------------------------------------------------------

export const IxqlFormPanel: React.FC<IxqlFormPanelProps> = ({ spec }) => {
  // Initialize form values from field defaults
  const [values, setValues] = useState<Record<string, unknown>>(() => {
    const initial: Record<string, unknown> = {};
    for (const f of spec.fields) {
      switch (f.fieldType) {
        case 'enum':
          initial[f.name] = f.options?.[0] || '';
          break;
        case 'slider':
        case 'number':
          initial[f.name] = f.min ?? 0;
          break;
        case 'text':
          initial[f.name] = '';
          break;
        case 'toggle':
          initial[f.name] = false;
          break;
      }
    }
    return initial;
  });

  const [submitting, setSubmitting] = useState(false);
  const [submitResult, setSubmitResult] = useState<{ ok: boolean; message: string } | null>(null);

  // Subscribe to signals for pre-fill
  const signalValues = useSignals(spec.subscribe);

  // Pre-fill from subscribed signals when they change
  React.useEffect(() => {
    if (spec.subscribe.length === 0) return;
    for (const sigName of spec.subscribe) {
      const sig = signalValues.get(sigName);
      if (sig?.value && typeof sig.value === 'object') {
        const data = sig.value as Record<string, unknown>;
        setValues(prev => {
          const next = { ...prev };
          for (const f of spec.fields) {
            if (data[f.name] !== undefined) {
              next[f.name] = data[f.name];
            }
          }
          return next;
        });
      }
    }
  }, [signalValues, spec.subscribe, spec.fields]);

  // Generate and publish form render proof — debounced to avoid firing on every keystroke
  React.useEffect(() => {
    const timer = setTimeout(() => {
      const proof = generateFormProof(
        spec.id,
        spec.fields.map(f => f.name),
        spec.fields.map(f => f.fieldType),
        spec.hexavalent,
        !!spec.submitCommand,
        values,
      );
      publishRenderProof(proof);
    }, 300);
    return () => clearTimeout(timer);
  }, [spec.id, spec.fields, spec.hexavalent, spec.submitCommand, values]);

  const updateValue = useCallback((name: string, value: unknown) => {
    setValues(prev => ({ ...prev, [name]: value }));
    setSubmitResult(null);
  }, []);

  const handleSubmit = useCallback(async () => {
    if (!spec.submitCommand) return;
    setSubmitting(true);
    setSubmitResult(null);

    try {
      const baseUrl = typeof window !== 'undefined' ? window.location.origin : 'http://localhost:5232';
      const response = await fetch(`${baseUrl}/api/${spec.submitCommand}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(values),
        signal: AbortSignal.timeout(10000),
      });

      if (!response.ok) {
        throw new Error(`${response.status}: ${response.statusText}`);
      }

      setSubmitResult({ ok: true, message: 'Submitted successfully' });

      // Fire ON_SUCCESS REFRESH signals
      for (const panelId of spec.onSuccess) {
        signalBus.publish('__refresh__' + panelId, { timestamp: Date.now() }, spec.id);
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : String(err);
      setSubmitResult({ ok: false, message });
    } finally {
      setSubmitting(false);
    }
  }, [spec, values]);

  return (
    <div style={{
      background: '#0d1117',
      border: '1px solid rgba(255,255,255,0.08)',
      borderRadius: 8,
      fontFamily: "'JetBrains Mono', monospace",
      overflow: 'hidden',
    }}>
      {/* Header */}
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: 8,
        padding: '10px 14px',
        borderBottom: '1px solid rgba(255,255,255,0.06)',
        background: 'rgba(0,0,0,0.3)',
      }}>
        <span style={{ color: '#e6edf3', fontSize: 13, fontWeight: 'bold' }}>
          {spec.id}
        </span>
        {spec.hexavalent && (
          <span style={{
            background: 'rgba(217,70,239,0.15)',
            color: '#d946ef',
            padding: '1px 6px',
            borderRadius: 8,
            fontSize: 9,
            fontWeight: 'bold',
          }}>
            HEXAVALENT
          </span>
        )}
        <GovernedByBadge articles={spec.governedBy} />
        <div style={{ flex: 1 }} />
        {spec.subscribe.length > 0 && (
          <span style={{
            color: '#6b7280',
            fontSize: 9,
          }} title={`Subscribes to: ${spec.subscribe.join(', ')}`}>
            SUB: {spec.subscribe.join(', ')}
          </span>
        )}
      </div>

      {/* Fields */}
      <div style={{ padding: '14px 14px 8px', display: 'flex', flexDirection: 'column', gap: 16 }}>
        {spec.fields.map(field => {
          switch (field.fieldType) {
            case 'enum':
              return (
                <EnumField
                  key={field.name}
                  field={field}
                  value={String(values[field.name] || '')}
                  onChange={v => updateValue(field.name, v)}
                  hexavalent={spec.hexavalent}
                  panelId={spec.id}
                />
              );
            case 'slider':
              return (
                <SliderField
                  key={field.name}
                  field={field}
                  value={Number(values[field.name]) || 0}
                  onChange={v => updateValue(field.name, v)}
                />
              );
            case 'text':
              return (
                <TextField
                  key={field.name}
                  field={field}
                  value={String(values[field.name] || '')}
                  onChange={v => updateValue(field.name, v)}
                />
              );
            case 'number':
              return (
                <NumberField
                  key={field.name}
                  field={field}
                  value={Number(values[field.name]) || 0}
                  onChange={v => updateValue(field.name, v)}
                />
              );
            case 'toggle':
              return (
                <ToggleField
                  key={field.name}
                  field={field}
                  value={Boolean(values[field.name])}
                  onChange={v => updateValue(field.name, v)}
                />
              );
            default:
              return null;
          }
        })}
      </div>

      {/* Submit area */}
      <div style={{
        padding: '8px 14px 14px',
        display: 'flex',
        alignItems: 'center',
        gap: 10,
      }}>
        {spec.submitCommand && (
          <button
            onClick={handleSubmit}
            disabled={submitting}
            style={{
              background: submitting ? 'rgba(255,215,0,0.08)' : 'rgba(255,215,0,0.15)',
              border: '1px solid rgba(255,215,0,0.3)',
              borderRadius: 4,
              color: '#ffd700',
              cursor: submitting ? 'wait' : 'pointer',
              padding: '6px 18px',
              fontFamily: "'JetBrains Mono', monospace",
              fontSize: 12,
              fontWeight: 'bold',
              opacity: submitting ? 0.6 : 1,
              transition: 'all 0.15s',
            }}
          >
            {submitting ? 'Submitting...' : 'Submit'}
          </button>
        )}
        {submitResult && (
          <span style={{
            fontSize: 11,
            color: submitResult.ok ? '#22c55e' : '#ef4444',
          }}>
            {submitResult.message}
          </span>
        )}
        {spec.onSuccess.length > 0 && (
          <span style={{ color: '#6b7280', fontSize: 9, marginLeft: 'auto' }}>
            On success: refresh {spec.onSuccess.join(', ')}
          </span>
        )}
      </div>
    </div>
  );
};

export default IxqlFormPanel;
