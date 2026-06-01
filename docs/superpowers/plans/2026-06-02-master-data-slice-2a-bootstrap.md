# MasterData Slice 2a — Schema-first Bootstrap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bootstrap two new Unity packages (`com.baegames.lop.masterdata.{client,server}`) + `.proto` schemas in `infrastructure/table/proto/` + protoc compile pipeline, with both LOP projects registering the packages while keeping runtime CSV flow unchanged.

**Architecture:** New git repos hold Unity package skeletons (empty `Scripts/MasterData/` + `StreamingAssets/MasterData/` filled in Slice 2b). `infrastructure/table/proto/` defines schema with custom options (`(lop.side)`, `(lop.fk)`, `(lop.range)`, `(lop.required)`, `(lop.unique)`). protoc tools are copied from LOP-Shared (`Tools/Protobuf/`) into infrastructure for self-containment. Compile script in 2a only verifies `.proto` compiles cleanly — actual `.cs`/`.bin` output to the new packages comes in Slice 2b.

**Tech Stack:** Unity 2022.3 packages (file: reference), Protocol Buffers 3.28 (custom field options), GameFramework `IMasterData`/`IMasterDataManager` interfaces, UnityNuGet `org.nuget.google.protobuf`.

**Spec:** `docs/superpowers/specs/2026-06-02-master-data-slice-2a-bootstrap-design.md` (commit `e1d42e7`)

**Current branch:** `feature/master-data-slice-2a-bootstrap` (LOP-Client)

---

## Pre-flight — repo branch setup

- [ ] **Verify LOP-Client is on `feature/master-data-slice-2a-bootstrap`**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git branch --show-current
```
Expected output: `feature/master-data-slice-2a-bootstrap`

- [ ] **Create matching feature branch in LOP-Server**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout main
git pull
git checkout -b feature/master-data-slice-2a-bootstrap
git branch --show-current
```
Expected output: `feature/master-data-slice-2a-bootstrap`

If `git pull` reports an existing untracked file conflict, stash first: `git stash push -u -m "WIP pre-2a"`.

- [ ] **Create matching feature branch in infrastructure**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git status
git checkout -b feature/master-data-slice-2a-bootstrap
git branch --show-current
```
Expected: `feature/master-data-slice-2a-bootstrap`. If `git status` shows unstaged changes you don't expect, stop and inspect.

---

## Task 1: Bootstrap LeagueOfPhysical-MasterData-Client repo

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/package.json`
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/README.md`
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/.gitignore`
- Create: `Runtime/baegames.LOP.MasterData.Client.Runtime.asmdef`
- Create: `Runtime.Generated/baegames.LOP.MasterData.Client.Generated.asmdef`
- Create: `Editor/baegames.LOP.MasterData.Client.Editor.asmdef`
- Create: `Tests/EditMode/baegames.LOP.MasterData.Client.Tests.EditMode.asmdef`
- Create: `Tests/PlayMode/baegames.LOP.MasterData.Client.Tests.PlayMode.asmdef`

- [ ] **Step 1: Create folder structure**

```bash
cd C:/Users/re5na/workspace/LOP
mkdir -p LeagueOfPhysical-MasterData-Client/Runtime
mkdir -p LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData
mkdir -p LeagueOfPhysical-MasterData-Client/Runtime.Generated/StreamingAssets/MasterData
mkdir -p LeagueOfPhysical-MasterData-Client/Editor
mkdir -p LeagueOfPhysical-MasterData-Client/Tests/EditMode
mkdir -p LeagueOfPhysical-MasterData-Client/Tests/PlayMode
ls LeagueOfPhysical-MasterData-Client/
```
Expected output lists 5 directories.

- [ ] **Step 2: Write package.json**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/package.json`
```json
{
  "name": "com.baegames.lop.masterdata.client",
  "version": "0.0.1",
  "displayName": "LOP MasterData (Client)",
  "description": "League of Physical 클라 전용 MasterData (Protobuf schema + data).",
  "unity": "2022.3",
  "author": { "name": "insoo.bae" },
  "dependencies": {
    "com.baegames.gameframework": "0.0.2"
  }
}
```

- [ ] **Step 3: Write README.md**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/README.md`
```markdown
# LeagueOfPhysical-MasterData-Client (com.baegames.lop.masterdata.client)

League of Physical 클라 전용 MasterData (Protobuf schema + 데이터).

## 책임

- proto 산출물 (Character/Skin/Action/Item/SkinAsset `.cs`) — Slice 2b부터 채워짐
- 데이터 (`.bin` in StreamingAssets) — Slice 2b부터 채워짐

## Use-side Requirements

- `com.baegames.gameframework` (package.json dependencies)
- `org.nuget.google.protobuf` (UnityNuGet)

상세 토폴로지: 사용 측 저장소의 `docs/lop-repo-topology.md` 참조.

## Editing

이 패키지는 *exporter 산출물*이라 직접 편집 금지. 변경하려면 `infrastructure/table/`에서 Excel/.proto 수정 + exporter 재실행 (Slice 2b부터).
```

- [ ] **Step 4: Write .gitignore**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/.gitignore`
```gitignore
# Unity-generated
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emory[Cc]aptures/

# Visual Studio / Rider / etc.
.vs/
.idea/
*.csproj
*.sln
*.user
*.suo

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 5: Write Runtime asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime/baegames.LOP.MasterData.Client.Runtime.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Client.Runtime",
    "rootNamespace": "LOP.MasterData",
    "references": ["baegames.GameFramework.Runtime"],
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

