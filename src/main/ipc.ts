import { ipcMain } from 'electron';
import type {
  AppSettings,
  CreateNamespaceInput,
  ListConfigsInput,
  KeyScanResult,
  NacosConfigItem,
  NacosConnection,
  RuntimeInfo,
  ScanKeyInput,
  SyncFilesInput,
  SyncKeyResultsInput,
  SyncNamespaceInput,
  SyncSummary
} from './types';
import type { SettingsStore } from './services/settingsStore';
import { NacosClient } from './services/nacosClient';
import { findKeyValue } from './services/configParser';
import { SyncService } from './services/syncService';
import type { ConfirmationRequest } from './services/confirmPolicy';

export const ipcChannels = {
  settingsLoad: 'settings:load',
  settingsSave: 'settings:save',
  appRuntimeInfo: 'app:runtime-info',
  appRestart: 'app:restart',
  devSaveRendererPort: 'dev:save-renderer-port',
  logChooseDirectory: 'log:choose-directory',
  logOpenDirectory: 'log:open-directory',
  nacosTestConnection: 'nacos:test-connection',
  nacosListNamespaces: 'nacos:list-namespaces',
  nacosCreateNamespace: 'nacos:create-namespace',
  nacosListConfigs: 'nacos:list-configs',
  syncNamespace: 'sync:namespace',
  syncFiles: 'sync:files',
  syncScanKey: 'sync:scan-key',
  syncKeyResults: 'sync:key-results'
} as const;

export interface IpcApi {
  loadSettings(): Promise<AppSettings>;
  saveSettings(settings: AppSettings): Promise<void>;
  getRuntimeInfo?(): Promise<RuntimeInfo>;
  restartApp?(): Promise<void>;
  saveRendererPort?(port: number): Promise<void>;
  chooseLogDirectory(): Promise<string | null>;
  openLogDirectory(): Promise<void>;
  testConnection(connection: NacosConnection): Promise<boolean>;
  listNamespaces(connection: NacosConnection): Promise<unknown[]>;
  createNamespace(input: CreateNamespaceInput): Promise<void>;
  listConfigs(input: ListConfigsInput): Promise<NacosConfigItem[]>;
  syncNamespace(input: SyncNamespaceInput): Promise<SyncSummary>;
  syncFiles(input: SyncFilesInput): Promise<SyncSummary>;
  scanKey(input: ScanKeyInput): Promise<KeyScanResult[]>;
  syncKeyResults(input: SyncKeyResultsInput): Promise<SyncSummary>;
}

export interface IpcDependencies {
  settingsStore?: SettingsStore;
  getRuntimeInfo?: () => RuntimeInfo;
  restartApp?: () => void;
  saveRendererPort?: (port: number) => Promise<void>;
  chooseLogDirectory?: () => Promise<string | null>;
  openLogDirectory?: () => Promise<void>;
}

