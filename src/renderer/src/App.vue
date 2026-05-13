<template>
  <main
    ref="shellRef"
    class="app-shell"
    :class="{ 'is-resizing-log': isResizingLog }"
    :data-theme="store.theme"
    :data-busy="store.busy"
    :style="{ '--log-panel-height': `${logPanelHeight}px` }"
  >
    <header class="app-titlebar">
      <div class="titlebar-brand">
        <span class="brand-mark" aria-hidden="true">NS</span>
        <strong>Nacos Sync Tool</strong>
      </div>
      <div class="titlebar-settings">
        <label v-if="!store.isPackaged" class="topbar-control port-control" title="页面端口，修改后会自动重启应用">
          <span>端口</span>
          <input
            v-model.number="rendererPortDraft"
            type="text"
            inputmode="numeric"
            pattern="[0-9]*"
            @change="changeRendererPort"
            @keydown.enter="changeRendererPort"
          />
        </label>
        <label class="topbar-control theme-control">
          <span>主题</span>
          <select v-model="store.theme" @change="changeTheme">
            <option value="forest">森林</option>
            <option value="slate">深灰</option>
            <option value="warm">暖色</option>
            <option value="nexus">海洋</option>
          </select>
        </label>
      </div>
    </header>

    <section class="connection-matrix" aria-label="连接设置">
      <ConnectionPanel role="source" title="源端 Nacos" />
      <ConnectionPanel role="target" title="目标端 Nacos" />
    </section>

    <section class="main-workbench" aria-label="配置工作区">
      <DataTable />
    </section>

    <button
      class="panel-resizer"
      type="button"
      aria-label="调整日志区域高度"
      title="向上拖动放大日志"
      @pointerdown="startResizeLogPanel"
      @dblclick="resetLogPanelHeight"
    >
      <span aria-hidden="true"></span>
    </button>
    <LogPanel />
    <NamespaceDialog />
    <ConfirmDialog :open="false" title="" body="" />
    <section
      v-if="store.notification"
      :class="['sync-toast', `is-${store.notification.type}`]"
      aria-live="polite"
      aria-atomic="true"
    >
      <div>
        <strong>{{ store.notification.title }}</strong>
        <p>{{ store.notification.message }}</p>
      </div>
      <button type="button" aria-label="关闭提示" @click="store.dismissNotification">x</button>
    </section>
  </main>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch, watchEffect } from 'vue';
import ConfirmDialog from './components/ConfirmDialog.vue';
import ConnectionPanel from './components/ConnectionPanel.vue';
import DataTable from './components/DataTable.vue';
import LogPanel from './components/LogPanel.vue';
import NamespaceDialog from './components/NamespaceDialog.vue';
import { useAppStore } from './stores/appStore';
import type { AppTheme } from '../../main/types';

const store = useAppStore();
const shellRef = ref<HTMLElement | null>(null);
const logPanelHeight = ref(124);
const isResizingLog = ref(false);
const rendererPortDraft = ref(store.rendererPort);

function startResizeLogPanel(event: PointerEvent): void {
  isResizingLog.value = true;
  window.addEventListener('pointermove', resizeLogPanel);
  window.addEventListener('pointerup', stopResizeLogPanel);
  window.addEventListener('pointercancel', stopResizeLogPanel);
  event.preventDefault();
}

function resizeLogPanel(event: PointerEvent): void {
  if (!isResizingLog.value || !shellRef.value) {
    return;
  }

  const shellRect = shellRef.value.getBoundingClientRect();
  const nextHeight = shellRect.bottom - event.clientY - 10;
  logPanelHeight.value = Math.min(Math.max(nextHeight, 96), Math.max(96, shellRect.height - 330));
}

function stopResizeLogPanel(): void {
  isResizingLog.value = false;
  window.removeEventListener('pointermove', resizeLogPanel);
  window.removeEventListener('pointerup', stopResizeLogPanel);
  window.removeEventListener('pointercancel', stopResizeLogPanel);
}

function resetLogPanelHeight(): void {
  logPanelHeight.value = 124;
}

function changeTheme(event: Event): void {
  void store.setTheme((event.target as HTMLSelectElement).value as AppTheme);
}

function changeRendererPort(event: Event): void {
  void store.setRendererPort(Number((event.target as HTMLInputElement).value));
}

onMounted(() => {
  void store.initialize();
});

watchEffect(() => {
  document.documentElement.dataset.theme = store.theme;
});

watch(
  () => store.rendererPort,
  (port) => {
    rendererPortDraft.value = port;
  }
);

onBeforeUnmount(() => {
  stopResizeLogPanel();
});
</script>
