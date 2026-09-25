# LOP-Shared Package: Bootstrap + Wire/Proto Migration Design

**Date:** 2026-05-31
**Branch:** `feature/lop-shared-bootstrap` (제안)
**Related:** [LOP 저장소 토폴로지](../../lop-repo-topology.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Netcode 재설계](../../netcode-redesign.md)

## Goal

LOP 클라/서버가 *반드시 동일하게 보아야 하는* 공통 코드를 별도 git 저장소·Unity 패키지 `LeagueOfPhysical-Shared` (`com.baegames.lop.shared`)로 추출한다. 이 spec의 scope는 **Slice 0 (부트스트랩) + Slice 1 (wire/proto + codegen 파이프라인 이전)**. Slice 2+(MasterData, 도메인 모델, Engine↔Simulation 추출)는 별도 spec.

### Slice 0/1 끝에 얻는 것

- `LeagueOfPhysical-Shared` git 저장소 + Unity 패키지가 살아있고, 클·서 양쪽 manifest가 file: 참조로 인식
- Google.Protobuf NuGet → UnityNuGet UPM 전환 완료
- wire 인프라(`MessageFactory`, `MessageHandler<T>`, `MessageIds`, `MessageInitializer`)와 proto 산출물(`Generated/Protobuf/*.cs`, `*.IMessage.cs`)이 Shared에서 단일 진실원본으로 살아있음
- proto 진실원본(`.proto`), protoc 도구, codegen 스크립트도 Shared로 단일화
- 클·서 양쪽 컴파일 통과 + 한 라운드 플레이 통과 + World Core slice 3 회귀 통과

## Architecture (1-pager)

```
External                    GameFramework          LOP-Shared              LOP-Client / LOP-Server
─────────                   ─────────────          ────────────            ──────────────────────
Mirror                                                                     Assembly-CSharp
Google.Protobuf  ◄─────── (use-side)               LOP.Shared.Generated   │
 (UnityNuGet UPM,                                  ├─ Protobuf/*.cs       │  view, reconciler,
  org.nuget.google.protobuf)                       ├─ *.IMessage.cs       │  LOPGameEngine(host),
                                                   ├─ MessageIds.cs       │  AI, lobby, matching,
R3, VContainer,                                    └─ MessageInitializer  │  Mirror NetworkClient/
UniTask  ◄─────── (use-side)                              │                │  Server, AutoMapper-
                                                          ▼                │  driven mappers
                            baegames.GameFramework  LOP.Shared.Runtime    │
                            .Runtime                ├─ Network/Message/   │
                              ▲                     │   ├─ MessageFactory │  …
                              │                     │   └─ MessageHandler │
                              │                     │ (future Slice 2+:   │
                              │                     │  MasterData, Model, │
                              │                     │  LOPGameSimulation) │
                              │                            │              │
                              └────────────────────────────┼──────────────┘
                                                           ▲
                                                           │ file: 참조 + testables
                                                           │
                                                  Packages/manifest.json
                                                  (양쪽 클라/서버)
```

핵심 디자인 결정:

- **GameFramework 패턴 따름**: Unity 패키지 + file: 참조 + 직접 편집 (현재 GameFramework와 동일)
- **Mirror 비참조**: wire 인터페이스가 `GameFramework.IMessage`라 LOP-Shared는 Mirror 미참조 가능 — 더 깔끔
- **namespace LOP 유지**: 호출부 변경 0줄로 마이그레이션
- **Slice 0/1 합산 결과 = wire/proto 시스템 단일 진실원본**: .proto/.sh/protoc/산출물 모두 Shared에
- **결정론 시뮬 코어 분리(`LOPGameSimulation`)는 별도 슬라이스(Slice 4)**: 이 spec scope 밖
- **명명은 임시**: `LOPGameEngine`/`LOPGameSimulation`은 Slice 4 완료 후 재논의 (`lop-repo-topology.md` Open Decisions)

## Scope

### In Scope (이 spec 구현)

