# GameFramework Runner 표준화 리팩터링 (umbrella)

옛 `GameEngine`을 이름만 `Runner`로 바꾼 레거시 호스트 계층을, 업계 표준(Unity DOTS / Photon
Quantum / Overwatch ECS)에 맞춰 **과감히 갈아엎는** 대대적 리팩터링. 이 문서는 트랙 전체를 정의하는
**우산(umbrella) 문서**다. 각 슬라이스는 이 문서를 근거로 자기 세부 spec/plan을 갖는다.

> 범위가 커서 한 spec에 다 담지 않는다. 여기서는 **감사 결과 · 목표 모양 · 잠금된 결정 · 슬라이스
> 분해**만 확정하고, 실제 구현은 슬라이스 단위로 진행한다(각자 브랜치→`--no-ff` 머지).

## 배경 (왜)

호스트 계층(`IRunner`/`RunnerBase`/static `Runner` + `TickUpdaterBase` + `IGameState` +
`IGamePresenter`)은 옛 `GameEngine` 시절 코드에서 **이름만 rename**됐고 구조는 그대로다. 감사 결과
여러 지점이 업계 표준과 어긋나거나(안티패턴), 자기 코드베이스 안에서도 이단아(예: 상태 표현)로 남아
있다. World Core(`GameFramework.World`)와 Netcode(`GameFramework.Netcode`)는 이미 정리됐는데
호스트 클러스터만 손이 안 닿았다.

- 코어↔프레젠테이션 연결 모델: `docs/world-core-connection-architecture.md`
- 레포 토폴로지(GF는 클·서 공유 패키지): `docs/lop-repo-topology.md`

## 목표 / 비목표

**목표**
- 호스트 계층을 표준 형태로 재구성: **틱 드라이버 / 순서 있는 시스템 리스트 / DI 클럭 / typed 상태**로
  책임 분해.
- 안티패턴 제거: Runner 내장 리플렉션 이벤트 버스, static 앰비언트 Service Locator, 무한 캐치업 틱 루프.
- 자기 코드베이스 일관성 회복: 상태는 표준(enum / 진짜 FSM)으로, 라이프사이클은 VContainer로 수렴.

**비목표**
- 게임플레이/시뮬 로직(`LOPWorld`, 시스템들) 동작 변경 — 이번 리팩터링은 **호스트 배선**만. sim은 이미
  `IWorld.Tick`으로 깔끔.
- 넷코드 알고리즘(예측·보정·보간) 변경 — `Reconciler`/스냅샷 로직은 배선만 바뀌고 알고리즘은 그대로.
- 매치/앱 FSM(`StateMachine<TEvent>`) 변경 — 이미 표준. 건드리지 않는다.

## 감사 결과 (findings)

호스트 계층 코드(`GameFramework/Runtime/Scripts/Game/`, `.../StateMachine/`, `.../Lifecycle/`)와
클·서 사용처를 전수 확인한 결과.

