import axios, { type AxiosInstance } from 'axios';
import type { NacosConfigItem, NacosConnection, NacosNamespace } from '../types';
import { normalizeNacosConfigType } from './configType';

type HttpMethod = 'GET' | 'POST';

interface HttpRequest {
  method: HttpMethod;
  url: string;
  params?: Record<string, unknown>;
  data?: unknown;
  headers?: Record<string, string>;
}

export interface HttpClient {
  request<T>(request: HttpRequest): Promise<{ data: T }>;
}

interface LoginResponse {
  accessToken?: string;
  token?: string;
}

interface ConfigListResponse {
  pageItems?: Array<Partial<NacosConfigItem>>;
  totalCount?: number;
}

export class NacosClient {
  private accessToken: string | undefined;

  constructor(
    private readonly connection: NacosConnection,
    private readonly http: HttpClient = createAxiosHttpClient(connection.baseUrl)
  ) {}

  async authenticate(): Promise<void> {
    const response = await this.http.request<LoginResponse>({
      method: 'POST',
      url: '/nacos/v1/auth/login',
      data: {
        username: this.connection.username,
        password: this.connection.password
      }
    });

    this.accessToken = response.data.accessToken ?? response.data.token;
  }

  async testConnection(): Promise<boolean> {
    await this.listNamespaces();
    return true;
  }

  async listNamespaces(): Promise<NacosNamespace[]> {
    try {
      const response = await this.http.request<unknown>({
        method: 'GET',
        url: '/nacos/v1/console/namespaces'
      });

      const namespaces = normalizeNamespaces(response.data);
      return namespaces.length > 0 ? namespaces : [publicNamespace()];
    } catch (error) {
      if (!shouldRetryNamespaceWithAuthentication(error)) {
        throw error;
      }
    }

    await this.ensureAuthenticated();

    try {
      const response = await this.http.request<unknown>({
        method: 'GET',
        url: '/nacos/v1/console/namespaces',
        params: this.authParams(),
        headers: this.authHeaders()
      });

      const namespaces = normalizeNamespaces(response.data);
      return namespaces.length > 0 ? namespaces : [publicNamespace()];
    } catch (error) {
      if (!isHttpStatus(error, 500)) {
        throw error;
      }

      // Some Nacos 2.2.x deployments return 500 from the console namespace API
      // while config OpenAPI calls still work with the same access token.
      await this.probePublicNamespace();
      return [publicNamespace()];
    }
  }

  async createNamespace(namespaceId: string, namespaceName = namespaceId, description = ''): Promise<void> {
    await this.ensureAuthenticated();

    await this.http.request({
      method: 'POST',
      url: '/nacos/v1/console/namespaces',
      data: {
        customNamespaceId: namespaceId,
        namespaceName,
        namespaceDesc: description,
        ...this.authParams()
      },
      headers: this.authHeaders()
    });
  }

  async listConfigs(namespaceId: string, pageSize = 100): Promise<NacosConfigItem[]> {
    await this.ensureAuthenticated();

    const items: NacosConfigItem[] = [];
    let pageNo = 1;
    let totalCount = Number.POSITIVE_INFINITY;

    while (items.length < totalCount) {
      const response = await this.http.request<ConfigListResponse>({
        method: 'GET',
        url: '/nacos/v1/cs/configs',
        params: {
          search: 'blur',
          dataId: '',
          group: '',
          appName: '',
          config_tags: '',
          pageNo,
          pageSize,
          tenant: namespaceId,
          ...this.authParams()
        },
        headers: this.authHeaders()
      });

      const pageItems = response.data.pageItems ?? [];
      totalCount = response.data.totalCount ?? pageItems.length;
      items.push(...pageItems.map(normalizeConfigItem));

      if (pageItems.length === 0 || items.length >= totalCount) {
        break;
      }

      pageNo += 1;
    }

    return Promise.all(
      items.map(async (item) => ({
        ...item,
        content: item.content || (await this.getConfig(namespaceId, item.dataId, item.group)) || ''
      }))
    );
  }

  async getConfig(namespaceId: string, dataId: string, group: string): Promise<string | null> {
    await this.ensureAuthenticated();

    try {
      const response = await this.http.request<string>({
        method: 'GET',
        url: '/nacos/v1/cs/configs',
        params: {
          tenant: namespaceId,
          dataId,
          group,
          ...this.authParams()
        },
        headers: this.authHeaders()
      });

      return response.data;
    } catch (error) {
      if (isHttpStatus(error, 404)) {
        return null;
      }

      throw error;
    }
  }

