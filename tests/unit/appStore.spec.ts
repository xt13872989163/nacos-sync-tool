import { createPinia, setActivePinia } from 'pinia';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { useAppStore } from '../../src/renderer/src/stores/appStore';

describe('appStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    Object.defineProperty(globalThis, 'window', {
      value: {},
      writable: true,
      configurable: true
    });
  });

  it('persists settings with plain clonable objects', async () => {
    const saveSettings = vi.fn(async (settings: unknown) => {
      expect(() => structuredClone(settings)).not.toThrow();
    });

    window.nacosSync = {
      loadSettings: vi.fn(async () => ({})),
      saveSettings,
      chooseLogDirectory: vi.fn(async () => null),
      openLogDirectory: vi.fn(async () => undefined),
      testConnection: vi.fn(async () => true),
      listNamespaces: vi.fn(async () => []),
      createNamespace: vi.fn(async () => undefined),
      listConfigs: vi.fn(async () => []),
      syncNamespace: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      syncFiles: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      scanKey: vi.fn(async () => []),
      syncKeyResults: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] }))
    };

    const store = useAppStore();
    store.source.baseUrl = 'http://172.31.0.62:8848';
    store.source.username = 'nacos';
    store.source.password = 'nacos';
    store.source.namespaceId = '__public__';
    store.source.history = [
      {
        baseUrl: 'http://172.31.0.62:8848',
        username: 'nacos',
        password: 'nacos'
      }
    ];

    await store.persistSettings();

    expect(saveSettings).toHaveBeenCalledOnce();
    expect(saveSettings).toHaveBeenCalledWith({
      source: {
        baseUrl: 'http://172.31.0.62:8848',
        username: 'nacos',
        password: 'nacos'
      },
      target: {
        baseUrl: '',
        username: '',
        password: ''
      },
      sourceHistory: [
        {
          baseUrl: 'http://172.31.0.62:8848',
          username: 'nacos',
          password: 'nacos'
        }
      ],
      targetHistory: [],
      sourceNamespaceId: '__public__',
      targetNamespaceId: '',
      logDirectory: undefined,
      theme: 'forest',
      rendererPort: 5173
    });
  });

  it('loads and persists the selected theme', async () => {
    const saveSettings = vi.fn(async () => undefined);

    window.nacosSync = {
      loadSettings: vi.fn(async () => ({ theme: 'warm' as const, rendererPort: 5180 })),
      saveSettings,
      saveRendererPort: vi.fn(async () => undefined),
      chooseLogDirectory: vi.fn(async () => null),
      openLogDirectory: vi.fn(async () => undefined),
      testConnection: vi.fn(async () => true),
      listNamespaces: vi.fn(async () => []),
      createNamespace: vi.fn(async () => undefined),
      listConfigs: vi.fn(async () => []),
      syncNamespace: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      syncFiles: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      scanKey: vi.fn(async () => []),
      syncKeyResults: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] }))
    };

    const store = useAppStore();
    await store.initialize();
    expect(store.theme).toBe('warm');
    expect(store.rendererPort).toBe(5180);

    await store.setTheme('nexus');
    await store.setRendererPort(5190);

    expect(store.theme).toBe('nexus');
    expect(saveSettings).toHaveBeenCalledWith(expect.objectContaining({ theme: 'nexus' }));
    expect(saveSettings).toHaveBeenCalledWith(expect.objectContaining({ rendererPort: 5190 }));
  });

  it('detects packaged runtime so dev-only controls can be hidden', async () => {
    window.nacosSync = {
      loadSettings: vi.fn(async () => ({})),
      saveSettings: vi.fn(async () => undefined),
      getRuntimeInfo: vi.fn(async () => ({ rendererPort: 5173, packaged: true })),
      chooseLogDirectory: vi.fn(async () => null),
      openLogDirectory: vi.fn(async () => undefined),
      testConnection: vi.fn(async () => true),
      listNamespaces: vi.fn(async () => []),
      createNamespace: vi.fn(async () => undefined),
      listConfigs: vi.fn(async () => []),
      syncNamespace: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      syncFiles: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      scanKey: vi.fn(async () => []),
      syncKeyResults: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] }))
    };

    const store = useAppStore();
    await store.initialize();

    expect(store.isPackaged).toBe(true);
  });

  it('clears stale source state before applying a different history connection', () => {
    const store = useAppStore();
    store.source.connected = true;
    store.source.namespaceId = '__public__';
    store.source.namespaces = [{ namespaceId: '', namespaceName: 'public' }];
    store.source.history = [
      {
        baseUrl: 'http://source-a:8848',
        username: 'nacos-a',
        password: 'password-a'
      },
      {
        baseUrl: 'http://source-b:8848',
        username: 'nacos-b',
        password: 'password-b'
      }
    ];
    store.fileRows = [{ dataId: 'application.yml', group: 'DEFAULT_GROUP', content: 'foo: bar', type: 'yaml' }];
    store.keyRows = [
      {
        id: 'DEFAULT_GROUP:application.yml:foo',
        dataId: 'application.yml',
        group: 'DEFAULT_GROUP',
        keyPath: 'foo',
        value: 'bar',
        type: 'yaml',
        syncStrategy: 'keyOnly'
      }
    ];
    store.selectedFileIds = ['DEFAULT_GROUP:application.yml'];
    store.selectedKeyIds = ['DEFAULT_GROUP:application.yml:foo'];

    store.applyHistoryConnection('source', 'http://source-b:8848');

    expect(store.source).toMatchObject({
      baseUrl: 'http://source-b:8848',
      username: 'nacos-b',
      password: 'password-b',
      connected: false,
      namespaceId: '',
      namespaces: []
    });
    expect(store.fileRows).toEqual([]);
    expect(store.keyRows).toEqual([]);
    expect(store.selectedFileIds).toEqual([]);
    expect(store.selectedKeyIds).toEqual([]);
  });

  it('syncs selected key results with a plain clonable payload', async () => {
    const syncKeyResults = vi.fn(async (input: unknown) => {
      expect(() => structuredClone(input)).not.toThrow();
      expect(input).toMatchObject({
        results: [
          {
            id: 'DEFAULT_GROUP:application.yml:application.storeService.invoiceAttachment',
            dataId: 'application.yml',
            group: 'DEFAULT_GROUP',
            keyPath: 'application.storeService.invoiceAttachment',
            value: {
              bucket: 'info-docs-staging',
              minSizeInBytes: 1024,
              maxSizeInBytes: 31457280,
              folder: 'invoice-attachment/'
            },
            syncStrategy: 'keyOnly'
          }
        ],
        overwriteExistingKeys: true,
        overwriteExistingFiles: false
      });

      return { created: 0, updated: 1, skipped: 0, failed: 0, messages: [] };
    });

    window.nacosSync = {
      loadSettings: vi.fn(async () => ({})),
      saveSettings: vi.fn(async () => undefined),
      chooseLogDirectory: vi.fn(async () => null),
      openLogDirectory: vi.fn(async () => undefined),
      testConnection: vi.fn(async () => true),
      listNamespaces: vi.fn(async () => []),
      createNamespace: vi.fn(async () => undefined),
      listConfigs: vi.fn(async () => []),
      syncNamespace: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      syncFiles: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      scanKey: vi.fn(async () => []),
      syncKeyResults
    };
    vi.stubGlobal('confirm', vi.fn(() => true));

    const store = useAppStore();
    store.source.namespaceId = 'staging-singapore';
    store.target.namespaceId = 'test-sync';
    store.keyRows = [
      {
        id: 'DEFAULT_GROUP:application.yml:application.storeService.invoiceAttachment',
        dataId: 'application.yml',
        group: 'DEFAULT_GROUP',
        keyPath: 'application.storeService.invoiceAttachment',
        value: {
          bucket: 'info-docs-staging',
          minSizeInBytes: 1024,
          maxSizeInBytes: 31457280,
          folder: 'invoice-attachment/'
        },
        type: 'yaml',
        syncStrategy: 'keyOnly'
      }
    ];
    store.selectedKeyIds = ['DEFAULT_GROUP:application.yml:application.storeService.invoiceAttachment'];

    await store.syncSelectedKeys();

    expect(syncKeyResults).toHaveBeenCalledOnce();
    expect(store.notification).toMatchObject({
      type: 'success',
      title: 'Key 同步完成'
    });
  });

  it('syncs full-file key results with a plain clonable payload', async () => {
    const syncKeyResults = vi.fn(async (input: unknown) => {
      expect(() => structuredClone(input)).not.toThrow();
      expect(input).toMatchObject({
        results: [
          {
            id: 'DEFAULT_GROUP:application.yml:application.storeService',
            keyPath: 'application.storeService',
            syncStrategy: 'fullFile',
            value: {
              invoiceAttachment: {
                bucket: 'info-docs-staging',
                minSizeInBytes: 1024,
                maxSizeInBytes: 31457280,
                folder: 'invoice-attachment/'
              }
            }
          }
        ],
        overwriteExistingKeys: true,
        overwriteExistingFiles: true
      });

      return { created: 0, updated: 1, skipped: 0, failed: 0, messages: [] };
    });

    window.nacosSync = {
      loadSettings: vi.fn(async () => ({})),
      saveSettings: vi.fn(async () => undefined),
      chooseLogDirectory: vi.fn(async () => null),
      openLogDirectory: vi.fn(async () => undefined),
      testConnection: vi.fn(async () => true),
      listNamespaces: vi.fn(async () => []),
      createNamespace: vi.fn(async () => undefined),
      listConfigs: vi.fn(async () => []),
      syncNamespace: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      syncFiles: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      scanKey: vi.fn(async () => []),
      syncKeyResults
    };
    vi.stubGlobal('confirm', vi.fn(() => true));

    const store = useAppStore();
    store.source.namespaceId = 'staging-singapore';
    store.target.namespaceId = 'test-sync';
    store.keyRows = [
      {
        id: 'DEFAULT_GROUP:application.yml:application.storeService',
        dataId: 'application.yml',
        group: 'DEFAULT_GROUP',
        keyPath: 'application.storeService',
        value: {
          invoiceAttachment: {
            bucket: 'info-docs-staging',
            minSizeInBytes: 1024,
            maxSizeInBytes: 31457280,
            folder: 'invoice-attachment/'
          }
        },
        type: 'yaml',
        syncStrategy: 'fullFile'
      }
    ];
    store.selectedKeyIds = ['DEFAULT_GROUP:application.yml:application.storeService'];

    await store.syncSelectedKeys();

    expect(syncKeyResults).toHaveBeenCalledOnce();
    expect(store.notification).toMatchObject({
      type: 'success',
      title: 'Key 同步完成'
    });
  });

  it('shows an error notification when key sync fails', async () => {
    window.nacosSync = {
      loadSettings: vi.fn(async () => ({})),
      saveSettings: vi.fn(async () => undefined),
      chooseLogDirectory: vi.fn(async () => null),
      openLogDirectory: vi.fn(async () => undefined),
      testConnection: vi.fn(async () => true),
      listNamespaces: vi.fn(async () => []),
      createNamespace: vi.fn(async () => undefined),
      listConfigs: vi.fn(async () => []),
      syncNamespace: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      syncFiles: vi.fn(async () => ({ created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })),
      scanKey: vi.fn(async () => []),
      syncKeyResults: vi.fn(async () => {
        throw new Error('An object could not be cloned.');
      })
    };
    vi.stubGlobal('confirm', vi.fn(() => true));

    const store = useAppStore();
    store.source.namespaceId = 'staging-singapore';
    store.target.namespaceId = 'test-sync';
    store.keyRows = [
      {
        id: 'DEFAULT_GROUP:application.yml:application.storeService.invoiceAttachment',
        dataId: 'application.yml',
        group: 'DEFAULT_GROUP',
        keyPath: 'application.storeService.invoiceAttachment',
        value: {
          bucket: 'info-docs-staging'
        },
        type: 'yaml',
        syncStrategy: 'keyOnly'
      }
    ];
    store.selectedKeyIds = ['DEFAULT_GROUP:application.yml:application.storeService.invoiceAttachment'];

    await store.syncSelectedKeys();

    expect(store.notification).toMatchObject({
      type: 'error',
      title: 'Key 同步失败',
      message: 'An object could not be cloned.'
    });
  });
});
