import { defineStore } from 'pinia';
import type {
  AppSettings,
  AppTheme,
  ClusterRole,
  KeyScanResult,
  NacosConfigItem,
  NacosConnection,
  NacosNamespace,
  SyncMode,
  SyncSummary
} from '../../../main/types';

const publicNamespaceValue = '__public__';

interface ConnectionForm extends NacosConnection {
  namespaceId: string;
  namespaces: NacosNamespace[];
  connected: boolean;
  history: NacosConnection[];
  lastError: string;
}

interface LogRow {
  id: number;
  level: 'INFO' | 'WARN' | 'ERROR';
  message: string;
  createdAt: string;
}

interface NotificationState {
  id: number;
  type: 'success' | 'error' | 'warning';
  title: string;
  message: string;
}

interface TargetNamespaceDialogState {
  open: boolean;
  namespaceId: string;
  description: string;
}

let notificationTimer: ReturnType<typeof setTimeout> | undefined;

function createConnectionForm(): ConnectionForm {
  return {
    baseUrl: '',
    username: '',
    password: '',
    namespaceId: '',
    namespaces: [],
    connected: false,
    history: [],
    lastError: ''
  };
}

export const useAppStore = defineStore('app', {
  state: () => ({
    source: createConnectionForm(),
    target: createConnectionForm(),
    syncMode: 'namespace' as SyncMode,
    fileRows: [] as NacosConfigItem[],
    keyRows: [] as KeyScanResult[],
    selectedFileIds: [] as string[],
    selectedKeyIds: [] as string[],
    keyName: '',
    targetNamespaceDialog: {
      open: false,
      namespaceId: '',
      description: ''
    } as TargetNamespaceDialogState,
    busy: false,
    connectingRole: undefined as ClusterRole | undefined,
    logDirectory: undefined as string | undefined,
    theme: 'forest' as AppTheme,
    rendererPort: readCurrentRendererPort(),
    isPackaged: false,
    logs: [] as LogRow[],
    nextLogId: 1,
    notification: null as NotificationState | null,
    nextNotificationId: 1
  }),
  getters: {
    canSync(state): boolean {
      return Boolean(state.source.namespaceId && state.target.namespaceId && !state.busy);
    }
  },
  actions: {
    async initialize(): Promise<void> {
      try {
        const settings = await window.nacosSync!.loadSettings();
        const runtimeInfo = await window.nacosSync?.getRuntimeInfo?.();
        applyStoredConnection(this.source, settings.source, settings.sourceHistory);
        applyStoredConnection(this.target, settings.target, settings.targetHistory);
        this.source.namespaceId = settings.sourceNamespaceId ?? '';
        this.target.namespaceId = settings.targetNamespaceId ?? '';
        this.logDirectory = settings.logDirectory;
        this.theme = settings.theme ?? 'forest';
        this.rendererPort = runtimeInfo?.rendererPort ?? settings.rendererPort ?? readCurrentRendererPort();
        this.isPackaged = runtimeInfo?.packaged ?? false;
      } catch (error) {
        this.addLog('ERROR', `加载本地设置失败：${readErrorMessage(error)}`);
      }
    },
    addLog(level: LogRow['level'], message: string): void {
      this.logs.push({
        id: this.nextLogId,
        level,
        message,
        createdAt: new Date().toLocaleTimeString()
      });
      this.nextLogId += 1;
    },
    clearVisibleLogs(): void {
      this.logs = [];
    },
    showNotification(type: NotificationState['type'], title: string, message: string): void {
      if (notificationTimer) {
        clearTimeout(notificationTimer);
      }

      this.notification = {
        id: this.nextNotificationId,
        type,
        title,
        message
      };
      this.nextNotificationId += 1;
      notificationTimer = setTimeout(() => {
        this.dismissNotification();
      }, 3000);
    },
    dismissNotification(): void {
      if (notificationTimer) {
        clearTimeout(notificationTimer);
        notificationTimer = undefined;
      }
      this.notification = null;
    },
    async persistSettings(): Promise<void> {
      try {
        await window.nacosSync!.saveSettings(toAppSettings(this));
      } catch (error) {
        this.addLog('ERROR', `保存本地设置失败：${readErrorMessage(error)}`);
      }
    },
    async setTheme(theme: AppTheme): Promise<void> {
      this.theme = theme;
      await this.persistSettings();
    },
    async setRendererPort(port: number): Promise<void> {
      if (!Number.isInteger(port) || port < 1 || port > 65535) {
        this.addLog('WARN', `端口无效：${port}`);
        return;
      }

      if (port === this.rendererPort) {
        this.showNotification('warning', '端口未变化', `当前运行端口已经是 ${port}。`);
        return;
      }

      this.rendererPort = port;
      await this.persistSettings();
      const canRestart = await this.persistRendererPortForRestart(port);

      if (canRestart) {
        this.showNotification('warning', '端口已修改', `端口已保存为 ${port}，应用将自动重启。`);
        setTimeout(() => {
          void window.nacosSync?.restartApp?.();
        }, 3000);
        return;
      }

      this.showNotification('warning', '端口已保存', `端口已保存为 ${port}，请重启应用后生效。`);
    },
    async persistRendererPortForRestart(port: number): Promise<boolean> {
      try {
        await window.nacosSync?.saveRendererPort?.(port);
        return Boolean(window.nacosSync?.restartApp);
      } catch (error) {
        const message = readErrorMessage(error);
        if (message.includes('No handler registered')) {
          this.addLog('WARN', '当前客户端需要重启一次后才能启用自动切换端口。');
          return false;
        }

        this.addLog('ERROR', `保存端口失败：${message}`);
        return false;
      }
    },
    applyHistoryConnection(role: ClusterRole, baseUrl: string): void {
      const form = this[role];
      this.markConnectionDirty(role);
      const match = form.history.find((item) => item.baseUrl === baseUrl);
      if (!match) {
        return;
      }

      form.baseUrl = match.baseUrl;
      form.username = match.username;
      form.password = match.password;
      const roleName = role === 'source' ? '源端' : '目标端';
      this.addLog('INFO', `${roleName}已切换到历史地址：${match.baseUrl}，需要重新连接测试`);
      this.showNotification('warning', `${roleName}地址已切换`, '连接状态已重置，请重新连接测试。');
    },
    markConnectionDirty(role: ClusterRole): void {
      const form = this[role];
      form.connected = false;
      form.namespaces = [];
      form.namespaceId = '';
      form.lastError = '';

      if (role === 'source') {
        this.fileRows = [];
        this.keyRows = [];
        this.selectedFileIds = [];
        this.selectedKeyIds = [];
      }
    },
    async testConnection(role: ClusterRole): Promise<void> {
      const form = this[role];
      const roleName = role === 'source' ? '源端' : '目标端';
      const validationMessage = validateConnectionForm(form, roleName);

      if (validationMessage) {
        this.addLog('WARN', validationMessage);
        return;
      }

      this.busy = true;
      this.connectingRole = role;
      this.addLog('INFO', `开始连接${roleName} Nacos：${formatConnectionTarget(form)}`);

      try {
        form.namespaces = (await window.nacosSync!.listNamespaces(toConnection(form))) as NacosNamespace[];
        form.connected = true;
        form.lastError = '';
        if (!form.namespaceId && form.namespaces.length > 0) {
          form.namespaceId = toUiNamespaceId(form.namespaces[0].namespaceId);
        }
        form.history = saveConnectionHistory(form.history, toConnection(form));
        await this.persistSettings();
        this.addLog(
          'INFO',
          `${roleName} Nacos 连接成功：已加载 ${form.namespaces.length} 个 Namespace，当前选择 ${formatNamespaceLabel(form.namespaceId)}`
        );
      } catch (error) {
        const message = readErrorMessage(error);
        form.connected = false;
        form.namespaces = [];
        form.namespaceId = '';
        form.lastError = message;
        this.addLog('ERROR', `${roleName} Nacos 连接失败：${message}`);
        this.showNotification('error', `${roleName}连接失败`, message);
      } finally {
        this.busy = false;
        this.connectingRole = undefined;
      }
    },
    openTargetNamespaceDialog(): void {
      if (!this.target.connected) {
        this.addLog('WARN', '请先连接目标端 Nacos');
        return;
      }

      this.targetNamespaceDialog = {
        open: true,
        namespaceId: '',
        description: ''
      };
    },
    closeTargetNamespaceDialog(): void {
      if (this.busy) {
        return;
      }

      this.targetNamespaceDialog.open = false;
    },
    async createTargetNamespace(): Promise<void> {
      if (!this.target.connected) {
        this.addLog('WARN', '请先连接目标端 Nacos');
        return;
      }

      const namespaceId = this.targetNamespaceDialog.namespaceId.trim();
      const description = this.targetNamespaceDialog.description.trim();

      if (!namespaceId) {
        this.addLog('WARN', '请输入 Namespace ID');
        return;
      }

      if (this.target.namespaces.some((namespace) => namespace.namespaceId === namespaceId)) {
        this.addLog('WARN', `目标 Namespace 已存在：${namespaceId}`);
        return;
      }

      this.busy = true;
      try {
        this.addLog('INFO', `开始创建目标 Namespace：${namespaceId}${description ? `，描述 ${description}` : ''}`);
        await window.nacosSync!.createNamespace({
          connection: toConnection(this.target),
          namespaceId,
          namespaceName: namespaceId,
          description
        });
        this.target.namespaces = (await window.nacosSync!.listNamespaces(toConnection(this.target))) as NacosNamespace[];
        this.target.namespaceId = toUiNamespaceId(namespaceId);
        this.targetNamespaceDialog.open = false;
        await this.persistSettings();
        this.addLog('INFO', `目标 Namespace 创建成功：${namespaceId}，并已切换为当前目标 Namespace`);
      } catch (error) {
        this.addLog('ERROR', `目标 Namespace 创建失败：${readErrorMessage(error)}`);
      } finally {
        this.busy = false;
      }
    },
    async loadSourceFiles(): Promise<void> {
      if (!this.source.namespaceId) {
        this.addLog('WARN', '请先选择源端 Namespace');
        return;
      }

      this.busy = true;
      try {
        this.addLog('INFO', `开始加载源端配置：Namespace ${formatNamespaceLabel(this.source.namespaceId)}`);
        this.fileRows = await window.nacosSync!.listConfigs({
          connection: toConnection(this.source),
          namespaceId: toNacosNamespaceId(this.source.namespaceId)
        });
        this.selectedFileIds = [];
        this.addLog('INFO', `源端配置加载完成：${this.fileRows.length} 个文件，默认未选中任何条目`);
      } catch (error) {
        this.addLog('ERROR', `加载配置文件失败：${readErrorMessage(error)}`);
      } finally {
        this.busy = false;
      }
    },
    async scanKey(): Promise<void> {
      const keyName = this.keyName.trim();

      if (!keyName) {
        this.addLog('WARN', '请输入要扫描的 Key');
        return;
      }

      if (!this.source.namespaceId) {
        this.addLog('WARN', '请先连接源端 Nacos 并选择 Namespace');
        return;
      }

      this.busy = true;
      try {
        this.addLog('INFO', `开始扫描 Key：${keyName}，Namespace ${formatNamespaceLabel(this.source.namespaceId)}`);
        this.keyRows = await window.nacosSync!.scanKey({
          connection: toConnection(this.source),
          namespaceId: toNacosNamespaceId(this.source.namespaceId),
          keyName
        });
        this.selectedKeyIds = this.keyRows.map((row) => row.id);
        this.addLog('INFO', `Key 扫描完成：${this.keyRows.length} 条结果，已默认选中全部结果`);
      } catch (error) {
        this.keyRows = [];
        this.selectedKeyIds = [];
        this.addLog('ERROR', `Key 扫描失败：${readErrorMessage(error)}`);
      } finally {
        this.busy = false;
      }
    },
    async startSync(): Promise<void> {
      if (!this.source.namespaceId || !this.target.namespaceId) {
        const message = '请先选择源端 Namespace 和目标端 Namespace';
        this.addLog('WARN', message);
        this.showNotification('warning', '暂不能同步', message);
        return;
      }

      this.addLog(
        'INFO',
        `开始准备同步：模式 ${this.syncMode}，源 ${formatNamespaceLabel(this.source.namespaceId)} -> 目标 ${formatNamespaceLabel(this.target.namespaceId)}`
      );

      if (this.syncMode === 'namespace') {
        await this.syncNamespace();
        return;
      }

      if (this.syncMode === 'file') {
        await this.syncSelectedFiles();
        return;
      }

      await this.syncSelectedKeys();
    },
    async syncNamespace(): Promise<void> {
      this.busy = true;

      try {
        this.addLog('INFO', '开始扫描源端与目标端配置，用于执行 Namespace 全量同步');
        const sourceFiles = await window.nacosSync!.listConfigs({
          connection: toConnection(this.source),
          namespaceId: toNacosNamespaceId(this.source.namespaceId)
        });
        const targetFiles = await window.nacosSync!.listConfigs({
          connection: toConnection(this.target),
          namespaceId: toNacosNamespaceId(this.target.namespaceId)
        });
        const existingCount = countExistingConfigs(sourceFiles, targetFiles);
        this.addLog(
          'INFO',
          `同步预检查完成：源端 ${sourceFiles.length} 个文件，目标端 ${targetFiles.length} 个文件，重名 ${existingCount} 个`
        );

        if (!confirm(`确认同步整个源端 Namespace？本次将扫描并同步 ${sourceFiles.length} 个配置文件。`)) {
          const message = '已取消 Namespace 同步：用户未通过第一次确认';
          this.addLog('WARN', message);
          this.showNotification('warning', '已取消同步', message);
          return;
        }

        const overwriteExistingFiles =
          existingCount > 0
            ? confirm(`目标端 Namespace 已存在 ${existingCount} 个同名 DataId + Group，是否覆盖这些已有配置？`)
            : false;

        this.addLog(
          'INFO',
          `Namespace 同步开始执行：覆盖已有文件 ${overwriteExistingFiles ? '是' : '否'}`
        );

        const summary = (await window.nacosSync!.syncNamespace({
          sourceConnection: toConnection(this.source),
          targetConnection: toConnection(this.target),
          sourceNamespaceId: toNacosNamespaceId(this.source.namespaceId),
          targetNamespaceId: toNacosNamespaceId(this.target.namespaceId),
          confirmNamespaceStart: true,
          overwriteExistingFiles
        })) as SyncSummary;

        const message = formatSummary('Namespace 同步完成', summary);
        this.addLog('INFO', message);
        this.showSyncNotification('Namespace 同步完成', summary, message);
      } catch (error) {
        const message = readErrorMessage(error);
        this.addLog('ERROR', `Namespace 同步失败：${message}`);
        this.showNotification('error', 'Namespace 同步失败', message);
      } finally {
        this.busy = false;
      }
    },
    async syncSelectedFiles(): Promise<void> {
      if (this.fileRows.length === 0) {
        await this.loadSourceFiles();
      }

      const files = this.fileRows
        .filter((row) => this.selectedFileIds.includes(fileRowId(row)))
        .map(toSyncConfigItem);
      if (files.length === 0) {
        const message = '请先选择要同步的配置文件';
        this.addLog('WARN', message);
        this.showNotification('warning', '暂不能同步', message);
        return;
      }

      const overwriteExistingFiles = confirm('如果目标端已存在同名配置文件，是否覆盖？选择取消则跳过已有文件。');
      this.busy = true;

      try {
        this.addLog(
          'INFO',
          `开始文件同步：选中 ${files.length} 个文件，目标 Namespace ${formatNamespaceLabel(this.target.namespaceId)}，覆盖已有文件 ${overwriteExistingFiles ? '是' : '否'}，文件 ${formatConfigPreview(files)}`
        );
        const summary = (await window.nacosSync!.syncFiles({
          targetConnection: toConnection(this.target),
          targetNamespaceId: toNacosNamespaceId(this.target.namespaceId),
          files,
          overwriteExistingFiles
        })) as SyncSummary;

        const message = formatSummary('文件同步完成', summary);
        this.addLog('INFO', message);
        this.showSyncNotification('文件同步完成', summary, message);
      } catch (error) {
        const message = readErrorMessage(error);
        this.addLog('ERROR', `文件同步失败：${message}`);
        this.showNotification('error', '文件同步失败', message);
      } finally {
        this.busy = false;
      }
    },
    async syncSelectedKeys(): Promise<void> {
      const results = this.keyRows
        .filter((row) => this.selectedKeyIds.includes(row.id))
        .map(toSyncKeyResult);

      if (results.length === 0) {
        const message = '请先扫描并选择要同步的 Key';
        this.addLog('WARN', message);
        this.showNotification('warning', '暂不能同步', message);
        return;
      }

      const overwriteExistingKeys = confirm('如果目标文件已存在当前 Key，是否覆盖该 Key 值？选择取消则跳过已有 Key。');
      const overwriteExistingFiles = results.some((row) => row.syncStrategy === 'fullFile')
        ? confirm('选中的结果包含完整文件同步。如果目标端已存在同名文件，是否覆盖整个文件？')
        : false;

      this.busy = true;
      try {
        const fullFileCount = results.filter((row) => row.syncStrategy === 'fullFile').length;
        this.addLog(
          'INFO',
          `开始 Key 同步：选中 ${results.length} 条结果，其中完整文件 ${fullFileCount} 条，仅 Key ${results.length - fullFileCount} 条，覆盖 Key ${overwriteExistingKeys ? '是' : '否'}，覆盖文件 ${overwriteExistingFiles ? '是' : '否'}，对象 ${formatKeyPreview(results)}`
        );
        const summary = (await window.nacosSync!.syncKeyResults({
          sourceConnection: toConnection(this.source),
          targetConnection: toConnection(this.target),
          sourceNamespaceId: toNacosNamespaceId(this.source.namespaceId),
          targetNamespaceId: toNacosNamespaceId(this.target.namespaceId),
          results,
          overwriteExistingFiles,
          overwriteExistingKeys
        })) as SyncSummary;

        const message = formatSummary('Key 同步完成', summary);
        this.addLog('INFO', message);
        this.showSyncNotification('Key 同步完成', summary, message);
      } catch (error) {
        const message = readErrorMessage(error);
        this.addLog('ERROR', `Key 同步失败：${message}`);
        this.showNotification('error', 'Key 同步失败', message);
      } finally {
        this.busy = false;
      }
    },
    async openLogDirectory(): Promise<void> {
      try {
        await window.nacosSync!.openLogDirectory();
      } catch (error) {
        this.addLog('ERROR', `打开日志目录失败：${readErrorMessage(error)}`);
      }
    },
    async chooseLogDirectory(): Promise<void> {
      try {
        const directory = await window.nacosSync!.chooseLogDirectory();
        if (directory) {
          this.logDirectory = directory;
          await this.persistSettings();
          this.addLog('INFO', `日志目录已切换：${directory}`);
        }
      } catch (error) {
        this.addLog('ERROR', `选择日志目录失败：${readErrorMessage(error)}`);
      }
    },
    showSyncNotification(title: string, summary: SyncSummary, message: string): void {
      if (summary.failed > 0) {
        this.showNotification('warning', `${title}，存在失败`, message);
        return;
      }

      this.showNotification('success', title, message);
    }
  }
});

