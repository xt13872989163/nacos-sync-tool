import type { ConfirmDecision, KeyScanResult, NacosConfigItem, SyncSummary } from '../types';
import {
  buildFileOverwriteConfirmation,
  buildKeyOverwriteConfirmation,
  buildNamespaceConfirmations,
  type ConfirmationRequest
} from './confirmPolicy';
import { normalizeNacosConfigType } from './configType';
import { findKeyValue, upsertKeyValue } from './configParser';

export interface SyncNacosClient {
  listConfigs(namespaceId: string): Promise<NacosConfigItem[]>;
  getConfig(namespaceId: string, dataId: string, group: string): Promise<string | null>;
  publishConfig(namespaceId: string, item: NacosConfigItem): Promise<void>;
}

export type ConfirmHandler = (request: ConfirmationRequest) => Promise<ConfirmDecision>;

export interface SyncServiceDependencies {
  sourceClient: SyncNacosClient;
  targetClient: SyncNacosClient;
  confirm: ConfirmHandler;
}

export interface NamespaceSyncInput {
  sourceNamespaceId: string;
  targetNamespaceId: string;
}

export interface FileSyncInput {
  targetNamespaceId: string;
  files: NacosConfigItem[];
}

export interface KeyScanInput {
  sourceNamespaceId: string;
  keyName: string;
}

export interface KeySyncInput {
  sourceNamespaceId: string;
  targetNamespaceId: string;
  results: KeyScanResult[];
}

export class SyncService {
  constructor(private readonly dependencies: SyncServiceDependencies) {}

  async syncNamespace(input: NamespaceSyncInput): Promise<SyncSummary> {
    const sourceFiles = await this.dependencies.sourceClient.listConfigs(input.sourceNamespaceId);
    const targetContentById = new Map<string, string | null>();
    const existingTargetConfigs: Array<Pick<NacosConfigItem, 'dataId' | 'group'>> = [];

    for (const file of sourceFiles) {
      const targetContent = await this.dependencies.targetClient.getConfig(input.targetNamespaceId, file.dataId, file.group);
      targetContentById.set(configKey(file), targetContent);

      if (targetContent !== null) {
        existingTargetConfigs.push({ dataId: file.dataId, group: file.group });
      }
    }

    const confirmations = buildNamespaceConfirmations(sourceFiles.length, existingTargetConfigs);
    const firstDecision = await this.dependencies.confirm(confirmations[0]);

    if (firstDecision !== 'confirm') {
      return createSummary({ skipped: sourceFiles.length, messages: ['Namespace sync cancelled before start.'] });
    }

    const overwriteDecision =
      confirmations[1] && existingTargetConfigs.length > 0
        ? await this.dependencies.confirm(confirmations[1])
        : 'confirm';

    const summary = createSummary();

    for (const file of sourceFiles) {
      const targetContent = targetContentById.get(configKey(file)) ?? null;

      if (targetContent === null) {
        await this.dependencies.targetClient.publishConfig(input.targetNamespaceId, file);
        summary.created += 1;
        continue;
      }

      if (overwriteDecision === 'confirm') {
        await this.dependencies.targetClient.publishConfig(input.targetNamespaceId, file);
        summary.updated += 1;
      } else {
        summary.skipped += 1;
      }
    }

    return summary;
  }

  async syncFiles(input: FileSyncInput): Promise<SyncSummary> {
    const summary = createSummary();

    for (const file of input.files) {
      const targetContent = await this.dependencies.targetClient.getConfig(input.targetNamespaceId, file.dataId, file.group);

      if (targetContent === null) {
        await this.dependencies.targetClient.publishConfig(input.targetNamespaceId, file);
        summary.created += 1;
        continue;
      }

      const decision = await this.dependencies.confirm(buildFileOverwriteConfirmation(file));
      if (decision === 'confirm') {
        await this.dependencies.targetClient.publishConfig(input.targetNamespaceId, file);
        summary.updated += 1;
      } else {
        summary.skipped += 1;
      }
    }

    return summary;
  }

