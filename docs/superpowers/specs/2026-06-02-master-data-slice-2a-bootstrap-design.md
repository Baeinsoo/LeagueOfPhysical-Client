# MasterData Slice 2a — Schema-first Bootstrap Design

**Date:** 2026-06-02
**Branch (제안):** `feature/master-data-slice-2a-bootstrap`
**Related:** [LOP 저장소 토폴로지](../../lop-repo-topology.md) · [LOP-Shared Slice 0+1 spec](2026-05-31-lop-shared-package-design.md) · 후속: 2b(exporter 재작성), 2c(런타임 전환)

## Goal

LOP MasterData를 **Protobuf 기반 schema-first 파이프라인**으로 전환하기 위한 **부트스트랩 슬라이스**. 다음 산출물 확보:

- `LeagueOfPhysical-MasterData-Client` + `LeagueOfPhysical-MasterData-Server` 두 git 저장소 + Unity 패키지
- `infrastructure/table/proto/` 신규 — 5종 `.proto` schema (Character/Skin/Action/Item/SkinAsset) + `lop_options.proto` custom field options
- `infrastructure/table/tools/protoc-28.2-win64/` + `infrastructure/table/scripts/` — protoc 도구 + 컴파일 스크립트 (Slice 1 LOP-Shared 패턴 복제)
- 양쪽 LOP-Client/Server `Packages/manifest.json`에 새 패키지 등록

**런타임은 여전히 CSV** — `LOPMasterDataManager`/`StreamingAssets/MasterData/*.csv`/`Assets/Scripts/MasterData/generated/*.cs` 모두 *변경 0*. 게임 동작 보존이 baseline.

## 잠긴 디자인 결정 (브레인스토밍 합의)

| # | 축 | 결정 |
|---|---|---|
| ① | 런타임 포맷 (Slice 2c) | **Protobuf** — wire 인프라(Slice 1) 통일 |
| ② | Schema 메타 위치 | **`.proto`(메타) + Excel(데이터)** — 메타 진실원본은 `.proto` |
| ③ | 매핑 방식 | **protoc 생성** — reflection 0, IL2CPP 안전 (① 종속) |
| ④ | 검증 시점 (Slice 2b) | **빌드(exporter) only** — `.proto` custom options 보고 |
| ⑤ | Side projection | **단일 .proto + `(lop.side)` annotation** — exporter가 side별 subset 자동 생성 |
| ⑥ | 자동 매니페스트 (Slice 2c) | **exporter가 Register-All 자동 생성** — `LoadMasterData()` 하드코딩 제거 (② 종속) |
| ⑦ | Hot Reload | **미도입** (YAGNI). 디자이너 요청 시 별도 슬라이스. |
| ⑧ | 산출물 분배 | **새 Unity 패키지 2개** — `lop.masterdata.client` + `lop.masterdata.server`. 클·서 *상호 비참조*로 코드 레벨 보안. |
| ⑨ | 에러 메시지 (Slice 2b) | **row + 컬럼 + 위반 규칙** 명시 |
| (추가) | StreamingAssets | `.bin`을 패키지 `Runtime.Generated/StreamingAssets/`에 두고 use-side에 자동 통합 — Unity 2020+ 동작 검증 필요 (Slice 2b/2c 검증) |
| (추가) | protoc 위치 | **infrastructure에 복제** — Slice 1 LOP-Shared `Tools/Protobuf` 패턴 그대로 |
| (추가) | 분해 | **2a / 2b / 2c** — 각자 머지 가능한 baseline |

## Architecture (1-pager)

