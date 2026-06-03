# MasterData Luban — Slice β (Client Runtime Switch) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Switch the **LOP-Client** runtime from the legacy CSV/reflection MasterData path to Luban: generate `.cs` + `.bytes` into the **MasterData-Client package**, add the `com.code-philosophy.luban` runtime, expose a thin `LOPMasterData` wrapper, repoint the 4 call sites + DI + loader to `md.Tables.TbXxx.Get(code)`, and delete the hand-written POCO + CSV + old manager. Server stays on CSV (Slice γ).

**Architecture:** Luban gen (infra) writes typed `cfg`-style `.cs` (namespace `LOP.MasterData`, `Tables`/`TbXxx`/bean) and binary `.bytes` into the MasterData-Client package's `Runtime.Generated/`. A hand-written `LOPMasterData` (package `Runtime/`) owns the generated `Tables` and async-preloads the `.bytes` from StreamingAssets (UnityWebRequest, Android-safe). LOP-Client injects `LOPMasterData` (VContainer Singleton) and reads typed rows via `Tables.TbXxx.Get(code)`. GameFramework's `IMasterData*` abstractions stay (server still uses them until γ).

**Tech Stack:** Luban v4.9.0 (codegen, committed in infra `table/tools/Luban/`), `com.code-philosophy.luban` v1.2.0 (Unity runtime UPM: `Luban.Runtime` asmdef, `Luban.ByteBuf`/`Luban.BeanBase`), UniTask (async UnityWebRequest), VContainer (DI).

---

## Repos & branches

- **infrastructure** — `C:/Users/re5na/workspace/LOP/infrastructure`, branch `feature/master-data-luban-slice-beta` (Task 1).
- **LeagueOfPhysical-MasterData-Client** (package) — `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client`, branch `feature/luban-slice-beta` (Tasks 2-4).
- **LeagueOfPhysical-Client** (Unity project) — this worktree `worktree-feature+master-data-luban-slice-beta` (Tasks 5-8 + this plan doc).
- Each task states its repo. **Slice γ** does the symmetric server work — out of scope here.

## Prerequisites

- Slice α merged (infra `main` has the Luban toolchain; verify `dotnet C:/Users/re5na/workspace/LOP/infrastructure/table/tools/Luban/Luban.dll --version` → `Luban 4.9.0`).
- The **LOP-Client Unity editor must be open and connected to UnityMCP** for `.meta` generation + compile/console verification. Per `CLAUDE.md`, pass `unity_instance` (the `LeagueOfPhysical-Client@<hash>` id from `mcpforunity://instances`) on EVERY UnityMCP call. Never operate the server instance.
- openpyxl present (α already installed it).

## Unity-in-the-loop note (read before executing)

Several tasks generate new `.cs`/`.bytes` files **inside the MasterData-Client package** (file:-referenced by LOP-Client). Unity generates `.meta` files on import. After writing generated/new files, trigger an import and wait for compile:
- `mcp__UnityMCP__refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")`
- poll `editor_state.isCompiling == false`, then `read_console(unity_instance=...)` for errors.
- Then the new `.meta` files exist on disk — commit `.cs`/`.bytes` **with** their `.meta` (CLAUDE.md: always commit Unity `.meta`).

---

## File Structure

**MasterData-Client package — created/modified:**
- `Runtime.Generated/Scripts/MasterData/*.cs` (Luban output: `Tables.cs`, `Character.cs`, `Skin.cs`, `SkinAsset.cs`, `Action.cs`, `Item.cs`, `TbCharacter.cs`, `TbSkin.cs`, `TbSkinAsset.cs`, `TbAction.cs`, `TbItem.cs`) + `.meta`
- `Runtime.Generated/StreamingAssets/MasterData/tb*.bytes` (5 files) + `.meta`
- `Runtime.Generated/baegames.LOP.MasterData.Client.Generated.asmdef` (rewire: Google.Protobuf → Luban.Runtime)
- `Runtime/Scripts/LOPMasterData.cs` (thin wrapper) + `.meta`
- `Runtime/baegames.LOP.MasterData.Client.Runtime.asmdef` (rewire: + Generated, Luban.Runtime, UniTask)
- `README.md` (use-side: Luban runtime requirement)

