import yaml from 'js-yaml';

type ConfigFormat = 'yaml' | 'json' | 'properties';

export interface KeyValueMatch {
  keyPath: string;
  value: unknown;
}

export function findKeyValue(content: string, keyPath: string, dataId: string, type?: string): KeyValueMatch | null {
  const format = detectConfigFormat(dataId, type);

  if (format === 'properties') {
    const properties = parseProperties(content);
    return properties.has(keyPath) ? { keyPath, value: properties.get(keyPath) } : null;
  }

  const parsed = parseStructuredContent(content, format);
  const value = getNestedValue(parsed, keyPath);

  return value === undefined ? null : { keyPath, value };
}

export function upsertKeyValue(
  content: string,
  keyPath: string,
  value: unknown,
  dataId: string,
  type?: string
): string {
  const format = detectConfigFormat(dataId, type);

  if (format === 'properties') {
    return upsertPropertiesValue(content, keyPath, value);
  }

  const parsed = parseStructuredContent(content, format);
  const next = isRecord(parsed) ? parsed : {};
  setNestedValue(next, keyPath, value);

  if (format === 'json') {
    return `${JSON.stringify(next, null, 2)}\n`;
  }

  return yaml.dump(next, { lineWidth: -1 });
}

export function detectConfigFormat(dataId: string, type?: string): ConfigFormat {
  const normalizedType = type?.toLowerCase();
  const normalizedDataId = dataId.toLowerCase();

  if (normalizedType === 'yaml' || normalizedType === 'yml' || normalizedDataId.endsWith('.yml') || normalizedDataId.endsWith('.yaml')) {
    return 'yaml';
  }

  if (normalizedType === 'json' || normalizedDataId.endsWith('.json')) {
    return 'json';
  }

  return 'properties';
}

function parseStructuredContent(content: string, format: Exclude<ConfigFormat, 'properties'>): unknown {
  if (!content.trim()) {
    return {};
  }

  return format === 'json' ? JSON.parse(content) : yaml.load(content);
}

function getNestedValue(input: unknown, keyPath: string): unknown {
  return keyPath.split('.').reduce<unknown>((current, segment) => {
    if (!isRecord(current)) {
      return undefined;
    }

    return current[segment];
  }, input);
}

function setNestedValue(input: Record<string, unknown>, keyPath: string, value: unknown): void {
  const segments = keyPath.split('.');
  let current = input;

  for (const [index, segment] of segments.entries()) {
    if (index === segments.length - 1) {
      current[segment] = value;
      return;
    }

    if (!isRecord(current[segment])) {
      current[segment] = {};
    }

    current = current[segment] as Record<string, unknown>;
  }
}

function parseProperties(content: string): Map<string, string> {
  const properties = new Map<string, string>();

  for (const line of content.split(/\r?\n/)) {
    const trimmed = line.trim();

    if (!trimmed || trimmed.startsWith('#') || trimmed.startsWith('!')) {
      continue;
    }

    const separatorIndex = findPropertySeparatorIndex(line);
    if (separatorIndex === -1) {
      continue;
    }

    const key = line.slice(0, separatorIndex).trim();
    const value = line.slice(separatorIndex + 1).trim();
    properties.set(key, value);
  }

  return properties;
}

function upsertPropertiesValue(content: string, keyPath: string, value: unknown): string {
  const lines = content.split(/\r?\n/);
  const stringValue = String(value);
  let updated = false;

  const nextLines = lines.map((line) => {
    const separatorIndex = findPropertySeparatorIndex(line);

    if (separatorIndex === -1 || line.trim().startsWith('#') || line.trim().startsWith('!')) {
      return line;
    }

    const key = line.slice(0, separatorIndex).trim();
    if (key !== keyPath) {
      return line;
    }

    updated = true;
    return `${keyPath}=${stringValue}`;
  });

  if (!updated) {
    if (nextLines.length > 0 && nextLines[nextLines.length - 1] !== '') {
      nextLines.push(`${keyPath}=${stringValue}`);
    } else {
      nextLines[nextLines.length - 1] = `${keyPath}=${stringValue}`;
    }
  }

  return `${nextLines.join('\n')}\n`;
}

function findPropertySeparatorIndex(line: string): number {
  const equalIndex = line.indexOf('=');
  const colonIndex = line.indexOf(':');

  if (equalIndex === -1) {
    return colonIndex;
  }

  if (colonIndex === -1) {
    return equalIndex;
  }

  return Math.min(equalIndex, colonIndex);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value));
}
