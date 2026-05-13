<script setup lang="ts">
import { useAppStore } from '../stores/appStore';

const store = useAppStore();
</script>

<template>
  <div class="mode-selector" aria-label="同步模式">
    <label :class="['mode-option', store.syncMode === 'namespace' ? 'is-active' : '']">
      <input v-model="store.syncMode" type="radio" value="namespace" />
      <span>
        <strong>Namespace</strong>
        <small>同步整个命名空间</small>
      </span>
    </label>
    <label :class="['mode-option', store.syncMode === 'file' ? 'is-active' : '']">
      <input v-model="store.syncMode" type="radio" value="file" />
      <span>
        <strong>文件</strong>
        <small>加载后选择配置文件</small>
      </span>
    </label>
    <label :class="['mode-option', store.syncMode === 'key' ? 'is-active' : '']">
      <input v-model="store.syncMode" type="radio" value="key" />
      <span>
        <strong>Key</strong>
        <small>扫描并选择同步项</small>
      </span>
    </label>
  </div>
</template>
