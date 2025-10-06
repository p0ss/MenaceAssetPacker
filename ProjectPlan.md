# Menace Modkit Project Plan

## Overview
GPL-3.0 licensed, cross-platform .NET tooling suite for managing Menace assets, typetrees, and future code mods. Solution consists of shared core services, CLI utilities for Windows typetree extraction, and an Avalonia-based desktop UI.

## Phase 0 – Licensing & Research Baseline
- [ ] Confirm GPL-3.0 licensing across the repo (LICENSE, third-party notices, README statement).
- [ ] Catalogue dependency licenses; automate notice generation if feasible.
- [ ] Snapshot Menace build metadata (IL2CPP version, Unity version) for reference.

## Phase 1 – Repository & Solution Scaffolding
- [ ] Initialize `.NET` solution with projects:
  - `Menace.Modkit.Core` (class library)
  - `Menace.Modkit.App` (Avalonia UI)
  - `Menace.Modkit.Cli` (Spectre.Console or System.CommandLine)
  - `Menace.Modkit.Tests` (xUnit)
- [ ] Configure shared build props (nullable, analyzers, code style).
- [ ] Set up dependency injection host (`Microsoft.Extensions.Hosting`).
- [ ] Add CI pipeline skeleton (lint/test on Linux & Windows).

## Phase 2 – Typetree & Asset Extraction Pipeline
- [ ] Implement Windows-only CLI command `modkit cache-typetrees` using AssetRipper to dump typetrees/metadata into project cache (`ProjectRoot/Typetrees/*.json`).
- [ ] Define cache manifest schema (JSON/TOML) including game build fingerprint.
- [ ] Create cross-platform loader in Core to consume cached typetrees with validations & version checks.
- [ ] Provide automation script/docs for Windows VM workflow.

## Phase 3 – Asset Services & Data Layer
- [ ] Build asset domain models (SerializedFile, ObjectRecord, Binary blobs).
- [ ] Implement `AssetService` abstractions for indexing, previewing, extracting, repacking via AssetRipper APIs.
- [ ] Introduce project manifest database (SQLite or LiteDB) tracking assets, edits, exports.
- [ ] Expose async/background task framework around long-running IO.

## Phase 4 – Stats Editor Foundations
- [ ] Identify target stats assets (ScriptableObjects) using typetree data; define strongly typed records.
- [ ] Implement `StatsService` for loading, validating, and saving stat modifications.
- [ ] Create change-set/diff representation for exporting modifications.
- [ ] Add unit tests covering round-trip serialization and validation rules.

## Phase 5 – Avalonia UI Shell
- [ ] Establish Fluent dark theme + layout primitives; add icon assets.
- [ ] Build navigation shell with areas for Project Dashboard, Asset Browser, Stats Editor, Task Monitor.
- [ ] Wire background task progress & logging to UI (ReactiveUI observables).
- [ ] Implement asset browser panels (tree, preview, metadata) and stats editor grid with inline validation.

## Phase 6 – CLI & Automation Enhancements
- [ ] Flesh out CLI commands for batch export/import, stats patching, project manifest operations.
- [ ] Support headless scripting via JSON inputs for CI or mod pipelines.
- [ ] Document CLI + UI feature parity matrix.

## Phase 7 – Code Mod Tooling (Forward-Looking)
- [ ] Integrate Il2Cpp metadata dump ingestion (Cpp2IL/Il2CppDumper outputs).
- [ ] Provide scaffolding for BepInEx + Il2CppInterop mod projects (template generator).
- [ ] Prototype runtime hook inspection (Frida optional) using ingested metadata.
- [ ] Document roadmap for deeper integration or custom launcher exploration.

## Phase 8 – Packaging & Release
- [ ] Configure self-contained Avalonia builds (Windows/Linux/macOS) with third-party notices.
- [ ] Provide signed Windows installer + portable zips; tarballs for Linux.
- [ ] Publish CLI binaries for Windows typetree extraction workflow.
- [ ] Produce user documentation, onboarding guides, and contribution guidelines.

## Phase 9 – QA & Maintenance
- [ ] Establish automated regression tests (unit, CLI smoke, UI via interaction tests).
- [ ] Monitor upstream Menace updates; plan typetree cache refresh strategy.
- [ ] Track dependency updates (AssetRipper, Avalonia) and maintain compatibility.
- [ ] Maintain backlog for community feature requests & bug triage.
