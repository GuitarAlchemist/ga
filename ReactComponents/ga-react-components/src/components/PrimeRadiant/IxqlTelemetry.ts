// IXQL Grammar Telemetry — tracks variant invocation counts in localStorage

export type IxqlVariantStats = {
  invoked: number;
  succeeded: number;
  failed: number;
  lastUsed: string | null;
};

export type IxqlTelemetryData = Record<string, IxqlVariantStats>;

const STORAGE_KEY = 'ixql-telemetry';

function load(): IxqlTelemetryData {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as IxqlTelemetryData) : {};
  } catch {
    return {};
  }
}

function save(data: IxqlTelemetryData): void {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(data));
}

export function recordInvocation(variant: string, success: boolean): void {
  const data = load();
  const entry = data[variant] ?? { invoked: 0, succeeded: 0, failed: 0, lastUsed: null };
  entry.invoked++;
  if (success) entry.succeeded++;
  else entry.failed++;
  entry.lastUsed = new Date().toISOString();
  data[variant] = entry;
  save(data);
}

export function getTelemetry(): IxqlTelemetryData {
  return load();
}

export function resetTelemetry(): void {
  localStorage.removeItem(STORAGE_KEY);
}