```
[디자이너 — Excel 그대로]
infrastructure/table/source/Character.xlsx   (row 0 = 타입, row 1 = column name, row 2+ = data)

         │   2a는 여기까지 미변경
         ▼
[infrastructure/table/proto/]   ← 2a NEW
   lop_options.proto    (custom field options: side/fk/range/required/unique)
   character.proto       (Slice 2a 정의)
   skin.proto, skin_asset.proto, action.proto, item.proto

[infrastructure/table/tools/]   ← 2a NEW (Slice 1 패턴 복제)
   protoc-28.2-win64/

[infrastructure/table/scripts/]  ← 2a NEW
   compile_masterdata_protos.sh   (.proto → .cs)

         │   2b에서: exporter가 .proto + Excel 보고
         │          → 클라/서버 패키지에 .cs (subset) + .bin 출력
         │   2a는 여기까지 *아직 채우지 않음* — 빈 껍데기 패키지만
         ▼
[LeagueOfPhysical-MasterData-Client/]  ← 2a NEW git repo + Unity package
   Runtime/                         (매니저 — 2c에서)
   Runtime.Generated/Scripts/       (.cs from protoc — 2b에서)
   Runtime.Generated/StreamingAssets/MasterData/  (.bin — 2b에서)

[LeagueOfPhysical-MasterData-Server/]  ← 동일 NEW
   (description 등 클라 전용 컬럼 *없음*)

         │   2a 끝 = 양쪽 LOP가 새 패키지 등록 + 컴파일 통과
         ▼
[LOP-Client manifest]              [LOP-Server manifest]
  com.baegames.lop.masterdata.client  com.baegames.lop.masterdata.server
  (file: 참조)                        (file: 참조)

[런타임은 여전히 CSV — 변경 0]
LOPMasterDataManager.LoadFromCSV ─→ 기존 StreamingAssets/MasterData/*.csv
Assets/Scripts/MasterData/generated/*.cs (수기)
```

핵심:
- **2a는 *부트스트랩만***. 새 패키지 + 새 `.proto` + protoc 도구 + manifest 등록. 런타임 흐름 *완전 비변경*.
- **2b가 *exporter 재작성***. Excel + `.proto` → side별 `.cs` + `.bin` → 새 패키지로 출력. CSV는 *병행 출력*해서 비교 검증 가능.
- **2c가 *런타임 전환***. 매니저를 Protobuf 로딩으로 갈고 기존 CSV/`generated/` 제거. *호출부 변경 0줄*.

## 토폴로지 변경

`docs/lop-repo-topology.md` 갱신 대상. 6 → 8 저장소:

| 저장소 | 종류 | 역할 |
|---|---|---|
| GameFramework | Unity 패키지 | 앱 비종속 인프라 |
| LOP-Shared | Unity 패키지 | LOP 도메인 공통 (proto/wire/MasterData *추상*은 포함, *데이터*는 비포함) |
| LOP-Client | Unity 프로젝트 | 클라 특화 |
| LOP-Server | Unity 프로젝트 | 서버 특화 |
| LOP-Art | git submodule | Art assets |
| LOP-MasterData-Client | Unity 패키지 | **NEW** — 클라용 schema + 데이터 |
| LOP-MasterData-Server | Unity 패키지 | **NEW** — 서버용 schema + 데이터 (description 등 클라 전용 제외) |
| infrastructure | Node + Python | k8s 매니페스트 + table 파이프라인 (Excel→`.proto`→`.cs`+`.bin`) |

의존 방향:

```
External (Mirror, Google.Protobuf via UnityNuGet)
       ▲
       │
GameFramework
       ▲
       ├─ LOP-Shared
       │      ▲
       │      │
       ├─ LOP-MasterData-Client ──┐
       │                            │
       ├─ LOP-MasterData-Server ──┐│
       │                          ││
       │     ┌────────────────────┘│
       │     │  ┌──────────────────┘
       ▼     ▼  ▼
   LOP-Client     LOP-Server
```

- **MasterData 패키지는 LOP-Shared를 참조하지 않음** (Shared가 MasterData에 의존하지도 않음). 둘은 *형제* — 각자 GameFramework만 의존.
- **클라/서버 MasterData 패키지 상호 비참조**: 패키지가 *완전 격리*. 코드 레벨에서 클라가 server-only 필드를 *알지 못함* → DTO/보안.

## 2a 작업 목록

### 1. 새 git 저장소 부트스트랩 (Slice 0 패턴 그대로)

각 새 패키지에 대해 동일 절차:

**로컬**:
```
C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client/
├─ package.json
├─ README.md
├─ .gitignore
├─ Runtime/
│   └─ baegames.LOP.MasterData.Client.Runtime.asmdef
├─ Runtime.Generated/
│   ├─ baegames.LOP.MasterData.Client.Generated.asmdef
│   ├─ Scripts/MasterData/       (.cs from protoc — 2b에서 채움)
│   └─ StreamingAssets/MasterData/   (.bin — 2b에서 채움)
├─ Editor/
│   └─ baegames.LOP.MasterData.Client.Editor.asmdef
└─ Tests/
    ├─ EditMode/  baegames.LOP.MasterData.Client.Tests.EditMode.asmdef
    └─ PlayMode/  baegames.LOP.MasterData.Client.Tests.PlayMode.asmdef
```

