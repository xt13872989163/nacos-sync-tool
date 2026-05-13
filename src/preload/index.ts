import { contextBridge, ipcRenderer } from 'electron';
import { ipcChannels, type IpcApi } from '../main/ipc';
import { toIpcPayload } from './ipcPayload';

const api: IpcApi = {
  loadSettings: () => ipcRenderer.invoke(ipcChannels.settingsLoad),
  saveSettings: (settings) => ipcRenderer.invoke(ipcChannels.settingsSave, toIpcPayload(settings)),
  getRuntimeInfo: () => ipcRenderer.invoke(ipcChannels.appRuntimeInfo),
  restartApp: () => ipcRenderer.invoke(ipcChannels.appRestart),
  saveRendererPort: (port) => ipcRenderer.invoke(ipcChannels.devSaveRendererPort, port),
  chooseLogDirectory: () => ipcRenderer.invoke(ipcChannels.logChooseDirectory),
  openLogDirectory: () => ipcRenderer.invoke(ipcChannels.logOpenDirectory),
  testConnection: (connection) => ipcRenderer.invoke(ipcChannels.nacosTestConnection, toIpcPayload(connection)),
  listNamespaces: (connection) => ipcRenderer.invoke(ipcChannels.nacosListNamespaces, toIpcPayload(connection)),
  createNamespace: (input) => ipcRenderer.invoke(ipcChannels.nacosCreateNamespace, toIpcPayload(input)),
  listConfigs: (input) => ipcRenderer.invoke(ipcChannels.nacosListConfigs, toIpcPayload(input)),
  syncNamespace: (input) => ipcRenderer.invoke(ipcChannels.syncNamespace, toIpcPayload(input)),
  syncFiles: (input) => ipcRenderer.invoke(ipcChannels.syncFiles, toIpcPayload(input)),
  scanKey: (input) => ipcRenderer.invoke(ipcChannels.syncScanKey, toIpcPayload(input)),
  syncKeyResults: (input) => ipcRenderer.invoke(ipcChannels.syncKeyResults, toIpcPayload(input))
};

contextBridge.exposeInMainWorld('nacosSync', api);
