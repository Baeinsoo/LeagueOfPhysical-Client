# LOP-Shared Package Bootstrap & Wire/Proto Migration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 새 git 저장소 `LeagueOfPhysical-Shared`(`com.baegames.lop.shared`)를 부트스트랩하고, 클·서 양쪽의 wire/proto 산출물 + codegen 파이프라인(.proto 진실원본, protoc 도구, .sh 스크립트)을 단일 진실원본으로 이전한다. 결과: 양쪽이 file: 참조로 패키지를 인식, 메시지 송수신 정상, 한 라운드 플레이 통과.

**Architecture:** GameFramework 패턴(Unity 패키지 + file: 참조 + 직접 편집)을 따른다. 5개 asmdef(Runtime / Runtime.Generated / Editor / Tests.EditMode / Tests.PlayMode)로 어셈블리 분리. Google.Protobuf는 NuGetForUnity → UnityNuGet UPM 전환. namespace `LOP` 유지로 호출부 변경 0줄. 자세한 책임 분리는 [spec](../specs/2026-05-31-lop-shared-package-design.md) 참조.

**Tech Stack:** Unity 2022.3 (양쪽), Unity Package Manager (file: 참조), VContainer, Mirror (use-side), Google.Protobuf 3.28.2 (UnityNuGet UPM 전환), NuGetForUnity (AutoMapper만 유지), Bash (codegen .sh), protoc 28.2 (Windows x64).

**Sources of truth this plan references:**
- Spec: `docs/superpowers/specs/2026-05-31-lop-shared-package-design.md`
- Topology: `docs/lop-repo-topology.md`
- World Core architecture: `docs/world-core-connection-architecture.md`

---

## Working Tree Map

### 신규 저장소: `LeagueOfPhysical-Shared/` (다른 워크스페이스 폴더)

```
C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/
├─ .gitignore                                                 (T1 신규)
├─ README.md                                                  (T2 신규)
├─ package.json                                               (T2 신규)
├─ Runtime/
│   └─ baegames.LOP.Shared.Runtime.asmdef                     (T3 신규)
├─ Runtime.Generated/
│   └─ baegames.LOP.Shared.Generated.asmdef                   (T3 신규)
├─ Editor/
│   └─ baegames.LOP.Shared.Editor.asmdef                      (T3 신규)
├─ Tests/EditMode/
│   └─ baegames.LOP.Shared.Tests.EditMode.asmdef              (T3 신규)
├─ Tests/PlayMode/
│   └─ baegames.LOP.Shared.Tests.PlayMode.asmdef              (T3 신규)
│
│ (Slice 1에서 채움)
├─ Runtime/Scripts/Network/Message/
│   ├─ MessageFactory.cs(.meta)                               (T10)
│   └─ MessageHandler.cs(.meta)                               (T10)
├─ Runtime.Generated/Scripts/
│   ├─ MessageIds.cs(.meta)                                   (T10)
│   ├─ MessageInitializer.cs(.meta)                           (T10)
│   └─ Protobuf/*.cs(.meta)  (~28 proto + ~13 IMessage)       (T10)
├─ Protos/*.proto  (~24)                                       (T11)
├─ Tools/Protobuf/protoc-28.2-win64/                          (T11)
└─ Scripts/{compile,generate_imessage,generate_message_ids,generate_message_initializer,generate_protos}.sh  (T11, T12)
```

### 변경: `LeagueOfPhysical-Client/`

```
C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/
├─ Packages/manifest.json                          (T4 수정)
├─ Assets/NuGetForUnity/packages.config            (T6 수정)
├─ Assets/Scripts/Network/Message/                 (T10 제거)
├─ Assets/Scripts/Generated/                       (T10 제거)
├─ Protos/                                          (T11 제거)
├─ Tools/Protobuf/                                  (T11 제거)
├─ Scripts/compile_protos.sh                       (T11 제거)
├─ Scripts/generate_imessage.sh                    (T11 제거)
├─ Scripts/generate_message_ids.sh                 (T11 제거)
├─ Scripts/generate_message_initializer.sh         (T11 제거)
├─ Scripts/generate_protos.sh                      (T11 제거)
└─ Scripts/upload-{apk,serverdata}-s3.sh           (유지)
```

### 변경: `LeagueOfPhysical-Server/`

```
C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/
├─ Packages/manifest.json                          (T5 수정)
├─ Assets/NuGetForUnity/packages.config            (T7 수정)
├─ Assets/Scripts/Network/Message/                 (T14 제거)
├─ Assets/Scripts/Generated/                       (T14 제거)
├─ Protos/                                          (T14 제거)
├─ Tools/Protobuf/                                  (T14 제거)
└─ Scripts/{compile,generate}_*.sh                 (T14 제거)
```

---

## SLICE 0 — Bootstrap (코드 0줄 이전, 양쪽 컴파일 기준선)

