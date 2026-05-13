import type { NacosConfigItem } from '../types';

export type ConfirmationKind = 'namespaceStart' | 'overwriteExistingFiles' | 'overwriteFile' | 'overwriteKey';

export interface ConfirmationRequest {
  kind: ConfirmationKind;
  title: string;
  body: string;
  affectedFileCount: number;
  affectedConfigs: Array<Pick<NacosConfigItem, 'dataId' | 'group'>>;
}

export function buildNamespaceConfirmations(
  sourceFileCount: number,
  existingTargetConfigs: Array<Pick<NacosConfigItem, 'dataId' | 'group'>>
): ConfirmationRequest[] {
  const requests: ConfirmationRequest[] = [
    {
      kind: 'namespaceStart',
      title: '确认同步整个 Namespace',
      body: `本次将同步源 Namespace 下的 ${sourceFileCount} 个配置文件。`,
      affectedFileCount: sourceFileCount,
      affectedConfigs: []
    }
  ];

  if (existingTargetConfigs.length > 0) {
    requests.push({
      kind: 'overwriteExistingFiles',
      title: '确认覆盖目标已有配置',
      body: `目标 Namespace 中已有 ${existingTargetConfigs.length} 个同名 DataId + Group，确认后将覆盖这些配置文件。`,
      affectedFileCount: existingTargetConfigs.length,
      affectedConfigs: existingTargetConfigs
    });
  }

  return requests;
}

export function buildFileOverwriteConfirmation(config: Pick<NacosConfigItem, 'dataId' | 'group'>): ConfirmationRequest {
  return {
    kind: 'overwriteFile',
    title: '确认覆盖配置文件',
    body: `目标已存在 ${config.dataId} / ${config.group}，确认后将覆盖整个配置文件。`,
    affectedFileCount: 1,
    affectedConfigs: [config]
  };
}

export function buildKeyOverwriteConfirmation(
  config: Pick<NacosConfigItem, 'dataId' | 'group'>,
  keyPath: string
): ConfirmationRequest {
  return {
    kind: 'overwriteKey',
    title: '确认覆盖当前 Key',
    body: `目标配置 ${config.dataId} / ${config.group} 已存在 ${keyPath}，确认后仅覆盖该 Key 的值。`,
    affectedFileCount: 1,
    affectedConfigs: [config]
  };
}
