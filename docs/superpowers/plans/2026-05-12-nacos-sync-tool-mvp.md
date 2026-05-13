# Nacos Sync Tool MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows/Mac Electron + Vue 3 desktop client that syncs Nacos configuration between two clusters at Namespace, file, and Key levels.

**Architecture:** Use Electron main process for filesystem, encryption, logs, installer-related path handling, and IPC; use Vue 3 renderer for the operational UI. Keep Nacos API access, config parsing, sync orchestration, confirmation policy, logging, and encrypted local settings in focused modules with tests around risky behavior.

**Tech Stack:** Electron, electron-vite, Vue 3, TypeScript, Pinia, Vitest, Playwright, electron-builder, axios, js-yaml, dotenv, proper-lockfile, keytar or Electron safeStorage fallback.

---

## File Structure

- `package.json`: scripts, dependencies, build targets.
- `electron.vite.config.ts`: Electron/Vue build config.
- `src/main/index.ts`: Electron app bootstrap, window creation, IPC registration.
- `src/main/ipc.ts`: typed IPC handlers.
- `src/main/services/logger.ts`: runtime, sync, and error file logs.
- `src/main/services/settingsStore.ts`: encrypted local connection and app settings.
- `src/main/services/pathService.ts`: default log path, user-selected log path, writability checks.
- `src/main/services/nacosClient.ts`: Nacos 1.x/2.x HTTP API wrapper.
- `src/main/services/syncService.ts`: Namespace, file, and Key sync orchestration.
- `src/main/services/configParser.ts`: YAML, JSON, Properties parsing and Key-path updates.
- `src/main/services/confirmPolicy.ts`: confirmation decisions, including Namespace double confirm.
- `src/main/types.ts`: shared domain types for main process.
- `src/preload/index.ts`: safe renderer API bridge.
- `src/renderer/src/App.vue`: main app shell.
- `src/renderer/src/stores/appStore.ts`: UI state, connections, namespaces, selected files, scan results, logs.
- `src/renderer/src/components/ConnectionPanel.vue`: source/target connection forms.
- `src/renderer/src/components/SyncModeSelector.vue`: Namespace/file/Key mode selector.
- `src/renderer/src/components/DataTable.vue`: file list and Key scan result table.
- `src/renderer/src/components/LogPanel.vue`: realtime log display and log actions.
- `src/renderer/src/components/ConfirmDialog.vue`: reusable confirmation modal.
- `tests/unit/*.spec.ts`: main-process service tests.
- `tests/e2e/*.spec.ts`: renderer workflow smoke tests.

## Task 1: Scaffold Electron + Vue 3 App

**Files:**
- Create: `package.json`
- Create: `electron.vite.config.ts`
- Create: `tsconfig.json`
- Create: `src/main/index.ts`
- Create: `src/preload/index.ts`
- Create: `src/renderer/index.html`
- Create: `src/renderer/src/main.ts`
- Create: `src/renderer/src/App.vue`

- [ ] **Step 1: Initialize project dependencies**

Run:

```powershell
npm create electron-vite@latest . -- --template vue-ts
npm install
```

Expected: project has Electron, Vue, TypeScript, and Vite files.

- [ ] **Step 2: Install runtime and test dependencies**

Run:

```powershell
npm install axios js-yaml dotenv proper-lockfile pinia
npm install -D vitest @vue/test-utils playwright electron-builder npm-run-all
```

Expected: dependencies are added to `package.json`.

- [ ] **Step 3: Add base scripts**

Update `package.json` scripts:

```json
{
  "dev": "electron-vite dev",
  "build": "electron-vite build",
  "test": "vitest run",
  "test:watch": "vitest",
  "lint": "tsc --noEmit",
  "pack:win": "electron-builder --win nsis",
  "pack:mac": "electron-builder --mac dmg"
}
```

- [ ] **Step 4: Verify scaffold**

Run:

