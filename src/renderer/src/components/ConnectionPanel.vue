<script setup lang="ts">
import { computed } from 'vue';
import type { ClusterRole } from '../../../main/types';
import { useAppStore } from '../stores/appStore';

const props = defineProps<{
  role: ClusterRole;
  title: string;
}>();

const store = useAppStore();
const form = props.role === 'source' ? store.source : store.target;
const isConnecting = computed(() => store.connectingRole === props.role);
const statusText = computed(() => {
  if (isConnecting.value) {
    return '连接中';
  }

  return form.connected ? '已连接' : '未连接';
});

function namespaceOptionValue(namespaceId: string): string {
  return namespaceId || '__public__';
}

function handleBaseUrlInput(event: Event): void {
  store.applyHistoryConnection(props.role, (event.target as HTMLInputElement).value);
}

function markConnectionDirty(): void {
  store.markConnectionDirty(props.role);
}
</script>

<template>
  <section
    :class="[
      'connection-panel',
      form.connected ? 'is-connected' : '',
      isConnecting ? 'is-connecting' : '',
      form.lastError ? 'has-error' : ''
    ]"
  >
    <div class="panel-title-row">
      <div class="panel-title-block">
        <h2>{{ title }}</h2>
      </div>
      <span :class="['status-pill', form.connected ? 'is-connected' : '', isConnecting ? 'is-busy' : '', form.lastError ? 'is-error' : '']">
        <span class="status-dot" aria-hidden="true"></span>
        {{ statusText }}
      </span>
      <p v-if="form.lastError" class="connection-error" :title="form.lastError">{{ form.lastError }}</p>
    </div>

    <div class="connection-fields">
      <label>
        地址
        <input
          v-model="form.baseUrl"
          :list="`${role}-history-list`"
          placeholder="http://127.0.0.1:8848"
          :disabled="store.busy"
          @input="handleBaseUrlInput"
        />
        <datalist :id="`${role}-history-list`">
          <option v-for="item in form.history" :key="`${role}-${item.baseUrl}`" :value="item.baseUrl">
            {{ item.username }}
          </option>
        </datalist>
      </label>

      <label>
        账号
        <input v-model="form.username" placeholder="nacos" :disabled="store.busy" @input="markConnectionDirty" />
      </label>

      <label>
        密码
        <input
          v-model="form.password"
          type="password"
          placeholder="请输入密码"
          :disabled="store.busy"
          @input="markConnectionDirty"
        />
      </label>
    </div>

    <div :class="['connection-actions', role === 'target' ? 'has-create' : '']">
      <label>
        Namespace
        <select
          v-model="form.namespaceId"
          :disabled="store.busy || form.namespaces.length === 0"
          @change="void store.persistSettings()"
        >
          <option value="" disabled>请选择 Namespace</option>
          <option
            v-for="namespace in form.namespaces"
            :key="namespace.namespaceId || 'public'"
            :value="namespaceOptionValue(namespace.namespaceId)"
          >
            {{ namespace.namespaceName || namespace.namespaceId || 'public' }}
          </option>
        </select>
      </label>
      <button
        v-if="role === 'target'"
        class="icon-button"
        type="button"
        :disabled="store.busy || !form.connected"
        title="新建 Namespace"
        @click="store.openTargetNamespaceDialog"
      >
        +
      </button>
      <button class="test-button" type="button" :disabled="store.busy" @click="store.testConnection(role)">
        {{ isConnecting ? '连接中...' : '连接测试' }}
      </button>
    </div>
  </section>
</template>
