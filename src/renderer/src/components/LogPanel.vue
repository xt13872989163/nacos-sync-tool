<script setup lang="ts">
import { computed } from 'vue';
import { useAppStore } from '../stores/appStore';

const store = useAppStore();

const errorCount = computed(() => store.logs.filter((row) => row.level === 'ERROR').length);
</script>

<template>
  <section class="log-panel">
    <div class="log-toolbar">
      <div>
        <p class="panel-kicker">Activity</p>
        <h2>日志</h2>
      </div>
      <div class="log-summary">
        <span>{{ store.logs.length }} 条</span>
        <span v-if="errorCount > 0" class="has-error">{{ errorCount }} 个错误</span>
      </div>
      <div class="log-actions">
        <button type="button" @click="store.chooseLogDirectory">选择目录</button>
        <button type="button" @click="store.openLogDirectory">打开目录</button>
        <button type="button" @click="store.clearVisibleLogs">清空</button>
      </div>
    </div>
    <div class="log-window" aria-live="polite">
      <p v-for="row in store.logs" :key="row.id" :class="`log-${row.level.toLowerCase()}`">
        <span>{{ row.createdAt }}</span>
        <strong>[{{ row.level }}]</strong>
        {{ row.message }}
      </p>
      <p v-if="store.logs.length === 0" class="log-empty">暂无日志，操作结果会显示在这里。</p>
    </div>
  </section>
</template>
