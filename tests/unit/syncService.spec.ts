import { describe, expect, it } from 'vitest';
import { SyncService, type ConfirmHandler, type SyncNacosClient } from '../../src/main/services/syncService';
import type { ConfirmDecision, NacosConfigItem } from '../../src/main/types';

class FakeNacosClient implements SyncNacosClient {
  published: Array<{ namespaceId: string; item: NacosConfigItem }> = [];

  constructor(
    private readonly configsByNamespace: Map<string, NacosConfigItem[]> = new Map(),
    private readonly contentByKey: Map<string, string | null> = new Map()
  ) {}

  async listConfigs(namespaceId: string): Promise<NacosConfigItem[]> {
    return this.configsByNamespace.get(namespaceId) ?? [];
  }

  async getConfig(namespaceId: string, dataId: string, group: string): Promise<string | null> {
    return this.contentByKey.get(`${namespaceId}:${group}:${dataId}`) ?? null;
  }

  async publishConfig(namespaceId: string, item: NacosConfigItem): Promise<void> {
    this.published.push({ namespaceId, item });
  }
}

function confirmWith(decisions: ConfirmDecision[]): ConfirmHandler {
  return async () => decisions.shift() ?? 'confirm';
}

describe('SyncService', () => {
  it('never publishes to source client', async () => {
    const source = new FakeNacosClient(
      new Map([
        [
          'source-dev',
          [{ dataId: 'application.yml', group: 'DEFAULT_GROUP', content: 'server:\n  port: 8080' }]
        ]
      ])
    );
    const target = new FakeNacosClient();
    const service = new SyncService({ sourceClient: source, targetClient: target, confirm: confirmWith(['confirm']) });

    await service.syncNamespace({ sourceNamespaceId: 'source-dev', targetNamespaceId: 'target-dev' });

    expect(source.published).toHaveLength(0);
    expect(target.published).toHaveLength(1);
  });

  it('creates missing files during Namespace sync', async () => {
    const source = new FakeNacosClient(
      new Map([
        ['source-dev', [{ dataId: 'demo.yml', group: 'DEFAULT_GROUP', content: 'demo: true' }]]
      ])
    );
    const target = new FakeNacosClient();
    const service = new SyncService({ sourceClient: source, targetClient: target, confirm: confirmWith(['confirm']) });

    await expect(service.syncNamespace({ sourceNamespaceId: 'source-dev', targetNamespaceId: 'target-dev' })).resolves.toMatchObject({
      created: 1,
      updated: 0
    });
  });

  it('skips Namespace overwrite when second confirmation is cancelled', async () => {
    const source = new FakeNacosClient(
      new Map([
        ['source-dev', [{ dataId: 'demo.yml', group: 'DEFAULT_GROUP', content: 'demo: source' }]]
      ])
    );
    const target = new FakeNacosClient(
      new Map(),
      new Map([['target-dev:DEFAULT_GROUP:demo.yml', 'demo: target']])
    );
    const service = new SyncService({
      sourceClient: source,
      targetClient: target,
      confirm: confirmWith(['confirm', 'cancel'])
    });

    await expect(service.syncNamespace({ sourceNamespaceId: 'source-dev', targetNamespaceId: 'target-dev' })).resolves.toMatchObject({
      updated: 0,
      skipped: 1
    });
    expect(target.published).toHaveLength(0);
  });

  it('overwrites selected file only after confirmation', async () => {
    const source = new FakeNacosClient();
    const target = new FakeNacosClient(
      new Map(),
      new Map([['target-dev:DEFAULT_GROUP:demo.yml', 'demo: target']])
    );
    const service = new SyncService({ sourceClient: source, targetClient: target, confirm: confirmWith(['confirm']) });

    await expect(
      service.syncFiles({
        targetNamespaceId: 'target-dev',
        files: [{ dataId: 'demo.yml', group: 'DEFAULT_GROUP', content: 'demo: source' }]
      })
    ).resolves.toMatchObject({ updated: 1 });

    expect(target.published[0].item.content).toBe('demo: source');
  });

  it('keeps yaml type when overwriting full file from Key result', async () => {
    const source = new FakeNacosClient(
      new Map(),
      new Map([['source-dev:DEFAULT_GROUP:application.yml', 'spring:\n  datasource:\n    url: jdbc:mysql://source\n']])
    );
    const target = new FakeNacosClient(
      new Map(),
      new Map([['target-dev:DEFAULT_GROUP:application.yml', 'spring:\n  datasource:\n    url: jdbc:mysql://target\n']])
    );
    const service = new SyncService({ sourceClient: source, targetClient: target, confirm: confirmWith(['confirm']) });

    await expect(
      service.syncKeyResults({
        sourceNamespaceId: 'source-dev',
        targetNamespaceId: 'target-dev',
        results: [
          {
            id: 'DEFAULT_GROUP:application.yml:spring.datasource.url',
            dataId: 'application.yml',
            group: 'DEFAULT_GROUP',
            keyPath: 'spring.datasource.url',
            value: 'jdbc:mysql://source',
            syncStrategy: 'fullFile'
          }
        ]
      })
    ).resolves.toMatchObject({ updated: 1 });

    expect(target.published[0].item).toMatchObject({
      dataId: 'application.yml',
      type: 'yaml'
    });
  });

  it('appends missing Key without overwrite confirmation', async () => {
    const source = new FakeNacosClient(
      new Map(),
      new Map([['source-dev:DEFAULT_GROUP:application.properties', 'spring.datasource.url=jdbc:mysql://source\n']])
    );
    const target = new FakeNacosClient(
      new Map(),
      new Map([['target-dev:DEFAULT_GROUP:application.properties', 'server.port=8080\n']])
    );
    const service = new SyncService({ sourceClient: source, targetClient: target, confirm: confirmWith([]) });

    await expect(
      service.syncKeyResults({
        sourceNamespaceId: 'source-dev',
        targetNamespaceId: 'target-dev',
        results: [
          {
            id: 'DEFAULT_GROUP:application.properties:spring.datasource.url',
            dataId: 'application.properties',
            group: 'DEFAULT_GROUP',
            keyPath: 'spring.datasource.url',
            value: 'jdbc:mysql://source',
            syncStrategy: 'keyOnly'
          }
        ]
      })
    ).resolves.toMatchObject({ updated: 1 });

    expect(target.published[0].item.content).toContain('server.port=8080');
    expect(target.published[0].item.content).toContain('spring.datasource.url=jdbc:mysql://source');
  });

  it('creates a missing target file with only the selected Key when using keyOnly', async () => {
    const sourceYaml = [
      'application:',
      '  storeService:',
      '    crmAccountAttachment:',
      '      bucket: crm-source',
      '    crmAccountAttachmentDisplay:',
      '      bucket: display-source'
    ].join('\n');
    const source = new FakeNacosClient(
      new Map(),
      new Map([['source-dev:DEFAULT_GROUP:common-service.yaml', sourceYaml]])
    );
    const target = new FakeNacosClient();
    const service = new SyncService({ sourceClient: source, targetClient: target, confirm: confirmWith([]) });

    await expect(
      service.syncKeyResults({
        sourceNamespaceId: 'source-dev',
        targetNamespaceId: 'target-dev',
        results: [
          {
            id: 'DEFAULT_GROUP:common-service.yaml:application.storeService.crmAccountAttachmentDisplay',
            dataId: 'common-service.yaml',
            group: 'DEFAULT_GROUP',
            keyPath: 'application.storeService.crmAccountAttachmentDisplay',
            value: {
              bucket: 'display-source'
            },
            type: 'yaml',
            syncStrategy: 'keyOnly'
          }
        ]
      })
    ).resolves.toMatchObject({ created: 1, updated: 0 });

    expect(target.published[0].item.content).toContain('crmAccountAttachmentDisplay:');
    expect(target.published[0].item.content).toContain('bucket: display-source');
    expect(target.published[0].item.content).not.toContain('crmAccountAttachment:');
    expect(target.published[0].item.content).not.toContain('bucket: crm-source');
  });

  it('overwrites existing Key only after confirmation', async () => {
    const source = new FakeNacosClient(
      new Map(),
      new Map([['source-dev:DEFAULT_GROUP:application.properties', 'spring.datasource.url=jdbc:mysql://source\n']])
    );
    const target = new FakeNacosClient(
      new Map(),
      new Map([['target-dev:DEFAULT_GROUP:application.properties', 'spring.datasource.url=jdbc:mysql://target\n']])
    );
    const service = new SyncService({ sourceClient: source, targetClient: target, confirm: confirmWith(['confirm']) });

    await expect(
      service.syncKeyResults({
        sourceNamespaceId: 'source-dev',
        targetNamespaceId: 'target-dev',
        results: [
          {
            id: 'DEFAULT_GROUP:application.properties:spring.datasource.url',
            dataId: 'application.properties',
            group: 'DEFAULT_GROUP',
            keyPath: 'spring.datasource.url',
            value: 'jdbc:mysql://source',
            syncStrategy: 'keyOnly'
          }
        ]
      })
    ).resolves.toMatchObject({ updated: 1 });

    expect(target.published[0].item.content).toContain('spring.datasource.url=jdbc:mysql://source');
  });

  it('keeps yaml object structure when syncing a parent object path from a config without file extension', async () => {
    const sourceYaml = [
      'application:',
      '  storeService:',
      '    crmAccountAttachment:',
      '      bucket: info-docs-staging',
      '    linkedinResume:',
      '      bucket: lnkd-resume',
      '      minSizeInBytes: 1024'
    ].join('\n');
    const targetYaml = [
      'application:',
      '  storeService:',
      '    linkedinResume:',
      '      bucket: old-resume',
      '      minSizeInBytes: 512'
    ].join('\n');
    const source = new FakeNacosClient(
      new Map(),
      new Map([['source-dev:DEFAULT_GROUP:application', sourceYaml]])
    );
    const target = new FakeNacosClient(
      new Map(),
      new Map([['target-dev:DEFAULT_GROUP:application', targetYaml]])
    );
    const service = new SyncService({ sourceClient: source, targetClient: target, confirm: confirmWith(['confirm']) });

    await expect(
      service.syncKeyResults({
        sourceNamespaceId: 'source-dev',
        targetNamespaceId: 'target-dev',
        results: [
          {
            id: 'DEFAULT_GROUP:application:application.storeService',
            dataId: 'application',
            group: 'DEFAULT_GROUP',
            keyPath: 'application.storeService',
            value: {
              crmAccountAttachment: {
                bucket: 'info-docs-staging'
              },
              linkedinResume: {
                bucket: 'lnkd-resume',
                minSizeInBytes: 1024
              }
            },
            type: 'yaml',
            syncStrategy: 'keyOnly'
          }
        ]
      })
    ).resolves.toMatchObject({ updated: 1 });

    expect(target.published[0].item.content).toContain('storeService:');
    expect(target.published[0].item.content).toContain('crmAccountAttachment:');
    expect(target.published[0].item.content).toContain('bucket: info-docs-staging');
    expect(target.published[0].item.content).not.toContain('[object Object]');
  });
});
