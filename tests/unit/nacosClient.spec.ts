import { describe, expect, it } from 'vitest';
import { NacosClient, type HttpClient } from '../../src/main/services/nacosClient';
import type { NacosConnection } from '../../src/main/types';

class FakeHttpClient implements HttpClient {
  requests: Array<{
    method: string;
    url: string;
    params?: Record<string, unknown>;
    data?: unknown;
    headers?: Record<string, string>;
  }> = [];
  private queue: Array<{ data?: unknown; error?: unknown }> = [];

  enqueueData(data: unknown): void {
    this.queue.push({ data });
  }

  enqueueError(error: unknown): void {
    this.queue.push({ error });
  }

  async request<T>(request: {
    method: 'GET' | 'POST';
    url: string;
    params?: Record<string, unknown>;
    data?: unknown;
    headers?: Record<string, string>;
  }) {
    this.requests.push(request);
    const next = this.queue.shift();

    if (!next) {
      throw new Error(`No fake response queued for ${request.method} ${request.url}`);
    }

    if (next.error) {
      throw next.error;
    }

    return { data: next.data as T };
  }
}

const connection: NacosConnection = {
  baseUrl: 'http://nacos.example.com:8848',
  username: 'nacos',
  password: 'password'
};

describe('NacosClient', () => {
  it('logs in and attaches access token to later requests', async () => {
    const http = new FakeHttpClient();
    http.enqueueError({ response: { status: 403 } });
    http.enqueueData({ accessToken: 'token-123' });
    http.enqueueData({ data: [] });

    const client = new NacosClient(connection, http);

    await expect(client.listNamespaces()).resolves.toEqual([
      {
        namespaceId: '',
        namespaceName: 'public',
        description: 'public'
      }
    ]);
    expect(http.requests[0]).toMatchObject({
      method: 'GET',
      url: '/nacos/v1/console/namespaces'
    });
    expect(http.requests[1]).toMatchObject({
      method: 'POST',
      url: '/nacos/v1/auth/login',
      data: {
        username: 'nacos',
        password: 'password'
      }
    });
    expect(http.requests[2].params).toMatchObject({ accessToken: 'token-123' });
    expect(http.requests[2].headers).toMatchObject({ Authorization: 'Bearer token-123' });
  });

  it('normalizes namespace list response wrapped in data', async () => {
    const http = new FakeHttpClient();
    http.enqueueData({
      data: [{ namespace: 'dev', namespaceShowName: 'Development', namespaceDesc: 'dev namespace' }]
    });

    const client = new NacosClient(connection, http);

    await expect(client.listNamespaces()).resolves.toEqual([
      {
        namespaceId: 'dev',
        namespaceName: 'Development',
        description: 'dev namespace'
      }
    ]);
  });

  it('normalizes namespace list response returned as an array', async () => {
    const http = new FakeHttpClient();
    http.enqueueData([{ namespace: 'prod', namespaceShowName: 'Production' }]);

    const client = new NacosClient(connection, http);

    await expect(client.listNamespaces()).resolves.toEqual([
      {
        namespaceId: 'prod',
        namespaceName: 'Production',
        description: undefined
      }
    ]);
  });

  it('falls back to public namespace when Nacos 2.2.x console namespace API returns 500 but config API works', async () => {
    const http = new FakeHttpClient();
    http.enqueueError({ response: { status: 403 } });
    http.enqueueData({ accessToken: 'token-123' });
    http.enqueueError({ response: { status: 500, data: { message: 'namespace api failed' } } });
    http.enqueueData({ totalCount: 0, pageItems: [] });

    const client = new NacosClient(connection, http);

    await expect(client.listNamespaces()).resolves.toEqual([
      {
        namespaceId: '',
        namespaceName: 'public',
        description: 'public'
      }
    ]);
    expect(http.requests[2]).toMatchObject({
      method: 'GET',
      url: '/nacos/v1/console/namespaces',
      params: { accessToken: 'token-123' },
      headers: { Authorization: 'Bearer token-123' }
    });
    expect(http.requests[3]).toMatchObject({
      method: 'GET',
      url: '/nacos/v1/cs/configs',
      params: expect.objectContaining({
        tenant: '',
        accessToken: 'token-123'
      }),
      headers: { Authorization: 'Bearer token-123' }
    });
  });

  it('loads paged config list until total count is reached', async () => {
    const http = new FakeHttpClient();
    http.enqueueData({ accessToken: 'token-123' });
    http.enqueueData({
      totalCount: 2,
      pageItems: [{ dataId: 'a.yml', group: 'DEFAULT_GROUP', content: 'a: 1', type: 'yaml' }]
    });
    http.enqueueData({
      totalCount: 2,
      pageItems: [{ dataId: 'b.yml', group: 'DEFAULT_GROUP', content: 'b: 2', type: 'yaml' }]
    });

    const client = new NacosClient(connection, http);

    await expect(client.listConfigs('dev', 1)).resolves.toHaveLength(2);
    expect(http.requests[1].params).toMatchObject({ pageNo: 1, pageSize: 1, tenant: 'dev' });
    expect(http.requests[2].params).toMatchObject({ pageNo: 2, pageSize: 1, tenant: 'dev' });
  });

  it('infers config type from DataId when list response omits type', async () => {
    const http = new FakeHttpClient();
    http.enqueueData({ accessToken: 'token-123' });
    http.enqueueData({
      totalCount: 1,
      pageItems: [{ dataId: 'application.yaml', group: 'DEFAULT_GROUP', content: 'server:\n  port: 8080' }]
    });

    const client = new NacosClient(connection, http);

    await expect(client.listConfigs('dev')).resolves.toEqual([
      {
        dataId: 'application.yaml',
        group: 'DEFAULT_GROUP',
        content: 'server:\n  port: 8080',
        type: 'yaml'
      }
    ]);
  });

  it('returns null when config is missing', async () => {
    const http = new FakeHttpClient();
    http.enqueueData({ accessToken: 'token-123' });
    http.enqueueError({ response: { status: 404 } });

    const client = new NacosClient(connection, http);

    await expect(client.getConfig('dev', 'missing.yml', 'DEFAULT_GROUP')).resolves.toBeNull();
  });

  it('publishes config to the target namespace', async () => {
    const http = new FakeHttpClient();
    http.enqueueData({ accessToken: 'token-123' });
    http.enqueueData(true);

    const client = new NacosClient(connection, http);

    await client.publishConfig('target-dev', {
      dataId: 'demo.yml',
      group: 'DEFAULT_GROUP',
      content: 'server:\n  port: 8080',
      type: 'yaml'
    });

    expect(http.requests[1]).toMatchObject({
      method: 'POST',
      url: '/nacos/v1/cs/configs',
      data: {
        tenant: 'target-dev',
        dataId: 'demo.yml',
        group: 'DEFAULT_GROUP',
        content: 'server:\n  port: 8080',
        type: 'yaml',
        accessToken: 'token-123'
      }
    });
  });

  it('publishes inferred yaml type when source item has no type', async () => {
    const http = new FakeHttpClient();
    http.enqueueData({ accessToken: 'token-123' });
    http.enqueueData(true);

    const client = new NacosClient(connection, http);

    await client.publishConfig('target-dev', {
      dataId: 'application.yml',
      group: 'DEFAULT_GROUP',
      content: 'spring:\n  application:\n    name: demo'
    });

    expect(http.requests[1]).toMatchObject({
      data: {
        dataId: 'application.yml',
        type: 'yaml'
      }
    });
  });
});
