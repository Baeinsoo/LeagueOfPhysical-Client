# MasterData Luban — Slice γ (Server Switch + GameFramework Cleanup) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Switch **LOP-Server** to Luban (symmetric to Slice β), then remove the now-unused GameFramework MasterData abstractions (`IMasterData`/`IMasterDataManager`/`MasterDataLoader`) once BOTH client and server are off them. Completes the CSV→Luban migration.

**Architecture:** Identical pattern to β, server side: Luban gen (server target, group `s`) → MasterData-Server package `Runtime.Generated/`; hand-written `LOPMasterData` wrapper (server pkgname, **4 tables — no SkinAsset**) in the package `Runtime/`; LOP-Server injects it and reads `Tables.TbXxx.Get(code)`. Finally, delete the three GameFramework MasterData files (guarded by a zero-usage grep across all repos) and update the topology doc.

**Tech Stack:** Same as β. Luban v4.9.0 (infra toolchain, already merged), `com.code-philosophy.luban` v1.2.0 (`Luban.Runtime`), UniTask, VContainer.

---

## Repos & branches (5 repos)

- **infrastructure** — branch `feature/master-data-luban-slice-gamma` (Task 1).
- **LeagueOfPhysical-MasterData-Server** (package) — branch `feature/luban-slice-gamma` (Tasks 2-4).
- **LeagueOfPhysical-Server** (Unity project, server editor open here) — branch `feature/master-data-luban-slice-gamma` (Tasks 5-7).
- **GameFramework** (package) — branch `feature/remove-masterdata-abstractions` (Task 8).
- **LeagueOfPhysical-Client** — branch `feature/master-data-luban-slice-gamma` (this plan doc + topology doc update, Task 9). Client editor stays connected to re-verify after the GameFramework removal.

## Prerequisites (verify before starting)

- Slices α, β merged (infra `main` has Luban toolchain + β gen.sh; MasterData-Client + LOP-Client on Luban).
- **The LOP-Server Unity editor must be open + connected to UnityMCP**, AND the LOP-Client editor should stay connected (for the post-GameFramework-removal client re-check). Resolve instance ids from `mcpforunity://instances`; there will be TWO instances — pass `unity_instance` EXPLICITLY on every UnityMCP call (per CLAUDE.md), using the SERVER id for server work and the CLIENT id only for the final client re-check. **Never mix them up.**
- .NET runtime 8 (for Luban), openpyxl (already installed).

## Unity-in-the-loop note

Same as β: after generating/editing files in the MasterData-Server package or LOP-Server Assets, `refresh_unity(unity_instance=<server-id>)` → poll `editor/state` `is_compiling==false` → `read_console(unity_instance=<server-id>)`. After an external manifest edit, `manage_packages(action="resolve_packages", unity_instance=<server-id>)` to fetch the git Luban package. Commit generated/new files only AFTER Unity generates their `.meta`.

## Lessons carried from β (apply these)