### Task 1: Create Shared repo skeleton + .gitignore

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/.gitignore`
- Create: 폴더 구조 `Runtime/Scripts/`, `Runtime.Generated/Scripts/`, `Editor/Scripts/`, `Tests/EditMode/`, `Tests/PlayMode/`

- [ ] **Step 1.1: Create Shared repo directory + init git**

Run:
```bash
mkdir -p C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git init
git config user.name "Baeinsoo"
git config user.email "$(git -C ../LeagueOfPhysical-Client config user.email)"
```

Expected: empty git repo. `git status` says "On branch main" or "No commits yet".

- [ ] **Step 1.2: Create folder skeleton**

Run:
```bash
mkdir -p Runtime/Scripts Runtime.Generated/Scripts Editor/Scripts Tests/EditMode Tests/PlayMode
```

Verify:
```bash
find . -type d ! -path "./.git*" | sort
```

Expected:
```
.
./Editor
./Editor/Scripts
./Runtime
./Runtime/Scripts
./Runtime.Generated
./Runtime.Generated/Scripts
./Tests
./Tests/EditMode
./Tests/PlayMode
```

- [ ] **Step 1.3: Write `.gitignore`**

Write `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/.gitignore`:

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

Note: `Runtime.Generated/Scripts/*`와 `Tools/Protobuf/protoc-28.2-win64/`는 *커밋* (use-side 사용을 위해). 위 .gitignore에는 그것들을 제외하는 항목 없음 — OK.

- [ ] **Step 1.4: Verify clean state**

Run:
```bash
git status --short
```

Expected: `?? .gitignore` (untracked, ready to add).

---

### Task 2: Write package.json + README

**Files:**
- Create: `LeagueOfPhysical-Shared/package.json`
- Create: `LeagueOfPhysical-Shared/README.md`

- [ ] **Step 2.1: Write `package.json`**

Write `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/package.json`:

```json
{
    "name": "com.baegames.lop.shared",
    "version": "0.0.1",
    "displayName": "LOP Shared",
    "description": "League of Physical 클라/서버 공유 도메인 (proto 메시지, 메시지 인프라, 향후 MasterData/Simulation).",
    "unity": "2022.3",
    "author": { "name": "insoo.bae" },
    "dependencies": {
        "com.baegames.gameframework": "0.0.2"
    }
}
```

- [ ] **Step 2.2: Write `README.md`**

Write `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/README.md`:

```markdown
# LeagueOfPhysical-Shared (com.baegames.lop.shared)

League of Physical 클라/서버가 공유 사용하는 도메인 Unity 패키지.

## 책임

- proto 산출물 (wire 메시지 클래스)
- 메시지 인프라 (MessageFactory, MessageHandler<T>, MessageIds, MessageInitializer)
- (Slice 2+) MasterData 스키마/로더
- (Slice 4+) `LOPGameSimulation` — 결정론 시뮬 코어

## Use-side Requirements

이 패키지에 의존하는 프로젝트는 다음을 제공해야 한다:

- `com.baegames.gameframework` (이 패키지의 `dependencies`에 선언됨)
- `org.nuget.google.protobuf` 3.28.x (UnityNuGet scoped registry로 설치)
- Mirror (Asset Store / git URL)
- (이후 슬라이스에서 사용 시) R3, VContainer, UniTask

상세 토폴로지: 사용 측 저장소의 `docs/lop-repo-topology.md` 참조.

## Editing

패키지는 use-side `Packages/manifest.json`에서 `file:` 참조로 들어와 있다. 이 폴더 안에서 직접 편집·커밋·push.

## Codegen

`.proto` 정의는 `Protos/`, 도구는 `Tools/Protobuf/`, 스크립트는 `Scripts/`. 새 메시지 추가 시:
1. `Protos/*.proto` 수정
2. `Scripts/`에서 `./generate_protos.sh`
3. 산출물이 `Runtime.Generated/Scripts/`에 생성
4. 사용 측 Unity가 자동 reimport
```

- [ ] **Step 2.3: Verify files**

Run:
```bash
ls -la package.json README.md
cat package.json | head -5
```

Expected: both files exist, `package.json` starts with `{` and shows `com.baegames.lop.shared`.

---

### Task 3: Write 5 asmdefs

**Files (all in `LeagueOfPhysical-Shared/`):**
- Create: `Runtime/baegames.LOP.Shared.Runtime.asmdef`
- Create: `Runtime.Generated/baegames.LOP.Shared.Generated.asmdef`
- Create: `Editor/baegames.LOP.Shared.Editor.asmdef`
- Create: `Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef`
- Create: `Tests/PlayMode/baegames.LOP.Shared.Tests.PlayMode.asmdef`

- [ ] **Step 3.1: Write Runtime asmdef**

Write `Runtime/baegames.LOP.Shared.Runtime.asmdef`:

```json
{
    "name": "baegames.LOP.Shared.Runtime",
    "rootNamespace": "LOP",
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

- [ ] **Step 3.2: Write Runtime.Generated asmdef**

Write `Runtime.Generated/baegames.LOP.Shared.Generated.asmdef`:

```json
{
    "name": "baegames.LOP.Shared.Generated",
    "rootNamespace": "",
    "references": ["baegames.LOP.Shared.Runtime"],
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

- [ ] **Step 3.3: Write Editor asmdef**

Write `Editor/baegames.LOP.Shared.Editor.asmdef`:

```json
{
    "name": "baegames.LOP.Shared.Editor",
    "rootNamespace": "LOP.Editor",
    "references": [
        "baegames.LOP.Shared.Runtime",
        "baegames.LOP.Shared.Generated"
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

- [ ] **Step 3.4: Write Tests.EditMode asmdef**

Write `Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef`:

```json
{
    "name": "baegames.LOP.Shared.Tests.EditMode",
    "rootNamespace": "LOP.Tests",
    "references": [
        "baegames.LOP.Shared.Runtime",
        "baegames.LOP.Shared.Generated",
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

- [ ] **Step 3.5: Write Tests.PlayMode asmdef**

Write `Tests/PlayMode/baegames.LOP.Shared.Tests.PlayMode.asmdef`:

```json
{
    "name": "baegames.LOP.Shared.Tests.PlayMode",
    "rootNamespace": "LOP.Tests",
    "references": [
        "baegames.LOP.Shared.Runtime",
        "baegames.LOP.Shared.Generated",
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

- [ ] **Step 3.6: Verify all 5 asmdef files**

Run:
```bash
find . -name "*.asmdef" | sort
```

Expected:
```
./Editor/baegames.LOP.Shared.Editor.asmdef
./Runtime.Generated/baegames.LOP.Shared.Generated.asmdef
./Runtime/baegames.LOP.Shared.Runtime.asmdef
./Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef
./Tests/PlayMode/baegames.LOP.Shared.Tests.PlayMode.asmdef
```

Run:
```bash
for f in $(find . -name "*.asmdef"); do echo "=== $f ==="; python -c "import json; json.load(open('$f'))" && echo "valid JSON"; done
```

Expected: each file prints "valid JSON" (or substitute any JSON validator).

> Note: Unity Editor가 실행되면 각 .asmdef 옆에 `.meta` 파일을 자동 생성. 우리는 직접 만들지 않음 — 이후 Unity가 만들 것 (Task 8).

---

### Task 4: Initial commit + push Shared repo

**Files:** N/A (commit only)

- [ ] **Step 4.1: Stage all bootstrap files**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add -A
git status --short
```

Expected:
```
A  .gitignore
A  Editor/baegames.LOP.Shared.Editor.asmdef
A  README.md
A  Runtime.Generated/baegames.LOP.Shared.Generated.asmdef
A  Runtime/baegames.LOP.Shared.Runtime.asmdef
A  Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef
A  Tests/PlayMode/baegames.LOP.Shared.Tests.PlayMode.asmdef
A  package.json
```

- [ ] **Step 4.2: Initial commit**

```bash
git commit -m "chore: bootstrap LOP-Shared package skeleton

- package.json: com.baegames.lop.shared 0.0.1, depends on com.baegames.gameframework 0.0.2
- 5 asmdef stubs: Runtime, Runtime.Generated (precompiledRef Google.Protobuf.dll), Editor, Tests.EditMode, Tests.PlayMode
- README.md with use-side contract
- .gitignore (Unity package standard)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

Verify:
```bash
git log --oneline
```

Expected: one commit on `main` (or `master`).

- [ ] **Step 4.3: Ask user — private/public + create + push remote**

**USER DECISION REQUIRED**: GitHub repo `Baeinsoo/LeagueOfPhysical-Shared` — public or private?

Once decided, run (replace `--private` with `--public` if chosen):
```bash
gh repo create Baeinsoo/LeagueOfPhysical-Shared --private --source=. --remote=origin --push
```

Expected: GitHub repo created, current branch pushed.

Verify:
```bash
git remote -v
git log --oneline origin/main
```

Expected: `origin` points to the new GitHub URL; `origin/main` matches local.

---

### Task 5: Wire LOP-Client manifest to Shared + UnityNuGet

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Packages/manifest.json`

> **Branch context:** Client work happens in the feature worktree at `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/feature+lop-shared-bootstrap/`. The branch is `worktree-feature+lop-shared-bootstrap`.

- [ ] **Step 5.1: Read current manifest**

Read `LeagueOfPhysical-Client/Packages/manifest.json` (in the worktree) and note the line that ends `"com.baegames.gameframework": "file:..."`.

- [ ] **Step 5.2: Add scopedRegistries section**

Add at the top of the JSON object, before `"dependencies"`:

```json
  "scopedRegistries": [
    {
      "name": "Unity NuGet",
      "url": "https://unitynuget-registry.openupm.com",
      "scopes": ["org.nuget"]
    }
  ],
```

- [ ] **Step 5.3: Add two dependencies**

Inside the `"dependencies"` object, add (alphabetical position after `com.baegames.gameframework`):

```json
    "com.baegames.lop.shared":    "file:C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared",
    "org.nuget.google.protobuf":  "3.28.2",
```

- [ ] **Step 5.4: Add to `testables` array**

Update the `testables` array from:
```json
  "testables": [
    "com.baegames.gameframework"
  ]
```
to:
```json
  "testables": [
    "com.baegames.gameframework",
    "com.baegames.lop.shared"
  ]
```

- [ ] **Step 5.5: Verify manifest JSON is valid**

Run:
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/feature+lop-shared-bootstrap
python -c "import json; json.load(open('Packages/manifest.json')); print('OK')"
```

Expected: `OK`.

---

### Task 6: Remove Google.Protobuf from LOP-Client NuGetForUnity

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/NuGetForUnity/packages.config`
- Remove (via Unity Restore): `LeagueOfPhysical-Client/Assets/NuGetForUnity/Packages/Google.Protobuf.3.28.2/`

- [ ] **Step 6.1: Read packages.config**

Read `Assets/NuGetForUnity/packages.config`. Verify it contains:
```xml
<package id="Google.Protobuf" version="3.28.2" manuallyInstalled="true" />
```

- [ ] **Step 6.2: Remove Google.Protobuf line**

Edit `Assets/NuGetForUnity/packages.config` — delete the line:
```diff
-  <package id="Google.Protobuf" version="3.28.2" manuallyInstalled="true" />
```

Keep `AutoMapper` and `System.Runtime.CompilerServices.Unsafe` entries.

- [ ] **Step 6.3: Verify resulting XML**

Run:
```bash
python -c "import xml.etree.ElementTree as ET; root = ET.parse('Assets/NuGetForUnity/packages.config').getroot(); pkgs = [p.get('id') for p in root]; assert 'Google.Protobuf' not in pkgs, pkgs; print('OK, remaining:', pkgs)"
```

Expected: `OK, remaining: ['AutoMapper', 'System.Runtime.CompilerServices.Unsafe']`.

- [ ] **Step 6.4: USER ACTION — Open Unity Client, run NuGet Restore**

**MANUAL STEP** (Claude cannot automate Unity Editor UI):

> Open Unity Client (`LeagueOfPhysical-Client`). Wait for package resolve (UnityNuGet may take 30-60s to fetch `org.nuget.google.protobuf` first time). Then **NuGet → Manage NuGet Packages → Restore**. Confirm `Assets/NuGetForUnity/Packages/Google.Protobuf.3.28.2/` folder is removed.

After Unity finishes:

Run:
```bash
ls Assets/NuGetForUnity/Packages 2>/dev/null
```

Expected: no `Google.Protobuf.3.28.2` folder. (May contain `AutoMapper.11.0.0`, `System.Runtime.CompilerServices.Unsafe.4.5.2`.)

---

### Task 7: Mirror manifest + NuGet changes to LOP-Server

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Packages/manifest.json`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/NuGetForUnity/packages.config`

> **Branch context:** LOP-Server is a separate git repository. Create a feature branch there using normal `git checkout -b` (no native worktree tool needed for server — this is a separate repo).

- [ ] **Step 7.1: Create feature branch in LOP-Server**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git checkout -b feature/lop-shared-bootstrap
```

Note any pre-existing modified files — if present, ask user before stashing.

- [ ] **Step 7.2: Modify server manifest.json**

Apply the same three changes as Task 5 (Steps 5.2, 5.3, 5.4) to `LeagueOfPhysical-Server/Packages/manifest.json`. Identical edits.

- [ ] **Step 7.3: Modify server packages.config**

Apply the same deletion as Task 6 (Step 6.2) to `LeagueOfPhysical-Server/Assets/NuGetForUnity/packages.config`.

- [ ] **Step 7.4: Verify both server files**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
python -c "import json; json.load(open('Packages/manifest.json')); print('manifest OK')"
python -c "import xml.etree.ElementTree as ET; root = ET.parse('Assets/NuGetForUnity/packages.config').getroot(); pkgs = [p.get('id') for p in root]; assert 'Google.Protobuf' not in pkgs; print('packages.config OK, remaining:', pkgs)"
```

Expected: `manifest OK`, `packages.config OK, remaining: [...]`.

- [ ] **Step 7.5: USER ACTION — Open Unity Server, NuGet Restore**

**MANUAL STEP**: Open Unity Server, wait for package resolve, then NuGet Restore. Confirm `Assets/NuGetForUnity/Packages/Google.Protobuf.3.28.2/` removed.

- [ ] **Step 7.6: Commit server changes**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Packages/manifest.json Assets/NuGetForUnity/packages.config
git status --short
git commit -m "chore: wire LOP-Shared package + switch Google.Protobuf to UnityNuGet UPM

- manifest.json: add scopedRegistries (UnityNuGet), com.baegames.lop.shared (file:), org.nuget.google.protobuf 3.28.2
- testables: add com.baegames.lop.shared
- packages.config: remove Google.Protobuf NuGet entry (replaced by UPM)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

> NuGetForUnity-managed folder `Assets/NuGetForUnity/Packages/Google.Protobuf.3.28.2/` deletion is handled by Unity Editor's Restore — usually excluded from git via NuGetForUnity's own .gitignore. Confirm with `git status` after Restore; if the folder shows as deleted, commit that too.

---

### Task 8: Slice 0 verification (compilation + playthrough baseline)

**Files:** N/A (verification only)

- [ ] **Step 8.1: Client compilation pass**

Open LOP-Client Unity. Wait for full package resolve + script compilation.

Verify in Unity Console:
- 0 compilation errors
- 0 errors related to `com.baegames.lop.shared` (package may show in Package Manager as 0.0.1 with empty Runtime/Generated)
- 0 errors related to `Google.Protobuf` (must resolve via UnityNuGet UPM now)

Expected console state: clean (only normal info messages).

- [ ] **Step 8.2: Verify Test Runner sees Shared assemblies**

Unity → Window → General → Test Runner. Switch between EditMode and PlayMode tabs.

Expected: `baegames.LOP.Shared.Tests.EditMode` and `baegames.LOP.Shared.Tests.PlayMode` assemblies appear (empty, no tests yet) alongside `baegames.GameFramework.World.Tests`.

- [ ] **Step 8.3: Verify Google.Protobuf DLL resolution**

In Unity Console, run a quick check by opening any existing `Assets/Scripts/Generated/Protobuf/*.cs` file (e.g. `DamageEventToC.cs`) in Unity's script editor — Unity must not flag `using global::Google.Protobuf;` as unresolved. Open Project Browser and verify the `Generated/Protobuf` folder is still indexed.

Alternative verification:
- In Project Browser, locate `Packages/Unity NuGet` (or similar) — confirm `Google.Protobuf` 3.28.2 is listed there.
- If unsure, run a quick script (Edit → Run code, or attach `[InitializeOnLoadMethod]` log) that does `typeof(Google.Protobuf.MessageParser).FullName` and prints — should not throw.

- [ ] **Step 8.4: Server compilation pass**

Repeat Step 8.1 on LOP-Server. Same expectations.

- [ ] **Step 8.5: USER ACTION — Play a round (regression baseline)**

**MANUAL STEP**: Start one round (Client + Server connected, character spawns, take damage, see damage popup, kill a character, see `[World] Death entity X (killer=Y)` log). Confirm no errors.

This is the *baseline* — Slice 1 must preserve it.

- [ ] **Step 8.6: Commit client manifest changes**

In the client feature worktree:

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/feature+lop-shared-bootstrap
git status --short
git add Packages/manifest.json Assets/NuGetForUnity/packages.config
git commit -m "chore: wire LOP-Shared package + switch Google.Protobuf to UnityNuGet UPM (client)

- manifest.json: add scopedRegistries (UnityNuGet), com.baegames.lop.shared (file:), org.nuget.google.protobuf 3.28.2
- testables: add com.baegames.lop.shared
- packages.config: remove Google.Protobuf NuGet entry (replaced by UPM)

Slice 0 baseline: client + server compile, play-through normal, World Core slice 3 regression clear.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

Also commit `Packages/packages-lock.json` if Unity regenerated it (separate `git add` then amend or follow-up commit).

---

## SLICE 1 — Wire/Proto + Codegen Pipeline Migration

### Task 9: Pre-flight diff verification

**Files:** N/A (verification only)

- [ ] **Step 9.1: Compare `.proto` files between client and server**

```bash
diff -rq C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Protos \
         C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Protos
```

Expected: no output (identical).

**If differences exist**: STOP. Decide which side is truth, reconcile manually (commit on appropriate branch), then re-run.

- [ ] **Step 9.2: Compare `Tools/Protobuf/`**

```bash
diff -rq C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Tools/Protobuf \
         C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Tools/Protobuf
```

Expected: no output (identical).

- [ ] **Step 9.3: Compare codegen `.sh` scripts**

```bash
diff -q C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Scripts/compile_protos.sh \
        C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Scripts/compile_protos.sh

diff -q C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Scripts/generate_imessage.sh \
        C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Scripts/generate_imessage.sh

diff -q C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Scripts/generate_message_ids.sh \
        C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Scripts/generate_message_ids.sh

diff -q C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Scripts/generate_message_initializer.sh \
        C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Scripts/generate_message_initializer.sh

diff -q C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Scripts/generate_protos.sh \
        C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Scripts/generate_protos.sh
```

Expected: all 5 silent (identical). `upload-*.sh` files (client-only) are NOT in this diff list.

- [ ] **Step 9.4: Compare generated `.cs` (Protobuf + IMessage + MessageIds + MessageInitializer)**

```bash
diff -rq C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Generated \
         C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Generated 2>&1 | grep -v "\.meta"
```

Expected: only `.meta` files differ (different per-project GUIDs). `.cs` files identical.

```bash
diff -rq C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Network/Message \
         C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Network/Message 2>&1 | grep -v "\.meta"
```

Expected: only `.meta` differs.

**If any `.cs` differs**: STOP and reconcile before proceeding.

---

### Task 10: Seed Shared with proto-generated artifacts (client → Shared)

**Files:**
- Source: `LeagueOfPhysical-Client/Assets/Scripts/Network/Message/*.cs(.meta)` (2 files × 2)
- Source: `LeagueOfPhysical-Client/Assets/Scripts/Generated/MessageIds.cs(.meta)`
- Source: `LeagueOfPhysical-Client/Assets/Scripts/Generated/MessageInitializer.cs(.meta)`
- Source: `LeagueOfPhysical-Client/Assets/Scripts/Generated/Protobuf/*.cs(.meta)` (all)
- Target: `LeagueOfPhysical-Shared/Runtime/Scripts/Network/Message/` + `Runtime.Generated/Scripts/[Protobuf/]`

> Note: `git mv` across separate repositories doesn't preserve git history into the target repo's log automatically (it's a copy + delete across boundaries). We move via filesystem `mv` for atomicity, then `git add` in target / `git rm` in source.

- [ ] **Step 10.1: Create target directories**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
mkdir -p Runtime/Scripts/Network/Message Runtime.Generated/Scripts/Protobuf
```

- [ ] **Step 10.2: Move `Network/Message/` (2 .cs + 2 .meta)**

```bash
CLIENT_WT=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/feature+lop-shared-bootstrap
SHARED=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared

mv "$CLIENT_WT/Assets/Scripts/Network/Message/MessageFactory.cs"      "$SHARED/Runtime/Scripts/Network/Message/"
mv "$CLIENT_WT/Assets/Scripts/Network/Message/MessageFactory.cs.meta" "$SHARED/Runtime/Scripts/Network/Message/"
mv "$CLIENT_WT/Assets/Scripts/Network/Message/MessageHandler.cs"      "$SHARED/Runtime/Scripts/Network/Message/"
mv "$CLIENT_WT/Assets/Scripts/Network/Message/MessageHandler.cs.meta" "$SHARED/Runtime/Scripts/Network/Message/"

# Verify client dir is now empty (will be removed in Step 10.6)
ls "$CLIENT_WT/Assets/Scripts/Network/Message/" || true
```

Expected: target now has 4 files (2 .cs + 2 .meta). Source dir empty or missing.

- [ ] **Step 10.3: Move `Generated/MessageIds.cs` and `MessageInitializer.cs`**

```bash
mv "$CLIENT_WT/Assets/Scripts/Generated/MessageIds.cs"           "$SHARED/Runtime.Generated/Scripts/"
mv "$CLIENT_WT/Assets/Scripts/Generated/MessageIds.cs.meta"      "$SHARED/Runtime.Generated/Scripts/"
mv "$CLIENT_WT/Assets/Scripts/Generated/MessageInitializer.cs"      "$SHARED/Runtime.Generated/Scripts/"
mv "$CLIENT_WT/Assets/Scripts/Generated/MessageInitializer.cs.meta" "$SHARED/Runtime.Generated/Scripts/"
```

- [ ] **Step 10.4: Move all `Generated/Protobuf/*.cs(.meta)`**

```bash
mv "$CLIENT_WT/Assets/Scripts/Generated/Protobuf/"*.cs       "$SHARED/Runtime.Generated/Scripts/Protobuf/"
mv "$CLIENT_WT/Assets/Scripts/Generated/Protobuf/"*.cs.meta  "$SHARED/Runtime.Generated/Scripts/Protobuf/"

# Verify count
ls "$SHARED/Runtime.Generated/Scripts/Protobuf/" | wc -l
ls "$CLIENT_WT/Assets/Scripts/Generated/Protobuf/" || true
```

Expected: target has ~80 entries (proto .cs + IMessage .cs + their .meta files combined). Source dir empty or missing.

- [ ] **Step 10.5: Clean up empty client directories**

```bash
rmdir "$CLIENT_WT/Assets/Scripts/Generated/Protobuf" 2>/dev/null || true
# Note: do NOT rmdir Generated/ yet — Slice 2/3 may keep adding to it (MasterData/generated, etc.)
# But for this slice we ONLY remove if it's empty after this task:
ls "$CLIENT_WT/Assets/Scripts/Generated/" || true
rmdir "$CLIENT_WT/Assets/Scripts/Generated" 2>/dev/null || true

rmdir "$CLIENT_WT/Assets/Scripts/Network/Message" 2>/dev/null || true
ls "$CLIENT_WT/Assets/Scripts/Network/" || true
# Do NOT remove Network/ — other server code may exist (not in this slice though)
```

Also remove the corresponding `.meta` for any removed directory:
```bash
rm -f "$CLIENT_WT/Assets/Scripts/Generated.meta"
rm -f "$CLIENT_WT/Assets/Scripts/Generated/Protobuf.meta"
rm -f "$CLIENT_WT/Assets/Scripts/Network/Message.meta"
```

Note: If `Assets/Scripts/Network/` becomes empty after only the `Message/` folder is gone, it can be removed too (and its `.meta`). Check first.

- [ ] **Step 10.6: Verify target structure**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
find Runtime Runtime.Generated -type f | sort | head -40
```

Expected: structure includes:
- `Runtime/Scripts/Network/Message/MessageFactory.cs` + `.meta`
- `Runtime/Scripts/Network/Message/MessageHandler.cs` + `.meta`
- `Runtime.Generated/Scripts/MessageIds.cs` + `.meta`
- `Runtime.Generated/Scripts/MessageInitializer.cs` + `.meta`
- `Runtime.Generated/Scripts/Protobuf/<message>.cs` + `<message>.cs.meta` for each (~13 messages × 2 files + ~13 .IMessage.cs × 2 files ≈ 52+ files)

---

### Task 11: Seed Shared with codegen pipeline (client → Shared)

**Files:**
- Source: `LeagueOfPhysical-Client/Protos/*.proto`
- Source: `LeagueOfPhysical-Client/Tools/Protobuf/`
- Source: `LeagueOfPhysical-Client/Scripts/{compile_protos,generate_imessage,generate_message_ids,generate_message_initializer,generate_protos}.sh`
- Target: `LeagueOfPhysical-Shared/{Protos,Tools/Protobuf,Scripts}/`

- [ ] **Step 11.1: Move `Protos/`**

```bash
CLIENT_WT=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/feature+lop-shared-bootstrap
SHARED=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared

mv "$CLIENT_WT/Protos" "$SHARED/Protos"
ls "$SHARED/Protos" | wc -l
```

Expected: ~24 `.proto` files moved.

- [ ] **Step 11.2: Move `Tools/Protobuf/`**

```bash
mkdir -p "$SHARED/Tools"
mv "$CLIENT_WT/Tools/Protobuf" "$SHARED/Tools/Protobuf"
ls "$SHARED/Tools/Protobuf/"
# Verify protoc binary
ls -la "$SHARED/Tools/Protobuf/protoc-28.2-win64/bin/protoc.exe"
```

Expected: `protoc.exe` exists with expected size.

```bash
# Remove Tools/ if empty afterward
rmdir "$CLIENT_WT/Tools" 2>/dev/null || true
```

- [ ] **Step 11.3: Move 5 codegen scripts**

```bash
mkdir -p "$SHARED/Scripts"
for f in compile_protos.sh generate_imessage.sh generate_message_ids.sh generate_message_initializer.sh generate_protos.sh; do
  mv "$CLIENT_WT/Scripts/$f" "$SHARED/Scripts/$f"
done

ls "$SHARED/Scripts/"
ls "$CLIENT_WT/Scripts/"
```

Expected: 5 scripts in Shared. Client `Scripts/` retains `upload-apk-s3.sh`, `upload-serverdata-s3.sh`.

---

### Task 12: Update codegen script output paths

**Files (all in `LeagueOfPhysical-Shared/Scripts/`):**
- Modify: `compile_protos.sh`
- Modify: `generate_imessage.sh`
- Modify: `generate_message_ids.sh`
- Modify: `generate_message_initializer.sh`
- Modify: `generate_protos.sh`

- [ ] **Step 12.1: Update `compile_protos.sh`**

In `Scripts/compile_protos.sh`, replace:

```diff
- OUT_PATH="../Assets/Scripts/Generated/Protobuf"
+ OUT_PATH="../Runtime.Generated/Scripts/Protobuf"
```

All other paths (`PROTOC="../Tools/Protobuf/protoc-28.2-win64/bin/protoc"`, `PROTO_PATH="../Protos"`, `INCLUDE_PATH="../Tools/Protobuf/protoc-28.2-win64/include"`) remain unchanged — they resolve correctly under Shared root.

- [ ] **Step 12.2: Update `generate_imessage.sh`**

Replace:
```diff
- OUTPUT_DIR="../Assets/Scripts/Generated/Protobuf"
+ OUTPUT_DIR="../Runtime.Generated/Scripts/Protobuf"
```

- [ ] **Step 12.3: Update `generate_message_ids.sh`**

Replace:
```diff
- OUTPUT_FILE="../Assets/Scripts/Generated/MessageIds.cs"
+ OUTPUT_FILE="../Runtime.Generated/Scripts/MessageIds.cs"
```

- [ ] **Step 12.4: Update `generate_message_initializer.sh`**

Replace:
```diff
- OUTPUT_FILE="../Assets/Scripts/Generated/MessageInitializer.cs"
+ OUTPUT_FILE="../Runtime.Generated/Scripts/MessageInitializer.cs"
```

- [ ] **Step 12.5: Update `generate_protos.sh` (orchestrator)**

Replace the clean-and-mkdir block:
```diff
- # generated 폴더 초기화
- echo "Cleaning up generated folder..."
- rm -rf ../Assets/Scripts/generated
- mkdir -p ../Assets/Scripts/generated
+ # generated outputs reset
+ echo "Cleaning generated outputs..."
+ rm -rf ../Runtime.Generated/Scripts/Protobuf
+ rm -f  ../Runtime.Generated/Scripts/MessageIds.cs
+ rm -f  ../Runtime.Generated/Scripts/MessageIds.cs.meta
+ rm -f  ../Runtime.Generated/Scripts/MessageInitializer.cs
+ rm -f  ../Runtime.Generated/Scripts/MessageInitializer.cs.meta
+ mkdir -p ../Runtime.Generated/Scripts/Protobuf
```

Rationale: removing `.meta` files for regenerated `.cs` is intentional during a *clean rebuild*. Unity will assign fresh GUIDs on reimport. Since these are static classes (not referenced by `.meta` GUID), this is safe.

> **Caveat for incremental use**: most everyday regeneration should NOT delete `.meta` (to preserve Unity's GUID stability). Users running `generate_protos.sh` for incremental updates should expect `.meta` regeneration on first run after this change. After that, individual scripts (`generate_message_ids.sh` etc.) overwrite `.cs` only and Unity preserves existing `.meta` GUIDs.

- [ ] **Step 12.6: Make scripts executable + verify**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
chmod +x *.sh
ls -la *.sh
# Quick syntax check
for f in *.sh; do bash -n "$f" && echo "$f: syntax OK"; done
```

Expected: all scripts marked executable, all pass syntax check.

---

### Task 13: Client Unity verification (reimport + compile + play)

**Files:** N/A (verification)

- [ ] **Step 13.1: USER ACTION — Open Client Unity, wait for reimport**

**MANUAL STEP**:

> Open LOP-Client Unity. It will:
> 1. Detect deleted scripts (`Assets/Scripts/Network/Message/`, `Assets/Scripts/Generated/`) and refresh asset database.
> 2. Detect new package contents in `com.baegames.lop.shared` Runtime/Generated.
> 3. Recompile.

Wait until Unity finishes (status bar idle).

- [ ] **Step 13.2: Verify Console**

Unity Console:
- 0 compilation errors
- Warnings about missing `.meta` references *should be absent* — file moves preserved `.meta`. If any appear, investigate immediately.

If errors exist (e.g., "type or namespace name `LOP.MessageFactory` could not be found"):
- Verify Shared package resolved in Package Manager (com.baegames.lop.shared 0.0.1).
- Verify `autoReferenced: true` in Runtime asmdef (Task 3 Step 3.1).
- Check Project Browser: Packages → LOP Shared → Runtime → Scripts → Network → Message → MessageFactory.cs should be visible.

- [ ] **Step 13.3: Verify Test Runner**

Unity → Window → General → Test Runner. Confirm `baegames.LOP.Shared.Tests.EditMode` still appears (empty, valid).

- [ ] **Step 13.4: USER ACTION — Play one round**

**MANUAL STEP**:

> Run one full round (Client + Server). Verify:
> - Character spawns
> - Damage popup appears when taking damage
> - On kill: `[World] Death entity X (killer=Y)` log appears
> - All 13 message types flow (no `MessageFactory.CreateMessage: unknown id` errors)

If errors occur, check `[RuntimeInitializeOnLoadMethod] MessageInitializer.Initialize()` is being called. Temporary debug aid (revert after verification):

In `Runtime.Generated/Scripts/MessageInitializer.cs`, add inside `Initialize`:
```csharp
UnityEngine.Debug.Log($"[Shared] MessageInitializer registered creators");
```

Expected on Unity Play: that log appears once. If it doesn't, Auto Referenced or assembly resolution failed.

After verification, remove the debug log.

---

### Task 14: Remove duplicated wire/proto + codegen from LOP-Server

**Files:**
- Remove: `LeagueOfPhysical-Server/Assets/Scripts/Network/Message/MessageFactory.cs(.meta)`
- Remove: `LeagueOfPhysical-Server/Assets/Scripts/Network/Message/MessageHandler.cs(.meta)`
- Remove: `LeagueOfPhysical-Server/Assets/Scripts/Generated/MessageIds.cs(.meta)`
- Remove: `LeagueOfPhysical-Server/Assets/Scripts/Generated/MessageInitializer.cs(.meta)`
- Remove: `LeagueOfPhysical-Server/Assets/Scripts/Generated/Protobuf/*.cs(.meta)`
- Remove: `LeagueOfPhysical-Server/Protos/`
- Remove: `LeagueOfPhysical-Server/Tools/Protobuf/`
- Remove: `LeagueOfPhysical-Server/Scripts/{compile,generate}_*.sh`

> Server is a separate git repo on branch `feature/lop-shared-bootstrap` (created in Task 7).

- [ ] **Step 14.1: Remove server wire infra**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git rm Assets/Scripts/Network/Message/MessageFactory.cs Assets/Scripts/Network/Message/MessageFactory.cs.meta
git rm Assets/Scripts/Network/Message/MessageHandler.cs Assets/Scripts/Network/Message/MessageHandler.cs.meta
# Remove now-empty Message folder + its .meta
git rm Assets/Scripts/Network/Message.meta 2>/dev/null || rm -f Assets/Scripts/Network/Message.meta
rmdir Assets/Scripts/Network/Message 2>/dev/null || true
```

- [ ] **Step 14.2: Remove server Generated/**

```bash
git rm -r Assets/Scripts/Generated/Protobuf
git rm Assets/Scripts/Generated/MessageIds.cs Assets/Scripts/Generated/MessageIds.cs.meta
git rm Assets/Scripts/Generated/MessageInitializer.cs Assets/Scripts/Generated/MessageInitializer.cs.meta
# Note: if MasterData/generated/* is here, those stay (Slice 2). Confirm:
ls Assets/Scripts/Generated/ 2>/dev/null
```

Expected: any remaining content is unrelated to this slice. If `Generated/` is now empty, remove:
```bash
git rm Assets/Scripts/Generated.meta 2>/dev/null || rm -f Assets/Scripts/Generated.meta
rmdir Assets/Scripts/Generated 2>/dev/null || true
```

- [ ] **Step 14.3: Remove server Protos + Tools + codegen scripts**

```bash
git rm -r Protos
git rm -r Tools/Protobuf
rmdir Tools 2>/dev/null || true

for f in compile_protos.sh generate_imessage.sh generate_message_ids.sh generate_message_initializer.sh generate_protos.sh; do
  git rm Scripts/$f
done

ls Scripts/
```

Expected: server `Scripts/` is now empty or contains only future-added files. The script removals are committed-ready.

- [ ] **Step 14.4: Verify status**

```bash
git status --short
```

Expected: ~80+ deletions staged, no other changes (server pre-existing modified files outside the scope of this task should not appear unless user explicitly added them).

---

### Task 15: Server Unity verification (reimport + compile + play)

**Files:** N/A (verification)

- [ ] **Step 15.1: USER ACTION — Open Server Unity, wait for reimport**

**MANUAL STEP**:

> Open LOP-Server Unity. Same expected behavior as Task 13 Step 13.1: detects removals, resolves Shared package, recompiles.

- [ ] **Step 15.2: Verify Console**

Same expectations as Task 13 Step 13.2: 0 compilation errors. Types from `LOP` namespace (e.g. `LOP.MessageFactory`) resolve via Shared package.

- [ ] **Step 15.3: USER ACTION — Play one round (client + server connected)**

**MANUAL STEP**:

> Both client and server use Shared package. Run a round to confirm:
> - Spawn, damage, death, kill log — all behaviors from Task 8 Step 8.5 baseline.
> - World Core slice 3 regression unchanged.

If issues arise, common causes:
- Server's Mirror NetworkServer needs registered message creators — same `[RuntimeInitializeOnLoadMethod]` triggers in server Unity as in client. Confirm via temporary log (revert after).
- `org.nuget.google.protobuf` not resolved on server side — verify in server's Package Manager.

---

### Task 16: Codegen regeneration smoke test

**Files:** N/A (regeneration verification)

- [ ] **Step 16.1: Backup current generated output**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
mkdir -p /tmp/lop-shared-gen-backup
cp -r Runtime.Generated/Scripts/* /tmp/lop-shared-gen-backup/
ls /tmp/lop-shared-gen-backup/Protobuf/ | wc -l
```

Expected: count matches Runtime.Generated/Scripts/Protobuf/ entry count.

- [ ] **Step 16.2: Run regeneration**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
```

Expected: output ends with "All proto-related scripts executed successfully." No errors.

- [ ] **Step 16.3: Compare regenerated output to backup**

```bash
diff -rq /tmp/lop-shared-gen-backup C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime.Generated/Scripts/ 2>&1 | grep -v "\.meta"
```

Expected: empty (only `.meta` may differ since `generate_protos.sh` removes them as per Task 12 Step 12.5).

If `.cs` files differ (beyond `.meta`), investigate — the codegen output should be byte-identical for the same `.proto` inputs.

- [ ] **Step 16.4: Restore `.meta` files if Unity regenerated them**

After Step 16.2, Unity (if open) will regenerate `.meta` for the new `.cs` files. Each new `.meta` gets a *new GUID*. If that causes issues for users with cached references (none expected for these static classes), restore from backup:

```bash
# Only if needed
cp /tmp/lop-shared-gen-backup/Protobuf/*.meta C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/
cp /tmp/lop-shared-gen-backup/MessageIds.cs.meta C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime.Generated/Scripts/
cp /tmp/lop-shared-gen-backup/MessageInitializer.cs.meta C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime.Generated/Scripts/
```

Normally: skip this step — Unity-assigned new GUIDs are fine since nothing references them.

- [ ] **Step 16.5: Cleanup backup**

```bash
rm -rf /tmp/lop-shared-gen-backup
```

---

### Task 17: Modify-sync regression smoke test

**Files:** Temporary edit to one `.proto`, then revert

- [ ] **Step 17.1: Make a harmless modification to a .proto file**

Pick a `.proto` (e.g. `DamageEventToC.proto`). Add a trailing comment line (no semantic change):

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
echo "// touched at $(date)" >> Protos/DamageEventToC.proto
```

- [ ] **Step 17.2: Regenerate**

```bash
cd Scripts
./generate_protos.sh
```

Expected: succeeds.

- [ ] **Step 17.3: USER ACTION — Verify both Unity Editors auto-pick up the change**

**MANUAL STEP**:

> Switch to client Unity (still open) — within a few seconds, it should refresh the package (file: watch). No errors. Switch to server Unity — same.

Both should remain in clean compile state.

- [ ] **Step 17.4: Revert the test edit**

> Shared repo has no commit yet at this point (T19 is the first commit), so `git checkout` cannot restore. Use `sed` to remove the marker line directly.

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
sed -i '/^\/\/ touched at /d' Protos/DamageEventToC.proto
# Regenerate to clean the test marker from outputs
cd Scripts
./generate_protos.sh
cd ..
```

Verify the marker line is gone:
```bash
grep -c '^// touched at' Protos/DamageEventToC.proto
```

Expected: `0`.

Verify the proto file is back to pristine state (no `// touched` anywhere, last line of file looks like original syntax not a comment):
```bash
tail -3 Protos/DamageEventToC.proto
```

Expected: original last 3 lines (e.g., closing `}` of message definition).

---

### Task 18: End-to-end playthrough regression

**Files:** N/A (final verification)

- [ ] **Step 18.1: USER ACTION — Play one full round, client + server**

**MANUAL STEP — exhaustive regression**:

> 1. Start LOP-Server, then LOP-Client. Confirm connection.
> 2. Spawn character.
> 3. Move (PlayerInputToS round-trip working).
> 4. Take damage (DamageEventToC popup).
> 5. Kill another entity (`[World] Death entity X (killer=Y)` log).
> 6. Respawn (EntitySpawnToC / EntityDespawnToC).
> 7. Stat allocation (StatAllocationToC / StatAllocationToS).
> 8. Game info exchange (GameInfoToC / GameInfoToS).

Expected: all 13 message types flow without `MessageFactory` errors, no console errors, baseline behavior matches Task 8 Step 8.5.

If any message type fails:
- Check `MessageInitializer.Initialize` ran (see Task 13 Step 13.4 debug aid).
- Check the corresponding `*.IMessage.cs` partial extension exists in `Runtime.Generated/Scripts/Protobuf/`.

---

### Task 19: Commit + push all three repos

**Files:** Commit accumulated changes

- [ ] **Step 19.1: Commit Shared package codegen + content**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add -A
git status --short
```

Expected: all wire/proto content + codegen pipeline staged.

```bash
git commit -m "feat: migrate wire/proto + codegen pipeline from client/server

Slice 1 of LOP-Shared package migration:

- Runtime/Scripts/Network/Message/: MessageFactory, MessageHandler (from client)
- Runtime.Generated/Scripts/: MessageIds, MessageInitializer (from client)
- Runtime.Generated/Scripts/Protobuf/: ~13 proto-generated .cs + their .IMessage.cs partials
- Protos/: ~24 .proto source-of-truth files
- Tools/Protobuf/protoc-28.2-win64/: protoc binary
- Scripts/: 5 codegen .sh with output paths redirected to Runtime.Generated/Scripts/

Use-side projects (client + server) have these files removed in matching commits.
Verified: codegen regeneration byte-identical for same inputs; modify-sync picked up
by both Unity editors via file: package reference.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"

git push origin main
```

- [ ] **Step 19.2: Commit + push LOP-Client removals**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/feature+lop-shared-bootstrap
git add -A
git status --short
```

Expected: deletions of `Assets/Scripts/Network/Message/*`, `Assets/Scripts/Generated/{MessageIds,MessageInitializer,Protobuf/*}`, `Protos/*`, `Tools/Protobuf/*`, 5 codegen `Scripts/*.sh`. Plus possibly `Packages/packages-lock.json` regenerated.

```bash
git commit -m "chore: remove wire/proto + codegen pipeline (moved to LOP-Shared)

Slice 1 of LOP-Shared migration. Client side:
- Assets/Scripts/Network/Message/* removed (moved to com.baegames.lop.shared)
- Assets/Scripts/Generated/{MessageIds,MessageInitializer,Protobuf/*} removed
- Protos/, Tools/Protobuf/, 5 codegen .sh removed
- Scripts/upload-{apk,serverdata}-s3.sh retained (mobile build, client-specific)

Same types and namespaces available transparently via Shared package
(autoReferenced: true, namespace LOP preserved).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 19.3: Commit + push LOP-Server removals**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add -A
git status --short
```

```bash
git commit -m "chore: remove wire/proto + codegen pipeline (moved to LOP-Shared)

Slice 1 of LOP-Shared migration. Server side, mirroring client commit.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"

git push origin feature/lop-shared-bootstrap
```

- [ ] **Step 19.4: Decide merge strategy**

**USER DECISION REQUIRED**: For each repo, choose:
- **A. Direct `--no-ff` merge to main** (per architecture-guidelines.md, no PR needed since solo dev):
  ```bash
  # In each repo:
  git checkout main
  git merge --no-ff feature/lop-shared-bootstrap -m "Merge branch 'feature/lop-shared-bootstrap'"
  git push origin main
  git branch -d feature/lop-shared-bootstrap
  ```
- **B. Open PRs on GitHub** for review (recommended for higher-stakes changes):
  ```bash
  gh pr create --title "feat: LOP-Shared bootstrap + wire/proto migration" --body "See spec: docs/superpowers/specs/2026-05-31-lop-shared-package-design.md"
  ```

For the **client worktree** (`worktree-feature+lop-shared-bootstrap` branch), before merging:
- Rename branch to `feature/lop-shared-bootstrap` for consistency: `git branch -m worktree-feature+lop-shared-bootstrap feature/lop-shared-bootstrap`
- (Or merge as-is and rename in the merge commit message.)

- [ ] **Step 19.5: Cleanup client worktree (after merge)**

After successful merge to client `main`:

Run from inside the worktree (will switch back to original directory):
```
ExitWorktree action=remove
```

Or manually:
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git worktree remove .claude/worktrees/feature+lop-shared-bootstrap
git branch -d feature/lop-shared-bootstrap   # (or worktree-feature+lop-shared-bootstrap if not renamed)
```

---

## Self-Review

**Spec coverage check:**
- Slice 0 §0-A (repo + folders + .gitignore): T1 ✓
- §0-B (5 asmdefs): T3 ✓
- §0-C (package.json): T2 ✓
- §0-D (README): T2 ✓
- §0-E (.gitignore): T1 Step 1.3 ✓
- §0-F (client/server manifest): T5, T7 ✓
- §0-G (client/server NuGet): T6, T7 ✓
- §0-H (Slice 0 verification): T8 ✓
- Slice 1 §1-A (pre-diff): T9 ✓
- §1-B (artifact migration): T10 ✓
- §1-C (codegen migration + script edits): T11, T12 ✓
- §1-D (seed → client verify → server cleanup → server verify): T10-15 ✓
- §1-E (GUID policy): covered conceptually in T10 (move preserves client .meta) ✓
- §1-F (Slice 1 verification): T13, T15, T16, T17, T18 ✓
- Final commit / merge: T19 ✓

**Placeholder scan:** Searched for "TBD", "TODO", "fill in", "appropriate", "similar to" — none found in implementation steps. Manual user-action steps are explicit. ✓

**Type/method consistency:**
- `MessageFactory.RegisterCreator(ushort, Func<IMessage>)` — used consistently (Task 13 debug aid mentions `MessageInitializer.Initialize`).
- asmdef names match between definition (Task 3) and references (Tests asmdefs).
- File paths consistent throughout (always absolute or with explicit `cd`).
- `Runtime.Generated/Scripts/Protobuf/` is the target dir in every script edit (Task 12) ✓

No issues found.

---

## Open Decisions (recap)

These are decisions left to the user during execution:

- **Task 4 Step 4.3**: GitHub Shared repo private vs public.
- **Task 19 Step 19.4**: merge directly (`--no-ff`) vs PR.
- **Task 19 Step 19.4**: worktree branch rename (`worktree-feature+lop-shared-bootstrap` → `feature/lop-shared-bootstrap`).

---

## Related

- Spec: [`../specs/2026-05-31-lop-shared-package-design.md`](../specs/2026-05-31-lop-shared-package-design.md)
- Topology: [`../../lop-repo-topology.md`](../../lop-repo-topology.md)
- World Core: [`../../world-core-connection-architecture.md`](../../world-core-connection-architecture.md)
