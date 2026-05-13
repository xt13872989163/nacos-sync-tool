<script setup lang="ts">
import { useAppStore } from '../stores/appStore';

const store = useAppStore();
</script>

<template>
  <div v-if="store.targetNamespaceDialog.open" class="dialog-backdrop">
    <form class="dialog namespace-dialog" @submit.prevent="store.createTargetNamespace">
      <div>
        <p class="panel-kicker">Target Namespace</p>
        <h2>新建目标 Namespace</h2>
        <p>创建成功后会刷新目标端 Namespace 列表，并自动选中新建项。</p>
      </div>

      <label>
        Namespace ID
        <input
          v-model="store.targetNamespaceDialog.namespaceId"
          autofocus
          placeholder="例如 dev-singapore"
          :disabled="store.busy"
        />
      </label>

      <label>
        描述
        <input v-model="store.targetNamespaceDialog.description" placeholder="可选" :disabled="store.busy" />
      </label>

      <div class="dialog-actions">
        <button type="button" :disabled="store.busy" @click="store.closeTargetNamespaceDialog">取消</button>
        <button type="submit" class="primary" :disabled="store.busy || !store.targetNamespaceDialog.namespaceId.trim()">
          创建并选中
        </button>
      </div>
    </form>
  </div>
</template>
