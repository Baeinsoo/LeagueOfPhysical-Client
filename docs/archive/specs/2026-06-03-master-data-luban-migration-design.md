# MasterData Luban Migration Design

**Date:** 2026-06-03
**Branch (제안):** `feature/master-data-luban-migration`
**Related:** [LOP 저장소 토폴로지](../../lop-repo-topology.md) · [MasterData Slice 2a Bootstrap (superseded)](2026-06-02-master-data-slice-2a-bootstrap-design.md) · [LOP-Shared Slice 0+1](2026-05-31-lop-shared-package-design.md)

## Goal

LOP MasterData를 **Luban**([focus-creative-games/luban](https://github.com/focus-creative-games/luban)) 기반 schema-first 파이프라인으로 전환한다. Excel(Excel-embedded 스키마) → Luban codegen(typed `.cs` + 바이너리 `.bytes`) → 클·서 MasterData 패키지로 출력하고, 런타임은 Luban이 생성한 `Tables` 매니저를 **얇은 LOP 래퍼**로 감싸 네이티브로 사용한다. 기존 CSV·리플렉션 로더·수기 POCO를 제거한다.

### 이 전환이 폐기하는 것 — 2a protobuf 경로

[Slice 2a spec](2026-06-02-master-data-slice-2a-bootstrap-design.md)이 잡았던 *protobuf/.proto custom-options + Python protoc exporter* 경로(계획상 2b/2c)를 **폐기**한다. 2a가 만든 두 git 저장소·Unity 패키지(`com.baegames.lop.masterdata.{client,server}`)는 **Luban 산출물의 host로 재사용**하고, 2a의 `.proto` 스키마·protoc 도구·Python exporter는 제거한다.

### 폐기 사유

- protobuf는 *wire(패킷) 직렬화*용 설계 — 정적 read-only config인 MasterData엔 `IMessage` 기계장치(WriteTo/MergeFrom/Clone/Parser)가 과하다.
- 검증(FK/range/required)은 *포맷이 아니라 exporter가 얹는 것*이라 protobuf여야 할 이유가 약하다.
- Luban은 Excel→typed C#+바이너리를 통째로 해결하고, **클·서 분기를 group `c`/`s`(테이블·필드 단위)로 네이티브 지원**한다 — 2a가 `(lop.side)` custom option으로 수기 구현하려던 것을 대체. `ref`(FK)/`range` 검증 내장, no-reflection codegen(IL2CPP 안전).

## 잠긴 디자인 결정 (브레인스토밍 합의)

| # | 축 | 결정 |
|---|---|---|
| ① | 도구 | **Luban** 도입, protobuf/.proto/protoc/Python exporter 폐기 |
| ② | 스키마 위치 | **Excel-embedded** — 타입·그룹(c/s)·검증을 각 테이블 `.xlsx` 헤더 행 + `__tables__` 인덱스에 둠 |
| ③ | 런타임 API | **Luban 네이티브** — 생성된 `Tables` 매니저 + 테이블별 강타입 접근자(`tables.TbCharacter.Get(code)`). 기존 `IMasterDataManager` facade 폐기 |
| ④ | 도구 배포 | **`Tools/Luban/` .dll 툴체인 커밋** (2a protoc 바이너리 커밋 선례와 동일). `dotnet Luban.dll`로 실행 (.NET 8 런타임 필요) |
| ⑤ | 네임스페이스 | `topModule = LOP.MasterData` → 생성 타입이 `LOP.MasterData.Character`/`Tables`/`TbCharacter` (기존 타입명 유지, 호출 한 줄만 변경) |
| ⑥ | 슬라이스 | **3 슬라이스 α/β/γ** — 도구 부트스트랩(infra) → 클라 전환 → 서버 전환+정리. 각 저장소 변경이 원자적·독립 검증 가능 |
| ⑦ | 얇은 래퍼 | 각 패키지 `Runtime/`에 사이드별 `LOPMasterData` — `Tables` 소유 + 비동기 `.bytes` 로딩만. 도메인 로직 0 |

## Architecture (1-pager)

```
[디자이너] infrastructure/table/Datas/*.xlsx
   __tables__.xlsx          (테이블 인덱스 + 각 테이블 group c/s)
   Character.xlsx 등        (Excel-embedded: ##type / group / 데이터 행)
        │
        │  dotnet Tools/Luban/Luban.dll  (gen.sh — 클·서 타깃 2회)
        ▼
 ┌─ -t client -c cs-bin -d bin ─┐        ┌─ -t server -c cs-bin -d bin ─┐
 │ MasterData-Client 패키지       │        │ MasterData-Server 패키지        │
 │  Runtime.Generated/Scripts/   │        │  (group c 필드 없음 —          │
 │    Tables.cs, TbXxx.cs, *.cs  │        │   description / SkinAsset 제외) │
 │  Runtime.Generated/StreamingAssets/MasterData/*.bytes                  │
 │  Runtime/LOPMasterData.cs  (얇은 래퍼)                                  │
 └───────────────┬───────────────┘        └───────────────────────────────┘
                 ▼  런타임 (Slice β/γ)
   [Inject] LOPMasterData md;
   md.Tables.TbCharacter.Get(code).Speed
        │
        └─ DI: builder.Register<LOPMasterData>(Singleton)
           로딩: LoadMasterDataComponent → md.LoadAsync()
```

핵심:
- **데이터·코드 진실원본 = Excel** (Excel-embedded). `.proto` 스키마 파일은 더 이상 없음.
- **런타임 = Luban `Tables`** + 얇은 래퍼. 인터페이스 브리지 없음. FK는 Luban `ResolveRef`가 자동 해소.
- **클·서 격리 = Luban group** — 서버 산출물엔 client-only 필드/테이블이 *코드 레벨에서 부재* → DTO/보안 자동.
- **infra 부트스트랩(α)은 런타임 무변경** — 생성물을 스크래치로 빼 데이터 파이프라인만 독립 검증 (2a가 `/tmp`로 protoc 검증한 패턴).

## Luban 핵심 메커니즘 (조사 결과, reference)

- **런타임 패키지**: Unity UPM `com.code-philosophy.luban` (git URL). 생성된 `Tables`를 `new Tables(file => new ByteBuf(bytes))`로 1회 조립.
- **생성 코드 모양** (실물 확인):
  - bean: `public sealed partial class Character : Luban.BeanBase { public string Code { get; private set; } ... }` — `partial`, 읽기전용 프로퍼티.
  - 테이블 매니저: `public partial class TbCharacter` — `Dictionary<string,Character> DataMap`, `List<Character> DataList`, `Character Get(string key)`, `GetOrDefault`, indexer, `ResolveRef(Tables)`.
  - `Tables`: 테이블별 필드(`TbCharacter` 등) + `Tables(Func<string,ByteBuf> loader)` 생성자, 생성 시 `ResolveRef()`로 cross-table FK 해소.
- **제네릭 없음**: 단일 `Get<T>` 대신 테이블별 강타입 매니저 — 캐스팅/`where T` 불필요.
- **`luban.conf`**: `groups`(c/s) + `targets`(client→groups[c], server→groups[s], `manager`=Tables, `topModule`) + 스키마/데이터 경로.
- **gen**: `dotnet Tools/Luban/Luban.dll -t <target> -c cs-bin -d bin --conf luban.conf -x outputCodeDir=... -x outputDataDir=...`.
- **Android/StreamingAssets**: Luban 기본 로더는 동기 + Android는 StreamingAssets 직접 read 불가 → `.bytes`를 UnityWebRequest로 *먼저 메모리에 프리로드* 후 `new Tables(...)`. (현 CSV 로더가 이미 쓰는 패턴.)

> exact CLI 플래그/`luban.conf` 키는 커밋된 Luban 버전 기준으로 **Slice α 구현 시 확정**한다. 위는 설계 의도.

## 디렉터리 구조 (`infrastructure/table/`)

```
infrastructure/table/
  Datas/                       # (기존 source/ 대체) Luban Excel-embedded
    __tables__.xlsx            # 테이블 인덱스 (5 테이블 + 각 group)
    __enums__.xlsx             # (필요 시) enum 정의
    Character.xlsx  Skin.xlsx  SkinAsset.xlsx  Action.xlsx  Item.xlsx
  Tools/Luban/                 # 커밋된 Luban .dll 툴체인 (protoc 자리 대체)
  luban.conf                   # groups/targets/경로
  gen.sh  gen.bat              # dotnet Luban.dll 호출 (클·서 2회)
```

**제거 대상** (2a protobuf 자산):
- `infrastructure/table/proto/` (6 `.proto`)
- `infrastructure/table/tools/protoc-28.2-win64/`
- `infrastructure/table/exporter/` (Python main.py/run.sh/requirements.txt)
- `infrastructure/table/scripts/compile_masterdata_protos.sh`
- `infrastructure/table/client_column_mapping.yaml`, `server_column_mapping.yaml`

## luban.conf (대표 형태 — α에서 확정)

```jsonc
{
  "groups": [
    { "names": ["c"], "default": true },
    { "names": ["s"], "default": true }
  ],
  "schemaFiles": [
    { "fileName": "Datas/__tables__.xlsx", "type": "table" },
    { "fileName": "Datas/__enums__.xlsx",  "type": "enum"  }
  ],
  "dataDir": "Datas",
  "targets": [
    { "name": "client", "manager": "Tables", "groups": ["c"], "topModule": "LOP.MasterData" },
    { "name": "server", "manager": "Tables", "groups": ["s"], "topModule": "LOP.MasterData" }
  ]
}
```

`gen.sh` (대표):
```bash
DOTNET="dotnet"
LUBAN="Tools/Luban/Luban.dll"
CLIENT_PKG="../../LeagueOfPhysical-MasterData-Client"
SERVER_PKG="../../LeagueOfPhysical-MasterData-Server"

# Slice α: 검증용 스크래치 출력 (패키지 미투입)
#   OUT=/tmp/lop-luban-test/{client,server}
# Slice β/γ: 실제 패키지 출력 (아래)

$DOTNET $LUBAN -t client -c cs-bin -d bin --conf luban.conf \
  -x outputCodeDir="$CLIENT_PKG/Runtime.Generated/Scripts/MasterData" \
  -x outputDataDir="$CLIENT_PKG/Runtime.Generated/StreamingAssets/MasterData"

$DOTNET $LUBAN -t server -c cs-bin -d bin --conf luban.conf \
  -x outputCodeDir="$SERVER_PKG/Runtime.Generated/Scripts/MasterData" \
  -x outputDataDir="$SERVER_PKG/Runtime.Generated/StreamingAssets/MasterData"
```

## 스키마 (Excel-embedded) — 5 테이블

현재 source 필드 기준. 컬럼명은 snake_case(Luban이 PascalCase 프로퍼티로 변환), key = `code`(string).

| 테이블 | 테이블 group | 필드 (group `c`=클라전용 표기) |
|---|---|---|
| **Character** | c,s | code(key, required), name(required), speed(range 0,30), jump_power(range 0,30), description **(c)**, default_skin_code(ref Skin.code) |
| **Skin** | c,s | code(key, required), name(required), description **(c)** |
| **SkinAsset** | **c** (테이블 전체 클라전용) | code(key, required), skin_code(ref Skin.code), model_path(required) |
| **Action** | c,s | code(key, required), name(required), description **(c)**, class(required), duration(range 0,60), cast_time(range 0,60), cooldown(range 0,300), hp_cost(range 0,9999), mp_cost(range 0,9999) |
| **Item** | c,s | code(key, required), name(required), description **(c)**, skin_code(ref Skin.code) |

**`__tables__.xlsx`** (인덱스): 각 행이 한 테이블 — full_name(`LOP.MasterData.Character`), value_type(`Character`), index(`code`), input(`Character.xlsx`), group(`c,s` 또는 `c`).

**각 테이블 `.xlsx`** (Excel-embedded 헤더):
- `##var` 행: 컬럼명 (code, name, speed, ...)
- `##type` 행: 타입 (string, string, float, ...) — `ref` / `range` 검증을 타입 자리에 표기 (예: `string#ref=Skin.code`, `float#range=[0,30]`)
- `##group` 행: 필드 group (빈칸=전 그룹, `c`=클라전용)
- 데이터 행: 실제 값

> 정확한 Excel-embedded 헤더 토큰(`##var`/`##type`/`##group`)과 검증 표기 문법은 커밋된 Luban 버전의 `define`/`type system` 문서로 **α에서 확정**.

### 검증 매핑 (2a custom option → Luban)

| 2a | Luban | 비고 |
|---|---|---|
| `(lop.fk) = "Skin.code"` | `ref=Skin.code` | Luban `ref` — FK 검사 + `ResolveRef` 자동 해소 |
| `(lop.range) = "0,30"` | `range=[0,30]` | Luban `range` 검증 |
| `(lop.unique)` | (테이블 key는 본질적 unique) | index 컬럼은 중복 시 gen 에러 |
| `(lop.required)` | (필요 시 데이터 검증) | string 빈값 금지가 필요하면 Luban validator로 — α에서 확정 |
| `(lop.side) = "client"` | field group `c` | 필드/테이블 group |

## 런타임 (Slice β/γ)

### 얇은 래퍼 — `LOPMasterData` (각 패키지 `Runtime/`)

```csharp
namespace LOP.MasterData
{
    // 기존 LOPMasterDataManager 자리 대체. cfg.Tables 소유 + 비동기 로딩만.
    public class LOPMasterData
    {
        public Tables Tables { get; private set; }

        public async Task LoadAsync()
        {
            // Luban 기본 로더는 동기 + Android는 StreamingAssets 직접 read 불가
            // → .bytes를 UnityWebRequest로 먼저 메모리에 프리로드 (기존 CSV 로더 패턴)
            var blobs = new Dictionary<string, byte[]>();
            foreach (var name in TableFiles)
                blobs[name] = await LoadBytes($"MasterData/{name}.bytes");

            Tables = new Tables(file => new ByteBuf(blobs[file]));
        }
    }
}
```

> 사이드별 `TableFiles` 목록은 Luban 타깃별 산출물에 맞춤(서버엔 SkinAsset 없음). 가능하면 Luban이 내는 매니페스트/타깃 파일목록을 사용.

### 호출부 before/after (클라 = Slice β, 서버 = Slice γ)

**DI** — `RootLifetimeScope.cs`:
```csharp
- builder.Register<IMasterDataManager, LOPMasterDataManager>(Lifetime.Singleton);
+ builder.Register<LOPMasterData>(Lifetime.Singleton);
```

**로딩 트리거** — `LoadMasterDataComponent.cs`:
```csharp
- [Inject] private IMasterDataManager masterDataManager;
- await masterDataManager.LoadMasterData();
+ [Inject] private LOPMasterData masterData;
+ await masterData.LoadAsync();
```

**조회 4곳** — 예 `CharacterComponent.cs`:
```csharp
- [Inject] private IMasterDataManager masterDataManager;
- this.masterData = masterDataManager.GetMasterData<MasterData.Character>(characterCode);
+ [Inject] private LOPMasterData md;
+ this.masterData = md.Tables.TbCharacter.Get(characterCode);
```
(나머지: `Component/Action.cs`, `Component/ItemComponent.cs`, `Game/LOPActionManager.cs` — `md.Tables.TbAction.Get(code)` 등)

`topModule = LOP.MasterData`라 `CharacterComponent.masterData`의 타입 `MasterData.Character`는 *그대로 유지*되고, 바뀌는 건 조회 한 줄뿐.

## 슬라이스 분해

### Slice α — Luban 도구 부트스트랩 (infrastructure 전용, 런타임 무변경)

**작업**:
- `Tools/Luban/` .dll 툴체인 커밋 (Luban 릴리스에서 가져옴)
- `Datas/` — source Excel 5종을 Excel-embedded로 재구성 + `__tables__.xlsx`(+필요 시 `__enums__.xlsx`)
- `luban.conf` 작성 (groups c/s, targets client/server, topModule=LOP.MasterData)
- `gen.sh`/`gen.bat` 작성 — **출력=검증용 스크래치 폴더** (패키지 미투입)
- 2a 자산 제거 (proto/protoc/exporter/yaml — 위 "제거 대상")
- `docs/lop-repo-topology.md` 갱신 (MasterData를 Luban으로)

**검증**:
- `gen.sh` 성공 — 5 테이블 클·서 타깃 모두 `.cs` + `.bytes` 생성
- **group 분기 확인**: server 산출물에 `description` 필드 없음 + `SkinAsset` 테이블 통째 부재
- `ref`(FK) 검증 동작 (예: 잘못된 `default_skin_code` 넣으면 gen 에러)
- 양쪽 Unity 여전히 **CSV로 정상 동작** (런타임 변경 0)

### Slice β — 클라이언트 런타임 전환 (LOP-Client)

**작업**:
- `gen.sh`를 MasterData-Client 패키지로 출력하도록 (client 타깃)
- LOP-Client `Packages/manifest.json`에 `com.code-philosophy.luban` 추가
- MasterData-Client `Generated.asmdef` 참조를 `Google.Protobuf.dll` → Luban 런타임 asmdef로
- MasterData-Client `Runtime/`에 `LOPMasterData` 래퍼 작성
- 호출부 4곳 + DI + 로더 컴포넌트 전환
- 클라 제거: `Assets/Scripts/MasterData/generated/*.cs`(수기 POCO), `Assets/StreamingAssets/MasterData/*.csv`, `LOPMasterDataManager.cs`
- (GameFramework `IMasterData*`는 서버가 아직 써서 **유지**)

**검증**:
- 클라 컴파일 통과 + `.bytes`로 MasterData 로드
- 캐릭터/액션/아이템 로딩 정상, 게임 한 라운드
- World Core slice 3 회귀 (데미지→사망 흐름) 정상

### Slice γ — 서버 전환 + 최종 정리 (LOP-Server)

**작업**:
- MasterData-Server 패키지 동일 전환 (server 타깃 gen, manifest, asmdef, 래퍼, 호출부, 수기 POCO+CSV 제거)
- 이제 미사용된 GameFramework `IMasterData` / `IMasterDataManager` / `MasterDataLoader` 제거

**검증**:
- 서버 컴파일 통과 + Luban 로드 정상
- 클·서 양쪽 컴파일 + 한 라운드 플레이 정상

## 문서 갱신

- `docs/lop-repo-topology.md` — MasterData 패키지 설명을 protobuf → Luban, 의존 그래프에 `com.code-philosophy.luban` 반영, "코드 분배 기준"의 MasterData 케이스 갱신
- 이 spec — 새로 추가, `CLAUDE.md`의 `@` 자동 로드 목록에 추가
- 2a spec — 상단에 *superseded by 이 spec* 표기 (기록 보존)

## 테스트 자리

2a/Slice1 패턴과 동일 — 패키지 Tests asmdef는 자리만. 핵심 검증은 *런타임 플레이* + α의 *gen 산출물 group 분기 확인*. 클·서 LOP EditMode 테스트 인프라는 이번에도 신설 안 함(ROI).

## Out of Scope

- AutoMapper IL2CPP 의사결정 (모바일 빌드 임박 시 별도)
- Hot Reload (Editor), `.bytes` 뷰어 Editor 도구
- Luban 멀티 OS 도구 (현재 .NET dll 1벌)
- MasterData 패키지 `file:` → git URL + tag 전환 (안정화 후)
- enum 도입 (현 string 필드 유지) / `class`↔`category` 명명 정리 (아래 Open Decisions)

## Open Decisions

- [ ] **Excel-embedded 헤더 문법 + 검증 표기** — `##var`/`##type`/`##group` 토큰과 `ref=`/`range=` 문법을 커밋 Luban 버전 `define` 문서로 α에서 확정.
- [ ] **`required`(string 빈값 금지)** — Luban이 직접 제공하는지, validator로 표현할지 α에서 확정.
- [ ] **Action `class` vs `category`** — 2a `.proto`는 `category`로 rename(commit `25f8eaa`), 현 CSV/POCO는 `class`. Luban에선 `class`→`Class`(유효 식별자) 가능. Excel 컬럼명/호출부(`LOPActionManager`)와 맞춰 α에서 확정.
- [ ] **Luban 런타임 asmdef 이름** — `com.code-philosophy.luban`이 노출하는 asmdef 이름을 β에서 확인해 Generated.asmdef 참조에 명시.
- [ ] **`.NET 8 런타임`** — `dotnet Luban.dll` 실행에 필요. 개발 머신 전제 (protoc는 standalone exe였음 — 차이 명시).

## 참고

- [Luban GitHub](https://github.com/focus-creative-games/luban) · [Unity 패키지](https://github.com/focus-creative-games/luban_unity) · [예제](https://github.com/focus-creative-games/luban_examples)
- [Luban 문서](https://www.datable.cn/en/docs/intro) · [빠른 시작](https://www.datable.cn/en/docs/beginner/quickstart) · [그룹(c/s)](https://www.datable.cn/en/docs/manual/luban.conf) · [런타임 로딩](https://www.datable.cn/en/docs/beginner/loadinruntime)
- [LOP 저장소 토폴로지](../../lop-repo-topology.md)
- [MasterData Slice 2a (superseded)](2026-06-02-master-data-slice-2a-bootstrap-design.md)

## 진행

- [x] 브레인스토밍 합의 (Luban 도입, Excel-embedded, 네이티브 API, Tools 커밋, 3 슬라이스)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec review
- [ ] writing-plans로 Slice α 구현 plan 작성
- [ ] subagent-driven 또는 inline 실행

이 spec은 Luban 전환 *전체*를 설계하고 슬라이스 α/β/γ로 분해한다. 구현 plan은 **Slice α부터** 작성한다 (2a/Slice 1과 동일 — 한 슬라이스씩 plan→실행→머지).
