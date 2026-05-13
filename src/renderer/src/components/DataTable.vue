<script setup lang="ts">
import { computed, ref } from 'vue';
import SyncModeSelector from './SyncModeSelector.vue';
import { useAppStore } from '../stores/appStore';

const store = useAppStore();
const cellTooltip = ref<{
  text: string;
  top: number;
  left: number;
} | null>(null);

const tableTitle = computed(() => {
  if (store.syncMode === 'key') {
    return 'Key 扫描结果';
  }

  if (store.syncMode === 'file') {
    return '配置文件列表';
  }

  return 'Namespace 同步';
});

const tableDescription = computed(() => {
  if (store.syncMode === 'key') {
    return '输入 Key，扫描并选择同步结果。';
  }

  if (store.syncMode === 'file') {
    return '加载源端配置后，勾选要同步的文件。';
  }

  return '当前模式会同步整个源端 Namespace。';
});

const selectedCount = computed(() => {
  if (store.syncMode === 'file') {
    return store.selectedFileIds.length;
  }

  if (store.syncMode === 'key') {
    return store.selectedKeyIds.length;
  }

  return 0;
});

const selectionLabel = computed(() => {
  if (store.syncMode === 'file') {
    return `已选 ${store.selectedFileIds.length}/${store.fileRows.length} 个文件`;
  }

  if (store.syncMode === 'key') {
    return `已选 ${store.selectedKeyIds.length}/${store.keyRows.length} 个 Key`;
  }

  return 'Namespace 同步';
});

const readinessText = computed(() => {
  if (store.busy) {
    return '处理中';
  }

  if (!store.canSync) {
    return '待连接';
  }

  if (store.syncMode === 'file' && store.selectedFileIds.length === 0) {
    return '待选文件';
  }

  if (store.syncMode === 'key' && store.selectedKeyIds.length === 0) {
    return '待选 Key';
  }

  return '可同步';
});

const modeLabel = computed(() => {
  if (store.syncMode === 'namespace') {
    return 'Namespace';
  }

  return store.syncMode === 'file' ? '文件' : 'Key';
});

const sourceNamespaceLabel = computed(() => formatNamespaceLabel(store.source.namespaceId));
const targetNamespaceLabel = computed(() => formatNamespaceLabel(store.target.namespaceId));
const keyOnlyCount = computed(() => store.keyRows.filter((row) => row.syncStrategy === 'keyOnly').length);
const fullFileCount = computed(() => store.keyRows.filter((row) => row.syncStrategy === 'fullFile').length);
const syncActionText = computed(() => (store.busy ? '同步中...' : '开始同步'));
const canStartSync = computed(() => {
  if (!store.canSync) {
    return false;
  }

  if (store.syncMode === 'file') {
    return store.selectedFileIds.length > 0;
  }

  if (store.syncMode === 'key') {
    return store.selectedKeyIds.length > 0;
  }

  return true;
});
const syncHintText = computed(() => {
  if (!store.source.namespaceId || !store.target.namespaceId) {
    return '请先连接源端和目标端，并选择 Namespace';
  }

  if (store.syncMode === 'file' && store.selectedFileIds.length === 0) {
    return '加载并勾选文件后再同步';
  }

  if (store.syncMode === 'key' && store.selectedKeyIds.length === 0) {
    return '扫描并勾选 Key 后再同步';
  }

  return '同步前会再次确认覆盖策略';
});

function fileRowId(dataId: string, group: string): string {
  return `${group}:${dataId}`;
}

function formatNamespaceLabel(namespaceId: string): string {
  if (!namespaceId) {
    return '未选择';
  }

  return namespaceId === '__public__' ? 'public' : namespaceId;
}

function formatCellValue(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }

  if (typeof value === 'object') {
    return JSON.stringify(value);
  }

  return String(value);
}

