import type { IpcApi } from '../../main/ipc';

const bridgeBaseUrl = 'http://127.0.0.1:37621';

export function installBrowserMockApi(): void {
  if (window.nacosSync) {
    return;
  }

  window.nacosSync = {
    loadSettings: async () => ({}),
    saveSettings: async () => undefined,
    getRuntimeInfo: async () => ({ rendererPort: Number(window.location.port) || 5173, packaged: false }),
    restartApp: async () => window.location.reload(),
    saveRendererPort: async () => undefined,
    chooseLogDirectory: async () => 'D:\\NacosSyncTool\\logs',
    openLogDirectory: async () => undefined,
    testConnection: (connection) => postBridge('/nacos/test-connection', connection, true),
    listNamespaces: (connection) => postBridge('/nacos/list-namespaces', connection, []),
    createNamespace: (input) => postBridge('/nacos/create-namespace', input, undefined),
    listConfigs: (input) => postBridge('/nacos/list-configs', input, []),
    syncNamespace: (input) => postBridge('/sync/namespace', input, { created: 0, updated: 0, skipped: 0, failed: 0, messages: [] }),
    syncFiles: (input) => postBridge('/sync/files', input, { created: 0, updated: 0, skipped: 0, failed: 0, messages: [] }),
    scanKey: (input) => postBridge('/sync/scan-key', input, []),
    syncKeyResults: (input) => postBridge('/sync/key-results', input, { created: 0, updated: 0, skipped: 0, failed: 0, messages: [] })
  } satisfies IpcApi;
}

async function postBridge<T>(path: string, body: unknown, fallback: T): Promise<T> {
  try {
    const response = await fetch(`${bridgeBaseUrl}${path}`, {
      method: 'POST',
      headers: {
        'content-type': 'application/json'
      },
      body: JSON.stringify(body)
    });

    const payload = (await response.json()) as T | { message?: string };

    if (!response.ok) {
      throw new Error(readBridgeError(payload));
    }

    return payload as T;
  } catch (error) {
    if (error instanceof TypeError) {
      return fallback;
    }

    throw error;
  }
}

function readBridgeError(payload: unknown): string {
  if (payload && typeof payload === 'object' && 'message' in payload) {
    return String((payload as { message?: string }).message);
  }

  return '本地调试桥请求失败';
}