  async scanKey(input: KeyScanInput): Promise<KeyScanResult[]> {
    const sourceFiles = await this.dependencies.sourceClient.listConfigs(input.sourceNamespaceId);

    return sourceFiles.flatMap((file) => {
      const match = findKeyValue(file.content, input.keyName, file.dataId, file.type);

      if (!match) {
        return [];
      }

      return [
        {
          id: `${file.group}:${file.dataId}:${match.keyPath}`,
          dataId: file.dataId,
          group: file.group,
          keyPath: match.keyPath,
          value: match.value,
          type: file.type,
          syncStrategy: 'keyOnly' as const
        }
      ];
    });
  }

  async syncKeyResults(input: KeySyncInput): Promise<SyncSummary> {
    const summary = createSummary();

    for (const result of input.results) {
      const sourceContent = await this.dependencies.sourceClient.getConfig(
        input.sourceNamespaceId,
        result.dataId,
        result.group
      );

      if (sourceContent === null) {
        summary.failed += 1;
        summary.messages.push(`Source config missing: ${result.dataId} / ${result.group}`);
        continue;
      }

      if (result.syncStrategy === 'fullFile') {
        await this.syncFullFileFromKeyResult(input.targetNamespaceId, result, sourceContent, summary);
      } else {
        await this.syncSingleKey(input.targetNamespaceId, result, sourceContent, summary);
      }
    }

    return summary;
  }

  private async syncFullFileFromKeyResult(
    targetNamespaceId: string,
    result: KeyScanResult,
    sourceContent: string,
    summary: SyncSummary
  ): Promise<void> {
    const targetContent = await this.dependencies.targetClient.getConfig(targetNamespaceId, result.dataId, result.group);
    const sourceFile = {
      dataId: result.dataId,
      group: result.group,
      content: sourceContent,
      type: normalizeNacosConfigType(result.type, result.dataId)
    };

    if (targetContent === null) {
      await this.dependencies.targetClient.publishConfig(targetNamespaceId, sourceFile);
      summary.created += 1;
      return;
    }

    const decision = await this.dependencies.confirm(buildFileOverwriteConfirmation(sourceFile));
    if (decision === 'confirm') {
      await this.dependencies.targetClient.publishConfig(targetNamespaceId, sourceFile);
      summary.updated += 1;
    } else {
      summary.skipped += 1;
    }
  }

  private async syncSingleKey(
    targetNamespaceId: string,
    result: KeyScanResult,
    sourceContent: string,
    summary: SyncSummary
  ): Promise<void> {
    const targetContent = await this.dependencies.targetClient.getConfig(targetNamespaceId, result.dataId, result.group);
    const dataId = result.dataId;
    const group = result.group;

    if (targetContent === null) {
      const newContent = upsertKeyValue('', result.keyPath, result.value, dataId, result.type);
      await this.dependencies.targetClient.publishConfig(targetNamespaceId, {
        dataId,
        group,
        content: newContent,
        type: normalizeNacosConfigType(result.type, dataId)
      });
      summary.created += 1;
      return;
    }

    const existingKey = findKeyValue(targetContent, result.keyPath, dataId, result.type);

    if (existingKey) {
      const decision = await this.dependencies.confirm(buildKeyOverwriteConfirmation({ dataId, group }, result.keyPath));
      if (decision !== 'confirm') {
        summary.skipped += 1;
        return;
      }
    }

    const updatedContent = upsertKeyValue(targetContent, result.keyPath, result.value, dataId, result.type);
    await this.dependencies.targetClient.publishConfig(targetNamespaceId, {
      dataId,
      group,
      content: updatedContent,
      type: normalizeNacosConfigType(result.type, dataId)
    });
    summary.updated += 1;
  }
}

function createSummary(overrides: Partial<SyncSummary> = {}): SyncSummary {
  return {
    created: 0,
    updated: 0,
    skipped: 0,
    failed: 0,
    messages: [],
    ...overrides
  };
}

function configKey(config: Pick<NacosConfigItem, 'dataId' | 'group'>): string {
  return `${config.group}:${config.dataId}`;
}
