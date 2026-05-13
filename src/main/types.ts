export type ClusterRole = 'source' | 'target';
export type SyncMode = 'namespace' | 'file' | 'key';
export type ConfirmDecision = 'confirm' | 'skip' | 'cancel';
export type AppTheme = 'forest' | 'slate' | 'warm' | 'nexus';

export interface NacosConnection {
  baseUrl: string;
  username: string;
  password: string;
}

export interface NacosNamespace {
  namespaceId: string;
  namespaceName: string;
  description?: string;
}

export interface NacosConfigItem {
  dataId: string;
  group: string;
  content: string;
  type?: string;
}

export interface KeyScanResult {
  id: string;
  dataId: string;
  group: string;
  keyPath: string;
  value: unknown;
  type?: string;
  syncStrategy: 'keyOnly' | 'fullFile';
}

export interface AppSettings {
  source?: NacosConnection;
  target?: NacosConnection;
  sourceHistory?: NacosConnection[];
  targetHistory?: NacosConnection[];
  sourceNamespaceId?: string;
  targetNamespaceId?: string;
  logDirectory?: string;
  theme?: AppTheme;
  rendererPort?: number;
}

export interface RuntimeInfo {
  rendererPort: number;
  packaged: boolean;
}

export interface CreateNamespaceInput {
  connection: NacosConnection;
  namespaceId: string;
  namespaceName?: string;
  description?: string;
}

export interface ListConfigsInput {
  connection: NacosConnection;
  namespaceId: string;
}

export interface ScanKeyInput {
  connection: NacosConnection;
  namespaceId: string;
  keyName: string;
}

export interface SyncNamespaceInput {
  sourceConnection: NacosConnection;
  targetConnection: NacosConnection;
  sourceNamespaceId: string;
  targetNamespaceId: string;
  confirmNamespaceStart: boolean;
  overwriteExistingFiles: boolean;
}

export interface SyncFilesInput {
  targetConnection: NacosConnection;
  targetNamespaceId: string;
  files: NacosConfigItem[];
  overwriteExistingFiles: boolean;
}

export interface SyncKeyResultsInput {
  sourceConnection: NacosConnection;
  targetConnection: NacosConnection;
  sourceNamespaceId: string;
  targetNamespaceId: string;
  results: KeyScanResult[];
  overwriteExistingFiles: boolean;
  overwriteExistingKeys: boolean;
}

export interface SyncSummary {
  created: number;
  updated: number;
  skipped: number;
  failed: number;
  messages: string[];
}