| # | 발견 | 근거(요지) |
|---|---|---|
| **A** | **Runner 내장 리플렉션 이벤트 버스** — `AddListener`/`RemoveListener`/`DispatchEvent<T>` + `[RunnerListen(type)]`. 리플렉션 메서드 스캔, `Type` 키, **payload 없는** `Action`. | 틱마다 페이즈 이벤트 ~7개 발화하나 실제 구독자는 클·서 합쳐 **4개뿐**(입력/AI/보간/GameInfo). `Before·AfterEntityUpdate`, `Before·AfterPhysicsSimulation`은 **양쪽 구독자 0** = 죽은 발화. 도메인 이벤트는 이미 MessagePipe + `WorldEventBuffer`가 담당 → **세 번째 이벤트 채널**. |
| **B** | **static 앰비언트 Service Locator** — `Runner.current`(`Run()`에서 세팅), `Runner.Time.*`, `Runner.NetworkTime.*`. | VContainer로 DI 정리된 코드베이스에서 유일한 전역 가변 상태. 단일 게임 인스턴스 강제, 테스트 적대적. 서버 `GameRuleSystem`은 이미 Runner를 우회해 `ITickUpdater` 직접 주입(순환 회피 주석). |
| **C** | **레거시 틱 루프** — `TickUpdaterBase`가 문자열 코루틴(`StartCoroutine("TickUpdateLoop")`) + **상한 없는 캐치업**(`while (tick <= processibleTick) TickBody();`). | 프레임 히칭/브레이크포인트 시 한 프레임에 무한 틱 = spiral of death. `Time.smoothDeltaTime`을 고정 틱 누적기에 혼입. |
| **D** | **`IGameState` 빈 마커 인터페이스** — `interface IGameState {}` + 마커 클래스 9개(`Initializing`/`Playing`/…). | 타입 안전 0(아무 object 대입 가능), 전이 규칙 0, 상태별 행동 0. 실체는 "단계 라벨 + 알림". 자기 코드의 진짜 FSM(`StateMachine<TEvent>`)과 딴판. |
| **E** | **`UpdateRunner()`가 `IRunner`에 public** — 틱 본문을 외부에서 직접 호출 가능. | 실제 호출자는 내부 `OnTick` 하나뿐인데 계약에 노출 = 캡슐화 누수. |
| **F** | **`IGamePresenter<T>`/`MonoGamePresenter<T>`** — "Presenter"는 UI/MVP 용어(비-UI에 쓰지 말라고 자체 문서가 경고). | 실체는 `runner` 홀더 + 로딩뷰 여닫기. **클라 전용**(서버 미사용), DI도 안 쓰고 `GetComponentInChildren`. |
| **G** | **라이프사이클 인터페이스 동물원** — `IInitializable`/`IInitializableAsync`/`IInitializable<T1..3>`/`IDeinitializable`/`IDeinitializableAsync`/`ICleanup`. teardown 개념만 3개. | VContainer의 `IInitializable`/`IAsyncStartable`/`IDisposable`을 부분 그림자. `MessageHandlerBase`는 이미 `VContainer.Unity.IInitializable`을 풀네임으로 써 충돌 우회 중. `IInitializable<T1..3>`은 구현체 없음(죽은 코드). |
| **H** | **네임스페이스 grab-bag** — 호스트 클러스터(Runner/TickUpdater/GameState/Factory/MapLoader/Presenter)가 루트 `GameFramework`에 흩어짐. | Netcode·World는 이미 `GameFramework.Netcode`/`.World`로 그룹핑됨. 호스트만 안 됨. |
| **I** | **`LOPRunner` god-object** (파생) — `[Inject]` 12개 + 12단계 틱 순서 인라인 + 물리모드 셋업 + 스냅샷 기록 전부 한 클래스. | GF 자체 잘못은 아니나 A/B가 이를 조장. A를 시스템 리스트로 바꾸면 자연 해소. |

## 목표 아키텍처 — `RunnerBase`의 책임 분해

지금 `RunnerBase` 하나가 5책임을 뭉쳐 든다. 표준(DOTS `SystemGroup`, Quantum `QuantumRunner`,
Overwatch ECS)은 이를 분해한다:

| 지금 (뭉친 god-base) | 목표 (분해) | 표준 근거 |
|---|---|---|
| ② 리플렉션 리스너 버스 | **순서 있는 시스템 리스트** `ITickSystem { void Tick(long tick, float dt); }` | DOTS `SystemGroup`, Quantum systems, Bevy schedule |
| ④ static `Runner.current`+시간 facade | **DI 클럭** — `ITickUpdater`/`INetworkTime` 직접 주입 | DI 위생(테스트·다중 인스턴스) |
| ③ `gameState` 빈 마커 홀더 | **typed `enum RunnerState`** + `event Action<RunnerState>` | .NET `ConnectionState`/`TaskStatus`, Photon `ClientState` |
| ① 코루틴 틱 루프 | **얇은 틱 드라이버** — spiral 캡 + 코루틴 정리 | Fiedler "Fix Your Timestep" |
| ⑤ networkTime 생성 훅 | DI 등록으로 흡수(별도 훅 제거) | DI 위생 |

분해 후 `LOPRunner`(finding I)는 "정렬된 시스템 리스트를 순서대로 돌리는 얇은 드라이버 + 명시적 sim
스텝(`world.Tick`)"이 된다. 사이드별 파이프라인 차이(클=reconcile/record-snapshot, 서=death-cascade/
broadcast)는 **사이드별 시스템 등록**으로 표현.

## 잠금된 결정 (locked decisions)