1. **StreamingAssets package path**: editor can't reach package StreamingAssets via `Application.streamingAssetsPath`. The server wrapper's editor branch uses pkgname **`com.baegames.lop.masterdata.server`**.
2. **Preserve `using GameFramework;`**: server `Action.cs`/`LOPActionManager.cs`/`RootLifetimeScope.cs` use GameFramework types (`IActionManager`/`IInitializable`/`IEntity`/`IDataStore`). Only swap the MasterData lookup; keep the using.
3. **Atomic switch**: call-site switch + hand-POCO removal in ONE compile unit (same-namespace shadowing else breaks).
4. **resolve_packages** after manifest edit (refresh alone doesn't fetch the git package).

---

## Task 1: infra — point server gen output at the MasterData-Server package

**Repo:** infrastructure → branch `feature/master-data-luban-slice-gamma`

- [ ] **Step 1: Branch**
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git checkout main && git checkout -b feature/master-data-luban-slice-gamma
```

- [ ] **Step 2: Edit `table/gen.sh`** — change the server target from scratch to the package (client block stays as β):
```bash
SERVER_PKG="../../LeagueOfPhysical-MasterData-Server/Runtime.Generated"
echo "[gen] target=server -> MasterData-Server package"
rm -rf "$SERVER_PKG/Scripts/MasterData" "$SERVER_PKG/StreamingAssets/MasterData"
dotnet "$LUBAN" -t server -c cs-bin -d bin --conf luban.conf \
  -x outputCodeDir="$SERVER_PKG/Scripts/MasterData" \
  -x outputDataDir="$SERVER_PKG/StreamingAssets/MasterData"
```
(Replace the β `server -> scratch` block. Remove the `_gen_scratch` server lines. Mirror in `gen.bat`: `SERVER_PKG=..\..\LeagueOfPhysical-MasterData-Server\Runtime.Generated`.)

- [ ] **Step 3: Run gen**
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
bash gen.sh
```
Expected `[done]`, no errors.

- [ ] **Step 4: Verify server package output**
```bash
ls ../../LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/
ls ../../LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData/
```
Expected cs: Tables.cs, Character.cs, Skin.cs, Action.cs, Item.cs, TbCharacter.cs, TbSkin.cs, TbAction.cs, TbItem.cs (**no SkinAsset**). Expected bytes: tbcharacter.bytes tbskin.bytes tbaction.bytes tbitem.bytes (**no tbskinasset, no Description column** in beans).

- [ ] **Step 5: Commit (infra)**
```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/gen.sh table/gen.bat
git commit -m "feat(table): gen server MasterData into MasterData-Server package (Slice γ)"
```

---

## Task 2: package — commit generated + rewire Generated.asmdef

**Repo:** LeagueOfPhysical-MasterData-Server → branch `feature/luban-slice-gamma`

- [ ] **Step 1: Branch**
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git checkout main && git checkout -b feature/luban-slice-gamma
git status --short   # expect untracked Runtime.Generated/Scripts/MasterData/* + StreamingAssets/MasterData/* from Task 1
```

- [ ] **Step 2: Rewire `Runtime.Generated/baegames.LOP.MasterData.Server.Generated.asmdef`** — overwrite with:
```json
{
    "name": "baegames.LOP.MasterData.Server.Generated",
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

- [ ] **Step 3:** Do **Task 6 Step 1 (server manifest add Luban)** now (the package can't compile without `Luban.Runtime`), then `manage_packages(resolve_packages, unity_instance=<server-id>)`, then `refresh_unity(unity_instance=<server-id>)` → wait `is_compiling==false` → `read_console(unity_instance=<server-id>)`. Expect no `Luban`/`ByteBuf` errors. `.meta` for generated files now exist.

- [ ] **Step 4: Commit generated + asmdef (with .meta)**
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git add Runtime.Generated
git status --short   # confirm .cs + .bytes + their .meta staged
git commit -m "feat: Luban-generated MasterData (server projection: .cs + .bytes); Generated.asmdef -> Luban.Runtime"
```

---

## Task 3: package — Runtime asmdef + `LOPMasterData` wrapper (server)

**Repo:** LeagueOfPhysical-MasterData-Server (same branch)

- [ ] **Step 1: Rewire `Runtime/baegames.LOP.MasterData.Server.Runtime.asmdef`**:
```json
{
    "name": "baegames.LOP.MasterData.Server.Runtime",
    "rootNamespace": "LOP.MasterData",
    "references": [
        "baegames.LOP.MasterData.Server.Generated",
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

- [ ] **Step 2: Create `Runtime/Scripts/LOPMasterData.cs`** — note: **server has 4 tables (no `tbskinasset`)** and pkgname `...server`:
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
    /// Thin server-side wrapper: owns the Luban-generated <see cref="Tables"/> (server projection)
    /// and async-preloads the binary table files. No domain logic. VContainer Singleton.
    /// </summary>
    public class LOPMasterData
    {
        // server projection: SkinAsset is client-only (group c), so it is absent here.
        private static readonly string[] TableFiles =
        {
            "tbcharacter", "tbskin", "tbaction", "tbitem"
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
#if UNITY_EDITOR
            // Editor: package StreamingAssets are not merged into Application.streamingAssetsPath.
            uri = "file://" + Path.GetFullPath(
                $"Packages/com.baegames.lop.masterdata.server/Runtime.Generated/StreamingAssets/{relativePath}");
#elif UNITY_ANDROID
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

- [ ] **Step 3:** `refresh_unity(unity_instance=<server-id>)` → wait → `read_console(unity_instance=<server-id>)` (no errors). `.meta` for wrapper generated.

- [ ] **Step 4: Commit**
```bash
git add Runtime
git commit -m "feat: server LOPMasterData wrapper (4-table projection, package StreamingAssets editor path)"
```

---

## Task 4: package — README use-side contract (server)

**Repo:** LeagueOfPhysical-MasterData-Server (same branch)

- [ ] **Step 1: Update `README.md`** — mirror the β client README: responsibility = Luban server-projection MasterData (`.cs` + `.bytes`); use-side requirements = `com.code-philosophy.luban` (`https://github.com/focus-creative-games/luban_unity.git#v1.2.0`) + `com.cysharp.unitask`; remove any Google.Protobuf MasterData requirement; editing = `infrastructure/table/Datas` + `gen.sh`.

- [ ] **Step 2: Commit**
```bash
git add README.md
git commit -m "docs: README use-side contract -> Luban runtime (server)"
```

---

## Task 5: LOP-Server — add Luban runtime to manifest

**Repo:** LeagueOfPhysical-Server → branch `feature/master-data-luban-slice-gamma`

- [ ] **Step 1: Branch**
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout main && git checkout -b feature/master-data-luban-slice-gamma
```

- [ ] **Step 2: Edit `Packages/manifest.json`** — add after the `com.baegames.lop.masterdata.server` line (keep `org.nuget.google.protobuf`):
```jsonc
    "com.code-philosophy.luban": "https://github.com/focus-creative-games/luban_unity.git#v1.2.0",
```
(This is the manifest add referenced by Task 2 Step 3 — do it before that package compile.)

- [ ] **Step 3: Resolve + compile** — `manage_packages(resolve_packages, unity_instance=<server-id>)` then `refresh_unity(unity_instance=<server-id>)` → wait → `read_console`. Expect Luban fetched + package compiles.

- [ ] **Step 4: Commit**
```bash
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "build(server): add com.code-philosophy.luban v1.2.0 (Luban runtime)"
```

---

## Task 6: LOP-Server — switch DI/loader/call sites + remove legacy (ATOMIC)

**Repo:** LeagueOfPhysical-Server (same branch). Make edits + git-rm; **no commit until after the Unity compile in Step N**. (Combined T6+T7 like β — these are interdependent.)

Read each file first, apply semantically, KEEP `using GameFramework;` where the file uses other GameFramework types.

- [ ] **Step 1: `Assets/Scripts/RootLifetimeScope.cs`** — `builder.Register<IMasterDataManager, LOPMasterDataManager>(Lifetime.Singleton);` → `builder.Register<LOP.MasterData.LOPMasterData>(Lifetime.Singleton);` (keep `using GameFramework;` — IDataStore etc. used here).

- [ ] **Step 2: `Assets/Scripts/Entrance/EntranceComponent/LoadMasterDataComponent.cs`** — `[Inject] private IMasterDataManager masterDataManager;` → `[Inject] private LOP.MasterData.LOPMasterData masterData;`; `await masterDataManager.LoadMasterData();` → `await masterData.LoadAsync();`. (Remove `using GameFramework;` only if unused otherwise.)

- [ ] **Step 3: `Assets/Scripts/Component/CharacterComponent.cs`** — inject `LOP.MasterData.LOPMasterData md;`; `...= masterDataManager.GetMasterData<MasterData.Character>(characterCode);` → `...= md.Tables.TbCharacter.Get(characterCode);`.

- [ ] **Step 4: `Assets/Scripts/Component/ItemComponent.cs`** — inject `md`; `...GetMasterData<MasterData.Item>(itemCode);` → `md.Tables.TbItem.Get(itemCode);`.

- [ ] **Step 5: `Assets/Scripts/Component/Action.cs`** — inject `md`; `...GetMasterData<MasterData.Action>(actionCode);` → `md.Tables.TbAction.Get(actionCode);` (KEEP `using GameFramework;` — IInitializable used).

- [ ] **Step 6: `Assets/Scripts/Game/LOPActionManager.cs`** — inject `md`; `masterDataManager.GetMasterData<MasterData.Action>(actionCode)` → `md.Tables.TbAction.Get(actionCode)`; `actionMasterData.Class` (line ~109) → `actionMasterData.Category` (KEEP `using GameFramework;` — IActionManager/IEntity used).
> Search the file for any other `IMasterDataManager`/`GetMasterData` usage and repoint it the same way.

- [ ] **Step 7: Remove legacy files (git rm; ls first for exact names incl .meta)**
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
ls Assets/Scripts/MasterData/generated/ ; ls "Assets/StreamingAssets/MasterData/"
git rm Assets/Scripts/MasterData/generated/*.cs Assets/Scripts/MasterData/generated/*.cs.meta
git rm Assets/Scripts/MasterData/LOPMasterDataManager.cs Assets/Scripts/MasterData/LOPMasterDataManager.cs.meta
git rm "Assets/StreamingAssets/MasterData"/*.csv "Assets/StreamingAssets/MasterData"/*.csv.meta
```
If `generated/` becomes empty, `git rm Assets/Scripts/MasterData/generated.meta`; if `StreamingAssets/MasterData/` becomes empty, `git rm Assets/StreamingAssets/MasterData.meta`. (Server has no SkinAsset POCO — only Character/Skin/Action/Item; adjust the glob to actual files.)

- [ ] **Step 8: Compile** — `refresh_unity(unity_instance=<server-id>)` → wait → `read_console(unity_instance=<server-id>)`. Expect **0 errors**. If a `using GameFramework;` was wrongly dropped (errors like `IActionManager`/`IEntity`/`IDataStore`/`IInitializable` not found), restore it and recompile. Fix any other flagged leftover.

- [ ] **Step 9: Commit**
```bash
git add Assets/Scripts/RootLifetimeScope.cs Assets/Scripts/Entrance/EntranceComponent/LoadMasterDataComponent.cs Assets/Scripts/Component/CharacterComponent.cs Assets/Scripts/Component/ItemComponent.cs Assets/Scripts/Component/Action.cs Assets/Scripts/Game/LOPActionManager.cs
git commit -m "feat(server): switch MasterData to Luban Tables; remove hand POCO + CSV + old manager"
```

---

## Task 7: LOP-Server — Play Mode verify (server)

**Repo:** LeagueOfPhysical-Server (server editor)

- [ ] **Step 1:** `manage_editor(action="play", unity_instance=<server-id>)`, then poll `editor/state`, then `read_console(unity_instance=<server-id>)`. Confirm MasterData loads from `.bytes` (no 404 / `[LOPMasterData] Failed to load` / NRE), entrance flow completes. `manage_editor(action="stop", unity_instance=<server-id>)`.
> Server may behave differently than client at the entrance (it's a Mirror host); minimally confirm the MasterData LoadAsync completes with no errors and the server initializes.

---

## Task 8: GameFramework — remove unused MasterData abstractions

**Repo:** GameFramework → branch `feature/remove-masterdata-abstractions`

- [ ] **Step 1: Zero-usage guard (across ALL repos)** — confirm nothing references these types anymore:
```bash
grep -rn "IMasterDataManager\|MasterDataLoader\|: *IMasterData\b\|IMasterData " \
  C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets \
  C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets \
  C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared \
  C:/Users/re5na/workspace/LOP/GameFramework/Runtime \
  2>/dev/null | grep -v "GameFramework/Runtime/Scripts/MasterData/"
```
Expected: **no matches** (the only hits would be the 3 definition files themselves, which the grep excludes). If anything else matches, STOP and report — do not remove until that usage is migrated.

- [ ] **Step 2: Branch + remove**
```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git checkout main && git checkout -b feature/remove-masterdata-abstractions
git rm Runtime/Scripts/MasterData/IMasterData.cs Runtime/Scripts/MasterData/IMasterData.cs.meta
git rm Runtime/Scripts/MasterData/IMasterDataManager.cs Runtime/Scripts/MasterData/IMasterDataManager.cs.meta
git rm Runtime/Scripts/MasterData/MasterDataLoader.cs Runtime/Scripts/MasterData/MasterDataLoader.cs.meta
# if Runtime/Scripts/MasterData/ becomes empty: git rm Runtime/Scripts/MasterData.meta
```

- [ ] **Step 3: Compile BOTH editors** — GameFramework is file:-referenced by both projects.
  - Server: `refresh_unity(unity_instance=<server-id>)` → wait → `read_console(unity_instance=<server-id>)` → 0 errors.
  - Client: `refresh_unity(unity_instance=<client-id>)` → wait → `read_console(unity_instance=<client-id>)` → 0 errors.
  (Client already off these abstractions since β; this confirms the removal doesn't break it.)

- [ ] **Step 4: Commit**
```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git commit -m "chore: remove MasterData abstractions (IMasterData/IMasterDataManager/MasterDataLoader) — superseded by Luban"
```

---

## Task 9: LOP-Client — topology doc update (GameFramework no longer owns MasterData abstractions)

**Repo:** LeagueOfPhysical-Client → branch `feature/master-data-luban-slice-gamma`

- [ ] **Step 1:** In `docs/lop-repo-topology.md`, update the GameFramework row (line ~11) and the "코드 분배 기준" list (line ~59) to drop `IMasterData`/`IMasterDataManager`/`MasterDataLoader` from GameFramework's responsibilities (now Luban handles MasterData entirely; GameFramework no longer has MasterData abstractions). Note the completion of the CSV→Luban migration (α/β/γ done).

- [ ] **Step 2: Commit (client)**
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/lop-repo-topology.md docs/superpowers/plans/2026-06-03-master-data-luban-slice-gamma.md
git commit -m "docs(topology): GameFramework no longer owns MasterData abstractions (Slice γ); add γ plan"
```

---

## Slice γ Done — Definition of Done

- [ ] Luban gen writes server `.cs`+`.bytes` (4-table projection, no SkinAsset/Description) into MasterData-Server package; committed with `.meta`.
- [ ] Server package asmdefs rewired; server `LOPMasterData` wrapper (4 tables, server pkgname) present.
- [ ] LOP-Server manifest has `com.code-philosophy.luban`; compiles 0 errors.
- [ ] Server call sites/DI/loader use `LOPMasterData`; hand POCO + CSV + manager removed.
- [ ] Server Play Mode: MasterData loads from `.bytes`, no errors.
- [ ] GameFramework `IMasterData`/`IMasterDataManager`/`MasterDataLoader` removed; BOTH client + server compile 0 errors.
- [ ] Topology doc updated. CSV→Luban migration complete (α/β/γ).

## Out of scope

- `ref`/`range`/`required` validators (still deferred).
- `file:` → git-URL+tag transition for the MasterData/Luban packages.
- Full in-match server gameplay regression (host/match) — minimal entrance-load verification only.

---

## Self-Review

**Spec/β-parity coverage:** server gen→package (T1-2) · luban manifest (T5) · Generated.asmdef Luban ref (T2) · server wrapper 4-table + server pkgname (T3) · call sites/DI/loader + remove legacy atomic (T6) · play verify (T7) · GameFramework removal guarded (T8) · topology doc (T9). ✅

**Placeholder scan:** No TBD. Server facts confirmed by inventory: 4 call sites + DI + loader, `.Class`→`.Category` at LOPActionManager:109, server projection = 4 tables (tbcharacter/tbskin/tbaction/tbitem, no SkinAsset/Description), package asmdef currently Google.Protobuf, manifest has unitask+protobuf+scopedRegistries (no luban yet), GameFramework has exactly the 3 files.

**Type/name consistency:** `Tables.TbCharacter/TbSkin/TbAction/TbItem` (server set, no TbSkinAsset). Wrapper `TableFiles` = 4 matching the server `.bytes`. pkgname `com.baegames.lop.masterdata.server`. asmdef `Luban.Runtime`/`UniTask`. Atomic T6 avoids the same-namespace shadow + type-mismatch trap from β.

**Ambiguity addressed:** TWO Unity instances during γ — every UnityMCP call must pass the correct `unity_instance` (server for server work, client only for the Task 8 client re-check). GameFramework removal is guarded by a cross-repo zero-usage grep before deletion.
