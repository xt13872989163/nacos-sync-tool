import { app, BrowserWindow, dialog, safeStorage, shell } from 'electron';
import { writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { is } from '@electron-toolkit/utils';
import { registerIpcHandlers } from './ipc';
import { startDevHttpBridge } from './services/devHttpBridge';
import { FileLogger } from './services/logger';
import { isDirectoryWritable, resolveInitialLogDirectory } from './services/pathService';
import { createElectronSafeStorageAdapter, createSettingsStore } from './services/settingsStore';

const defaultRendererPort = 5173;

function createWindow(): void {
  const mainWindow = new BrowserWindow({
    useContentSize: true,
    width: 772,
    height: 673,
    minWidth: 772,
    minHeight: 673,
    titleBarStyle: 'hidden',
    titleBarOverlay: {
      color: '#eef0ed',
      symbolColor: '#202520',
      height: 34
    },
    show: false,
    autoHideMenuBar: true,
    webPreferences: {
      preload: join(__dirname, '../preload/index.mjs'),
      sandbox: false,
      contextIsolation: true,
      nodeIntegration: false
    }
  });

  mainWindow.on('ready-to-show', () => {
    mainWindow.show();
  });

  if (is.dev && process.env.ELECTRON_RENDERER_URL) {
    void mainWindow.loadURL(process.env.ELECTRON_RENDERER_URL);
  } else {
    void mainWindow.loadFile(join(__dirname, '../renderer/index.html'));
  }
}

app.whenReady().then(async () => {
  const settingsStore = createSettingsStore(app.getPath('userData'), createElectronSafeStorageAdapter(safeStorage));
  const settings = await settingsStore.load();
  const logDirectory = settings.logDirectory ?? (await resolveInitialLogDirectory(process.platform, app.getPath('userData')));
  const logger = new FileLogger(logDirectory);

  registerIpcHandlers({
    settingsStore,
    getRuntimeInfo: () => ({
      rendererPort: resolveRuntimeRendererPort(settings.rendererPort),
      packaged: app.isPackaged
    }),
    restartApp: () => {
      app.relaunch();
      app.quit();
    },
    saveRendererPort: async (port: number) => {
      if (!is.dev) {
        return;
      }

      await writeFile(join(process.cwd(), 'dev.config.json'), `${JSON.stringify({ port }, null, 2)}\n`, 'utf8');
    },
    chooseLogDirectory: async () => {
      const result = await dialog.showOpenDialog({
        title: '选择日志存储目录',
        properties: ['openDirectory', 'createDirectory']
      });

      if (result.canceled || !result.filePaths[0]) {
        return null;
      }

      const selectedDirectory = result.filePaths[0];
      if (!(await isDirectoryWritable(selectedDirectory))) {
        throw new Error('选择的日志目录不可写，请重新选择。');
      }

      logger.setLogDirectory(selectedDirectory);
      await settingsStore.save({
        ...(await settingsStore.load()),
        logDirectory: selectedDirectory
      });
      await logger.info('runtime', `Log directory changed: ${selectedDirectory}`);

      return selectedDirectory;
    },
    openLogDirectory: async () => {
      await shell.openPath(logger.getLogDirectory());
    }
  });

  await logger.info('runtime', 'Application started.');
  if (is.dev) {
    startDevHttpBridge();
  }
  createWindow();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

function resolveRuntimeRendererPort(savedPort?: number): number {
  const candidates = [
    readPortFromArgs(),
    readPortFromUrl(process.env.ELECTRON_RENDERER_URL),
    process.env.NACOS_SYNC_PORT,
    process.env.VITE_PORT,
    process.env.PORT,
    savedPort
  ];

  for (const candidate of candidates) {
    const port = Number(candidate);
    if (Number.isInteger(port) && port > 0 && port <= 65535) {
      return port;
    }
  }

  return defaultRendererPort;
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

function readPortFromUrl(url?: string): number | undefined {
  if (!url) {
    return undefined;
  }

  try {
    return Number(new URL(url).port);
  } catch {
    return undefined;
  }
}