**Slice 0 — 부트스트랩**:
- 새 git 저장소 `LeagueOfPhysical-Shared` 생성 (`github.com/Baeinsoo/LeagueOfPhysical-Shared.git`)
- Unity 패키지 `com.baegames.lop.shared` 0.0.1 메타데이터
- 5개 asmdef 빈 껍데기 (Runtime, Runtime.Generated, Editor, Tests.EditMode, Tests.PlayMode)
- 클·서 `Packages/manifest.json`에 file: 참조 + UnityNuGet scoped registry + `org.nuget.google.protobuf` 추가 + `testables` 등록
- 클·서 `Assets/NuGetForUnity/packages.config`에서 Google.Protobuf 제거, NuGetForUnity Restore로 폴더 정리
- README.md (use-side 계약)
- .gitignore (Unity 패키지 표준)

**Slice 1 — wire/proto + codegen 파이프라인 이전**:
- 양쪽 `Assets/Scripts/Network/Message/{MessageFactory,MessageHandler}.cs(.meta)` → Shared `Runtime/Scripts/Network/Message/`
- 양쪽 `Assets/Scripts/Generated/{MessageIds,MessageInitializer}.cs(.meta)` → Shared `Runtime.Generated/Scripts/`
- 양쪽 `Assets/Scripts/Generated/Protobuf/*.cs(.meta)` → Shared `Runtime.Generated/Scripts/Protobuf/`
- 양쪽 `Protos/*.proto` → Shared `Protos/` (시드 후 한쪽 삭제)
- 양쪽 `Tools/Protobuf/protoc-28.2-win64/` → Shared `Tools/Protobuf/` (시드 후 한쪽 삭제)
- 양쪽 `Scripts/{compile_protos,generate_imessage,generate_message_ids,generate_message_initializer,generate_protos}.sh` → Shared `Scripts/` (시드 + 출력 경로 수정, 한쪽 삭제)
- 클라 `Scripts/upload-apk-s3.sh`, `Scripts/upload-serverdata-s3.sh`는 **유지** (모바일 빌드 클라 특화)

### Out of Scope (이번 spec 밖, 별도 슬라이스)

- **Slice 2**: MasterData 스키마/로더 이전 (`MasterData/generated/*.cs`, `LOPMasterDataManager` 베이스 — 양쪽 본문 차이 검증 후)
- **Slice 3**: 도메인 Model 이전 (`Model/PlayerInput`, 양쪽 본문 수렴 작업 동반)
- **Slice 4(a~e)**: Engine ↔ Simulation 추출
  - 4a: GameFramework에 추상 추가 (`IGameSimulation`, `GameSimulationBase`, `ITickUpdater`/`TickUpdaterBase`, `IInputSource`, `IEventSink`, `IPhysicsSimulator`, `INetworkSession`)
  - 4b: LOP-Shared에 `LOPGameSimulation` 빈 골격
  - 4c: 시뮬 매니저 점진 흡수
  - 4d: I/O 어댑터화
  - 4e: `LOPGameEngine` 얇은 호스트화
- **Stage ④**: 클라 Snapshot/Restore, commit gate, 예측, reconciliation — `LOPGameEngine` 측 `SnapshotHistory` 등 (`netcode-redesign.md` §6.5)
- AutoMapper IL2CPP 위험 의사결정 (모바일 빌드 임박 시 별도 작업)
- 와이어 envelope (`WorldEventBatch`) 도입 — 서버 측 변경 필요
- protoc 멀티 OS 지원 (현재 win64만 — 향후 결정)
- 클라/서버 측 *Assembly-CSharp 분리* (asmdef 격리) — 이 작업의 범위 밖, 향후 별도 작업

## Slice 0 — 부트스트랩

### 0-A. 저장소 인프라

**로컬**:
- `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/` 폴더 생성
- `git init` + `.gitignore` (아래 §0-E)
- `README.md` (use-side 의존 계약, `lop-repo-topology.md`의 use-side 계약 섹션과 정합)
- `package.json` (`com.baegames.lop.shared`, version `0.0.1`, deps: `com.baegames.gameframework: 0.0.2`)
- 5개 asmdef 빈 껍데기 + 폴더 (`Runtime/Scripts/`, `Runtime.Generated/Scripts/`, `Editor/Scripts/`, `Tests/EditMode/`, `Tests/PlayMode/`)
- 초기 커밋 `chore: bootstrap LOP-Shared package skeleton`

