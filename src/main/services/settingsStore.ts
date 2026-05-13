import { mkdir, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';
import type { AppSettings } from '../types';

export interface CryptoAdapter {
  encrypt(value: string): Buffer;
  decrypt(value: Buffer): string;
}

export class SettingsStore {
  constructor(
    private readonly filePath: string,
    private readonly cryptoAdapter: CryptoAdapter
  ) {}

  async load(): Promise<AppSettings> {
    try {
      const encrypted = await readFile(this.filePath);
      return JSON.parse(this.cryptoAdapter.decrypt(encrypted)) as AppSettings;
    } catch (error) {
      if (isMissingFileError(error)) {
        return {};
      }

      throw error;
    }
  }

  async save(settings: AppSettings): Promise<void> {
    await mkdir(path.dirname(this.filePath), { recursive: true });
    const encrypted = this.cryptoAdapter.encrypt(JSON.stringify(settings));
    await writeFile(this.filePath, encrypted);
  }
}

export function createElectronSafeStorageAdapter(storage: {
  encryptString(value: string): Buffer;
  decryptString(value: Buffer): string;
}): CryptoAdapter {
  return {
    encrypt: (value) => storage.encryptString(value),
    decrypt: (value) => storage.decryptString(value)
  };
}

export function createSettingsStore(userDataPath: string, cryptoAdapter: CryptoAdapter): SettingsStore {
  return new SettingsStore(path.join(userDataPath, 'settings.enc'), cryptoAdapter);
}

function isMissingFileError(error: unknown): boolean {
  return error instanceof Error && 'code' in error && error.code === 'ENOENT';
}