| 발견 | 결정 |
|---|---|
| **A** | `[RunnerListen]`/`DispatchEvent`/`AddListener`/`RemoveListener` **전부 삭제**. `interface ITickSystem { void Tick(long tick, float dt); }` + 순서 선언(phase/order), Runner는 DI로 받은 **정렬된 리스트를 순회**. 현재 훅(입력/AI/보간/GameInfo) + 인라인 스텝(물리/이벤트드레인/스냅샷/브로드캐스트)이 각각 시스템으로. `world.Tick`만 sim 코어라 별도 명시 호출 유지. 구독자 0 페이즈 자연 소멸. |
| **B** | static `Runner` 클래스 **삭제**. 클럭 = `ITickUpdater`(tick/interval/elapsed 보유) **직접 주입**, 계산값(delta/tickTime)은 거기 흡수. `INetworkTime`도 **DI 등록·주입**(클·서 Mirror) → `CreateNetworkTime()` 훅 + `Runner.NetworkTime` facade + ⑤ 제거. `Runner.current == null`("실행 중?") → 러너 상태(`RunnerState`)로 대체. |
| **C** | 틱 루프에 **프레임당 최대 스텝 캡** 도입(초과분 clamp/drop, 로그). 문자열 코루틴 → 명시적 참조 또는 `Update`/`FixedUpdate` 누적기. `Time.smoothDeltaTime` 혼입 정리. |
| **D** | `IGameState` + 마커 9개 **삭제** → **`enum RunnerState`**. 실제 안 쓰는 값 가지치기(사용처 확인 후: `None`/`Preparing`/`Prepared`/`Error` 후보). `onGameStateChanged` → `event Action<RunnerState>`. (상태 관리 결정 상세는 아래 절.) |
| **E** | `UpdateRunner()`를 `IRunner`에서 **제거**(틱 드라이버 내부로). |
| **F** | GF에서 `IGamePresenter`/`MonoGamePresenter` **삭제**. 클라 `LOPGamePresenter`는 평범한 뷰 코디네이터로 rename(비-UI 용어 회피). |
| **G** | `IInitializable<T1..3>`(죽은 코드) + 중복 sync 변형 **삭제**. `ICleanup` → VContainer `IDisposable`로 흡수(스코프 teardown). **남기는 것 = `IInitializableAsync`/`IDeinitializableAsync` 한 쌍**(VContainer가 async teardown 미제공이라 정당). |
| **H** | 호스트 클러스터를 **`GameFramework.Runner`** 네임스페이스로 그룹핑(`.World`/`.Netcode`와 짝). static `Runner` 삭제로 이름이 비어 충돌 없음. *(이름은 Open Decision — Hosting 등 대안.)* |

## 상태 관리 결정 상세 — 왜 enum이 표준인가 (③)

"상태"는 두 개의 다른 도구다. 표준은 **상태의 모양에 도구를 맞추는 것**이다.

| 도구 | 언제 | 님 코드 예 |
|---|---|---|
| **행동 FSM** (`StateMachine<TEvent>`) | 상태가 *행동을 하고*, 전이가 *규칙*일 때 | 앱 흐름(Boot/FrontEnd/InMatch), 매칭 흐름 — **정확한 선택** |
| **상태 플래그** (enum + 이벤트) | 그냥 *"어느 단계냐" + "바뀌면 알림"* 만 필요, 규칙·행동 없음 | Runner 수명 — **enum이 정답** |

Runner 수명은 상태 플래그다: 실제 행동(맵 로드/물리 셋업/틱 시작)은 이미 `Initialize`/`Run`/`Stop`
**메서드**에 있고, 전이는 호스트가 직접 몬다(외부 이벤트가 몰지 않음). 이를 `StateMachine`으로 승격하면
anemic 상태 클래스만 양산 = **과설계·YAGNI 위반**. 업계도 컴포넌트 수명/상태는 enum으로 표현한다
(.NET `ConnectionState`/`TaskStatus`/`WebSocketState`, Photon `ClientState`, 엔진 루프는 FSM이
아니라 페이즈). → **`enum RunnerState` 확정.**

## 슬라이스 분해 & 순서

각 슬라이스는 독립 머지 가능(이 프로젝트 리듬: 브랜치→`--no-ff`). 저위험 워밍업 → 큰 구조 → 네이밍 정리
순. 의존상 **4(클럭)를 5(시스템) 앞**에 둬 시스템들이 static 대신 주입 클럭을 쓰게 한다.

| 슬 | 작업 | 발견 | 위험 | 핵심 |
|---|---|---|---|---|
| **1** | 틱 루프 강화 | C | 저 | spiral 캡 + 문자열 코루틴 정리. API 무변경 워밍업. |
| **2** | 상태 enum화 | D | 저 | 마커 9개 → `RunnerState`. 격리됨. |
| **3** | 라이프사이클 통합 | G | 저~중 | 죽은 제네릭·중복 삭제, `ICleanup`→`IDisposable`. 기계적(클 6+서 3파일). |
| **4** | 클럭 DI화 | B, ⑤ | 중~고 | static 제거, `ITickUpdater`/`INetworkTime` 주입. 호출처 다수(클·서 전반). |
| **5** | 틱 파이프라인: 버스 → 시스템 리스트 | A, I | 고 | 최대 구조 작업. god-object 해소. 사이드별 시스템 등록. |
| **6** | 네이밍/네임스페이스 | E, F, H | 저 | 인터페이스 정리·Presenter rename·네임스페이스 그룹핑. rename churn은 구조 확정 후가 최소. |

