# LOP 저장소 토폴로지

LOP는 8개의 git 저장소로 나뉘어 있다. 이 문서는 *어떤 코드가 어디에 사는지*, 의존 방향, 패키지 메타데이터, use-side 계약을 정의한다.

> `architecture-guidelines.md`가 *도메인 비종속* 일반 가이드라인이라면, 이 문서는 *LOP 종속* 토폴로지 결정이다.

## 8개 저장소

| 저장소 | 종류 | 역할 |
|---|---|---|
| **GameFramework** (`github.com/Baeinsoo/GameFramework.git`) | Unity 패키지 (`com.baegames.gameframework`) | 앱 비종속 엔진 인프라 — 결정론 시뮬 추상, World Core(Entity/Component/EntityRegistry), wire 추상(`IMessage`), FSM 등. 다음 프로젝트에서도 그대로 재사용. (MasterData 추상은 Luban 전환으로 제거됨 — Slice γ) |
| **LeagueOfPhysical-Shared** (`github.com/Baeinsoo/LeagueOfPhysical-Shared.git`) | Unity 패키지 (`com.baegames.lop.shared`) | **LOP 도메인 공통 — 시뮬·proto·메시지** — proto 산출물(wire), 메시지 인프라(`MessageFactory`/`MessageHandler<T>`/`MessageIds`/`MessageInitializer`), `LOPGameSimulation`(시뮬 코어, Slice 4부터). **MasterData는 의도적으로 비포함**(아래 LOP-MasterData-* 참조). |
| **LeagueOfPhysical-MasterData-Client** (`github.com/Baeinsoo/LeagueOfPhysical-MasterData-Client.git`) | Unity 패키지 (`com.baegames.lop.masterdata.client`) | **클라 전용 MasterData** — **Luban** 생성 schema(`.cs`) + 데이터(`.bytes`). description 등 클라 전용 컬럼 포함(Luban group `c`). Slice 2a 부트스트랩, Luban 전환 후 자동 채워짐. |
| **LeagueOfPhysical-MasterData-Server** (`github.com/Baeinsoo/LeagueOfPhysical-MasterData-Server.git`) | Unity 패키지 (`com.baegames.lop.masterdata.server`) | **서버 전용 MasterData** — server projection(클라 전용 컬럼 제외, Luban group `s`) + 데이터. **클라 패키지와 상호 비참조** — 코드 레벨 격리로 DTO/보안. |
| **LeagueOfPhysical-Client** | Unity 프로젝트 | 클라 특화 — `LOPGameEngine`(host 측), View(`LOPEntityView`, `DamageView`), 보정(`SnapReconciler`, `ServerStateReconciler`, `SnapInterpolator`), 매칭/로비, 입력 캡처. |
| **LeagueOfPhysical-Server** | Unity 프로젝트 (Mirror 호스트) | 서버 특화 — `LOPGameEngine`(host 측), AI(`LOPAIController`), 와이어 송신(`WireBroadcaster`), `EntityInputComponent`, EntityCreationDataFactory. |
| **LeagueOfPhysical-Art** (`github.com/Baeinsoo/LeagueOfPhysical-Art.git`) | git submodule | 디자이너 asset — 클라/서버 양쪽 `Assets/Art/`에 mount. |
| **infrastructure** | Node.js + Python | RoomServer 인프라(`k8s/`) + **MasterData 파이프라인**(`table/`): Excel(Excel-embedded `Datas/`) → **Luban**(`tools/Luban/`, `luban.conf`, `gen.sh`) → `.cs` + `.bytes` → MasterData-Client/Server 패키지로 출력. group `c`/`s`로 클·서 분기. |

> **LeagueOfPhysical-RoomServer**는 Node.js 프로젝트(prisma/k8s)라 이 토폴로지의 범위 밖.