**infrastructure — modified:** `table/gen.sh`, `table/gen.bat` (client target → package; server target → scratch).

**LOP-Client — modified/removed:**
- `Packages/manifest.json` (+ `com.code-philosophy.luban`)
- `Assets/RootLifetimeScope.cs` (DI)
- `Assets/Scripts/Entrance/EntranceComponent/LoadMasterDataComponent.cs`
- `Assets/Scripts/Component/{CharacterComponent,Action,ItemComponent}.cs`, `Assets/Scripts/Game/LOPActionManager.cs` (call sites; `.Class`→`.Category`)
- **Removed:** `Assets/Scripts/MasterData/generated/*.cs(.meta)`, `Assets/Scripts/MasterData/LOPMasterDataManager.cs(.meta)`, `Assets/StreamingAssets/MasterData/*.csv(.meta)`

---

## Task 1: infra — point client gen output at the MasterData-Client package

**Repo:** infrastructure → branch `feature/master-data-luban-slice-beta`

- [ ] **Step 1: Branch**
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git checkout main && git checkout -b feature/master-data-luban-slice-beta
```

- [ ] **Step 2: Update `table/gen.sh`** — client → package, server → scratch (γ flips server later):
```bash
#!/bin/bash
set -e
cd "$(dirname "$0")"
LUBAN="tools/Luban/Luban.dll"
CLIENT_PKG="../../LeagueOfPhysical-MasterData-Client/Runtime.Generated"
SCRATCH="_gen_scratch"

echo "[gen] target=client -> MasterData-Client package"
rm -rf "$CLIENT_PKG/Scripts/MasterData" "$CLIENT_PKG/StreamingAssets/MasterData"
dotnet "$LUBAN" -t client -c cs-bin -d bin --conf luban.conf \
  -x outputCodeDir="$CLIENT_PKG/Scripts/MasterData" \
  -x outputDataDir="$CLIENT_PKG/StreamingAssets/MasterData"

echo "[gen] target=server -> scratch (wired in Slice γ)"
rm -rf "$SCRATCH/server"
dotnet "$LUBAN" -t server -c cs-bin -d bin --conf luban.conf \
  -x outputCodeDir="$SCRATCH/server/cs" \
  -x outputDataDir="$SCRATCH/server/bytes"

echo "[done]"
```
Mirror the same in `table/gen.bat` (client → `..\..\LeagueOfPhysical-MasterData-Client\Runtime.Generated\Scripts\MasterData` / `...\StreamingAssets\MasterData`; server → `_gen_scratch\server\...`).

- [ ] **Step 3: Run gen**
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
bash gen.sh
```
Expected: `[done]`, no errors.

- [ ] **Step 4: Verify output landed in the package**
```bash
ls ../../LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/
ls ../../LeagueOfPhysical-MasterData-Client/Runtime.Generated/StreamingAssets/MasterData/
```
Expected: `Tables.cs` + 5 beans + 5 `Tb*.cs`; `tbcharacter.bytes tbskin.bytes tbskinasset.bytes tbaction.bytes tbitem.bytes`.

- [ ] **Step 5: Commit (infra)**
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/gen.sh table/gen.bat
git commit -m "feat(table): gen client MasterData into MasterData-Client package (Slice β)"
```
(The generated files now sit in the package repo working tree — committed in Task 2, a different repo.)

---

## Task 2: package — commit generated output + rewire Generated.asmdef

**Repo:** LeagueOfPhysical-MasterData-Client → branch `feature/luban-slice-beta`

- [ ] **Step 1: Branch**
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git checkout main && git checkout -b feature/luban-slice-beta
git status --short   # expect untracked Runtime.Generated/Scripts/MasterData/* and StreamingAssets/MasterData/* from Task 1 gen
```