function toConnection(form: ConnectionForm): NacosConnection {
  return {
    baseUrl: form.baseUrl,
    username: form.username,
    password: form.password
  };
}

function toAppSettings(state: {
  source: ConnectionForm;
  target: ConnectionForm;
  logDirectory?: string;
  theme: AppTheme;
  rendererPort: number;
}): AppSettings {
  return {
    source: toConnection(state.source),
    target: toConnection(state.target),
    sourceHistory: state.source.history.map(toStoredConnection),
    targetHistory: state.target.history.map(toStoredConnection),
    sourceNamespaceId: state.source.namespaceId,
    targetNamespaceId: state.target.namespaceId,
    logDirectory: state.logDirectory,
    theme: state.theme,
    rendererPort: state.rendererPort
  };
}

function readCurrentRendererPort(): number {
  if (typeof window === 'undefined') {
    return 5173;
  }

  const port = Number((window.location as Location | undefined)?.port);
  return Number.isInteger(port) && port > 0 ? port : 5173;
}

function toStoredConnection(connection: NacosConnection): NacosConnection {
  return {
    baseUrl: connection.baseUrl,
    username: connection.username,
    password: connection.password
  };
}

function toSyncConfigItem(item: NacosConfigItem): NacosConfigItem {
  return {
    dataId: item.dataId,
    group: item.group,
    content: item.content,
    type: item.type
  };
}