  async publishConfig(namespaceId: string, item: NacosConfigItem): Promise<void> {
    await this.ensureAuthenticated();

    await this.http.request({
      method: 'POST',
      url: '/nacos/v1/cs/configs',
      data: {
        tenant: namespaceId,
        dataId: item.dataId,
        group: item.group,
        content: item.content,
        type: normalizeNacosConfigType(item.type, item.dataId),
        ...this.authParams()
      },
      headers: this.authHeaders()
    });
  }

  private async probePublicNamespace(): Promise<void> {
    await this.http.request<ConfigListResponse>({
      method: 'GET',
      url: '/nacos/v1/cs/configs',
      params: {
        search: 'blur',
        dataId: '',
        group: '',
        appName: '',
        config_tags: '',
        pageNo: 1,
        pageSize: 1,
        tenant: '',
        ...this.authParams()
      },
      headers: this.authHeaders()
    });
  }

  private async ensureAuthenticated(): Promise<void> {
    if (!this.accessToken) {
      await this.authenticate();
    }
  }

  private authParams(): Record<string, string> {
    return this.accessToken ? { accessToken: this.accessToken } : {};
  }

  private authHeaders(): Record<string, string> | undefined {
    return this.accessToken ? { Authorization: `Bearer ${this.accessToken}` } : undefined;
  }
}

export function createAxiosHttpClient(baseUrl: string): HttpClient {
  const axiosInstance: AxiosInstance = axios.create({
    baseURL: baseUrl.replace(/\/$/, ''),
    timeout: 15000
  });

  return {
    request: (request) =>
      axiosInstance.request({
        ...request,
        data: request.method === 'POST' ? serializeNacosFormData(request.data) : request.data,
        headers: {
          ...(request.method === 'POST' ? { 'content-type': 'application/x-www-form-urlencoded' } : {}),
          ...request.headers
        }
      })
  };
}

export function serializeNacosFormData(data: unknown): unknown {
  if (!isPlainRecord(data)) {
    return data;
  }

  const formData = new URLSearchParams();

  for (const [key, value] of Object.entries(data)) {
    if (value !== undefined && value !== null) {
      formData.set(key, String(value));
    }
  }

  return formData;
}

function normalizeNamespaces(raw: unknown): NacosNamespace[] {
  const data = Array.isArray(raw) ? raw : readObjectArray(raw, 'data');

  return data.map((item) => {
    const namespaceId = readString(item, 'namespace') ?? readString(item, 'namespaceId') ?? readString(item, 'tenant') ?? '';
    const namespaceName =
      readString(item, 'namespaceShowName') ?? readString(item, 'namespaceName') ?? readString(item, 'name') ?? namespaceId;

    return {
      namespaceId,
      namespaceName,
      description: readString(item, 'namespaceDesc') ?? readString(item, 'description')
    };
  });
}

function publicNamespace(): NacosNamespace {
  return {
    namespaceId: '',
    namespaceName: 'public',
    description: 'public'
  };
}

function shouldRetryNamespaceWithAuthentication(error: unknown): boolean {
  return isHttpStatus(error, 401) || isHttpStatus(error, 403);
}

function normalizeConfigItem(item: Partial<NacosConfigItem>): NacosConfigItem {
  return {
    dataId: item.dataId ?? '',
    group: item.group ?? 'DEFAULT_GROUP',
    content: item.content ?? '',
    type: normalizeNacosConfigType(item.type, item.dataId ?? '')
  };
}

function readObjectArray(raw: unknown, key: string): Record<string, unknown>[] {
  if (raw && typeof raw === 'object' && key in raw) {
    const value = (raw as Record<string, unknown>)[key];
    return Array.isArray(value) ? (value as Record<string, unknown>[]) : [];
  }

  return [];
}

function readString(raw: unknown, key: string): string | undefined {
  if (raw && typeof raw === 'object' && key in raw) {
    const value = (raw as Record<string, unknown>)[key];
    return typeof value === 'string' ? value : undefined;
  }

  return undefined;
}

function isHttpStatus(error: unknown, status: number): boolean {
  return Boolean(
    error &&
      typeof error === 'object' &&
      'response' in error &&
      (error as { response?: { status?: number } }).response?.status === status
  );
}

function isPlainRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value && typeof value === 'object' && !Array.isArray(value) && !(value instanceof URLSearchParams));
}
