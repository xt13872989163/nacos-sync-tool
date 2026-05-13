<script setup lang="ts">
defineProps<{
  open: boolean;
  title: string;
  body: string;
}>();

defineEmits<{
  confirm: [];
  skip: [];
  cancel: [];
}>();
</script>

<template>
  <div v-if="open" class="dialog-backdrop">
    <section class="dialog">
      <h2>{{ title }}</h2>
      <p>{{ body }}</p>
      <div class="dialog-actions">
        <button type="button" @click="$emit('cancel')">取消</button>
        <button type="button" @click="$emit('skip')">跳过</button>
        <button type="button" class="primary" @click="$emit('confirm')">确认</button>
      </div>
    </section>
  </div>
</template>