function toSyncKeyResult(result: KeyScanResult): KeyScanResult {
  return {
    id: result.id,
    dataId: result.dataId,
    group: result.group,
    keyPath: result.keyPath,
    value: toPlainValue(result.value),
    type: result.type,
    syncStrategy: result.syncStrategy
  };
}

function toPlainValue(value: unknown): unknown {
  if (value === undefined || value === null || typeof value !== 'object') {
    return value;
  }

  return JSON.parse(JSON.stringify(value));
}

function validateConnectionForm(form: ConnectionForm, roleName: string): string | null {
  if (!form.baseUrl.trim()) {
    return `请先填写${roleName} Nacos 地址`;
  }

  try {
    const url = new URL(form.baseUrl.trim());
    if (!['http:', 'https:'].includes(url.protocol)) {
      return `${roleName} Nacos 地址只支持 http 或 https`;
    }
  } catch {
    return `${roleName} Nacos 地址格式不正确，请填写类似 http://127.0.0.1:8848`;
  }

  return null;
}

function readErrorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

function toUiNamespaceId(namespaceId: string): string {
  return namespaceId || publicNamespaceValue;
}

function toNacosNamespaceId(namespaceId: string): string {
  return namespaceId === publicNamespaceValue ? '' : namespaceId;
}

function fileRowId(row: Pick<NacosConfigItem, 'dataId' | 'group'>): string {
  return `${row.group}:${row.dataId}`;
}

