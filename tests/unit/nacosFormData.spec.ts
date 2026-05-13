import { describe, expect, it } from 'vitest';
import { serializeNacosFormData } from '../../src/main/services/nacosClient';

describe('serializeNacosFormData', () => {
  it('serializes POST body as x-www-form-urlencoded data', () => {
    const formData = serializeNacosFormData({
      username: 'nacos',
      password: 'nacos',
      empty: '',
      skipped: undefined
    });

    expect(formData).toBeInstanceOf(URLSearchParams);
    expect(String(formData)).toBe('username=nacos&password=nacos&empty=');
  });
});
