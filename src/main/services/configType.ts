const dataIdTypeMap: Array<[RegExp, string]> = [
  [/\.(ya?ml)$/i, 'yaml'],
  [/\.json$/i, 'json'],
  [/\.properties$/i, 'properties'],
  [/\.xml$/i, 'xml'],
  [/\.html?$/i, 'html']
];

export function normalizeNacosConfigType(type: string | undefined, dataId: string): string {
  const normalizedType = type?.trim().toLowerCase();

  if (normalizedType) {
    return normalizedType === 'yml' ? 'yaml' : normalizedType;
  }

  for (const [pattern, inferredType] of dataIdTypeMap) {
    if (pattern.test(dataId)) {
      return inferredType;
    }
  }

  return 'text';
}