export function registerIpcHandlers(dependencies: IpcDependencies = {}): void {
  ipcMain.handle(ipcChannels.settingsLoad, () => dependencies.settingsStore?.load() ?? ({} satisfies AppSettings));
  ipcMain.handle(ipcChannels.settingsSave, (_event, settings: AppSettings) => dependencies.settingsStore?.save(settings));
  ipcMain.handle(ipcChannels.appRuntimeInfo, () => dependencies.getRuntimeInfo?.() ?? { rendererPort: 5173, packaged: false });
  ipcMain.handle(ipcChannels.appRestart, () => dependencies.restartApp?.());
  ipcMain.handle(ipcChannels.devSaveRendererPort, (_event, port: number) => dependencies.saveRendererPort?.(port));
  ipcMain.handle(ipcChannels.logChooseDirectory, () => dependencies.chooseLogDirectory?.() ?? null);
  ipcMain.handle(ipcChannels.logOpenDirectory, () => dependencies.openLogDirectory?.());
  ipcMain.handle(ipcChannels.nacosTestConnection, async (_event, connection: NacosConnection) => {
    const client = new NacosClient(connection);
    return client.testConnection();
  });
  ipcMain.handle(ipcChannels.nacosListNamespaces, async (_event, connection: NacosConnection) => {
    const client = new NacosClient(connection);
    return client.listNamespaces();
  });
  ipcMain.handle(ipcChannels.nacosCreateNamespace, async (_event, input: CreateNamespaceInput) => {
    const client = new NacosClient(input.connection);
    await client.createNamespace(input.namespaceId, input.namespaceName, input.description);
  });
  ipcMain.handle(ipcChannels.nacosListConfigs, async (_event, input: ListConfigsInput) => {
    const client = new NacosClient(input.connection);
    return client.listConfigs(input.namespaceId);
  });
  ipcMain.handle(ipcChannels.syncNamespace, async (_event, input: SyncNamespaceInput) => {
    const service = createSyncService(input.sourceConnection, input.targetConnection, async (request) =>
      resolveConfirmDecision(request, {
        confirmNamespaceStart: input.confirmNamespaceStart,
        overwriteExistingFiles: input.overwriteExistingFiles,
        overwriteExistingKeys: false
      })
    );

    return service.syncNamespace({
      sourceNamespaceId: input.sourceNamespaceId,
      targetNamespaceId: input.targetNamespaceId
    });
  });
  ipcMain.handle(ipcChannels.syncFiles, async (_event, input: SyncFilesInput) => {
    const targetClient = new NacosClient(input.targetConnection);
    const service = new SyncService({
      sourceClient: targetClient,
      targetClient,
      confirm: async (request) =>
        resolveConfirmDecision(request, {
          confirmNamespaceStart: true,
          overwriteExistingFiles: input.overwriteExistingFiles,
          overwriteExistingKeys: false
        })
    });

    return service.syncFiles({
      targetNamespaceId: input.targetNamespaceId,
      files: input.files
    });
  });
  ipcMain.handle(ipcChannels.syncScanKey, async (_event, input: ScanKeyInput) => {
    const client = new NacosClient(input.connection);
    const configs = await client.listConfigs(input.namespaceId);

    return configs.flatMap((config): KeyScanResult[] => {
      const match = findKeyValue(config.content, input.keyName, config.dataId, config.type);

      if (!match) {
        return [];
      }

      return [
        {
          id: `${config.group}:${config.dataId}:${match.keyPath}`,
          dataId: config.dataId,
          group: config.group,
          keyPath: match.keyPath,
          value: match.value,
          type: config.type,
          syncStrategy: 'keyOnly'
        }
      ];
    });
  });
  ipcMain.handle(ipcChannels.syncKeyResults, async (_event, input: SyncKeyResultsInput) => {
    const service = createSyncService(input.sourceConnection, input.targetConnection, async (request) =>
      resolveConfirmDecision(request, {
        confirmNamespaceStart: true,
        overwriteExistingFiles: input.overwriteExistingFiles,
        overwriteExistingKeys: input.overwriteExistingKeys
      })
    );

    return service.syncKeyResults({
      sourceNamespaceId: input.sourceNamespaceId,
      targetNamespaceId: input.targetNamespaceId,
      results: input.results
    });
  });
}

function createSyncService(
  sourceConnection: NacosConnection,
  targetConnection: NacosConnection,
  confirm: (request: ConfirmationRequest) => Promise<'confirm' | 'skip' | 'cancel'>
): SyncService {
  return new SyncService({
    sourceClient: new NacosClient(sourceConnection),
    targetClient: new NacosClient(targetConnection),
    confirm
  });
}

function resolveConfirmDecision(
  request: ConfirmationRequest,
  options: {
    confirmNamespaceStart: boolean;
    overwriteExistingFiles: boolean;
    overwriteExistingKeys: boolean;
  }
): 'confirm' | 'skip' | 'cancel' {
  if (request.kind === 'namespaceStart') {
    return options.confirmNamespaceStart ? 'confirm' : 'cancel';
  }

  if (request.kind === 'overwriteExistingFiles' || request.kind === 'overwriteFile') {
    return options.overwriteExistingFiles ? 'confirm' : 'skip';
  }

  if (request.kind === 'overwriteKey') {
    return options.overwriteExistingKeys ? 'confirm' : 'skip';
  }

  return 'skip';
}