**원격**:
- 사용자가 GitHub 원격 `Baeinsoo/LeagueOfPhysical-Shared` 생성 후 push (private/public 결정은 사용자) — `gh repo create Baeinsoo/LeagueOfPhysical-Shared --private --source=. --remote=origin --push`

### 0-B. asmdef 정의

5개 asmdef는 다음 정의로 시작 (Slice 1 이전 *코드 0줄*이라도 컴파일 통과해야 함):

**`Runtime/baegames.LOP.Shared.Runtime.asmdef`**
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

**`Runtime.Generated/baegames.LOP.Shared.Generated.asmdef`**
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

`overrideReferences: true` + `precompiledReferences: ["Google.Protobuf.dll"]`로 use-side가 제공하는 Google.Protobuf DLL을 이름으로 매칭. (Slice 0에선 Generated 안 코드가 없으므로 컴파일 통과 — Slice 1에서 활성)

**`Editor/baegames.LOP.Shared.Editor.asmdef`**
```json
{
  "name": "baegames.LOP.Shared.Editor",
  "rootNamespace": "LOP.Editor",
  "references": [
    "baegames.LOP.Shared.Runtime",
    "baegames.LOP.Shared.Generated"
  ],
  "includePlatforms": ["Editor"],
  "autoReferenced": false,
  "noEngineReferences": false
}
```

**`Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef`**
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
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false,
  "noEngineReferences": false
}
```

**`Tests/PlayMode/baegames.LOP.Shared.Tests.PlayMode.asmdef`**
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
  "optionalUnityReferences": ["TestAssemblies"],
  "autoReferenced": false,
  "noEngineReferences": false
}
```

각 asmdef 옆에 `.meta` 파일은 Unity가 자동 생성 시 같이 커밋.

### 0-C. package.json

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

Mirror, R3, VContainer, UniTask, AutoMapper, Google.Protobuf 같은 외부 의존은 **`dependencies`에 선언하지 않음** — use-side 책임. README에 *문서적 계약*으로 명시.

### 0-D. README.md

`lop-repo-topology.md`의 "use-side 계약" 섹션을 참조하는 짧은 안내:

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

### 0-E. .gitignore

Unity 패키지 표준:

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

`Runtime.Generated/Scripts/*`는 **커밋** — use-side가 .sh 실행 환경 없이도 동작.
`Tools/Protobuf/protoc-28.2-win64/`도 **커밋** — codegen에 필요.

### 0-F. 클·서 manifest 변경

양쪽 `Packages/manifest.json`에 동일 변경:

```diff
 {
+  "scopedRegistries": [
+    {
+      "name": "Unity NuGet",
+      "url": "https://unitynuget-registry.openupm.com",
+      "scopes": ["org.nuget"]
+    }
+  ],
   "dependencies": {
     "com.baegames.gameframework": "file:C:/Users/re5na/workspace/LOP/GameFramework",
+    "com.baegames.lop.shared":    "file:C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared",
+    "org.nuget.google.protobuf":  "3.28.2",
     // ... 기존 dependencies
   },
   "testables": [
-    "com.baegames.gameframework"
+    "com.baegames.gameframework",
+    "com.baegames.lop.shared"
   ]
 }
```

### 0-G. 클·서 NuGetForUnity 정리

양쪽 `Assets/NuGetForUnity/packages.config`에서 Google.Protobuf 항목 제거:

```diff
   <package id="AutoMapper" version="11.0.0" manuallyInstalled="true" />
-  <package id="Google.Protobuf" version="3.28.2" manuallyInstalled="true" />
   <package id="System.Runtime.CompilerServices.Unsafe" version="4.5.2" />
```

Unity Editor의 **NuGet → Manage NuGet Packages → Restore**로 `Assets/NuGetForUnity/Packages/Google.Protobuf.3.28.2/` 폴더 자동 정리.

### 0-H. Slice 0 검증 (코드 0줄 이전 기준선)

