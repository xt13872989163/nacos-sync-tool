import { createServer, type IncomingMessage, type Server, type ServerResponse } from 'node:http';
import { NacosClient } from './nacosClient';
import type {
  CreateNamespaceInput,
  KeyScanResult,
  ListConfigsInput,
  NacosConnection,
  ScanKeyInput,
  SyncFilesInput,
  SyncKeyResultsInput,
  SyncNamespaceInput
} from '../types';
import { findKeyValue } from './configParser';
import { SyncService } from './syncService';
import type { ConfirmationRequest } from './confirmPolicy';

export const devBridgePort = 37621;

export function startDevHttpBridge(): Server {
  const server = createServer(async (request, response) => {
    response.setHeader('Access-Control-Allow-Origin', '*');
    response.setHeader('Access-Control-Allow-Headers', 'content-type');
    response.setHeader('Access-Control-Allow-Methods', 'GET,POST,OPTIONS');

    if (request.method === 'OPTIONS') {
      response.writeHead(204);
      response.end();
      return;
    }

    try {
      if (request.method === 'GET' && request.url === '/health') {
        sendJson(response, 200, { ok: true });
        return;
      }

      if (request.method === 'POST' && request.url === '/nacos/test-connection') {
        const connection = await readJson<NacosConnection>(request);
        const client = new NacosClient(connection);
        sendJson(response, 200, await client.testConnection());
        return;
      }

      if (request.method === 'POST' && request.url === '/nacos/list-namespaces') {
        const connection = await readJson<NacosConnection>(request);
        const client = new NacosClient(connection);
        sendJson(response, 200, await client.listNamespaces());
        return;
      }

      if (request.method === 'POST' && request.url === '/nacos/create-namespace') {
        const input = await readJson<CreateNamespaceInput>(request);
        const client = new NacosClient(input.connection);
        await client.createNamespace(input.namespaceId, input.namespaceName, input.description);
        sendJson(response, 200, null);
        return;
      }

      if (request.method === 'POST' && request.url === '/nacos/list-configs') {
        const input = await readJson<ListConfigsInput>(request);
        const client = new NacosClient(input.connection);
        sendJson(response, 200, await client.listConfigs(input.namespaceId));
        return;
      }

      if (request.method === 'POST' && request.url === '/sync/scan-key') {
        const input = await readJson<ScanKeyInput>(request);
        const client = new NacosClient(input.connection);
        const configs = await client.listConfigs(input.namespaceId);
        const results = configs.flatMap((config): KeyScanResult[] => {
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
        sendJson(response, 200, results);
        return;
      }

      if (request.method === 'POST' && request.url === '/sync/namespace') {
        const input = await readJson<SyncNamespaceInput>(request);
        const service = createSyncService(input.sourceConnection, input.targetConnection, async (confirmation) =>
          resolveConfirmDecision(confirmation, {
            confirmNamespaceStart: input.confirmNamespaceStart,
            overwriteExistingFiles: input.overwriteExistingFiles,
            overwriteExistingKeys: false
          })
        );
        sendJson(
          response,
          200,
          await service.syncNamespace({
            sourceNamespaceId: input.sourceNamespaceId,
            targetNamespaceId: input.targetNamespaceId
          })
        );
        return;
      }

      if (request.method === 'POST' && request.url === '/sync/files') {
        const input = await readJson<SyncFilesInput>(request);
        const targetClient = new NacosClient(input.targetConnection);
        const service = new SyncService({
          sourceClient: targetClient,
          targetClient,
          confirm: async (confirmation) =>
            resolveConfirmDecision(confirmation, {
              confirmNamespaceStart: true,
              overwriteExistingFiles: input.overwriteExistingFiles,
              overwriteExistingKeys: false
            })
        });
        sendJson(
          response,
          200,
          await service.syncFiles({
            targetNamespaceId: input.targetNamespaceId,
            files: input.files
          })
        );
        return;
      }

      if (request.method === 'POST' && request.url === '/sync/key-results') {
        const input = await readJson<SyncKeyResultsInput>(request);
        const service = createSyncService(input.sourceConnection, input.targetConnection, async (confirmation) =>
          resolveConfirmDecision(confirmation, {
            confirmNamespaceStart: true,
            overwriteExistingFiles: input.overwriteExistingFiles,
            overwriteExistingKeys: input.overwriteExistingKeys
          })
        );
        sendJson(
          response,
          200,
          await service.syncKeyResults({
            sourceNamespaceId: input.sourceNamespaceId,
            targetNamespaceId: input.targetNamespaceId,
            results: input.results
          })
        );
        return;
      }

      sendJson(response, 404, { message: 'Not found' });
    } catch (error) {
      sendJson(response, 500, { message: readErrorMessage(error) });
    }
  });

  server.listen(devBridgePort, '127.0.0.1');
  return server;
}

async function readJson<T>(request: IncomingMessage): Promise<T> {
  const chunks: Buffer[] = [];

  for await (const chunk of request) {
    chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
  }

  return JSON.parse(Buffer.concat(chunks).toString('utf8')) as T;
}

function sendJson(response: ServerResponse, statusCode: number, body: unknown): void {
  response.writeHead(statusCode, { 'content-type': 'application/json; charset=utf-8' });
  response.end(JSON.stringify(body));
}

function readErrorMessage(error: unknown): string {
  if (error && typeof error === 'object' && 'response' in error) {
    const axiosError = error as {
      config?: { method?: string; url?: string; baseURL?: string };
      response?: { status?: number; data?: unknown };
      message?: string;
    };
    const method = axiosError.config?.method?.toUpperCase() ?? 'HTTP';
    const url = `${axiosError.config?.baseURL ?? ''}${axiosError.config?.url ?? ''}`;
    const status = axiosError.response?.status ?? 'unknown';
    const data = formatResponseData(axiosError.response?.data);

    return `${method} ${url} failed with status ${status}${data ? `: ${data}` : ''}`;
  }

  return error instanceof Error ? error.message : String(error);
}

function formatResponseData(data: unknown): string {
  if (!data) {
    return '';
  }

  if (typeof data === 'string') {
    return data.slice(0, 500);
  }

  return JSON.stringify(data).slice(0, 500);
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
