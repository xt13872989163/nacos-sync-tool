import { spawn } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';

const defaultPort = 5173;
const rootDir = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const port = resolvePort();
const electronViteBin = join(
  rootDir,
  'node_modules',
  '.bin',
  process.platform === 'win32' ? 'electron-vite.cmd' : 'electron-vite'
);

const child = spawn(electronViteBin, ['dev'], {
  cwd: rootDir,
  env: {
    ...process.env,
    NACOS_SYNC_PORT: String(port)
  },
  stdio: 'inherit'
});

child.on('exit', (code, signal) => {
  if (signal) {
    process.kill(process.pid, signal);
    return;
  }

  process.exit(code ?? 0);
});

function resolvePort() {
  const rawPort =
    readPortFromArgs() ?? process.env.NACOS_SYNC_PORT ?? process.env.VITE_PORT ?? process.env.PORT ?? readPortFromConfig();

  if (!rawPort) {
    return defaultPort;
  }

  const port = Number(rawPort);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error(`Invalid dev server port: ${rawPort}`);
  }

  return port;
}

function readPortFromConfig() {
  try {
    const config = JSON.parse(readFileSync(join(rootDir, 'dev.config.json'), 'utf8'));
    return config.port === undefined ? undefined : String(config.port);
  } catch {
    return undefined;
  }
}

function readPortFromArgs() {
  const args = process.argv.slice(2);
  const portArg = args.find((arg) => arg.startsWith('--port='));
  if (portArg) {
    return portArg.slice('--port='.length);
  }

  const portIndex = args.indexOf('--port');
  if (portIndex >= 0) {
    return args[portIndex + 1];
  }

  return args.find((arg) => /^\d+$/.test(arg));
}