- [ ] 클라 Unity 자동 패키지 resolve → 컴파일 통과
- [ ] 서버 Unity 자동 패키지 resolve → 컴파일 통과
- [ ] Test Runner에 `baegames.LOP.Shared.*` 어셈블리 표시 (testables 등록 확인)
- [ ] UnityNuGet의 `org.nuget.google.protobuf` 3.28.2가 `Google.Protobuf.dll`을 use-side에 제공
- [ ] 기존 `Assets/Scripts/Generated/Protobuf/*.cs`가 여전히 `using global::Google.Protobuf;` 컴파일 통과
- [ ] 게임 한 라운드 플레이 — proto 메시지 송수신 정상 (NuGet → UPM 전환 회귀 확인)

## Slice 1 — wire/proto + codegen 파이프라인 이전

### 1-A. 사전 검증 (시드 전)

양쪽 자원이 *완전 동일*한지 확인 — 동일하지 않으면 어느 쪽이 옳은지 결정 후 통일.

```bash
diff -rq C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Protos \
         C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Protos

diff -rq C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Tools/Protobuf \
         C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Tools/Protobuf

diff -rq C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Scripts \
         C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Scripts
```

`Scripts/`는 클라 측 `upload-*.sh` 차이가 보일 것 (클라 전용) — 클라엔 유지, 서버엔 없음. 그 외 `{compile,generate}_*.sh`는 *완전 동일* 기대.

### 1-B. 이전 대상 — 산출물 + 인프라

| 출처 (양쪽 동일) | 대상 (LOP-Shared) | 어셈블리 |
|---|---|---|
| `Assets/Scripts/Network/Message/MessageFactory.cs(.meta)` | `Runtime/Scripts/Network/Message/MessageFactory.cs` | Runtime |
| `Assets/Scripts/Network/Message/MessageHandler.cs(.meta)` | `Runtime/Scripts/Network/Message/MessageHandler.cs` | Runtime |
| `Assets/Scripts/Generated/MessageIds.cs(.meta)` | `Runtime.Generated/Scripts/MessageIds.cs` | Generated |
| `Assets/Scripts/Generated/MessageInitializer.cs(.meta)` | `Runtime.Generated/Scripts/MessageInitializer.cs` | Generated |
| `Assets/Scripts/Generated/Protobuf/*.cs(.meta)` (proto-generated, ~28 파일) | `Runtime.Generated/Scripts/Protobuf/*.cs` | Generated |
| `Assets/Scripts/Generated/Protobuf/*.IMessage.cs(.meta)` (수기 partial, ~13 파일) | `Runtime.Generated/Scripts/Protobuf/*.IMessage.cs` | Generated |

### 1-C. 이전 대상 — codegen 파이프라인

| 출처 | 대상 (LOP-Shared) |
|---|---|
| `Protos/*.proto` (~24 파일) | `Protos/*.proto` (Shared 루트, 패키지 *바깥*) |
| `Tools/Protobuf/protoc-28.2-win64/` | `Tools/Protobuf/protoc-28.2-win64/` (Shared 루트) |
| `Scripts/compile_protos.sh` | `Scripts/compile_protos.sh` (Shared 루트) — 출력 경로 수정 |
| `Scripts/generate_imessage.sh` | `Scripts/generate_imessage.sh` — 출력 경로 수정 |
| `Scripts/generate_message_ids.sh` | `Scripts/generate_message_ids.sh` — 출력 경로 수정 |
| `Scripts/generate_message_initializer.sh` | `Scripts/generate_message_initializer.sh` — 출력 경로 수정 |
| `Scripts/generate_protos.sh` | `Scripts/generate_protos.sh` — 출력 경로 수정 |

스크립트 출력 경로 변경:

```diff
# compile_protos.sh
- OUT_PATH="../Assets/Scripts/Generated/Protobuf"
+ OUT_PATH="../Runtime.Generated/Scripts/Protobuf"

# generate_imessage.sh
- OUTPUT_DIR="../Assets/Scripts/Generated/Protobuf"
+ OUTPUT_DIR="../Runtime.Generated/Scripts/Protobuf"

# generate_message_ids.sh
- OUTPUT_FILE="../Assets/Scripts/Generated/MessageIds.cs"
+ OUTPUT_FILE="../Runtime.Generated/Scripts/MessageIds.cs"

# generate_message_initializer.sh
- OUTPUT_FILE="../Assets/Scripts/Generated/MessageInitializer.cs"
+ OUTPUT_FILE="../Runtime.Generated/Scripts/MessageInitializer.cs"

# generate_protos.sh (orchestrator)
- rm -rf ../Assets/Scripts/generated
- mkdir -p ../Assets/Scripts/generated
+ rm -rf ../Runtime.Generated/Scripts/Protobuf
+ rm -f  ../Runtime.Generated/Scripts/MessageIds.cs
+ rm -f  ../Runtime.Generated/Scripts/MessageInitializer.cs
+ mkdir -p ../Runtime.Generated/Scripts/Protobuf
```

`PROTOC`(`../Tools/Protobuf/protoc-28.2-win64/bin/protoc`), `PROTO_PATH`(`../Protos`), `INCLUDE_PATH`는 그대로 동작 (Shared 루트 기준 상대 경로).

### 1-D. 이전 단계

**Step 1: 시드 (클라 → Shared)**

클라가 시드 쪽. 이유:
- 현 git status가 클라 워크트리에서 작업 중 (`Packages/manifest.json` 수정 등이 이미 클라에서 시작됨)
- 양쪽 산출물이 .meta GUID 외엔 *바이트 동일*이고 .meta GUID는 어차피 한쪽만 살아남음 → 어느 쪽이든 정합. 클라 선택은 *현 워크플로우 정합* 차원
- 두 쪽 모두 MonoBehaviour 아닌 정적 클래스/데이터 클래스라 GUID 보존이 *결과에 영향 없음* (§1-E)

산출물 `.meta` GUID는 *클라 GUID 보존* (Shared에 그대로 옮김).

```bash
# 산출물
git mv Assets/Scripts/Network/Message/MessageFactory.cs   ../LeagueOfPhysical-Shared/Runtime/Scripts/Network/Message/
git mv Assets/Scripts/Network/Message/MessageHandler.cs   ../LeagueOfPhysical-Shared/Runtime/Scripts/Network/Message/
git mv Assets/Scripts/Generated/MessageIds.cs             ../LeagueOfPhysical-Shared/Runtime.Generated/Scripts/
git mv Assets/Scripts/Generated/MessageInitializer.cs     ../LeagueOfPhysical-Shared/Runtime.Generated/Scripts/
git mv Assets/Scripts/Generated/Protobuf/*.cs             ../LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/
# .meta 동반 (Unity가 .cs 옆에 .meta 자동 따라옴)

# 진실원본 + 도구 + 스크립트
git mv Protos               ../LeagueOfPhysical-Shared/
git mv Tools/Protobuf       ../LeagueOfPhysical-Shared/Tools/
git mv Scripts/compile_protos.sh                ../LeagueOfPhysical-Shared/Scripts/
git mv Scripts/generate_imessage.sh             ../LeagueOfPhysical-Shared/Scripts/
git mv Scripts/generate_message_ids.sh          ../LeagueOfPhysical-Shared/Scripts/
git mv Scripts/generate_message_initializer.sh  ../LeagueOfPhysical-Shared/Scripts/
git mv Scripts/generate_protos.sh               ../LeagueOfPhysical-Shared/Scripts/
```

> 클라가 *별도 git 저장소*라 `git mv` 한 번에 양쪽이 안 됨 — 실제로는 클라에선 `git rm` + Shared 쪽에서 `git add` 패턴. 위는 의도 표기.

스크립트 5개의 출력 경로를 §1-C대로 수정.

**Step 2: 클라 Unity 검증**

- 클라 Unity 열기 → 자동 reimport → 컴파일 통과
- 패키지로부터 동일 타입(`LOP.MessageFactory`, `LOP.MessageIds`, 등) 자동 노출 (`Auto Referenced: true`, `namespace LOP` 유지)
- 한 라운드 플레이 — 메시지 송수신 정상

