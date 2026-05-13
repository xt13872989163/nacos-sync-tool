import { resolve } from 'node:path';
import { defineConfig } from 'electron-vite';
import vue from '@vitejs/plugin-vue';

const defaultRendererPort = 5173;
const rendererPort = resolveRendererPort();

export default defineConfig({
  main: {},
  preload: {},
  renderer: {
    root: resolve('src/renderer'),
    server: {
      port: rendererPort,
      strictPort: true
    },
    plugins: [vue()]
  }
});

function resolveRendererPort(): number {
  const rawPort = readPortFromArgs() ?? process.env.NACOS_SYNC_PORT ?? process.env.VITE_PORT ?? process.env.PORT;

  if (!rawPort) {
    return defaultRendererPort;
  }

  const port = Number(rawPort);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error(`Invalid renderer dev server port: ${rawPort}`);
  }

  return port;
}

function readPortFromArgs(): string | undefined {
  const portArg = process.argv.find((arg) => arg.startsWith('--port='));
  if (portArg) {
    return portArg.slice('--port='.length);
  }

  const portIndex = process.argv.indexOf('--port');
  if (portIndex >= 0) {
    return process.argv[portIndex + 1];
  }

  return undefined;
}