```powershell
npm run lint
npm run build
```

Expected: both commands complete successfully.

## Task 2: Define Shared Types and IPC Contract

**Files:**
- Create: `src/main/types.ts`
- Create: `src/main/ipc.ts`
- Modify: `src/main/index.ts`
- Modify: `src/preload/index.ts`

- [ ] **Step 1: Create domain types**

Create `src/main/types.ts`:

```ts
export type ClusterRole = 'source' | 'target';
export type SyncMode = 'namespace' | 'file' | 'key';
export type ConfirmDecision = 'confirm' | 'skip' | 'cancel';

export interface NacosConnection {
  baseUrl: string;
  username: string;
  password: string;
}

export interface NacosNamespace {
  namespaceId: string;
  namespaceName: string;
  description?: string;
}

export interface NacosConfigItem {
  dataId: string;
  group: string;
  content: string;
  type?: string;
}

export interface KeyScanResult {
  id: string;
  dataId: string;
  group: string;
  keyPath: string;
  value: unknown;
  syncStrategy: 'keyOnly' | 'fullFile';
}

export interface AppSettings {
  source?: NacosConnection;
  target?: NacosConnection;
  sourceNamespaceId?: string;
  targetNamespaceId?: string;
  logDirectory?: string;
}
```

- [ ] **Step 2: Register typed IPC channels**

Create handlers for:

```ts
export const ipcChannels = {
  settingsLoad: 'settings:load',
  settingsSave: 'settings:save',
  logChooseDirectory: 'log:choose-directory',
  logOpenDirectory: 'log:open-directory',
  nacosTestConnection: 'nacos:test-connection',
  nacosListNamespaces: 'nacos:list-namespaces',
  nacosCreateNamespace: 'nacos:create-namespace',
  nacosListConfigs: 'nacos:list-configs',
  syncNamespace: 'sync:namespace',
  syncFiles: 'sync:files',
  syncScanKey: 'sync:scan-key',
  syncKeyResults: 'sync:key-results'
} as const;
```

- [ ] **Step 3: Expose preload API**

Expose `window.nacosSync` with one function per IPC channel. The renderer must not import Electron directly.

- [ ] **Step 4: Verify IPC typing**

Run:

```powershell
npm run lint
```

Expected: TypeScript compiles without implicit `any` in IPC boundaries.

## Task 3: Implement Log Path and File Logger

**Files:**
- Create: `src/main/services/pathService.ts`
- Create: `src/main/services/logger.ts`
- Create: `tests/unit/pathService.spec.ts`
- Create: `tests/unit/logger.spec.ts`

- [ ] **Step 1: Test Windows default log path**

