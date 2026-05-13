import { describe, expect, it } from 'vitest';
import { reactive } from 'vue';
import { toIpcPayload } from '../../src/preload/ipcPayload';

describe('toIpcPayload', () => {
  it('converts Vue reactive payloads into cloneable plain objects', () => {
    const payload = reactive({
      results: [
        {
          keyPath: 'application.storeService.invoiceAttachment',
          value: {
            bucket: 'info-docs-staging',
            minSizeInBytes: 1024,
            maxSizeInBytes: 31457280,
            folder: 'invoice-attachment/'
          },
          syncStrategy: 'keyOnly'
        }
      ]
    });

    const plainPayload = toIpcPayload(payload);

    expect(() => structuredClone(plainPayload)).not.toThrow();
    expect(plainPayload).toEqual({
      results: [
        {
          keyPath: 'application.storeService.invoiceAttachment',
          value: {
            bucket: 'info-docs-staging',
            minSizeInBytes: 1024,
            maxSizeInBytes: 31457280,
            folder: 'invoice-attachment/'
          },
          syncStrategy: 'keyOnly'
        }
      ]
    });
  });

  it('keeps undefined payloads undefined', () => {
    expect(toIpcPayload(undefined)).toBeUndefined();
  });
});
