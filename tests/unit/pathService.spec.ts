import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { resolveDefaultLogDirectory } from '../../src/main/services/pathService';

describe('pathService', () => {
  it('uses D drive logs on Windows when D drive is writable', () => {
    expect(resolveDefaultLogDirectory('win32', 'C:\\Users\\tester\\AppData\\Roaming\\Nacos Sync Tool', true)).toBe(
      'D:\\NacosSyncTool\\logs'
    );
  });

  it('falls back to userData logs when D drive is not writable', () => {
    const userDataPath = 'C:\\Users\\tester\\AppData\\Roaming\\Nacos Sync Tool';

    expect(resolveDefaultLogDirectory('win32', userDataPath, false)).toBe(path.join(userDataPath, 'logs'));
  });

  it('uses userData logs on Mac', () => {
    const userDataPath = '/Users/tester/Library/Application Support/Nacos Sync Tool';

    expect(resolveDefaultLogDirectory('darwin', userDataPath, false)).toBe(path.join(userDataPath, 'logs'));
  });
});