Create a unit test that stubs platform as `win32`, marks `D:\` writable, and expects default path `D:\NacosSyncTool\logs`.

- [ ] **Step 2: Test fallback log path**

Create a unit test that stubs missing or unwritable `D:\` and expects Electron `userData/logs`.

- [ ] **Step 3: Implement `resolveDefaultLogDirectory`**

Implement:

```ts
export function resolveDefaultLogDirectory(platform: NodeJS.Platform, userDataPath: string, dDriveWritable: boolean): string {
  if (platform === 'win32' && dDriveWritable) {
    return 'D:\\NacosSyncTool\\logs';
  }
  return path.join(userDataPath, 'logs');
}
```

- [ ] **Step 4: Implement categorized file logger**

Logger must write:

- `runtime.log`
- `sync.log`
- `error.log`

Each line format:

```text
2026-05-12T12:00:00.000Z [INFO] message
```

- [ ] **Step 5: Verify logger tests**

Run:

```powershell
npm run test -- tests/unit/pathService.spec.ts tests/unit/logger.spec.ts
```

Expected: tests pass and temp log files are created in test temp directories.

## Task 4: Implement Encrypted Settings Store

**Files:**
- Create: `src/main/services/settingsStore.ts`
- Create: `tests/unit/settingsStore.spec.ts`
- Modify: `src/main/ipc.ts`

- [ ] **Step 1: Test settings round trip**

Test saving and loading `AppSettings` including source/target credentials and log directory.

- [ ] **Step 2: Implement encrypted storage adapter**

Use Electron `safeStorage` when available. If unavailable in unit tests, inject an adapter:

```ts
export interface CryptoAdapter {
  encrypt(value: string): Buffer;
  decrypt(value: Buffer): string;
}
```

- [ ] **Step 3: Keep settings file private**

Write encrypted settings to `settings.enc` under Electron `userData`.

- [ ] **Step 4: Register settings IPC**

Renderer can load and save settings, but password fields are never written to logs.

- [ ] **Step 5: Verify settings tests**

Run:

```powershell
npm run test -- tests/unit/settingsStore.spec.ts
```

Expected: encrypted file does not contain plaintext username or password.

## Task 5: Implement Nacos API Client

**Files:**
- Create: `src/main/services/nacosClient.ts`
- Create: `tests/unit/nacosClient.spec.ts`

- [ ] **Step 1: Test login and token handling**

Mock `POST /nacos/v1/auth/login` and assert token is attached to later requests when returned.

- [ ] **Step 2: Test namespace listing**

Support common Nacos namespace response shapes:

```json
{ "data": [ { "namespace": "dev", "namespaceShowName": "dev" } ] }
```

```json
[ { "namespace": "dev", "namespaceShowName": "dev" } ]
```

- [ ] **Step 3: Implement config listing**

Call `/nacos/v1/cs/configs` with `search=accurate`, `pageNo`, `pageSize`, `tenant`, and read paged results until all configs are loaded.

- [ ] **Step 4: Implement config read and publish**

Implement:

```ts
getConfig(namespaceId: string, dataId: string, group: string): Promise<string | null>
publishConfig(namespaceId: string, item: NacosConfigItem): Promise<void>
```

- [ ] **Step 5: Verify client tests**

Run:

```powershell
npm run test -- tests/unit/nacosClient.spec.ts
```

Expected: tests pass for login, namespace parsing, config list paging, missing config, and publish.

## Task 6: Implement Config Parser and Key Merge

**Files:**
- Create: `src/main/services/configParser.ts`
- Create: `tests/unit/configParser.spec.ts`

- [ ] **Step 1: Test YAML Key search and update**

Input:

```yaml
spring:
  datasource:
    url: jdbc:mysql://source
```

Search key: `spring.datasource.url`.

Expected scan result value: `jdbc:mysql://source`.

- [ ] **Step 2: Test JSON Key search and update**

Input:

```json
{ "spring": { "datasource": { "url": "jdbc:mysql://source" } } }
```

Expected update changes only `spring.datasource.url`.

- [ ] **Step 3: Test Properties Key append**

Input:

```properties
server.port=8080
```

Append key `spring.datasource.url=jdbc:mysql://source`.

Expected content keeps `server.port=8080` and appends the new key.

- [ ] **Step 4: Implement parser**

Implement content-type detection from file extension and optional Nacos type:

- `.yml`, `.yaml` use YAML.
- `.json` use JSON.
- `.properties` and unknown text use line-based properties behavior.

- [ ] **Step 5: Verify parser tests**

Run:

```powershell
npm run test -- tests/unit/configParser.spec.ts
```

Expected: YAML, JSON, and Properties tests pass.

## Task 7: Implement Confirmation Policy

**Files:**
- Create: `src/main/services/confirmPolicy.ts`
- Create: `tests/unit/confirmPolicy.spec.ts`

- [ ] **Step 1: Test Namespace double confirm required**

For Namespace sync with existing target files, policy must require:

```ts
['namespaceStart', 'overwriteExistingFiles']
```

- [ ] **Step 2: Test file sync single confirm**

For file-level overwrite, policy requires:

```ts
['overwriteFile']
```

- [ ] **Step 3: Test Key overwrite single confirm**

For Key-level existing key, policy requires:

```ts
['overwriteKey']
```

- [ ] **Step 4: Implement policy**

Return explicit confirmation requests with title, body, affected file count, and affected config identifiers.

- [ ] **Step 5: Verify policy tests**

Run:

```powershell
npm run test -- tests/unit/confirmPolicy.spec.ts
```

Expected: Namespace sync always requires the first confirm, and overwrite requires the second confirm.

## Task 8: Implement Sync Service

**Files:**
- Create: `src/main/services/syncService.ts`
- Create: `tests/unit/syncService.spec.ts`
- Modify: `src/main/ipc.ts`

- [ ] **Step 1: Test source is read-only**

Mock source and target clients. Assert sync service never calls `publishConfig` on source client.

- [ ] **Step 2: Test Namespace sync creates missing files**

Target `getConfig` returns `null`; sync publishes source item to target.

- [ ] **Step 3: Test Namespace sync double confirm stops overwrite**

If second confirm returns `cancel`, existing target config is not overwritten.

- [ ] **Step 4: Test file sync overwrite confirm**

Existing target file is overwritten only when confirm returns `confirm`.

- [ ] **Step 5: Test Key-only sync append and overwrite**

Missing key is appended automatically. Existing key is overwritten only after confirmation.

- [ ] **Step 6: Implement sync service**

Expose:

```ts
syncNamespace(input): Promise<SyncSummary>
syncFiles(input): Promise<SyncSummary>
scanKey(input): Promise<KeyScanResult[]>
syncKeyResults(input): Promise<SyncSummary>
```

- [ ] **Step 7: Verify sync tests**

Run:

```powershell
npm run test -- tests/unit/syncService.spec.ts
```

Expected: tests pass for create, skip, overwrite, source read-only, and Key-only merge behavior.

## Task 9: Build Vue UI

**Files:**
- Modify: `src/renderer/src/App.vue`
- Create: `src/renderer/src/stores/appStore.ts`
- Create: `src/renderer/src/components/ConnectionPanel.vue`
- Create: `src/renderer/src/components/SyncModeSelector.vue`
- Create: `src/renderer/src/components/DataTable.vue`
- Create: `src/renderer/src/components/LogPanel.vue`
- Create: `src/renderer/src/components/ConfirmDialog.vue`

- [ ] **Step 1: Create app store**

Store state includes source/target connection forms, namespace lists, selected namespaces, sync mode, file rows, Key scan rows, selected rows, busy flag, and realtime log rows.

- [ ] **Step 2: Build connection panels**

Source panel fields:

- Address
- Account
- Password
- Test connection
- Namespace dropdown

Target panel adds:

- Namespace dropdown
- Add Namespace button

- [ ] **Step 3: Build sync mode selector**

Use radio or segmented control for:

- Namespace
- File
- Key

- [ ] **Step 4: Build data table**

File mode columns:

- Selected
- DataId
- Group
- Type

Key mode columns:

- Selected
- DataId
- Group
- Key path
- Source value
- Strategy

- [ ] **Step 5: Build log panel**

Show realtime logs and buttons:

- Open log directory
- Clear visible logs

- [ ] **Step 6: Build confirmation dialog**

Support single confirm and Namespace double confirm by rendering the confirmation requests in sequence.

- [ ] **Step 7: Verify renderer build**

Run:

```powershell
npm run build
```

Expected: renderer and main process compile.

## Task 10: Implement Log Directory Selection UX

**Files:**
- Modify: `src/main/ipc.ts`
- Modify: `src/main/services/pathService.ts`
- Modify: `src/renderer/src/App.vue`
- Modify: `src/renderer/src/components/LogPanel.vue`

- [ ] **Step 1: Windows default behavior**