**Step 3: 서버 측 중복 제거**

서버에선 *클라가 가져간 동일 자원*을 모두 삭제:

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git rm Assets/Scripts/Network/Message/MessageFactory.cs{,.meta}
git rm Assets/Scripts/Network/Message/MessageHandler.cs{,.meta}
git rm Assets/Scripts/Generated/MessageIds.cs{,.meta}
git rm Assets/Scripts/Generated/MessageInitializer.cs{,.meta}
git rm Assets/Scripts/Generated/Protobuf/*.cs{,.meta}
git rm -r Protos
git rm -r Tools/Protobuf
git rm Scripts/compile_protos.sh
git rm Scripts/generate_imessage.sh
git rm Scripts/generate_message_ids.sh
git rm Scripts/generate_message_initializer.sh
git rm Scripts/generate_protos.sh
```

서버 Unity 열기 → reimport → 컴파일 통과 (Shared 패키지로부터 동일 타입 자동 노출).

### 1-E. GUID 정책

이전 대상 파일은 모두 **MonoBehaviour/ScriptableObject 아님** (정적 클래스, 도메인 데이터 클래스, partial). prefab·scene·SerializedField가 `.meta` GUID로 참조할 가능성 0.

- 클라 `.cs.meta` GUID를 그대로 보존 → Shared에 이동
- 서버 `.cs.meta`는 *버림* (Step 3에서 삭제)
- **GUID 불일치 위험 0**

향후 슬라이스(특히 Slice 4 호스트 매니저 흡수)에서 MonoBehaviour를 옮길 땐 정책 다름 (prefab 재바인딩) — 그건 그 슬라이스 spec에서.

### 1-F. Slice 1 검증

- [ ] **사전**: §1-A diff 결과가 codegen 자원에 대해 0 (`upload-*.sh` 제외)
- [ ] 양쪽 `Assets/Scripts/Network/Message/`, `Assets/Scripts/Generated/`, `Protos/`, `Tools/Protobuf/`, codegen `.sh` *부재*
- [ ] LOP-Shared `Scripts/generate_protos.sh` 실행 → `Runtime.Generated/Scripts/`에 산출물 정상 생성 (현재 산출물과 *바이트 단위 동일*)
- [ ] 클라 컴파일 통과
- [ ] 서버 컴파일 통과
- [ ] `[RuntimeInitializeOnLoadMethod]`로 `MessageInitializer.Initialize` 호출 확인 (Debug.Log 임시 삽입 후 13개 message creator 등록 확인 → Debug.Log 제거)
- [ ] 한 라운드 플레이 — 13종 메시지 송수신 정상 (ActionEndToC/ActionStartToC/DamageEventToC/EntityDespawnToC/EntitySnapsToC/EntitySpawnToC/GameInfoToC/GameInfoToS/InputSequenceToC/PlayerInputToS/StatAllocationToC/StatAllocationToS/UserEntitySnapToC)
- [ ] World Core slice 3 회귀: `[World] Death entity X (killer=Y)` 로그가 그대로 (데미지 → 사망 경로 정상)
- [ ] *수정 동기 회귀*: 한 .proto에 호환 변경(예: 주석 한 줄 또는 새 `optional` 필드) 추가 + `generate_protos.sh` 실행 → 양쪽 Unity가 즉시 변경 인식 (file: 참조 file watch) + 양쪽 컴파일 통과. 변경 되돌리고 재실행해도 동일하게 양쪽 인식.

## 테스트 자리 (Slice 0에 자리만, 채우지 않음)

이번 spec scope에선 *Shared 자체*에 테스트 인프라 자리만 잡고, 내부는 비워둠.

- `Tests/EditMode/baegames.LOP.Shared.Tests.EditMode.asmdef` — 자리만
- `Tests/PlayMode/baegames.LOP.Shared.Tests.PlayMode.asmdef` — 자리만

선택적으로 Slice 1 끝에 추가할 수 있는 *짧은* EditMode 테스트 (강제 아님):
- `MessageIdsUniquenessTest` — `MessageIds`의 모든 `ushort` 상수가 유일한지 reflection 검증
- `MessageFactoryRegisterDuplicateTest` — 같은 messageId로 두 번 등록 시 경고 동작

ROI 평가 후 추가 결정. 핵심 검증은 *런타임 플레이*가 우선.

클라/서버 LOP 측 EditMode 테스트 인프라는 *이번 spec 도입 안 함* (World Core slice 2와 동일 판단 — 인프라 비용 vs ROI).

## Wiring & 의존성 갱신 요약

| 파일 | 변경 |
|---|---|
| `LeagueOfPhysical-Shared/*` | 신규 패키지 전체 (Slice 0) |
| LOP-Client `Packages/manifest.json` | scoped registry + `com.baegames.lop.shared` + `org.nuget.google.protobuf` 추가, `testables` 갱신 (Slice 0) |
| LOP-Server `Packages/manifest.json` | 동일 (Slice 0) |
| LOP-Client `Assets/NuGetForUnity/packages.config` | Google.Protobuf 제거 (Slice 0) |
| LOP-Server `Assets/NuGetForUnity/packages.config` | Google.Protobuf 제거 (Slice 0) |
| LOP-Client `Assets/NuGetForUnity/Packages/Google.Protobuf.3.28.2/` | 자동 정리 (Slice 0, Restore) |
| LOP-Server 동일 | 자동 정리 (Slice 0) |
| LOP-Client `Assets/Scripts/{Network/Message, Generated}/` | 파일 제거 (Slice 1) |
| LOP-Server `Assets/Scripts/{Network/Message, Generated}/` | 파일 제거 (Slice 1) |
| LOP-Client `Protos/`, `Tools/Protobuf/`, `Scripts/{compile,generate}_*.sh` | 파일 제거 (Slice 1) |
| LOP-Server 동일 | 파일 제거 (Slice 1) |
| LOP-Client `Scripts/upload-*.sh` | **유지** |

## 진행

- [x] 토폴로지 + 컴포지션 결정 + 명명 임시 합의 (브레인스토밍 완료)
- [x] 컨셉 md 업데이트 (`lop-repo-topology.md` 신규, `world-core-connection-architecture.md`/`netcode-redesign.md`/`CLAUDE.md` 수정)
- [ ] 이 spec 사용자 리뷰
- [ ] `writing-plans`로 구현 plan 작성
- [ ] subagent-driven 또는 inline 실행

이 spec은 두 슬라이스(0+1)를 한 spec으로 묶는다 — 부트스트랩과 wire/proto 이전이 *논리적 단위*이고, 분리하면 0 끝에 *반쪽 상태*(패키지 살아있지만 비어있음)로 commit가 머지될 위험. 한 spec으로 묶어 한 PR/머지로 처리.

## Open Decisions

- `LOPGameEngine` 명명 재논의 — Slice 4 흡수 완료 후. `lop-repo-topology.md` Open Decisions와 통합.
- 클라/서버 LOP 측 EditMode 테스트 asmdef 신설 — 이번 spec 안 함. 향후 *진짜 두꺼운 seam* 발생 시 도입.
- protoc 멀티 OS (Linux/macOS 바이너리 추가) — 현재 win64 한정. 다른 환경 빌드 필요 시 별도 결정.
- AutoMapper IL2CPP 위험 의사결정 — 모바일 빌드 임박 시 별도 작업 (수기 매핑, Mapperly compile-time mapper 등 대안 검토).
- 패키지 git URL + tag 전환 시점 (`file:` → `https://...?ref=v0.x.y`) — 안정화 후 결정.

## 참고

- [LOP 저장소 토폴로지](../../lop-repo-topology.md)
- [World Core 연결 아키텍처](../../world-core-connection-architecture.md)
- [Netcode 재설계](../../netcode-redesign.md)
- [World Core Slice 3 spec (선행)](2026-05-30-world-core-slice3-design.md)
- [UnityNuGet (org.nuget scope)](https://github.com/xoofx/UnityNuGet)
- [Photon Quantum 명명 참고](https://doc.photonengine.com/quantum/current/manual/player/player)
- [Quake 3 Architecture (Fabien Sanglard)](https://fabiensanglard.net/quake3/)