function countExistingConfigs(sourceFiles: NacosConfigItem[], targetFiles: NacosConfigItem[]): number {
  const targetIds = new Set(targetFiles.map(fileRowId));
  return sourceFiles.filter((file) => targetIds.has(fileRowId(file))).length;
}

function formatSummary(title: string, summary: SyncSummary): string {
  return `${title}：创建 ${summary.created}，更新 ${summary.updated}，跳过 ${summary.skipped}，失败 ${summary.failed}`;
}

function formatConfigPreview(files: NacosConfigItem[]): string {
  return files
    .slice(0, 3)
    .map((file) => `${file.group}/${file.dataId}`)
    .join('，') + (files.length > 3 ? ` 等 ${files.length} 个` : '');
}

function formatKeyPreview(results: KeyScanResult[]): string {
  return results
    .slice(0, 3)
    .map((result) => `${result.group}/${result.dataId}#${result.keyPath}(${result.syncStrategy === 'keyOnly' ? '仅Key' : '整文件'})`)
    .join('，') + (results.length > 3 ? ` 等 ${results.length} 条` : '');
}

function formatConnectionTarget(form: Pick<ConnectionForm, 'baseUrl' | 'username'>): string {
  const username = form.username.trim() || '-';
  return `${form.baseUrl.trim()}，账号 ${username}`;
}

function formatNamespaceLabel(namespaceId: string): string {
  return namespaceId === publicNamespaceValue ? 'public' : namespaceId;
}

function applyStoredConnection(
  form: ConnectionForm,
  connection?: NacosConnection,
  history: NacosConnection[] = []
): void {
  form.baseUrl = connection?.baseUrl ?? '';
  form.username = connection?.username ?? '';
  form.password = connection?.password ?? '';
  form.history = history;
}

function saveConnectionHistory(history: NacosConnection[], connection: NacosConnection): NacosConnection[] {
  const next = [connection, ...history.filter((item) => item.baseUrl !== connection.baseUrl)];
  return next.slice(0, 8);
}
