import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import { installBrowserMockApi } from './browserMock';
import './styles.css';

installBrowserMockApi();
createApp(App).use(createPinia()).mount('#app');