- [ ] **Step 2: Rewire `Runtime.Generated/baegames.LOP.MasterData.Client.Generated.asmdef`** — generated cs-bin code uses `using Luban` only (no Google.Protobuf, no IMasterData bridge). Replace file contents with:
```json
{
    "name": "baegames.LOP.MasterData.Client.Generated",
    "rootNamespace": "",
    "references": ["Luban.Runtime"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```
(Removed: `baegames.LOP.MasterData.Client.Runtime`, `baegames.GameFramework.Runtime`, `Google.Protobuf.dll`. Added: `Luban.Runtime`. This also breaks any Generated↔Runtime cycle — now Runtime→Generated only.)

- [ ] **Step 3: Generate `.meta` + verify compile** (Unity-in-the-loop)
First ensure LOP-Client manifest has the Luban package — that is Task 5. **If Task 5 is not yet done, do it now** (the package can't compile without `Luban.Runtime`). Recommended order: do **Task 5 Step 2 (manifest add)** before this step, then return here.
- `mcp__UnityMCP__refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")`
- wait `editor_state.isCompiling == false`
- `mcp__UnityMCP__read_console(unity_instance=...)` → expect **no errors** about `Luban`/`ByteBuf`/missing refs in the generated assembly.

- [ ] **Step 4: Commit generated output + asmdef (with .meta)**
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git add Runtime.Generated
git status --short   # confirm .cs, .bytes, AND their .meta are staged
git commit -m "feat: Luban-generated MasterData (.cs + .bytes); Generated.asmdef -> Luban.Runtime"
```

---

## Task 3: package — Runtime asmdef + `LOPMasterData` wrapper

**Repo:** LeagueOfPhysical-MasterData-Client (same branch)

- [ ] **Step 1: Rewire `Runtime/baegames.LOP.MasterData.Client.Runtime.asmdef`** — the wrapper uses generated `Tables` + `Luban.ByteBuf` + UniTask + UnityWebRequest:
```json
{
    "name": "baegames.LOP.MasterData.Client.Runtime",
    "rootNamespace": "LOP.MasterData",
    "references": [
        "baegames.LOP.MasterData.Client.Generated",
        "Luban.Runtime",
        "UniTask"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```
(Removed `baegames.GameFramework.Runtime` — wrapper does not use `IMasterData`. Added Generated, Luban.Runtime, UniTask.)

- [ ] **Step 2: Create `Runtime/Scripts/LOPMasterData.cs`**
```csharp
using Cysharp.Threading.Tasks;
using Luban;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace LOP.MasterData
{
    /// <summary>
    /// Thin client-side wrapper that owns the Luban-generated <see cref="Tables"/> and
    /// async-preloads the binary table files from StreamingAssets (Android-safe).
    /// No domain logic. Registered as a VContainer Singleton in LOP-Client.
    /// </summary>
    public class LOPMasterData
    {
        // loader keys == generated Tables.cs loader("...") keys == .bytes file stems
        private static readonly string[] TableFiles =
        {
            "tbcharacter", "tbskin", "tbskinasset", "tbaction", "tbitem"
        };

        public Tables Tables { get; private set; }

        public async Task LoadAsync()
        {
            var blobs = new Dictionary<string, byte[]>(TableFiles.Length);
            foreach (var name in TableFiles)
            {
                blobs[name] = await LoadBytes($"MasterData/{name}.bytes");
            }
            Tables = new Tables(file => new ByteBuf(blobs[file]));
        }

        private static async Task<byte[]> LoadBytes(string relativePath)
        {
            string uri;
#if UNITY_ANDROID && !UNITY_EDITOR
            uri = Path.Combine(Application.streamingAssetsPath, relativePath);
#else
            uri = "file://" + Path.Combine(Application.streamingAssetsPath, relativePath);
#endif
            using var www = UnityWebRequest.Get(uri);
            await www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[LOPMasterData] Failed to load {uri}: {www.error}");
                return Array.Empty<byte>();
            }
            return www.downloadHandler.data;
        }
    }
}
```

- [ ] **Step 3: Generate `.meta` + compile** (Unity-in-the-loop): refresh_unity → wait isCompiling false → read_console (expect no errors).

- [ ] **Step 4: Commit**
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git add Runtime
git status --short   # confirm LOPMasterData.cs + .meta + asmdef staged
git commit -m "feat: LOPMasterData wrapper (owns Tables, async StreamingAssets .bytes load)"
```

---

## Task 4: package — README use-side contract

**Repo:** LeagueOfPhysical-MasterData-Client (same branch)

- [ ] **Step 1: Update `README.md`** — replace the Protobuf use-side requirement with Luban. Ensure it states:
  - Responsibility: Luban-generated MasterData (`Character/Skin/SkinAsset/Action/Item` `.cs`) + data (`.bytes` in StreamingAssets).
  - Use-side requirements: `com.code-philosophy.luban` (UPM git `https://github.com/focus-creative-games/luban_unity.git#v1.2.0`), `com.cysharp.unitask`. (No Google.Protobuf for MasterData.)
  - Editing: do not hand-edit generated output; change `infrastructure/table/Datas` + re-run `gen.sh`.

- [ ] **Step 2: Commit**
```bash
git add README.md
git commit -m "docs: README use-side contract -> Luban runtime"
```

---

## Task 5: LOP-Client — add Luban runtime to manifest

**Repo:** LeagueOfPhysical-Client (this worktree)

- [ ] **Step 1: Edit `Packages/manifest.json`** — add to `dependencies` (keep `org.nuget.google.protobuf`; it is still used by the wire/Shared layer):
```jsonc
    "com.code-philosophy.luban": "https://github.com/focus-creative-games/luban_unity.git#v1.2.0",
```
Place it near the other `com.baegames.*` / git-url entries.

- [ ] **Step 2: Resolve + compile** (Unity-in-the-loop): refresh_unity(unity_instance=...) → wait isCompiling false → read_console. Expect Unity to fetch `com.code-philosophy.luban` and compile (the MasterData-Client Generated + Runtime asmdefs from Tasks 2-3 now resolve `Luban.Runtime`). **No errors.**

- [ ] **Step 3: Commit**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/feature+master-data-luban-slice-beta"
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "build(client): add com.code-philosophy.luban v1.2.0 (Luban runtime)"
```

---

## Task 6: LOP-Client — switch DI + loader + call sites

**Repo:** LeagueOfPhysical-Client (this worktree). All paths under `Assets/`.

- [ ] **Step 1: `Assets/RootLifetimeScope.cs`** — replace the manager registration:
```diff
- builder.Register<IMasterDataManager, LOPMasterDataManager>(Lifetime.Singleton);
+ builder.Register<LOP.MasterData.LOPMasterData>(Lifetime.Singleton);
```

- [ ] **Step 2: `Assets/Scripts/Entrance/EntranceComponent/LoadMasterDataComponent.cs`**
```diff
- [Inject] private IMasterDataManager masterDataManager;
+ [Inject] private LOP.MasterData.LOPMasterData masterData;
  public async Task Execute()
  {
-     await masterDataManager.LoadMasterData();
+     await masterData.LoadAsync();
  }
```
(Drop the now-unused `using GameFramework;` only if nothing else in the file needs it.)

- [ ] **Step 3: `Assets/Scripts/Component/CharacterComponent.cs`**
```diff
- [Inject] private IMasterDataManager masterDataManager;
+ [Inject] private LOP.MasterData.LOPMasterData md;
  public MasterData.Character masterData { get; private set; }
  public void Initialize(string characterCode)
  {
      this.characterCode = characterCode;
-     this.masterData = masterDataManager.GetMasterData<MasterData.Character>(characterCode);
+     this.masterData = md.Tables.TbCharacter.Get(characterCode);
  }
```
> Note the field rename: inject as `md` to avoid clashing with the existing `masterData` property. Verify the property type `MasterData.Character` still resolves to the Luban type `LOP.MasterData.Character` (it does — `topModule=LOP.MasterData`, file is in `namespace LOP`).

- [ ] **Step 4: `Assets/Scripts/Component/ItemComponent.cs`** — same pattern:
```diff
- [Inject] private IMasterDataManager masterDataManager;
+ [Inject] private LOP.MasterData.LOPMasterData md;
-     this.masterData = masterDataManager.GetMasterData<MasterData.Item>(itemCode);
+     this.masterData = md.Tables.TbItem.Get(itemCode);
```

- [ ] **Step 5: `Assets/Scripts/Component/Action.cs`** — repoint the masterData lookup (line ~32):
```diff
- [Inject] private IMasterDataManager masterDataManager;
+ [Inject] private LOP.MasterData.LOPMasterData md;
-     this.masterData = masterDataManager.GetMasterData<MasterData.Action>(actionCode);
+     this.masterData = md.Tables.TbAction.Get(actionCode);
```
(The reads `masterData.CastTime`, `masterData.Cooldown`, `masterData.Duration` are unchanged — those Luban property names match.)

- [ ] **Step 6: `Assets/Scripts/Game/LOPActionManager.cs`** — repoint lookup + `.Class`→`.Category`:
```diff
- [Inject] private IMasterDataManager masterDataManager;
+ [Inject] private LOP.MasterData.LOPMasterData md;
  ...
-     var actionMasterData = masterDataManager.GetMasterData<MasterData.Action>(actionCode);
+     var actionMasterData = md.Tables.TbAction.Get(actionCode);
  ...
-     var actionType = System.Type.GetType($"LOP.{actionMasterData.Class}");
+     var actionType = System.Type.GetType($"LOP.{actionMasterData.Category}");
```
(Read the file first to get exact surrounding lines; the two edits are the inject field, the `GetMasterData` call at ~line 89, and `.Class` at line 90.)

- [ ] **Step 7: Compile** (Unity-in-the-loop): refresh_unity → wait → read_console. Expect **no errors**. Fix any missed call site the compiler flags (search `GetMasterData`, `IMasterDataManager`, `.Class` across `Assets/` if errors appear).

- [ ] **Step 8: Commit**
```bash
git add Assets/RootLifetimeScope.cs Assets/Scripts/Entrance/EntranceComponent/LoadMasterDataComponent.cs Assets/Scripts/Component/CharacterComponent.cs Assets/Scripts/Component/ItemComponent.cs Assets/Scripts/Component/Action.cs Assets/Scripts/Game/LOPActionManager.cs
git commit -m "feat(client): switch MasterData access to Luban Tables (native)"
```

---

## Task 7: LOP-Client — remove hand POCO + CSV + old manager

**Repo:** LeagueOfPhysical-Client (this worktree)

- [ ] **Step 1: Remove legacy files (with .meta)**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/feature+master-data-luban-slice-beta"
git rm Assets/Scripts/MasterData/generated/Character.cs Assets/Scripts/MasterData/generated/Skin.cs Assets/Scripts/MasterData/generated/SkinAsset.cs Assets/Scripts/MasterData/generated/Action.cs Assets/Scripts/MasterData/generated/Item.cs
git rm Assets/Scripts/MasterData/generated/*.cs.meta 2>/dev/null || true
git rm Assets/Scripts/MasterData/LOPMasterDataManager.cs
git rm "Assets/StreamingAssets/MasterData"/*.csv
git rm "Assets/StreamingAssets/MasterData"/*.csv.meta 2>/dev/null || true
```
(Use `ls` first to confirm exact filenames incl. `.meta`. Keep `Assets/Scripts/MasterData/generated/` folder removal clean — if the folder becomes empty, also `git rm` its `.meta`.)
> KEEP GameFramework `IMasterData`/`IMasterDataManager`/`MasterDataLoader` — server still uses them (γ removes).

- [ ] **Step 2: Compile** (Unity-in-the-loop): refresh_unity → wait → read_console. Expect **no errors** (nothing should reference the removed types now — Task 6 repointed everything). If the compiler flags a leftover reference, repoint it.

- [ ] **Step 3: Commit**
```bash
git add -A Assets/Scripts/MasterData Assets/StreamingAssets/MasterData
git status --short
git commit -m "chore(client): remove hand-written MasterData POCO + CSV + old manager"
```

---

## Task 8: Verify client runs on Luban (Play Mode)

**Repo:** LeagueOfPhysical-Client — runtime verification (human + UnityMCP)

- [ ] **Step 1: Enter Play Mode** and exercise MasterData paths:
  - `mcp__UnityMCP__manage_editor(action="play", unity_instance=...)`, then `read_console(unity_instance=...)`.
  - Confirm a character/action/item loads (uses `Tables.TbCharacter/TbAction/TbItem.Get`), no `KeyNotFound`/NRE, no `Failed to load MasterData` errors.
  - Confirm `LoadMasterDataComponent.Execute` completed (MasterData loaded from `.bytes`).
  - World Core slice 3 regression: damage→death path still logs `[World] Death entity ...`.
- [ ] **Step 2: Stop Play Mode** (`manage_editor(action="stop", ...)`). Record the result.

> This is the real proof the client now runs on Luban `.bytes` instead of CSV.

---

## Slice β Done — Definition of Done

- [ ] Luban gen writes client `.cs`+`.bytes` into the MasterData-Client package; committed with `.meta`.
- [ ] Package asmdefs rewired (Generated→Luban.Runtime; Runtime→Generated+Luban.Runtime+UniTask); `LOPMasterData` wrapper present.
- [ ] LOP-Client manifest has `com.code-philosophy.luban` v1.2.0; project compiles with zero errors.
- [ ] 4 call sites + DI + loader use `LOPMasterData`/`Tables.TbXxx.Get`; `LOPActionManager` uses `.Category`.
- [ ] Hand POCO + CSV + `LOPMasterDataManager` removed; GameFramework `IMasterData*` retained.
- [ ] Play Mode: client loads MasterData from `.bytes`, no errors; damage→death regression OK.

## Out of scope (γ / later)

- Server: gen→MasterData-Server package, server `LOPMasterData`, server call sites/DI, server CSV/POCO removal, then remove GameFramework `IMasterData*`.
- `ref`/`range`/`required` validators in the schema (still deferred; `default_skin_code`/`skin_code` remain plain strings — no `ResolveRef` linking needed for runtime).
- `file:` → git-URL+tag transition for the MasterData packages.

---

## Self-Review

**Spec coverage (β slice of the migration spec):** gen→package (T1-2) · `com.code-philosophy.luban` (T5) · Generated.asmdef Luban ref (T2) · `LOPMasterData` wrapper (T3) · call sites/DI/loader (T6) · remove hand POCO+CSV (T7) · GameFramework IMasterData* retained (T7 note) ✅. Play-mode verification (T8) ✅.

**Placeholder scan:** No TBD. Loader keys (`tbcharacter`…), asmdef names (`Luban.Runtime`, `UniTask`), package tag (`v1.2.0`), `ByteBuf(byte[])` ctor, and the single `.Class`→`.Category` rename are all confirmed facts (verified against α scratch output + luban_unity repo).

**Type/name consistency:** `Tables.TbCharacter/TbSkin/TbSkinAsset/TbAction/TbItem` match the generated `Tables.cs` (verified). Bean property names (`Code/Name/Speed/JumpPower/CastTime/Cooldown/Duration/Category`) match Luban PascalCase output. `LOP.MasterData.LOPMasterData` namespace consistent across wrapper, DI, and call-site injects. Generated↔Runtime asmdef cycle avoided (Runtime→Generated only).

**Ambiguity addressed:** asmdef Generated no longer references Runtime (was a 2a leftover for the IMasterData bridge we dropped) — prevents a Runtime↔Generated cycle once the wrapper (Runtime) references Tables (Generated).