- [ ] **Step 6: Write Runtime.Generated asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Runtime.Generated/baegames.LOP.MasterData.Client.Generated.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Client.Generated",
    "rootNamespace": "",
    "references": [
        "baegames.LOP.MasterData.Client.Runtime",
        "baegames.GameFramework.Runtime"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["Google.Protobuf.dll"],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 7: Write Editor asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Editor/baegames.LOP.MasterData.Client.Editor.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Client.Editor",
    "rootNamespace": "LOP.MasterData.Editor",
    "references": [
        "baegames.LOP.MasterData.Client.Runtime",
        "baegames.LOP.MasterData.Client.Generated"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 8: Write Tests.EditMode asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Tests/EditMode/baegames.LOP.MasterData.Client.Tests.EditMode.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Client.Tests.EditMode",
    "rootNamespace": "LOP.MasterData.Tests",
    "references": [
        "baegames.LOP.MasterData.Client.Runtime",
        "baegames.LOP.MasterData.Client.Generated",
        "baegames.GameFramework.Runtime"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false,
    "optionalUnityReferences": ["TestAssemblies"]
}
```

- [ ] **Step 9: Write Tests.PlayMode asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/Tests/PlayMode/baegames.LOP.MasterData.Client.Tests.PlayMode.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Client.Tests.PlayMode",
    "rootNamespace": "LOP.MasterData.Tests",
    "references": [
        "baegames.LOP.MasterData.Client.Runtime",
        "baegames.LOP.MasterData.Client.Generated",
        "baegames.GameFramework.Runtime"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false,
    "optionalUnityReferences": ["TestAssemblies"]
}
```

- [ ] **Step 10: git init + initial commit**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git init -b main
git add .
git commit -m "chore: bootstrap LOP-MasterData-Client package skeleton

- package.json (com.baegames.lop.masterdata.client 0.0.1)
- 5 asmdefs (Runtime, Generated, Editor, Tests.EditMode, Tests.PlayMode)
- README + .gitignore (Unity package standard)
- empty Runtime/Scripts/MasterData/ and Runtime.Generated/StreamingAssets/MasterData/
  to be filled in Slice 2b (protoc output + .bin)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
git log --oneline -1
```
Expected: `<hash> chore: bootstrap LOP-MasterData-Client package skeleton`

---

## Task 2: Bootstrap LeagueOfPhysical-MasterData-Server repo

**Files:** Identical structure to Task 1 with `Client` → `Server` in all names and paths.

- [ ] **Step 1: Create folder structure**

```bash
cd C:/Users/re5na/workspace/LOP
mkdir -p LeagueOfPhysical-MasterData-Server/Runtime
mkdir -p LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData
mkdir -p LeagueOfPhysical-MasterData-Server/Runtime.Generated/StreamingAssets/MasterData
mkdir -p LeagueOfPhysical-MasterData-Server/Editor
mkdir -p LeagueOfPhysical-MasterData-Server/Tests/EditMode
mkdir -p LeagueOfPhysical-MasterData-Server/Tests/PlayMode
```

- [ ] **Step 2: Write package.json**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/package.json`
```json
{
  "name": "com.baegames.lop.masterdata.server",
  "version": "0.0.1",
  "displayName": "LOP MasterData (Server)",
  "description": "League of Physical 서버 전용 MasterData (Protobuf schema + data).",
  "unity": "2022.3",
  "author": { "name": "insoo.bae" },
  "dependencies": {
    "com.baegames.gameframework": "0.0.2"
  }
}
```

- [ ] **Step 3: Write README.md**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/README.md`
```markdown
# LeagueOfPhysical-MasterData-Server (com.baegames.lop.masterdata.server)

League of Physical 서버 전용 MasterData (Protobuf schema + 데이터).

## 책임

- proto 산출물 (Character/Skin/Action/Item `.cs` — description 등 클라 전용 컬럼 *없음*) — Slice 2b부터 채워짐
- 데이터 (`.bin` in StreamingAssets) — Slice 2b부터 채워짐

## Use-side Requirements

- `com.baegames.gameframework` (package.json dependencies)
- `org.nuget.google.protobuf` (UnityNuGet)

상세 토폴로지: 사용 측 저장소의 `docs/lop-repo-topology.md` 참조.

## Editing

이 패키지는 *exporter 산출물*이라 직접 편집 금지. 변경하려면 `infrastructure/table/`에서 Excel/.proto 수정 + exporter 재실행 (Slice 2b부터).
```

- [ ] **Step 4: Write .gitignore**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/.gitignore`
```gitignore
# Unity-generated
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emory[Cc]aptures/

# Visual Studio / Rider / etc.
.vs/
.idea/
*.csproj
*.sln
*.user
*.suo

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 5: Write Runtime asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime/baegames.LOP.MasterData.Server.Runtime.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Server.Runtime",
    "rootNamespace": "LOP.MasterData",
    "references": ["baegames.GameFramework.Runtime"],
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

- [ ] **Step 6: Write Runtime.Generated asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Runtime.Generated/baegames.LOP.MasterData.Server.Generated.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Server.Generated",
    "rootNamespace": "",
    "references": [
        "baegames.LOP.MasterData.Server.Runtime",
        "baegames.GameFramework.Runtime"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": ["Google.Protobuf.dll"],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 7: Write Editor asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Editor/baegames.LOP.MasterData.Server.Editor.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Server.Editor",
    "rootNamespace": "LOP.MasterData.Editor",
    "references": [
        "baegames.LOP.MasterData.Server.Runtime",
        "baegames.LOP.MasterData.Server.Generated"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 8: Write Tests.EditMode asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Tests/EditMode/baegames.LOP.MasterData.Server.Tests.EditMode.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Server.Tests.EditMode",
    "rootNamespace": "LOP.MasterData.Tests",
    "references": [
        "baegames.LOP.MasterData.Server.Runtime",
        "baegames.LOP.MasterData.Server.Generated",
        "baegames.GameFramework.Runtime"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false,
    "optionalUnityReferences": ["TestAssemblies"]
}
```

- [ ] **Step 9: Write Tests.PlayMode asmdef**

File: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server/Tests/PlayMode/baegames.LOP.MasterData.Server.Tests.PlayMode.asmdef`
```json
{
    "name": "baegames.LOP.MasterData.Server.Tests.PlayMode",
    "rootNamespace": "LOP.MasterData.Tests",
    "references": [
        "baegames.LOP.MasterData.Server.Runtime",
        "baegames.LOP.MasterData.Server.Generated",
        "baegames.GameFramework.Runtime"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false,
    "optionalUnityReferences": ["TestAssemblies"]
}
```

- [ ] **Step 10: git init + initial commit**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git init -b main
git add .
git commit -m "chore: bootstrap LOP-MasterData-Server package skeleton

- package.json (com.baegames.lop.masterdata.server 0.0.1)
- 5 asmdefs (Runtime, Generated, Editor, Tests.EditMode, Tests.PlayMode)
- README + .gitignore (Unity package standard)
- empty Runtime/Scripts/MasterData/ and Runtime.Generated/StreamingAssets/MasterData/
  to be filled in Slice 2b (protoc output + .bin)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
git log --oneline -1
```
Expected: `<hash> chore: bootstrap LOP-MasterData-Server package skeleton`

---

## Task 3: Copy protoc tools into infrastructure/table/

**Files:**
- Copy: `LeagueOfPhysical-Shared/Tools/Protobuf/protoc-28.2-win64/` → `infrastructure/table/tools/protoc-28.2-win64/`

- [ ] **Step 1: Create infrastructure tools directory**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
mkdir -p table/tools
```

- [ ] **Step 2: Copy protoc-28.2-win64 from LOP-Shared**

```bash
cp -rv C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tools/Protobuf/protoc-28.2-win64 \
       C:/Users/re5na/workspace/LOP/infrastructure/table/tools/
ls C:/Users/re5na/workspace/LOP/infrastructure/table/tools/protoc-28.2-win64/bin/
```
Expected: `protoc.exe` listed (Windows binary).

- [ ] **Step 3: Verify protoc runs**

```bash
C:/Users/re5na/workspace/LOP/infrastructure/table/tools/protoc-28.2-win64/bin/protoc.exe --version
```
Expected: `libprotoc 28.2`

- [ ] **Step 4: Commit in infrastructure repo**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/tools/
git commit -m "chore: copy protoc 28.2 toolchain into infrastructure/table/tools/

Self-contained protoc for MasterData .proto compilation. Same binary as
LOP-Shared/Tools/Protobuf/ — Slice 2a infrastructure auto-sufficiency.

Future protoc upgrades require sync between LOP-Shared/Tools/Protobuf
and this copy (tracked in Slice 2a spec Open Decisions).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
git log --oneline -1
```

---

## Task 4: Create lop_options.proto (custom field options)

**Files:**
- Create: `infrastructure/table/proto/lop_options.proto`

- [ ] **Step 1: Create proto directory**

```bash
mkdir -p C:/Users/re5na/workspace/LOP/infrastructure/table/proto
```

- [ ] **Step 2: Write lop_options.proto**

File: `C:/Users/re5na/workspace/LOP/infrastructure/table/proto/lop_options.proto`
```protobuf
syntax = "proto3";
package lop;
import "google/protobuf/descriptor.proto";

// Custom field options for LOP MasterData schemas.
// Read by Slice 2b exporter to drive side projection + build-time validation.
extend google.protobuf.FieldOptions {
  // Side projection: "client" | "server" | "both" (생략 시 "both")
  string side     = 51001;

  // Foreign key reference: "TableName.column" 형식 (예: "Skin.code")
  string fk       = 51002;

  // 숫자 범위 제약: "min,max" (예: "0,30")
  string range    = 51003;

  // 빈 값(null/empty string) 금지
  bool   required = 51004;

  // 테이블 내 unique key
  bool   unique   = 51005;
}
```

- [ ] **Step 3: Commit** (deferred — combined with Task 5 to keep proto commits together)

(No commit yet — proceed to Task 5.)

---

## Task 5: Create 5 message .proto files

**Files:**
- Create: `infrastructure/table/proto/character.proto`
- Create: `infrastructure/table/proto/skin.proto`
- Create: `infrastructure/table/proto/skin_asset.proto`
- Create: `infrastructure/table/proto/action.proto`
- Create: `infrastructure/table/proto/item.proto`

- [ ] **Step 1: Write character.proto**

File: `C:/Users/re5na/workspace/LOP/infrastructure/table/proto/character.proto`
```protobuf
syntax = "proto3";
package LOP.MasterData;
import "lop_options.proto";

message Character {
  string code              = 1 [(lop.unique) = true, (lop.required) = true];
  string name              = 2 [(lop.required) = true];
  float  speed             = 3 [(lop.range)   = "0,30"];
  float  jump_power        = 4 [(lop.range)   = "0,30"];
  string description       = 5 [(lop.side)    = "client"];
  string default_skin_code = 6 [(lop.fk)      = "Skin.code"];
}

message CharacterTable {
  repeated Character entries = 1;
}
```

- [ ] **Step 2: Write skin.proto**

File: `C:/Users/re5na/workspace/LOP/infrastructure/table/proto/skin.proto`
```protobuf
syntax = "proto3";
package LOP.MasterData;
import "lop_options.proto";

message Skin {
  string code        = 1 [(lop.unique) = true, (lop.required) = true];
  string name        = 2 [(lop.required) = true];
  string description = 3 [(lop.side)    = "client"];
}

message SkinTable {
  repeated Skin entries = 1;
}
```

- [ ] **Step 3: Write skin_asset.proto** (client-only; every field tagged client)

File: `C:/Users/re5na/workspace/LOP/infrastructure/table/proto/skin_asset.proto`
```protobuf
syntax = "proto3";
package LOP.MasterData;
import "lop_options.proto";

message SkinAsset {
  string code       = 1 [(lop.unique)   = true, (lop.required) = true, (lop.side) = "client"];
  string skin_code  = 2 [(lop.fk)       = "Skin.code", (lop.side) = "client"];
  string model_path = 3 [(lop.required) = true, (lop.side) = "client"];
}

message SkinAssetTable {
  repeated SkinAsset entries = 1;
}
```

- [ ] **Step 4: Write action.proto**

File: `C:/Users/re5na/workspace/LOP/infrastructure/table/proto/action.proto`
```protobuf
syntax = "proto3";
package LOP.MasterData;
import "lop_options.proto";

message Action {
  string code        = 1 [(lop.unique) = true, (lop.required) = true];
  string name        = 2 [(lop.required) = true];
  string description = 3 [(lop.side)    = "client"];
  string class       = 4 [(lop.required) = true];
  float  duration    = 5 [(lop.range)   = "0,60"];
  float  cast_time   = 6 [(lop.range)   = "0,60"];
  float  cooldown    = 7 [(lop.range)   = "0,300"];
  float  hp_cost     = 8 [(lop.range)   = "0,9999"];
  float  mp_cost     = 9 [(lop.range)   = "0,9999"];
}

message ActionTable {
  repeated Action entries = 1;
}
```

- [ ] **Step 5: Write item.proto**

File: `C:/Users/re5na/workspace/LOP/infrastructure/table/proto/item.proto`
```protobuf
syntax = "proto3";
package LOP.MasterData;
import "lop_options.proto";

message Item {
  string code        = 1 [(lop.unique) = true, (lop.required) = true];
  string name        = 2 [(lop.required) = true];
  string description = 3 [(lop.side)    = "client"];
  string skin_code   = 4 [(lop.fk)      = "Skin.code"];
}

message ItemTable {
  repeated Item entries = 1;
}
```

- [ ] **Step 6: Commit all 6 protos (lop_options.proto + 5 messages)**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/proto/
git commit -m "feat: add LOP MasterData .proto schemas

- lop_options.proto: custom field options (side/fk/range/required/unique)
- character.proto: 6 fields, description client-only, defaultSkinCode FK→Skin
- skin.proto: 3 fields, description client-only
- skin_asset.proto: entirely client-only (3 fields all client-tagged),
  skinCode FK→Skin
- action.proto: 9 fields, description client-only, hp_cost/mp_cost ranged
- item.proto: 4 fields, description client-only, skinCode FK→Skin

Schema metadata = .proto (per Slice 2a design ② decision).
Slice 2b exporter will read these + Excel data + apply side projection.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
git log --oneline -1
```

---

## Task 6: Create compile_masterdata_protos.sh + verify all 6 protos compile

**Files:**
- Create: `infrastructure/table/scripts/compile_masterdata_protos.sh`

- [ ] **Step 1: Create scripts directory**

```bash
mkdir -p C:/Users/re5na/workspace/LOP/infrastructure/table/scripts
```

- [ ] **Step 2: Write compile_masterdata_protos.sh**

File: `C:/Users/re5na/workspace/LOP/infrastructure/table/scripts/compile_masterdata_protos.sh`
```bash
#!/bin/bash
# Slice 2a — compile verification only. Output is /tmp; actual .cs to packages
# comes in Slice 2b once the exporter is rewritten.
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TABLE_DIR="$(dirname "$SCRIPT_DIR")"
PROTOC="$TABLE_DIR/tools/protoc-28.2-win64/bin/protoc.exe"
PROTO_PATH="$TABLE_DIR/proto"
INCLUDE_PATH="$TABLE_DIR/tools/protoc-28.2-win64/include"

OUT_PATH="/tmp/lop-masterdata-cs-test"
rm -rf "$OUT_PATH"
mkdir -p "$OUT_PATH"

if [ ! -x "$PROTOC" ]; then
  echo "[error] protoc not found at $PROTOC"
  exit 1
fi

echo "[info] protoc: $($PROTOC --version)"
echo "[info] proto dir: $PROTO_PATH"
echo "[info] output: $OUT_PATH"
echo

PROTOS_COMPILED=0
for proto in "$PROTO_PATH"/*.proto; do
  if [ "$(basename "$proto")" = "lop_options.proto" ]; then
    # lop_options.proto은 다른 .proto가 import해서 자동 컴파일됨.
    # 단독 컴파일도 검증한다.
    echo "[compile] $(basename "$proto") (options definition)"
  else
    echo "[compile] $(basename "$proto")"
  fi
  "$PROTOC" \
    --proto_path="$PROTO_PATH" \
    --proto_path="$INCLUDE_PATH" \
    --csharp_out="$OUT_PATH" \
    "$proto"
  PROTOS_COMPILED=$((PROTOS_COMPILED + 1))
done

echo
echo "[ok] compiled $PROTOS_COMPILED .proto files"
echo "[ok] generated .cs files:"
ls -1 "$OUT_PATH"
```

- [ ] **Step 3: Make script executable**

```bash
chmod +x C:/Users/re5na/workspace/LOP/infrastructure/table/scripts/compile_masterdata_protos.sh
```

- [ ] **Step 4: Run the script — verify all 6 protos compile**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table/scripts
./compile_masterdata_protos.sh
```
Expected output:
- `[info] protoc: libprotoc 28.2`
- 6 `[compile]` lines (action / character / item / lop_options / skin / skin_asset — alphabetical)
- `[ok] compiled 6 .proto files`
- `[ok] generated .cs files:` then a list including:
  - `Action.cs` (or similar — protoc may rename to PascalCase based on package)
  - `Character.cs`
  - `Item.cs`
  - `LopOptions.cs`
  - `Skin.cs`
  - `SkinAsset.cs`

If protoc reports an error about `(lop.side)` not being a known option, verify `lop_options.proto` defines the extension and is on the `--proto_path` correctly.

- [ ] **Step 5: Inspect one generated .cs to confirm custom options are recognized**

```bash
head -30 /tmp/lop-masterdata-cs-test/Character.cs
```
Expected: file starts with `// <auto-generated>`, contains `namespace LOP.MasterData {`, and includes `public sealed partial class Character`. The custom options themselves don't appear in generated C# (protoc reads them but they're metadata, not codegen output) — this is correct behavior.

- [ ] **Step 6: Cleanup /tmp output**

```bash
rm -rf /tmp/lop-masterdata-cs-test
```

- [ ] **Step 7: Commit script**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add table/scripts/
git commit -m "feat: add compile_masterdata_protos.sh

Slice 2a verification: compile all .proto files into /tmp and confirm
exit 0. Actual .cs/.bin output into MasterData packages happens in
Slice 2b once the Python exporter is rewritten.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
git log --oneline -1
```

---

## Task 7: Register lop.masterdata.client in LOP-Client manifest

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Packages/manifest.json`

- [ ] **Step 1: Read current LOP-Client manifest.json**

```bash
cat C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Packages/manifest.json
```
Confirm `com.baegames.gameframework` and `com.baegames.lop.shared` lines exist (file: references) and `testables` block has both registered.

- [ ] **Step 2: Insert masterdata.client dependency**

In `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Packages/manifest.json`, add one line in `dependencies` (right after `com.baegames.lop.shared` line) and one line in `testables`:

```diff
   "dependencies": {
     "com.baegames.gameframework": "file:C:/Users/re5na/workspace/LOP/GameFramework",
     "com.baegames.lop.shared":    "file:C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared",
+    "com.baegames.lop.masterdata.client": "file:C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client",
     "org.nuget.google.protobuf":  "3.28.2",
     ...
   },
   "testables": [
     "com.baegames.gameframework",
-    "com.baegames.lop.shared"
+    "com.baegames.lop.shared",
+    "com.baegames.lop.masterdata.client"
   ]
```

- [ ] **Step 3: Verify Unity recompiles cleanly**

Open LOP-Client Unity Editor (or use UnityMCP `refresh_unity` if running headlessly — see CLAUDE.md instance targeting). Wait for Package Manager to resolve + script compilation to finish.

```bash
# Optional MCP check if Unity is running:
# refresh_unity with unity_instance="LeagueOfPhysical-Client@<hash>" first,
# then read_console (Errors filter) — expect 0 errors.
```
Expected: 0 compile errors, 0 import errors related to MasterData package.

- [ ] **Step 4: Verify Test Runner shows new asmdefs**

In Unity: **Window > General > Test Runner**. Confirm two new assemblies appear:
- `baegames.LOP.MasterData.Client.Tests.EditMode`
- `baegames.LOP.MasterData.Client.Tests.PlayMode`

(Both will be empty — that's expected per spec.)

- [ ] **Step 5: Commit**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Packages/manifest.json
git commit -m "build(client): register com.baegames.lop.masterdata.client

file: reference to ../LeagueOfPhysical-MasterData-Client (Slice 2a
bootstrap). Package is currently an empty skeleton; runtime continues
to use CSV via LOPMasterDataManager (no behavior change).

Slice 2b will populate Runtime.Generated/Scripts/MasterData (.cs from
protoc) and Runtime.Generated/StreamingAssets/MasterData (.bin).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
git log --oneline -1
```

---

## Task 8: Register lop.masterdata.server in LOP-Server manifest

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Packages/manifest.json`

- [ ] **Step 1: Read current LOP-Server manifest.json**

```bash
cat C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Packages/manifest.json
```
Confirm `com.baegames.gameframework` and `com.baegames.lop.shared` lines exist (file: references).

- [ ] **Step 2: Insert masterdata.server dependency**

In `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Packages/manifest.json`, add one line in `dependencies` and one line in `testables`:

```diff
   "dependencies": {
     "com.baegames.gameframework": "file:C:/Users/re5na/workspace/LOP/GameFramework",
     "com.baegames.lop.shared":    "file:C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared",
+    "com.baegames.lop.masterdata.server": "file:C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server",
     "org.nuget.google.protobuf":  "3.28.2",
     ...
   },
   "testables": [
     "com.baegames.gameframework",
-    "com.baegames.lop.shared"
+    "com.baegames.lop.shared",
+    "com.baegames.lop.masterdata.server"
   ]
```

- [ ] **Step 3: Verify Unity recompiles cleanly**

Open LOP-Server Unity Editor and wait for Package Manager + script compilation to finish.

Expected: 0 compile errors.

- [ ] **Step 4: Verify Test Runner shows new asmdefs**

In Unity Test Runner, confirm:
- `baegames.LOP.MasterData.Server.Tests.EditMode`
- `baegames.LOP.MasterData.Server.Tests.PlayMode`

- [ ] **Step 5: Commit**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Packages/manifest.json
git commit -m "build(server): register com.baegames.lop.masterdata.server

file: reference to ../LeagueOfPhysical-MasterData-Server (Slice 2a
bootstrap). Package is currently an empty skeleton; runtime continues
to use CSV via LOPMasterDataManager (no behavior change).

Slice 2b will populate the package with server-projection schemas (no
description columns) and .bin data.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
git log --oneline -1
```

---

## Task 9: Push both new repos to GitHub (user action)

This is a **manual step** the user performs. The plan must call it out so the implementer pauses and verifies.

- [ ] **Step 1: Ask the user to create both GitHub repos and push**

Suggested commands (the user can use either `gh` CLI or the GitHub UI). For each new repo:

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
gh repo create Baeinsoo/LeagueOfPhysical-MasterData-Client --public --source=. --remote=origin --push

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
gh repo create Baeinsoo/LeagueOfPhysical-MasterData-Server --public --source=. --remote=origin --push
```

(Use `--private` instead if the user prefers private.)

- [ ] **Step 2: Verify origin/main matches local for both**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git ls-remote origin main

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git ls-remote origin main
```
Expected: each command outputs the local `main` SHA matching `git rev-parse main`.

---

## Task 10: Update lop-repo-topology.md (6 → 8 repos)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/docs/lop-repo-topology.md`

This task updates the topology document to reflect:
1. Two new repos (MasterData-Client/Server)
2. infrastructure repo now also gets MasterData pipeline (proto/, tools/, scripts/)
3. MasterData package mutual non-reference (security)
4. Updated dependency graph

- [ ] **Step 1: Read current lop-repo-topology.md**

```bash
cat C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/docs/lop-repo-topology.md | head -80
```
Identify the "5개 저장소" table and the "의존 그래프" section.

- [ ] **Step 2: Replace the 5-row table with an 8-row table**

In `docs/lop-repo-topology.md`, find the section heading `## 5개 저장소` and replace it with `## 8개 저장소`, then update the table:

```markdown
## 8개 저장소

| 저장소 | 종류 | 역할 |
|---|---|---|
| **GameFramework** (`github.com/Baeinsoo/GameFramework.git`) | Unity 패키지 (`com.baegames.gameframework`) | 앱 비종속 엔진 인프라 — 결정론 시뮬 추상, World Core(Entity/Component/EntityRegistry), wire 추상(`IMessage`), MasterData 추상(`IMasterData`/`IMasterDataManager`/`MasterDataLoader`), FSM 등. 다음 프로젝트에서도 그대로 재사용 |
| **LeagueOfPhysical-Shared** (`github.com/Baeinsoo/LeagueOfPhysical-Shared.git`) | Unity 패키지 (`com.baegames.lop.shared`) | **LOP 도메인 공통 — 시뮬·proto·메시지** — proto 산출물(wire), 메시지 인프라(`MessageFactory`/`MessageHandler<T>`/`MessageIds`/`MessageInitializer`), `LOPGameSimulation`(시뮬 코어, Slice 4부터). **MasterData는 의도적으로 비포함**(아래 LOP-MasterData-* 참조). |
| **LeagueOfPhysical-MasterData-Client** (`github.com/Baeinsoo/LeagueOfPhysical-MasterData-Client.git`) | Unity 패키지 (`com.baegames.lop.masterdata.client`) | **클라 전용 MasterData** — Protobuf schema(`.cs` from protoc) + 데이터(`.bin`). description 등 클라 전용 컬럼 포함. Slice 2a부터 부트스트랩, Slice 2b부터 자동 채워짐. |
| **LeagueOfPhysical-MasterData-Server** (`github.com/Baeinsoo/LeagueOfPhysical-MasterData-Server.git`) | Unity 패키지 (`com.baegames.lop.masterdata.server`) | **서버 전용 MasterData** — server projection schema(클라 전용 컬럼 제외) + 데이터. **클라 패키지와 상호 비참조** — 코드 레벨 격리로 DTO/보안. |
| **LeagueOfPhysical-Client** | Unity 프로젝트 | 클라 특화 — `LOPGameEngine`(host 측), View(`LOPEntityView`, `DamageView`), 보정(`SnapReconciler`, `ServerStateReconciler`, `SnapInterpolator`), 매칭/로비, 입력 캡처. |
| **LeagueOfPhysical-Server** | Unity 프로젝트 (Mirror 호스트) | 서버 특화 — `LOPGameEngine`(host 측), AI(`LOPAIController`), 와이어 송신(`WireBroadcaster`), `EntityInputComponent`, EntityCreationDataFactory. |
| **LeagueOfPhysical-Art** (`github.com/Baeinsoo/LeagueOfPhysical-Art.git`) | git submodule | 디자이너 asset — 클라/서버 양쪽 `Assets/Art/`에 mount. |
| **infrastructure** | Node.js + Python | RoomServer 인프라(`k8s/`) + **MasterData 파이프라인**(`table/`): Excel `source/` → `.proto` schema → protoc → `.cs` + `.bin` → MasterData-Client/Server 패키지로 출력. Slice 2a에 `table/proto/` + `table/tools/protoc/` + `table/scripts/` 추가. |

> **LeagueOfPhysical-RoomServer**는 Node.js 프로젝트(prisma/k8s)라 이 토폴로지의 범위 밖.
```

- [ ] **Step 3: Replace the dependency graph**

Find the section `## 의존 그래프` and replace the ASCII graph:

````markdown
## 의존 그래프

```
External (Mirror, Google.Protobuf via UnityNuGet, R3, VContainer, UniTask, AutoMapper)
              ▲
              │
       GameFramework            (앱 비종속, 재사용)
              ▲
              ├─────────────────────────────────────┐
              │                                     │
       LOP-Shared                            LOP-MasterData-Client
       (시뮬·proto·메시지, MasterData X)     LOP-MasterData-Server
              ▲                                     ▲ (상호 비참조)
              │                                     │
   ┌──────────┴──────────┐                          │
LOP-Client             LOP-Server  ─────────────────┘
   (각자 자기 사이드 MasterData 패키지만 참조)

  infrastructure/table/
   ├─ source (Excel — 진실원본 데이터)
   ├─ proto  (진실원본 schema, custom options)
   ├─ tools/protoc + scripts
   └─ exporter (Slice 2b부터 — Excel + .proto → MasterData-Client/Server에 .cs + .bin)
```

- 역참조 금지: Shared → Client/Server (X), GameFramework → Shared (X), MasterData → Client/Server (X), MasterData-Client ↔ MasterData-Server (X — 보안)
- LOP-Shared가 Mirror에 *직접* 의존하지 않는다 (wire 인터페이스가 `GameFramework.IMessage`라 가능). 네트워크 transport는 use-side(Client/Server) 책임.
- **MasterData 패키지 상호 비참조**: 클라가 server-only 컬럼(`(lop.side) = "server"`)을 *코드 레벨에서 알지 못함*. DTO 패턴.
````

- [ ] **Step 4: Add a new section "MasterData 패키지 구조" after "LOP-Shared 패키지 구조" section**

Locate `## LOP-Shared 패키지 구조` and add this new section just after it (before `## use-side 계약`):

````markdown
## MasterData 패키지 구조 (Slice 2a부터)

```
LeagueOfPhysical-MasterData-{Client,Server}/
├─ package.json                                       com.baegames.lop.masterdata.{client,server}
├─ README.md                                          (use-side 계약 — direct edit 금지)
├─ .gitignore
│
├─ Runtime/
│   ├─ baegames.LOP.MasterData.{Client,Server}.Runtime.asmdef
│   └─ Scripts/                                       (수기 매니저 등 — Slice 2c에서 채움)
│
├─ Runtime.Generated/
│   ├─ baegames.LOP.MasterData.{Client,Server}.Generated.asmdef
│   ├─ Scripts/MasterData/                            (.cs from protoc — Slice 2b부터)
│   └─ StreamingAssets/MasterData/                    (.bin — Slice 2b부터)
│
├─ Editor/baegames.LOP.MasterData.{Client,Server}.Editor.asmdef
└─ Tests/{EditMode,PlayMode}/                         (자리 잡기 — 채우지 않음)
```

### asmdef 의존 그래프 (각 패키지 내부)

```
External (UnityEngine, Google.Protobuf.dll)
       ▲
       │
GameFramework.Runtime
       ▲
       ├──────────────────────┐
       │                      │
LOP.MasterData.{Side}.Runtime ◄── LOP.MasterData.{Side}.Generated
       ▲                                       ▲
       │                                       │
LOP.MasterData.{Side}.Editor          (UseSide: Assembly-CSharp)
LOP.MasterData.{Side}.Tests.{EditMode,PlayMode}
```

### asmdef 정책

| asmdef | references | autoReferenced | overrideReferences | precompiledReferences |
|---|---|---|---|---|
| Runtime | `baegames.GameFramework.Runtime` | true | false | (없음) |
| Generated | Runtime, GameFramework.Runtime | true | true | `Google.Protobuf.dll` |
| Editor | Runtime, Generated | false | false | (없음) |
| Tests.EditMode | Runtime, Generated, GameFramework.Runtime | (TestAssemblies) | false | (없음) |
| Tests.PlayMode | Runtime, Generated, GameFramework.Runtime | (TestAssemblies) | false | (없음) |

- **Generated.asmdef가 GameFramework.Runtime을 *직접* 참조**: protoc 생성 `.cs`가 `GameFramework.IMasterData`를 구현. asmdef는 전이 참조를 따르지 않으므로 직접 명시 필요 (Slice 1에서 발견된 동일 이슈).
- **Auto Referenced: true** (Runtime, Generated) — use-side 편의. Editor/Tests는 false.
- 네임스페이스 `LOP.MasterData` 유지.
````

- [ ] **Step 5: Update the "코드 분배 기준" section**

Find the decision tree under `## 코드 분배 기준` and add MasterData case. Insert a new bullet point between the `LOP-Shared` and "한쪽만 쓰는 I/O" lines:

```markdown
1. **앱 비종속 인프라**(다른 게임에도 그대로 쓸 만한 추상·골격)인가? → **GameFramework**
   - 예: `IGameSimulation`, `GameSimulationBase`, `ITickUpdater`, `IInputSource`, `IEventSink`, `EntityRegistry`, `WorldEventBuffer`, `IMasterData`, `IMasterDataManager`, `MasterDataLoader`
2. **LOP 도메인이고 클·서 양쪽이 *반드시 동일하게* 보아야 하는가**? → **LOP-Shared**
   - 예: proto-generated `.cs`(wire), `MessageIds`, `LOPGameSimulation`(시뮬 조립)
3. **LOP 도메인이지만 *클·서가 다른 projection*을 봐야 하는가**(DTO/보안)? → **LOP-MasterData-{Client,Server}**
   - 예: Character/Skin/Action/Item 등 게임 디자인 데이터. 클라는 description 포함, 서버는 제외.
4. **한쪽만 쓰는 I/O·View·정책**인가? → **각자**(Client / Server)
   - 예: `SnapReconciler`(클라), `LOPAIController`(서버), `LOPGameEngine`(호스트 — 양쪽 *서로 다른* 본문)
```

- [ ] **Step 6: Update Open Decisions section**

Append to existing Open Decisions list at end of file:

```markdown
- [ ] **MasterData 패키지 `file:` → git URL + tag 전환 시점** — Slice 2c 안정화 후 결정. 다른 패키지(GameFramework, LOP-Shared)와 *함께* 전환할지 별도 일정인지.
- [ ] **MasterData 패키지 message-level `(lop.side)`** — 현재 field-level만. SkinAsset 같은 *전체 client-only* message는 모든 field에 `(lop.side) = "client"` 명시 중. message-level 옵션은 2b/2c에서 도입 결정.
- [ ] **protoc 도구 다중 저장소 sync** — LOP-Shared/Tools/Protobuf와 infrastructure/table/tools/protoc 두 곳에 사본. 업그레이드 시 수기 sync. 공통 도구 저장소 분리 검토.
```

- [ ] **Step 7: Commit**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/lop-repo-topology.md
git commit -m "docs(topology): expand to 8 repos with MasterData packages

- Add LOP-MasterData-Client and LOP-MasterData-Server rows
- Restructure infrastructure repo description to include table/ pipeline
- Update dependency graph with MasterData package fan-out from
  GameFramework and mutual non-reference between client/server
- Add new section: MasterData 패키지 구조 (folder layout + asmdef policy)
- Update code-placement decision tree to include MasterData case (DTO/보안)
- Add Open Decisions: file:→git URL transition, message-level side,
  protoc multi-repo sync

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
git log --oneline -1
```

---

## Task 11: Final 2a baseline verification

This task confirms the 2a invariants per the spec's "2a 검증 (baseline)" section.

- [ ] **Step 1: Client Unity compilation pass**

Open LOP-Client Unity (or use UnityMCP):
```
read_console (Errors filter) — expected 0 errors
```
The `baegames.LOP.MasterData.Client.Runtime` and `Generated` asmdefs should appear in the project's assembly list but produce 0 errors (empty Runtime/Scripts/MasterData/ folders compile cleanly).

- [ ] **Step 2: Server Unity compilation pass**

Same check on LOP-Server Unity.

- [ ] **Step 3: Test Runner asmdef visibility**

In both Unity editors, **Window > General > Test Runner** must list:
- (Client) `baegames.LOP.MasterData.Client.Tests.EditMode`, `...Tests.PlayMode`
- (Server) `baegames.LOP.MasterData.Server.Tests.EditMode`, `...Tests.PlayMode`

All four are empty — expected.

- [ ] **Step 4: Play one round + World Core slice 3 regression**

Start the server first (LOP-Server Play Mode), then connect from client (LOP-Client Play Mode). Run one combat exchange (spawn → damage → death).

Expected console output on client side:
- Standard EntityCreated/EntityDespawn flow
- `[World] Death entity <id> (killer=<id>)` log line on death events
- 0 errors, 0 warnings related to MasterData

This confirms the CSV-based runtime flow is unchanged.

- [ ] **Step 5: Verify infrastructure protoc still compiles all 6 schemas**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table/scripts
./compile_masterdata_protos.sh
```
Expected: `[ok] compiled 6 .proto files` (regression check after any later changes).

- [ ] **Step 6: Report Slice 2a complete**

If all of Steps 1–5 pass, announce: "Slice 2a baseline complete. Ready for finishing-a-development-branch."

Then invoke `superpowers:finishing-a-development-branch` to choose merge/PR/keep/discard for each of the 4 branches that contain Slice 2a commits:
- LOP-Client: `feature/master-data-slice-2a-bootstrap` (spec + manifest + topology doc)
- LOP-Server: `feature/master-data-slice-2a-bootstrap` (manifest only)
- infrastructure: `feature/master-data-slice-2a-bootstrap` (proto/, tools/, scripts/)
- LOP-MasterData-Client: `main` (single bootstrap commit)
- LOP-MasterData-Server: `main` (single bootstrap commit)

Per Slice 1 pattern, expected choice: **Option 1 — Merge back to main** for the three feature branches (`--no-ff` to preserve commit history per project convention). The two new MasterData repos already live on `main` so they just push.

---

## Spec coverage check

| Spec requirement | Plan task(s) |
|---|---|
| 2 new git repos (MasterData-Client/Server) | T1, T2, T9 |
| 5 asmdef per package | T1, T2 |
| package.json | T1, T2 |
| README.md + .gitignore | T1, T2 |
| `infrastructure/table/proto/lop_options.proto` | T4 |
| `infrastructure/table/proto/{character,skin,skin_asset,action,item}.proto` | T5 |
| `infrastructure/table/tools/protoc-28.2-win64/` copy from LOP-Shared | T3 |
| `infrastructure/table/scripts/compile_masterdata_protos.sh` | T6 |
| Compile verification (5 + 1 protos) | T6 step 4, T11 step 5 |
| LOP-Client manifest + testables | T7 |
| LOP-Server manifest + testables | T8 |
| Topology doc 6→8 repos | T10 |
| Game one round play (CSV preserved) | T11 step 4 |
| World Core slice 3 regression | T11 step 4 |
| Test Runner shows new asmdefs | T7 step 4, T8 step 4, T11 step 3 |

All spec sections covered. No gaps.