각 슬라이스 착수 시 이 문서를 근거로 세부 spec/plan을 쓴다. 5번은 규모가 커 하위 스텝(현재 훅→시스템
먼저, 인라인 스텝→시스템 점진)으로 더 쪼갤 수 있다.

## 크로스-레포 영향

GF는 클·서 공유 패키지라 GF 시그니처 변경은 **양쪽 use-side 동시 수정**이다.

- **GameFramework** (`Runtime/Scripts/Game`, `StateMachine`, `Lifecycle`): 인터페이스/베이스 본체.
- **LeagueOfPhysical-Client** (`Assets/Scripts`): `LOPRunner`, `LOPTickUpdater`, `LOPGamePresenter`,
  `PlayerInputManager`, `LocalEntityInterpolator`, `Reconciler`/스냅샷 배선, `DebugHudViewModel`, DI 스코프.
- **LeagueOfPhysical-Server** (`Assets/Scripts`): `LOPRunner`, `LOPTickUpdater`, `LOPAIController`,
  `GameInfoMessageHandler`, `GameRuleSystem`, 각 message handler, `WorldEventSink`, DI 스코프.

각 슬라이스는 관련 3레포를 **한 논리 단위**로 처리하고 컴파일·EditMode 그린을 게이트로 삼는다.
GF `.cs`/`.meta` 삭제 시 클·서 stale `CS2001` 주의(refresh scope=all force).

## 리스크 & 검증

- **결정론**: 시뮬 경로(`world.Tick`, `Reconciler` 재생)는 배선만 바뀌고 로직 불변이어야 한다.
  라이브==재생 불변식(감사 #6)을 깨지 않도록, 4·5 슬라이스는 재생 경로를 함께 검증.
- **클럭 순환(4)**: `LOPTickUpdater`가 `INetworkTime`을 주입받는데, 그 networkTime은 지금 Runner가
  생성한다 → DI 등록 순서/생명주기 배선 주의(클럭이 러너보다 먼저 존재해야 함).
- **테스트**: 순수 로직(틱 캡, 상태 전이, 시스템 순서)은 GF EditMode로 단위 테스트(TDD). 배선은
  플레이 스모크(클·서 2에디터 룸 접속 + 이동/전투)로 검증.
- **네이밍 churn(6)**: 구조가 다 확정된 뒤 마지막에 몰아 rename해 재작업 최소화.

## 산업 표준 매핑

- **② 시스템 리스트**: Unity DOTS `SystemGroup`/`[UpdateBefore/After]`, Photon Quantum systems,
  Overwatch ECS(Tim Ford GDC 2017), Bevy schedule.
- **④ DI 클럭 / no static locator**: DI 위생(전역 가변 상태 = Service Locator 안티패턴). 시간 facade
  자체는 흔하나(`UnityEngine.Time`) *가변 static set-in-Run*이 문제.
- **③ enum 상태**: .NET `ConnectionState`/`TaskStatus`/`WebSocketState`, Photon `ClientState`.
  (행동 FSM은 앱·매칭 흐름처럼 상태가 일하고 이벤트가 전이를 몰 때 — 그건 이미 표준대로.)
- **① 틱 캡**: Fiedler "Fix Your Timestep"(max sub-steps).
- **호스트/시뮬 분리**: Quake3 `game.qvm`↔`quake3.exe`, Quantum `QuantumGame`↔`QuantumRunner`.

## Open Decisions

- [ ] **호스트 네임스페이스 이름(H)** — `GameFramework.Runner`(잠정) vs `GameFramework.Hosting` 등.
  6번 슬라이스 착수 시 확정.
- [ ] **`RunnerState` 값 집합(D)** — 실제 사용처 확인 후 `None`/`Preparing`/`Prepared`/`Error` 가지치기 범위.
- [ ] **5번 하위 분해** — 훅→시스템 / 인라인→시스템 두 스텝으로 나눌지, 착수 시 규모 보고 결정.
- [ ] **`ITickSystem` 순서 선언 방식** — phase enum vs 정수 order vs `[UpdateBefore/After]`류. 5번 spec에서 확정.

## 상태

감사 완료, 목표 모양·잠금 결정·슬라이스 순서 확정. 다음 = 1번 슬라이스(틱 루프 강화) 세부 spec/plan.