**원격**: 사용자가 GitHub repo 2개 생성 + push. Slice 0/1 패턴(예: `gh repo create Baeinsoo/LeagueOfPhysical-MasterData-Client --public --source=. --remote=origin --push`) 또는 GitHub UI로 생성 후 `git remote add` + `git push`. public/private 결정은 사용자.

### 2. package.json

`LeagueOfPhysical-MasterData-Client/package.json`:
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

`...-Server/package.json` 동일하지만 name = `com.baegames.lop.masterdata.server`, displayName/description 조정.

### 3. asmdef 정의

**`Runtime/baegames.LOP.MasterData.Client.Runtime.asmdef`**:
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

**`Runtime.Generated/baegames.LOP.MasterData.Client.Generated.asmdef`**:
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

`baegames.GameFramework.Runtime`을 Generated에 직접 참조 — Slice 1에서 발견된 *전이 의존 미해결* 문제와 동일 (protoc 생성 `.cs`가 `GameFramework.IMessage`/`IMasterData` 구현 시 직접 참조 필요).

**`Editor/`, `Tests/EditMode/`, `Tests/PlayMode/`** — LOP-Shared 패턴과 동일 (TestAssemblies, autoReferenced: false).

서버 패키지는 *Client → Server*로 명명 치환.

### 4. README.md + .gitignore

LOP-Shared 패턴 그대로. README는 use-side 계약 명시:

```markdown
# LeagueOfPhysical-MasterData-Client

League of Physical 클라 전용 MasterData (Protobuf schema + 데이터).

## 책임

- proto 산출물 (Character/Skin/Action/Item/SkinAsset .cs)
- 데이터 (.bin in StreamingAssets)

## Use-side Requirements

- `com.baegames.gameframework` (package.json dependencies)
- `org.nuget.google.protobuf` (UnityNuGet)

## Editing

이 패키지는 *exporter 산출물*이라 직접 편집 금지. 변경하려면 `infrastructure/table/` 에서 Excel/.proto 수정 + exporter 재실행.
```

`.gitignore`는 Unity 표준 (Slice 1).

### 5. infrastructure/table/proto/ 신규

`infrastructure/table/proto/lop_options.proto`:
```protobuf
syntax = "proto3";
package lop;
import "google/protobuf/descriptor.proto";

extend google.protobuf.FieldOptions {
  // side projection: "client" | "server" | "both" (생략 시 "both")
  string side     = 51001;

  // foreign key: "TableName.column" 형식 (예: "Skin.code")
  string fk       = 51002;

  // 숫자 범위: "min,max" (예: "0,30")
  string range    = 51003;

  // 빈 값 금지
  bool   required = 51004;

  // 테이블 내 unique
  bool   unique   = 51005;
}
```

`infrastructure/table/proto/character.proto`:
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

`skin.proto`:
```protobuf
syntax = "proto3";
package LOP.MasterData;
import "lop_options.proto";

message Skin {
  string code        = 1 [(lop.unique) = true, (lop.required) = true];
  string name        = 2 [(lop.required) = true];
  string description = 3 [(lop.side)    = "client"];
}
message SkinTable { repeated Skin entries = 1; }
```

`skin_asset.proto` (현재 클라 전용):
```protobuf
syntax = "proto3";
package LOP.MasterData;
import "lop_options.proto";

message SkinAsset {
  string code       = 1 [(lop.unique)   = true, (lop.required) = true, (lop.side) = "client"];
  string skin_code  = 2 [(lop.fk)       = "Skin.code", (lop.side) = "client"];
  string model_path = 3 [(lop.required) = true, (lop.side) = "client"];
}
message SkinAssetTable { repeated SkinAsset entries = 1; }
```

> SkinAsset 전체가 *클라 전용*이므로 message 자체에 side annotation 자리 검토 필요. 현재 `(lop.side)`는 *field-level*. message-level은 `(lop.side_message)` 같은 추가 옵션 또는 *모든 필드에 client 명시*로 해결. **Slice 2a 결정**: 모든 필드에 `(lop.side) = "client"` 명시 (단순함). message-level은 2b/2c에서 도입 결정.

`action.proto`:
```protobuf
syntax = "proto3";
package LOP.MasterData;
import "lop_options.proto";

message Action {
  string code        = 1 [(lop.unique) = true, (lop.required) = true];
  string name        = 2 [(lop.required) = true];
  string description = 3 [(lop.side) = "client"];
  string class       = 4 [(lop.required) = true];
  float  duration    = 5 [(lop.range) = "0,60"];
  float  cast_time   = 6 [(lop.range) = "0,60"];
  float  cooldown    = 7 [(lop.range) = "0,300"];
  float  hp_cost     = 8 [(lop.range) = "0,9999"];
  float  mp_cost     = 9 [(lop.range) = "0,9999"];
}
message ActionTable { repeated Action entries = 1; }
```