> ⚠️ **MasterData 도구 PIVOT (2026-06-03)**: 당초 Protobuf(`.proto`+protoc, 계획 2b/2c)로 가려던 MasterData를 **Luban**(focus-creative-games/luban)으로 전환. 클·서 분기는 Luban group `c`/`s`(테이블·필드 단위)로, 런타임은 Luban 생성 `Tables` 매니저를 얇은 `LOPMasterData` 래퍼로 사용. 상세·슬라이스는 `docs/superpowers/specs/2026-06-03-master-data-luban-migration-design.md`. 아래 "MasterData 패키지 구조" 절의 protoc/Google.Protobuf 기반 asmdef 세부는 *전환 전(2a)* 기준이며, 패키지 실제 배선은 Luban 전환(Slice β/γ)에서 `com.code-philosophy.luban` 참조로 정정됨. **마이그레이션 완료(α/β/γ, 2026-06-03)**: 양쪽 패키지가 Luban 생성 `.cs`+`.bytes`를 host, 클·서 런타임이 `LOPMasterData.Tables`로 동작, CSV·수기 POCO·GameFramework `IMasterData*` 모두 제거됨.

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
   ├─ Datas (Excel-embedded — 진실원본 스키마+데이터: __tables__ + #Name.xlsx)
   ├─ tools/Luban (Luban v4.9.0 툴체인)
   ├─ luban.conf (groups c/s, targets client/server, topModule=LOP.MasterData)
   ├─ gen.sh / gen.bat (dotnet Luban.dll -t {client,server} -c cs-bin -d bin)
   └─ scripts/convert_source_to_luban.py (1회 source→Excel-embedded 변환)
```

- 역참조 금지: Shared → Client/Server (X), GameFramework → Shared (X), MasterData → Client/Server (X), MasterData-Client ↔ MasterData-Server (X — 보안)
- LOP-Shared가 Mirror에 *직접* 의존하지 않는다 (wire 인터페이스가 `GameFramework.IMessage`라 가능). 네트워크 transport는 use-side(Client/Server) 책임.
- **MasterData 패키지 상호 비참조**: 클라가 server-only 컬럼(Luban group `s` 필드/테이블)을 *코드 레벨에서 알지 못함* — Luban이 client 타깃 생성 시 해당 필드/테이블을 아예 안 만듦. DTO 패턴.

## 코드 분배 기준

새 코드를 어디에 둘지의 결정 트리:

1. **앱 비종속 인프라**(다른 게임에도 그대로 쓸 만한 추상·골격)인가? → **GameFramework**
   - 예: `IGameSimulation`, `GameSimulationBase`, `ITickUpdater`, `IInputSource`, `IEventSink`, `EntityRegistry`, `WorldEventBuffer` (MasterData 추상은 Luban 전환으로 제거 — MasterData는 이제 Luban 런타임이 전담)
2. **LOP 도메인이고 클·서 양쪽이 *반드시 동일하게* 보아야 하는가**? → **LOP-Shared**
   - 예: proto-generated `.cs`(wire), `MessageIds`, `LOPGameSimulation`(시뮬 조립)
3. **LOP 도메인이지만 *클·서가 다른 projection*을 봐야 하는가**(DTO/보안)? → **LOP-MasterData-{Client,Server}**
   - 예: Character/Skin/Action/Item 등 게임 디자인 데이터. 클라는 description 포함, 서버는 제외.
4. **한쪽만 쓰는 I/O·View·정책**인가? → **각자**(Client / Server)
   - 예: `SnapReconciler`(클라), `LOPAIController`(서버), `LOPGameEngine`(호스트 — 양쪽 *서로 다른* 본문)

### 의도적으로 양쪽에 분기된 케이스

같은 책임을 *역할별로 다른 구현체*가 가지면 양쪽에 둔다 — 통합 대상 아님. 단지 *같은 인터페이스/베이스를 GameFramework에 두고* 양쪽이 구현체를 가진다.

- `LOPGameEngine` (외각 호스트) — 양쪽이 `GameEngineBase` 상속해 자기 I/O를 구현
- `IInputSource` 구현 — 클라(`PlayerInputManagerInputSource`) vs 서버(`NetworkInputSource`)
- `IEventSink` 구현 — 클라(`WorldEventBridge`) vs 서버(`WireBroadcaster`)
- `ITickUpdater` 구현 — 클라(Mirror NetworkTime smoothing) vs 서버(자기 클럭)

## LOP-Shared 패키지 구조

```
LeagueOfPhysical-Shared/
├─ package.json                                  ← com.baegames.lop.shared
├─ README.md                                     (use-side 계약 명시)
├─ .gitignore                                    (Unity 패키지 표준)
│
├─ Runtime/
│   ├─ baegames.LOP.Shared.Runtime.asmdef        ← 수기 도메인 코드
│   └─ Scripts/
│       ├─ Network/Message/                       (Slice 1: MessageFactory, MessageHandler<T>)
│       └─ Game/                                  (Slice 4: LOPGameSimulation)
│
├─ Runtime.Generated/
│   ├─ baegames.LOP.Shared.Generated.asmdef      ← proto 산출물 격리
│   └─ Scripts/
│       ├─ Protobuf/                              (Slice 1: *.cs, *.IMessage.cs)
│       ├─ MessageIds.cs                          (Slice 1)
│       └─ MessageInitializer.cs                  (Slice 1)
│
├─ Editor/
│   ├─ baegames.LOP.Shared.Editor.asmdef
│   └─ Scripts/
│
└─ Tests/
    ├─ EditMode/  baegames.LOP.Shared.Tests.EditMode.asmdef
    └─ PlayMode/  baegames.LOP.Shared.Tests.PlayMode.asmdef
```

### asmdef 의존 그래프

```
External (UnityEngine, Google.Protobuf.dll)
       ▲
       │
GameFramework.Runtime
       ▲
       │
LOP.Shared.Runtime    ────────┐  (수기 코드)
       ▲                      │
       │                      ▼
LOP.Shared.Generated  (proto 산출물, Google.Protobuf precompiled ref)
       ▲
       │
Use-side: Assembly-CSharp (Client / Server, auto reference)
```

### asmdef 정책

| asmdef | references | autoReferenced | noEngineReferences | precompiledReferences |
|---|---|---|---|---|
| Runtime | `baegames.GameFramework.Runtime` | true | false | (없음) |
| Generated | `baegames.LOP.Shared.Runtime` | true | false | `Google.Protobuf.dll` |
| Editor | Runtime, Generated | false | false | (없음) |
| Tests.EditMode | Runtime, Generated, GameFramework.Runtime | (TestAssemblies) | false | (없음) |
| Tests.PlayMode | Runtime, Generated, GameFramework.Runtime | (TestAssemblies) | false | (없음) |

- **wire 인터페이스 `IMessage`는 GameFramework에 있음** → Shared는 Mirror 비참조 가능. wire transport는 use-side.
- `MessageFactory`가 `UnityEngine.Debug`, `MessageInitializer`가 `[RuntimeInitializeOnLoadMethod]` 사용 → `noEngineReferences: false`.
- `Auto Referenced: true` (Runtime, Generated) — use-side 편의. Editor/Tests는 false.
- 네임스페이스는 `namespace LOP` 유지 (마이그레이션 시 호출부 변경 0줄).

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

### 마스터데이터 키 규약 — 기본키 = 정수 `id` (산업 표준)

설정/마스터데이터 테이블의 **기본키(고유 식별)는 정수 `id`** 를 표준으로 한다. 사람이 읽는 이름은 *키가 아니라* `name`/`code` **컬럼**(메타데이터)으로 둔다.

- **근거(산업 표준):** TrinityCore(WoW 서버, MMO 데이터 정전)의 `item_template`/`creature_template`/`spell`은 모두 **int 기본키**(entry/id) + `name` 컬럼. 일반 DB 베스트프랙티스도 "int 기본키 디폴트, string 키는 ISO 국가코드·통화 같은 *안정된 전역 표준*일 때만". Luban 자체 관용도 int 키(`TbItem.Get(12)`, `TbMail[1001]`). 숫자 키 = 인덱스 효율·FK 일관성.
- **적용:** **신규 World Core 설정 테이블**(`TbStatusEffect`, 향후 `TbAbility`)은 int `id` 기본키. 코어가 이미 int(`EntityStatType`·`AbilityData.AbilityId`·`StatusEffectData.EffectId`·`ProducesEffectIds[]`)라 정합. int→int FK(어빌리티가 효과 id 참조)도 자연스러움.
- **레거시:** 기존 `TbCharacter`/`TbAction`/`TbItem`/`TbSkin`은 string `code` 기본키(2a 부트스트랩 관성). 이는 *비표준 레거시*다. **지금 일괄 마이그레이션하지 않는다**(YAGNI) — 해당 테이블을 손대는 슬라이스에서 *기회 있을 때* int id로 수렴(예: `TbAction→TbAbility` 리네임 시). 새 string 키 테이블을 *추가하지 않는다.*
- **구분:** 엔티티 *런타임 인스턴스 id*(`EntityRegistry`/`Entity.Id`, 현재 string)는 *설정 키와 별개 축*이다 — 인스턴스 식별이지 설정 참조가 아니므로 이 규약 대상 아님(타입 변경은 netcode/Stage④ 소관).

## use-side 계약

LOP-Shared와 MasterData-Client/Server 패키지가 *패키지 dependencies로 표현하기 어려운* 외부 의존은 README에 *문서적 계약*으로 명시한다 (세 패키지 공통):

| 외부 의존 | 계약 |
|---|---|
| `com.baegames.gameframework` | `package.json`의 `dependencies`에 선언 |
| **Google.Protobuf 3.28.x** | use-side는 `org.nuget.google.protobuf`를 **UnityNuGet scoped registry**로 설치 (기존 NuGetForUnity 항목에서 이전됨) |
| Mirror | use-side가 자체 보유 (Asset Store / git URL). Shared는 미참조. |
| AutoMapper 11.x | use-side가 NuGetForUnity로 보유 (현 상태 유지). ⚠️ **IL2CPP 호환 위험** — `System.Reflection.Emit.DynamicMethod` 의존, 모바일 빌드 시 별도 의사결정 필요 |
| R3, VContainer, UniTask | use-side 보유. Shared가 *사용하기 시작할 때* asmdef references에 추가 |

### UnityNuGet scoped registry (use-side `Packages/manifest.json`)

```jsonc
{
  "scopedRegistries": [
    {
      "name": "Unity NuGet",
      "url": "https://unitynuget-registry.openupm.com",
      "scopes": ["org.nuget"]
    }
  ],
  "dependencies": {
    "com.baegames.gameframework": "file:C:/Users/re5na/workspace/LOP/GameFramework",
    "com.baegames.lop.shared":    "file:C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared",
    "org.nuget.google.protobuf":  "3.28.2",
    // ... 기존 dependencies
  },
  "testables": [
    "com.baegames.gameframework",
    "com.baegames.lop.shared"
  ]
}
```

기존 `Assets/NuGetForUnity/packages.config`에서 `Google.Protobuf` 항목 제거(NuGetForUnity의 Google.Protobuf 폴더는 Manage NuGet Packages에서 Restore로 정리).

## 편집 워크플로우

- **현재 단계(file: 참조)**: 양쪽 manifest가 `file:.../LeagueOfPhysical-Shared`로 로컬 경로를 본다. Shared 코드는 *그 폴더*에서 직접 편집·커밋·push. GameFramework와 동일 방식.
- **향후(git URL + tag 전환)**: 안정화 후 `https://github.com/Baeinsoo/LeagueOfPhysical-Shared.git?path=/Package#v0.x.y`로 클·서가 *같은 commit*을 보도록 잠금 가능.

## 마이그레이션 슬라이스 — 요약

| 슬라이스 | 작업 | 상태 |
|---|---|---|
| **Slice 0** | LOP-Shared 저장소 부트스트랩 + Google.Protobuf NuGet → UnityNuGet UPM 전환 + 양쪽 manifest 등록. 코드 0줄 이전. 양쪽 컴파일 통과 기준선 확보. | 완료 |
| **Slice 1** | wire/proto 이전 — `Network/Message/*`, `Generated/MessageIds.cs`, `Generated/MessageInitializer.cs`, `Generated/Protobuf/*` 양쪽에서 Shared로 이동(시드 → 중복 제거). 호출부 변경 0줄(`namespace LOP` 유지). | 완료 |
| **Slice 2a** | MasterData 부트스트랩 — 새 패키지 2개(MasterData-Client/Server) + `infrastructure/table/proto/` schema + protoc 도구 + 양쪽 manifest 등록. 런타임 변경 0(여전히 CSV). | 진행 중 |
| **Slice 2b** | exporter 재작성 — Excel + `.proto` → `.cs` + `.bin` → MasterData-Client/Server 패키지. side projection 자동 분기 + FK/range/required/unique 검증. CSV는 병행 출력(2c까지 비교 검증). | 예정 |
| **Slice 2c** | 런타임 전환 — `LOPMasterDataManager`를 Protobuf 로딩으로. 자동 매니페스트 도입. 기존 CSV/`generated/` 제거. StreamingAssets 패키지 통합 검증. | 예정 |
| **Slice 4(a~e)** | 시뮬 코어 추출 — GameFramework 추상 추가 → `LOPGameSimulation` 골격 → 시뮬 매니저 점진 흡수 → I/O 어댑터화 → `LOPGameEngine` 얇은 호스트화 | 별도 spec, Stage ④와 함께 |

상세는 각 슬라이스 spec/plan 참조.

## 산업 표준 매핑

이 토폴로지는 *shared game code* 컨벤션의 직접 적용:

- **Quake 3**: `quake3.exe`(engine) + `game.qvm`/`cgame.qvm`(shared game module, server/client side wrapper)
- **Source / CS:GO**: engine binary + `server.dll`/`client.dll` (shared C++ code, 조건부 컴파일)
- **Overwatch**(Tim Ford GDC 2017): 한 ECS World 코드를 양쪽이 *동일 코드*로 컴파일, 인스턴스만 다름
- **Photon Quantum**: `QuantumGame`(시뮬) ↔ `QuantumRunner`(외각 호스트)

LOP 매핑:
- GameFramework + LOP-Shared = "shared game code"
- LOP-Client / LOP-Server = 양쪽 외각 호스트 + 사이드 특화
- `LOPGameSimulation`(Shared) = "the game module" — 양쪽 동일 코드
- `LOPGameEngine`(Client / Server 각자) = host wrapper — 양쪽 *서로 다른* 본문

## 참고

- [Quake 3 Architecture (Fabien Sanglard)](https://fabiensanglard.net/quake3/)
- [Game engine — Wikipedia](https://en.wikipedia.org/wiki/Game_engine)
- [Overwatch Gameplay Architecture and Netcode (Tim Ford, GDC 2017)](https://www.gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and)
- [Photon Quantum — QuantumRunner / QuantumGame](https://doc.photonengine.com/quantum/current/manual/player/player)
- [UnityNuGet (org.nuget scope)](https://github.com/xoofx/UnityNuGet)

## 상태

토폴로지 확정. Slice 0/1 spec 작성 중. 명명 일부는 *Open Decision*(아래 참조).

## Open Decisions

- [ ] **`LOPGameEngine` 명명 재논의** — 현재 외각 호스트가 `LOPGameEngine`이지만 *엄밀 업계 정의*("engine = 인프라/플랫폼")와 어긋남. Photon Quantum의 `Runner`(외각 호스트 통용) 등으로 rename 검토. Slice 4 흡수가 끝나 호스트가 *얇아진 시점*에 결정. spec/plan 작성 시점에는 `LOPGameEngine` 유지.
- [ ] `LOPGameSimulation` 명명 — 위와 함께 묶어 재논의 (`Simulation` / `World` / `Game` 등 통용어 비교)
- [ ] **MasterData 패키지 `file:` → git URL + tag 전환 시점** — Slice 2c 안정화 후 결정. 다른 패키지(GameFramework, LOP-Shared)와 *함께* 전환할지 별도 일정인지.
- [ ] **MasterData 패키지 message-level `(lop.side)`** — 현재 field-level만. SkinAsset 같은 *전체 client-only* message는 모든 field에 `(lop.side) = "client"` 명시 중. message-level 옵션은 2b/2c에서 도입 결정.
- [ ] **protoc 도구 다중 저장소 sync** — LOP-Shared/Tools/Protobuf와 infrastructure/table/tools/protoc 두 곳에 사본. 업그레이드 시 수기 sync. 공통 도구 저장소 분리 검토.
