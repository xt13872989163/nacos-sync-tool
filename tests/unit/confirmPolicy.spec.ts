import { describe, expect, it } from 'vitest';
import {
  buildFileOverwriteConfirmation,
  buildKeyOverwriteConfirmation,
  buildNamespaceConfirmations
} from '../../src/main/services/confirmPolicy';

describe('confirmPolicy', () => {
  it('requires Namespace double confirmation when target has existing files', () => {
    const confirmations = buildNamespaceConfirmations(5, [
      { dataId: 'application.yml', group: 'DEFAULT_GROUP' },
      { dataId: 'bootstrap.yml', group: 'DEFAULT_GROUP' }
    ]);

    expect(confirmations.map((request) => request.kind)).toEqual(['namespaceStart', 'overwriteExistingFiles']);
    expect(confirmations[0].affectedFileCount).toBe(5);
    expect(confirmations[1].affectedFileCount).toBe(2);
  });

  it('requires only Namespace start confirmation when all target files are missing', () => {
    const confirmations = buildNamespaceConfirmations(3, []);

    expect(confirmations.map((request) => request.kind)).toEqual(['namespaceStart']);
  });

  it('requires single file overwrite confirmation for file sync', () => {
    const confirmation = buildFileOverwriteConfirmation({
      dataId: 'application.yml',
      group: 'DEFAULT_GROUP'
    });

    expect(confirmation.kind).toBe('overwriteFile');
    expect(confirmation.affectedFileCount).toBe(1);
  });

  it('requires single key overwrite confirmation for Key sync', () => {
    const confirmation = buildKeyOverwriteConfirmation(
      {
        dataId: 'application.yml',
        group: 'DEFAULT_GROUP'
      },
      'spring.datasource.url'
    );

    expect(confirmation.kind).toBe('overwriteKey');
    expect(confirmation.body).toContain('spring.datasource.url');
  });
});
