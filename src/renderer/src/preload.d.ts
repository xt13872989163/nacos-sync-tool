import type { IpcApi } from '../../main/ipc';

declare global {
  interface Window {
    nacosSync?: IpcApi;
  }
}

export {};