On first launch, if no log directory is saved and platform is Windows, default to `D:\NacosSyncTool\logs` when writable.

- [ ] **Step 2: Mac first launch behavior**

On first launch, if no log directory is saved and platform is Mac, show directory selection dialog. If skipped, use default app data logs path.

- [ ] **Step 3: App-level change behavior**

User can choose a new log directory from the UI. After change, new log writes go to the new directory, and old logs are not migrated.

- [ ] **Step 4: Writability failure behavior**

If selected directory is not writable, show a UI error and keep the previous log directory.

- [ ] **Step 5: Verify manually**

Run:

```powershell
npm run dev
```

Expected: user can choose a log directory, open it, and see new log files appear there.

## Task 11: Add E2E Smoke Tests

**Files:**
- Create: `tests/e2e/app.spec.ts`
- Create: `playwright.config.ts`

- [ ] **Step 1: Test app shell renders**

Assert source panel, target panel, sync mode selector, data table region, and log panel are visible.

- [ ] **Step 2: Test busy state disables sync buttons**

Simulate a pending sync IPC promise and assert action buttons are disabled until it resolves.

- [ ] **Step 3: Test Namespace double confirm UI**

Trigger a mocked Namespace sync requiring two confirmations and assert both dialogs appear in order.

- [ ] **Step 4: Verify E2E tests**

Run:

```powershell
npx playwright install
npm run build
npx playwright test
```

Expected: smoke tests pass on the local platform.

## Task 12: Configure Packaging

**Files:**
- Modify: `package.json`
- Create: `build/installer.nsh`

- [ ] **Step 1: Configure app metadata**

Set:

```json
{
  "name": "nacos-sync-tool",
  "productName": "Nacos Sync Tool",
  "version": "0.1.0"
}
```

- [ ] **Step 2: Configure Windows NSIS**

Use NSIS installer target. Add installer page for log directory selection and write the selected path to app config on first launch through an installer-created config file under user data.

- [ ] **Step 3: Configure Mac DMG**

Use DMG target. Mac log directory selection remains first-launch behavior inside the app.

- [ ] **Step 4: Verify packaging commands**

Run on Windows:

```powershell
npm run pack:win
```

Run on Mac:

```bash
npm run pack:mac
```

Expected: Windows installer and Mac DMG are generated under `dist/`.

## Task 13: Final Verification

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Run unit tests**

Run:

```powershell
npm run test
```

Expected: all unit tests pass.

- [ ] **Step 2: Run type check and build**

Run:

```powershell
npm run lint
npm run build
```

Expected: both commands pass.

- [ ] **Step 3: Manual Nacos smoke test**

Use two test Nacos clusters or two test namespaces. Verify:

- Source connection can list namespaces.
- Target connection can list and create namespaces.
- Namespace sync creates missing files.
- Namespace sync requires double confirm before overwriting files.
- File sync supports multi-select and overwrite confirmation.
- Key scan lists file name, group, key path, and original value.
- Key-only sync appends missing keys and confirms existing-key overwrite.
- Source cluster remains unchanged.
- Logs are written to the selected directory.

- [ ] **Step 4: Update README run instructions**

Add commands:

```powershell
npm install
npm run dev
npm run test
npm run build
npm run pack:win
```

For Mac:

```bash
npm run pack:mac
```

## Self-Review

- Spec coverage: The plan covers Electron + Vue 3, source/target connection fields, namespace dropdowns, target namespace creation, Namespace/file/Key sync, DataId + Group existence checks, source read-only behavior, overwrite confirmations, Namespace double confirm, local sync logs, encrypted settings, log directory selection, busy-state protection, Nacos 1.x/2.x API wrapper, Windows/Mac packaging, and final verification.
- Marker scan: No task contains unresolved marker text or deferred implementation language.
- Type consistency: Shared types are introduced before services and UI tasks reference them. Sync service methods and IPC channels use consistent names across tasks.
