import { describe, expect, it } from 'vitest';
import { findKeyValue, upsertKeyValue } from '../../src/main/services/configParser';

describe('configParser', () => {
  it('finds and updates YAML key paths', () => {
    const source = [
      'spring:',
      '  datasource:',
      '    url: jdbc:mysql://source'
    ].join('\n');

    expect(findKeyValue(source, 'spring.datasource.url', 'application.yml')).toEqual({
      keyPath: 'spring.datasource.url',
      value: 'jdbc:mysql://source'
    });

    const updated = upsertKeyValue(source, 'spring.datasource.url', 'jdbc:mysql://target', 'application.yml');

    expect(updated).toContain('spring:');
    expect(updated).toContain('datasource:');
    expect(updated).toContain('url: jdbc:mysql://target');
  });

  it('finds and updates JSON key paths without changing unrelated values', () => {
    const source = JSON.stringify({
      spring: {
        datasource: {
          url: 'jdbc:mysql://source',
          username: 'keep-me'
        }
      }
    });

    expect(findKeyValue(source, 'spring.datasource.url', 'application.json')).toEqual({
      keyPath: 'spring.datasource.url',
      value: 'jdbc:mysql://source'
    });

    const updated = upsertKeyValue(source, 'spring.datasource.url', 'jdbc:mysql://target', 'application.json');
    const parsed = JSON.parse(updated) as { spring: { datasource: { url: string; username: string } } };

    expect(parsed.spring.datasource.url).toBe('jdbc:mysql://target');
    expect(parsed.spring.datasource.username).toBe('keep-me');
  });

  it('appends missing Properties key to the end of the file', () => {
    const source = 'server.port=8080\n';

    const updated = upsertKeyValue(source, 'spring.datasource.url', 'jdbc:mysql://source', 'application.properties');

    expect(updated).toContain('server.port=8080');
    expect(updated).toContain('spring.datasource.url=jdbc:mysql://source');
  });

  it('updates existing Properties key without changing unrelated lines', () => {
    const source = 'server.port=8080\nspring.datasource.url=jdbc:mysql://source\n';

    const updated = upsertKeyValue(source, 'spring.datasource.url', 'jdbc:mysql://target', 'application.properties');

    expect(updated).toContain('server.port=8080');
    expect(updated).toContain('spring.datasource.url=jdbc:mysql://target');
    expect(updated).not.toContain('jdbc:mysql://source');
  });

  it('finds and updates YAML object paths when the selected key points to a nested object', () => {
    const source = [
      'application:',
      '  storeService:',
      '    linkedinResume:',
      '      bucket: lnkd-resume',
      '      minSizeInBytes: 1024'
    ].join('\n');

    expect(findKeyValue(source, 'application.storeService', 'application', 'yaml')).toEqual({
      keyPath: 'application.storeService',
      value: {
        linkedinResume: {
          bucket: 'lnkd-resume',
          minSizeInBytes: 1024
        }
      }
    });

    const updated = upsertKeyValue(
      source,
      'application.storeService',
      {
        linkedinResume: {
          bucket: 'target-resume',
          minSizeInBytes: 2048
        }
      },
      'application',
      'yaml'
    );

    expect(updated).toContain('application:');
    expect(updated).toContain('storeService:');
    expect(updated).toContain('bucket: target-resume');
    expect(updated).toContain('minSizeInBytes: 2048');
  });
});