`item.proto`:
```protobuf
syntax = "proto3";
package LOP.MasterData;
import "lop_options.proto";

message Item {
  string code        = 1 [(lop.unique) = true, (lop.required) = true];
  string name        = 2 [(lop.required) = true];
  string description = 3 [(lop.side) = "client"];
  string skin_code   = 4 [(lop.fk)   = "Skin.code"];
}
message ItemTable { repeated Item entries = 1; }
```

> 명명: `.proto` 파일명은 snake_case (`skin_asset.proto`), message는 PascalCase (`SkinAsset`). Slice 1 wire .proto와 일관.

### 6. protoc 도구 복제

LOP-Shared의 도구를 infrastructure로 *수기 cp* (한 번만, ~5MB binary):

```bash
cp -rv C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tools/Protobuf \
       C:/Users/re5na/workspace/LOP/infrastructure/table/tools/
```

결과 구조:
```
infrastructure/table/
├─ tools/
│   └─ protoc-28.2-win64/    (cp 결과)
└─ scripts/
    └─ compile_masterdata_protos.sh
```

향후 protoc 버전 업그레이드 시 *두 곳 sync* 필요 — Open Decisions에 명시.

`scripts/compile_masterdata_protos.sh` (Slice 1 패턴):
```bash
#!/bin/bash
set -e

PROTOC="../tools/protoc-28.2-win64/bin/protoc"
PROTO_PATH="../proto"
INCLUDE_PATH="../tools/protoc-28.2-win64/include"

# 2a 단계는 *출력 대상* 빈 패키지 디렉토리에 직접 .cs 생성하지 않음.
# 컴파일 자체가 통과하는지만 검증.
OUT_PATH="/tmp/lop-masterdata-cs-test"
mkdir -p "$OUT_PATH"

cd "$(dirname "$0")"

for proto in "$PROTO_PATH"/*.proto; do
  echo "[compile] $proto"
  "$PROTOC" \
    --proto_path="$PROTO_PATH" \
    --proto_path="$INCLUDE_PATH" \
    --csharp_out="$OUT_PATH" \
    "$proto"
done

echo "[done] 컴파일 통과. 2a 검증 완료."
```

2a 단계의 컴파일 스크립트는 *출력을 새 패키지에 쓰지 않음* — 단순 *컴파일 통과 검증*. 실제 출력은 2b에서.

### 7. 양쪽 LOP 패키지 manifest 등록

LOP-Client `Packages/manifest.json`:
```diff
   "dependencies": {
     "com.baegames.gameframework": "file:.../GameFramework",
     "com.baegames.lop.shared":    "file:.../LeagueOfPhysical-Shared",
+    "com.baegames.lop.masterdata.client": "file:C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client",
     ...
   },
   "testables": [
     "com.baegames.gameframework",
     "com.baegames.lop.shared",
+    "com.baegames.lop.masterdata.client"
   ]
```

LOP-Server `Packages/manifest.json` 동일 패턴, `...-Server` 패키지로.

### 8. 토폴로지 문서 갱신

`docs/lop-repo-topology.md` 수정:
- 5 → 8 저장소 표 갱신
- MasterData 패키지의 *클·서 별도 + 상호 비참조* 명시
- LOP-Shared가 *MasterData 비포함*이라는 *명시적 비-소속* 명시
- 의존 그래프 갱신 (위 ASCII)
- Open Decisions: MasterData 패키지 *git URL + tag* 전환 시점

추가로 `lop-repo-topology.md`의 코드 분배 결정 트리에 *MasterData* 케이스 명시.

## 2a 검증 (baseline)

- [ ] LOP-MasterData-Client 저장소 로컬 생성 + 5 asmdef 컴파일 통과 (빈 껍데기)
- [ ] LOP-MasterData-Server 동일
- [ ] GitHub 원격 생성 + push (사용자가 수행)
- [ ] `infrastructure/table/scripts/compile_masterdata_protos.sh` 실행 → 5개 `.proto` 모두 protoc 컴파일 통과 (산출물은 `/tmp`)
- [ ] LOP-Client `Packages/manifest.json`에 `masterdata.client` 추가 → Unity 자동 resolve → 컴파일 통과
- [ ] LOP-Server 동일
- [ ] Test Runner에 `baegames.LOP.MasterData.Client.Tests.*`, `baegames.LOP.MasterData.Server.Tests.*` 표시
- [ ] **게임 한 라운드 플레이 — 기존 CSV 동작 보존** (런타임 변경 0)
- [ ] World Core slice 3 회귀 정상 (데미지 → 사망 흐름)