function showCellTooltip(event: MouseEvent | FocusEvent, text: string): void {
  if (!text) {
    return;
  }

  const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
  cellTooltip.value = {
    text,
    top: Math.max(12, rect.top - 10),
    left: Math.min(rect.left, window.innerWidth - 580)
  };
}

function hideCellTooltip(): void {
  cellTooltip.value = null;
}

async function copyValue(value: unknown): Promise<void> {
  const text = formatCellValue(value);
  try {
    await copyText(text);
    store.showNotification('success', '已复制原始值', text.length > 80 ? `${text.slice(0, 80)}...` : text);
  } catch (error) {
    store.showNotification('error', '复制失败', error instanceof Error ? error.message : String(error));
  }
}

async function copyText(text: string): Promise<void> {
  if (navigator.clipboard?.writeText) {
    await navigator.clipboard.writeText(text);
    return;
  }

  const textarea = document.createElement('textarea');
  textarea.value = text;
  textarea.setAttribute('readonly', 'true');
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.appendChild(textarea);
  textarea.select();
  const copied = document.execCommand('copy');
  document.body.removeChild(textarea);

  if (!copied) {
    throw new Error('当前环境不允许写入剪贴板');
  }
}
</script>

<template>
  <section class="data-table">
    <div class="table-toolbar">
      <div class="toolbar-summary">
        <p class="panel-kicker">Workspace</p>
        <h2>{{ tableTitle }}</h2>
        <p class="section-description">{{ tableDescription }}</p>
      </div>

      <div class="toolbar-mode">
        <SyncModeSelector />
      </div>

      <div :class="['toolbar-actions', store.syncMode === 'key' ? 'is-key-mode' : '']">
        <span class="status-chip is-muted">{{ selectionLabel }}</span>
        <span :class="['run-state', store.busy ? 'is-busy' : canStartSync ? 'is-ready' : '']">
          {{ readinessText }}
        </span>
        <button class="primary-action" type="button" :disabled="!canStartSync" :title="syncHintText" @click="store.startSync">
          {{ syncActionText }}
        </button>
        <span v-if="store.syncMode !== 'namespace'" class="selection-meter">已选 {{ selectedCount }}</span>
        <button v-if="store.syncMode === 'file'" type="button" :disabled="store.busy" @click="store.loadSourceFiles">
          加载文件
        </button>

        <div v-if="store.syncMode === 'key'" class="key-search">
          <input
            v-model="store.keyName"
            aria-label="Key"
            placeholder="spring.datasource.url"
            :disabled="store.busy"
            @keydown.enter.prevent="store.scanKey"
          />
          <button
            type="button"
            :disabled="store.busy || !store.keyName.trim() || !store.source.namespaceId"
            @click="store.scanKey"
          >
            扫描
          </button>
        </div>
      </div>
    </div>

    <div class="workspace-context">
      <span>模式 <strong>{{ modeLabel }}</strong></span>
      <span>源 <strong>{{ sourceNamespaceLabel }}</strong></span>
      <span>目标 <strong>{{ targetNamespaceLabel }}</strong></span>
      <span>{{ syncHintText }}</span>
    </div>

    <div v-if="store.syncMode === 'namespace'" class="namespace-state">
      <div class="namespace-state__icon" aria-hidden="true">↔</div>
      <div>
        <h3>准备同步整个 Namespace</h3>
        <p>确认源端和目标端 Namespace 后，点击上方“开始同步”。同步前仍会确认覆盖策略。</p>
      </div>
    </div>

    <div v-if="store.syncMode === 'key' && store.keyRows.length > 0" class="risk-note">
      <strong>Key 同步提示：</strong>
      仅同步当前 Key {{ keyOnlyCount }} 条，完整文件 {{ fullFileCount }} 条。目标文件不存在时，仅同步 Key 会创建只包含所选 Key 的最小配置，不会复制整份源文件。
    </div>

    <div v-if="store.syncMode !== 'namespace'" class="table-scroll">
      <table v-if="store.syncMode === 'file'" :class="{ 'is-empty': store.fileRows.length === 0 }">
        <thead>
          <tr>
            <th></th>
            <th>DataId</th>
            <th>Group</th>
            <th>Type</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="row in store.fileRows"
            :key="fileRowId(row.dataId, row.group)"
            :class="{ 'is-selected': store.selectedFileIds.includes(fileRowId(row.dataId, row.group)) }"
          >
            <td>
              <input v-model="store.selectedFileIds" type="checkbox" :value="fileRowId(row.dataId, row.group)" />
            </td>
            <td class="mono-cell clipped-cell" :title="row.dataId">{{ row.dataId }}</td>
            <td class="clipped-cell" :title="row.group">{{ row.group }}</td>
            <td>{{ row.type || '-' }}</td>
          </tr>
          <tr v-if="store.fileRows.length === 0">
            <td colspan="4" class="empty-cell">
              暂无配置文件。连接源端并选择 Namespace 后，点击“加载文件”。
            </td>
          </tr>
        </tbody>
      </table>

      <table v-else :class="['key-results-table', { 'is-empty': store.keyRows.length === 0 }]">
        <thead>
          <tr>
            <th></th>
            <th>DataId</th>
            <th>Group</th>
            <th>Key 路径</th>
            <th>原值</th>
            <th>同步方式</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="row in store.keyRows" :key="row.id" :class="{ 'is-selected': store.selectedKeyIds.includes(row.id) }">
            <td><input v-model="store.selectedKeyIds" type="checkbox" :value="row.id" /></td>
            <td class="mono-cell clipped-cell">
              <span
                class="cell-popover"
                tabindex="0"
                @mouseenter="showCellTooltip($event, row.dataId)"
                @mouseleave="hideCellTooltip"
                @focus="showCellTooltip($event, row.dataId)"
                @blur="hideCellTooltip"
              >
                {{ row.dataId }}
              </span>
            </td>
            <td class="clipped-cell">
              <span
                class="cell-popover"
                tabindex="0"
                @mouseenter="showCellTooltip($event, row.group)"
                @mouseleave="hideCellTooltip"
                @focus="showCellTooltip($event, row.group)"
                @blur="hideCellTooltip"
              >
                {{ row.group }}
              </span>
            </td>
            <td class="mono-cell clipped-cell">
              <span
                class="cell-popover"
                tabindex="0"
                @mouseenter="showCellTooltip($event, row.keyPath)"
                @mouseleave="hideCellTooltip"
                @focus="showCellTooltip($event, row.keyPath)"
                @blur="hideCellTooltip"
              >
                {{ row.keyPath }}
              </span>
            </td>
            <td class="value-cell">
              <button class="copy-value-button" type="button" @click="copyValue(row.value)">
                <span
                  class="cell-popover"
                  @mouseenter="showCellTooltip($event, `${formatCellValue(row.value)}\n\n点击可复制原始值`)"
                  @mouseleave="hideCellTooltip"
                  @focus="showCellTooltip($event, `${formatCellValue(row.value)}\n\n点击可复制原始值`)"
                  @blur="hideCellTooltip"
                >
                  {{ formatCellValue(row.value) }}
                </span>
              </button>
            </td>
            <td class="strategy-cell">
              <select v-model="row.syncStrategy">
                <option value="keyOnly">仅同步当前 Key</option>
                <option value="fullFile">同步完整文件</option>
              </select>
            </td>
          </tr>
          <tr v-if="store.keyRows.length === 0">
            <td colspan="6" class="empty-cell">
              暂无扫描结果。输入 Key 后按 Enter 或点击“扫描”。
            </td>
          </tr>
        </tbody>
      </table>
    </div>
    <div
      v-if="cellTooltip"
      class="cell-tooltip"
      :style="{ top: `${cellTooltip.top}px`, left: `${cellTooltip.left}px` }"
    >
      {{ cellTooltip.text }}
    </div>
  </section>
</template>
