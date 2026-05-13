import { mkdtemp, readFile, rm } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { FileLogger } from '../../src/main/services/logger';

describe('FileLogger', () => {
  let tempDirectory: string;

  beforeEach(async () => {
    tempDirectory = await mkdtemp(path.join(os.tmpdir(), 'nacos-sync-logs-'));
  });

  afterEach(async () => {
    await rm(tempDirectory, { recursive: true, force: true });
  });

  it('writes runtime, sync, and error logs to categorized files', async () => {
    const logger = new FileLogger(tempDirectory);

    await logger.info('runtime', 'application started');
    await logger.info('sync', 'created config demo.yml');
    await logger.error('failed to connect target nacos');

    await expect(readFile(path.join(tempDirectory, 'runtime.log'), 'utf8')).resolves.toContain(
      '[INFO] application started'
    );
    await expect(readFile(path.join(tempDirectory, 'sync.log'), 'utf8')).resolves.toContain(
      '[INFO] created config demo.yml'
    );
    await expect(readFile(path.join(tempDirectory, 'error.log'), 'utf8')).resolves.toContain(
      '[ERROR] failed to connect target nacos'
    );
  });

  it('writes new log lines to a changed directory', async () => {
    const nextDirectory = await mkdtemp(path.join(os.tmpdir(), 'nacos-sync-next-'));
    const logger = new FileLogger(tempDirectory);

    try {
      logger.setLogDirectory(nextDirectory);
      await logger.warn('runtime', 'log path changed');

      await expect(readFile(path.join(nextDirectory, 'runtime.log'), 'utf8')).resolves.toContain(
        '[WARN] log path changed'
      );
    } finally {
      await rm(nextDirectory, { recursive: true, force: true });
    }
  });
});