## 테스트 자리

Slice 1 패턴과 동일 — *자리만 잡고 채우지 않음*. Tests/EditMode/PlayMode asmdef만. 실제 테스트는 2b/2c에서 추가:
- 2b: exporter 검증 테스트 (FK/range/unique/required)
- 2c: 매니저 Protobuf 로딩 단위 테스트

## Out of Scope (2b/2c, 별도)

**2b (exporter 재작성 + 데이터 출력)**:
- Python exporter 재작성 (Excel + .proto → .cs + .bin)
- side projection 자동 분기 (client/server subset 생성)
- FK/range/required/unique 검증
- `.cs` + `.bin` 양쪽 새 패키지에 출력 (run.sh 갱신)
- CSV 병행 출력 유지 (2c까지 *옆에 둠*, 비교 검증용)
- Register-All 매니페스트 .cs 자동 생성 (LOPMasterDataManager 대체용)

**2c (런타임 전환)**:
- `LOPMasterDataManager` Protobuf 로딩 전환
- 자동 생성 매니페스트 도입 (LoadMasterData 하드코딩 제거)
- 기존 `Assets/Scripts/MasterData/generated/*.cs` 제거 (이전 시 사용자 LoadMasterData 호출 전부 확인)
- 기존 `Assets/StreamingAssets/MasterData/*.csv` 제거
- `MasterDataLoader.LoadFromCSV` deprecate 또는 제거 결정
- StreamingAssets 패키지 통합 동작 검증 (Unity 2020+ 패키지 안 StreamingAssets 자동 통합)

**Stage ④/별도**:
- Hot Reload (Editor)
- `.bin` viewer Editor 도구
- AutoMapper IL2CPP 호환 검토
- protoc 멀티 OS (Linux/macOS)
- 패키지 `file:` → `git URL + tag` 전환

## 명명 / 컨벤션

- `.proto` 파일명: snake_case (`character.proto`, `skin_asset.proto`)
- message 이름: PascalCase (`Character`, `SkinAsset`)
- field 이름: snake_case (`default_skin_code`) — protoc가 자동으로 PascalCase property (`DefaultSkinCode`)로 생성
- C# namespace: `LOP.MasterData`
- 패키지명: `com.baegames.lop.masterdata.{client,server}`
- asmdef 명: `baegames.LOP.MasterData.{Client,Server}.{Runtime,Generated,Editor,Tests.EditMode,Tests.PlayMode}`

## Open Decisions

- [ ] **package version 0.0.1 vs git tag**: 2a는 `file:`로 시작 (Slice 0/1 패턴). 안정화 후 `git URL + tag` 전환 시점은 별도 결정 — `lop-repo-topology.md` Open Decisions와 통합.
- [ ] **message-level `(lop.side)`**: 현재 field-level만. SkinAsset 같은 *message 전체가 한 side*인 경우 *모든 field에 명시*가 반복적. 2b/2c에서 message-level 옵션 추가 결정.
- [ ] **protoc 도구의 다중 저장소 sync**: LOP-Shared/Tools/Protobuf와 infrastructure/table/tools/protoc 두 곳에 사본. 업그레이드 시 *수기 sync*. 향후 *공통 도구 저장소* 분리 검토.

## 참고

- [LOP 저장소 토폴로지](../../lop-repo-topology.md)
- [LOP-Shared Slice 0+1 spec (선행 패턴)](2026-05-31-lop-shared-package-design.md)
- [World Core 연결 아키텍처](../../world-core-connection-architecture.md)
- [Cysharp MasterMemory](https://github.com/Cysharp/MasterMemory) — schema-first MasterData 사례
- [Protocol Buffers Custom Options](https://protobuf.dev/programming-guides/proto3/#options)

## 진행

- [x] 브레인스토밍 합의 (Protobuf + 단일 .proto annotation + 메타=.proto + 패키지 2개 + protoc infrastructure 복제 + 2a/2b/2c 분해 + default 4종 잠금)
- [x] 이 spec 작성
- [ ] 사용자 spec review
- [ ] writing-plans로 2a 구현 plan 작성
- [ ] subagent-driven 또는 inline 실행
- [ ] 2a 머지 후 2b spec 브레인스토밍
