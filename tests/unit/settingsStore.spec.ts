import { mkdtemp, readFile, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  createSettingsStore,
  type CryptoAdapter,
  SettingsStore
} from '../../src/main/services/settingsStore';
import type { AppSettings } from '../../src/main/types';

class TestCryptoAdapter implements CryptoAdapter {
  encrypt(value: string): Buffer {
    return Buffer.from(Buffer.from(value, 'utf8').toString('base64').split('').reverse().join(''), 'utf8');
  }

  decrypt(value: Buffer): string {
    return Buffer.from(value.toString('utf8').split('').reverse().join(''), 'base64').toString('utf8');
  }
}

describe('SettingsStore', () => {
  let tempDirectory: string;
  let store: SettingsStore;

  beforeEach(async () => {
    tempDirectory = await mkdtemp(path.join(os.tmpdir(), 'nacos-sync-settings-'));
    store = createSettingsStore(tempDirectory, new TestCryptoAdapter());
  });

  afterEach(async () => {
    await rm(tempDirectory, { recursive: true, force: true });
  });

  it('returns empty settings when the encrypted settings file does not exist', async () => {
    await expect(store.load()).resolves.toEqual({});
  });

  it('saves and loads settings without plaintext credentials in the file', async () => {
    const settings: AppSettings = {
      source: {
        baseUrl: 'http://source-nacos:8848',
        username: 'source-user',
        password: 'source-password'
      },
      target: {
        baseUrl: 'http://target-nacos:8848',
        username: 'target-user',
        password: 'target-password'
      },
      sourceNamespaceId: 'source-dev',
      targetNamespaceId: 'target-dev',
      logDirectory: 'D:\\NacosSyncTool\\logs'
    };

    await store.save(settings);

    await expect(store.load()).resolves.toEqual(settings);

    const rawFile = await readFile(path.join(tempDirectory, 'settings.enc'), 'utf8');
    expect(rawFile).not.toContain('source-user');
    expect(rawFile).not.toContain('source-password');
    expect(rawFile).not.toContain('target-user');
    expect(rawFile).not.toContain('target-password');
  });
});
