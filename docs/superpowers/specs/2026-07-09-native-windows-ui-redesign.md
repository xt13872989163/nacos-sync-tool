# Native Windows UI Redesign

## Goal

Improve the native Windows app layout so it feels like a focused Nacos sync workstation instead of a stack of raw forms and tables.

## Direction

Use a restrained Windows utility style: clear hierarchy, compact connection setup, a visible source-to-target flow, a dedicated workbench area, and a quieter log panel. The redesign keeps the current WPF technology and existing bindings, avoiding business logic changes.

## Layout

The window is organized into four visible bands:

1. Lightweight top bar: app identity, version, theme selector, and no heavy card treatment.
2. Connection flow: source Nacos and target Nacos panels are compact and aligned, with the sync direction visually centered between them.
3. Workbench: left vertical mode navigation for Namespace, File, and Key modes; right content area for the active operation, table, and primary actions.
4. Log drawer: compact bottom panel with terminal-like styling, kept secondary to the main work area.

## Interaction

Primary actions are grouped in the workbench action bar. Loading configs and starting sync should be close to the table instead of floating over it. The start sync button remains available across modes, but never overlays content.

## Scope

This pass changes XAML layout and shared styles only. It does not change Nacos API behavior, sync logic, authentication, or data models.

