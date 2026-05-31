# LOP 저장소 토폴로지

LOP는 5개의 git 저장소로 나뉘어 있다. 이 문서는 *어떤 코드가 어디에 사는지*, 의존 방향, 패키지 메타데이터, use-side 계약을 정의한다.

> `architecture-guidelines.md`가 *도메인 비종속* 일반 가이드라인이라면, 이 문서는 *LOP 종속* 토폴로지 결정이다.

## 5개 저장소

| 저장소 | 종류 | 역할 |
|---|---|---|
| **GameFramework** (`github.com/Baeinsoo/GameFramework.git`) | Unity 패키지 (`com.baegames.gameframework`) | 앱 비종속 엔진 인프라 — 결정론 시뮬 추상, World Core(Entity/Component/EntityRegistry), wire 추상(`IMessage`), FSM 등. 다음 프로젝트에서도 그대로 재사용 |
| **LeagueOfPhysical-Shared** (`github.com/Baeinsoo/LeagueOfPhysical-Shared.git`) | Unity 패키지 (`com.baegames.lop.shared`) | **LOP 도메인 공통** — proto 산출물, 메시지 인프라(`MessageFactory`/`MessageHandler<T>`/`MessageIds`/`MessageInitializer`), MasterData 스키마, `LOPGameSimulation`(시뮬 코어). 클라/서버가 **반드시 동일하게 본다**. |
| **LeagueOfPhysical-Client** | Unity 프로젝트 | 클라 특화 — `LOPGameEngine`(host 측), View(`LOPEntityView`, `DamageView`), 보정(`SnapReconciler`, `ServerStateReconciler`, `SnapInterpolator`), 매칭/로비, 입력 캡처. |
| **LeagueOfPhysical-Server** | Unity 프로젝트 (Mirror 호스트) | 서버 특화 — `LOPGameEngine`(host 측), AI(`LOPAIController`), 와이어 송신(`WireBroadcaster`), `EntityInputComponent`, EntityCreationDataFactory. |
| **LeagueOfPhysical-Art** (`github.com/Baeinsoo/LeagueOfPhysical-Art.git`) | git submodule | 디자이너 asset — 클라/서버 양쪽 `Assets/Art/`에 mount. |

> **LeagueOfPhysical-RoomServer**는 Node.js 프로젝트(prisma/k8s)라 이 토폴로지의 범위 밖.

## 의존 그래프

```
External (Mirror, Google.Protobuf via UnityNuGet, R3, VContainer, UniTask, AutoMapper)
              ▲
              │
       GameFramework            (앱 비종속, 재사용)
              ▲
              │
       LOP-Shared                (LOP 도메인 공통, 클·서 동일)
              ▲
              │
   ┌──────────┴──────────┐
LOP-Client             LOP-Server   (각자 특화 + Mirror 의존)
```

- 역참조 금지: Shared → Client/Server (X), GameFramework → Shared (X)
- LOP-Shared가 Mirror에 *직접* 의존하지 않는다 (wire 인터페이스가 `GameFramework.IMessage`라 가능). 네트워크 transport는 use-side(Client/Server) 책임.

## 코드 분배 기준

새 코드를 어디에 둘지의 결정 트리:

1. **앱 비종속 인프라**(다른 게임에도 그대로 쓸 만한 추상·골격)인가? → **GameFramework**
   - 예: `IGameSimulation`, `GameSimulationBase`, `ITickUpdater`, `IInputSource`, `IEventSink`, `EntityRegistry`, `WorldEventBuffer`
2. **LOP 도메인이고 클·서 양쪽이 *반드시 동일하게* 보아야 하는가**? → **LOP-Shared**
   - 예: proto-generated `.cs`, `MessageIds`, `LOPGameSimulation`(시뮬 조립), MasterData 스키마
3. **한쪽만 쓰는 I/O·View·정책**인가? → **각자**(Client / Server)
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
│       ├─ MasterData/                            (Slice 2+)
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

## use-side 계약

LOP-Shared 패키지가 *패키지 dependencies로 표현하기 어려운* 외부 의존은 README에 *문서적 계약*으로 명시한다:

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
| **Slice 0** | LOP-Shared 저장소 부트스트랩 + Google.Protobuf NuGet → UnityNuGet UPM 전환 + 양쪽 manifest 등록. 코드 0줄 이전. 양쪽 컴파일 통과 기준선 확보. | 예정 |
| **Slice 1** | wire/proto 이전 — `Network/Message/*`, `Generated/MessageIds.cs`, `Generated/MessageInitializer.cs`, `Generated/Protobuf/*` 양쪽에서 Shared로 이동(시드 → 중복 제거). 호출부 변경 0줄(`namespace LOP` 유지). | 예정 |
| Slice 2+ | MasterData 스키마/로더, 도메인 Model 점진 흡수 | 별도 spec |
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
