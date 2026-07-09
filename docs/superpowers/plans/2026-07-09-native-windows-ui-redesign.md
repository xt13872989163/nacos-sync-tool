# Native Windows UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rework the native Windows WPF interface into a cleaner Nacos sync workstation layout.

**Architecture:** Keep the current MVVM bindings and command surface intact. Replace the main window structure and extend shared WPF styles so the UI hierarchy is clearer without touching API or sync services.

**Tech Stack:** WPF XAML, .NET WindowsDesktop, existing CommunityToolkit MVVM bindings.

---

### Task 1: Redesign Window Structure

**Files:**
- Modify: `D:/project/codex/nacos-sync-tool/native/windows/src/NacosSyncTool.Windows/MainWindow.xaml`

- [ ] Replace the current stacked layout with top bar, connection flow, workbench, and log drawer rows.
- [ ] Keep all existing command bindings: `TestSourceConnectionCommand`, `TestTargetConnectionCommand`, `CreateTargetNamespaceCommand`, `LoadConfigListCommand`, `ScanKeyCommand`, `SyncCommand`, and `ClearLogsCommand`.
- [ ] Move the primary sync action into the workbench action bar so it does not overlap table content.
- [ ] Keep the existing DataGrid columns and mode visibility converters.

### Task 2: Add Shared Styles

**Files:**
- Modify: `D:/project/codex/nacos-sync-tool/native/windows/src/NacosSyncTool.Windows/Themes/Shared.xaml`

- [ ] Add styles for compact connection panels, workbench shell, side mode navigation, and subtle section labels.
- [ ] Clean remaining mojibake comments in shared styles.
- [ ] Keep existing theme resource keys so all four themes still work.

### Task 3: Verify and Build

**Files:**
- Verify: `D:/project/codex/nacos-sync-tool/native/windows/src/NacosSyncTool.Windows/MainWindow.xaml`
- Verify: `D:/project/codex/nacos-sync-tool/native/windows/src/NacosSyncTool.Windows/Themes/Shared.xaml`

- [ ] Run XML parsing checks for all modified XAML files.
- [ ] Run `git diff --check`.
- [ ] Push to `codex/nacos-sync-tool-mvp` and confirm GitHub Actions `Build Windows Native App` succeeds.
- [ ] Download the new x64 artifact and start it locally for visual testing.

