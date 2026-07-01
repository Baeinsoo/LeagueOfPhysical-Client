# 입력 추상화 — `IInputSource` 포트 (클라 호스트, Slice 4d)

> **⛔ 보류 — Stage④로 이관 (2026-07-01 결정).** 업계 표준 `IInputSource`는 *틱별 입력 데이터를 제공/폴링하는 provider*(`Poll(tick)→데이터`; Photon Quantum `PollInput`/`SetInput`, Unity Netcode for Entities `IInputComponentData`/`ICommandData`+`GetDataAtTick`)다. 이 표준 모양은 *적용(예측 mutate)·송신을 source 밖으로* 빼야 성립 = 클라 예측/롤백 = **Stage④**. 반면 이 spec의 wrap-only `void ProcessInput()`는 capture+예측+송신을 묶은 *비표준 verb*라, 지금 GameFramework `IInputSource`로 박으면 Stage④에서 `Poll(tick)`으로 **인터페이스를 깨는 reshape**(=임의명명→표준 rename churn, CLAUDE.md 금지)이 된다. `IRandom`/`IPhysicsSimulator`는 wrap-only 모양이 *이미 최종 표준*(구현만 깊어짐)이라 안전했지만 입력은 *모양 자체가 바뀐다*는 비대칭. → **4d는 Stage④에서 표준 provider로 한 번에 구현**한다. 이 문서는 *그때의 입력 부분 설계 참고용*으로 남긴다(현 슬라이스에선 구현하지 않음). Slice 4 "닫기"는 **4e(얇은 호스트)** 로 진행.

**Date:** 2026-06-30
**Status:** 보류 → Stage④ 이관 (위 박스 참고). 미구현.
**Branch (제안):** (해당 없음 — Stage④ spec에서 다룸)
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (I/O 어댑터 — `IInputSource`) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) (`IInputSource` 구현: 클 `PlayerInputManager…` / 서 `NetworkInputSource`) · 자매 슬라이스 [물리 시뮬레이터 어댑터](2026-06-21-slice4-physics-simulator-adapter-design.md) · [RNG 추상화](2026-06-22-slice4-random-abstraction-design.md)

## Goal

클라 호스트(`LOPRunner`)가 입력 처리를 **구체 클래스(`PlayerInputManager`)가 아니라 주입된 `IInputSource`(GameFramework) 포트**를 통해 부르게 한다. 호스트의 입력 호출을 **이벤트 dispatch(`DispatchEvent<ProcessInput>()`)에서 직접 포트 호출(`inputSource.ProcessInput()`)로** 바꾼다. **동작 100% 보존** — 물리·RNG 어댑터와 동일한 port 도입 패턴.

## 배경 / 동기

Slice 4(시뮬↔호스트 분리)의 한 조각. 이미 포트 뒤로 빠진 I/O: `ITickUpdater`·`IEventSink`(`WorldEventSink`)·`IPhysicsSimulator`·`IRandom`. **`IInputSource`만 없다** — 호스트가 입력만 구체 경로(이벤트 dispatch → `PlayerInputManager`)로 부르고 있다.

### 이 슬라이스가 *하지 않는* 것 (핵심 한정)

`PlayerInputManager.ProcessInput`은 현재 **"입력 읽기 + 클라 예측 적용(`ApplyInput` = 이동/발동 mutate) + 서버 송신"** 을 한 덩어리로 한다. 즉 *결정(상태 변경)이 포트 뒤에 들어 있다.* 그래서 이건 **순수 어댑터가 아니라 "갈아끼우기 쉽게 포트만 깐 의존성 역전 seam"** 이다.

- **진짜 어댑터 형태**(입력=읽기 전용 + 적용은 `world.Collection→Mutation` 안쪽)는 **Stage④**에서 한다 — 클라 예측을 world 안으로 넣는 건 reconcile/롤백과 한 몸이라 지금 분해하면 Stage④ 결정을 앞당겨 박제한다.
- 이 슬라이스는 RNG 슬라이스가 "seam만, 결정론 deferred"였던 것과 같은 결: **seam만, 분해는 deferred.** 사용자가 이를 알고 ① 안으로 진행 결정함.
- **범위 = 클라만.** 서버 입력 경로(`NetworkInputSource` = 네트워크 입력 큐)는 별도 후속 슬라이스. 이 슬라이스는 서버 무변경 → 서버 에디터 불필요.

## 현재 상태 (실측)

**호스트 `LOPRunner.UpdateRunner()`** (`Assets/Scripts/Game/LOPRunner.cs`):
- `:128-131` `ProcessInput()` 페이즈 메서드 = `DispatchEvent<ProcessInput>()` 하나.
- 어댑터는 직접 호출하는 패턴이 이미 있다: `:154` `physicsSimulator.Simulate((float)tickUpdater.interval)`, `:169` `eventSink.Emit(snapshot)`, `:95` `world.Tick(Runner.Time.tick, ...)`. 이벤트는 그 *앞뒤 확장 훅*(`Before/AfterPhysicsSimulation` 등)으로 쓰인다.

**`PlayerInputManager`** (`Assets/Scripts/Game/PlayerInputManager.cs`) — 순수 C# 클래스(MonoBehaviour 아님):
- `:40` `[RunnerListen(typeof(ProcessInput))]` + `:41` `private void ProcessInput()` — 이벤트 구독으로 호출됨.
- `:19,27` 생성자가 `IRunner runner`를 받아 `runner.AddListener(this)`로 자기 자신을 리스너 등록. (`IRunner`는 *오직* 이 등록에만 쓰임.)
- 내부에서 시각은 정적 facade `Runner.Time.tick`을 읽음(`:58,116,131`) — 주입된 `IRunner`와 무관.
- `ProcessInput` 이벤트 리스너는 **이것 하나뿐** (grep 확인 — `RunnerListen(typeof(ProcessInput))` 단일 매치). → dispatch를 직접 호출로 바꿔도 누락 없음.

**`PlayerInputManager`를 구체 타입으로 resolve하는 곳** (포트로만 바꾸면 안 됨, 둘 다 살려야 함):
- `Assets/Scripts/UI/GamePad/GamePadViewModel.cs:19` — ctor 주입, 입력 setter(`SetHorizontal` 등) 푸시.
- `Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs:13` — 필드 주입(seq 시드 `SetSequenceNumber`).

**DI 등록**: `Assets/Scripts/Game/GameLifetimeScope.cs:64` `builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf();`

**GameFramework**: `Runtime/Scripts/Game/`에 자매 포트 `IPhysicsSimulator`(`void Simulate(float)`)·`IRandom` 존재. `IInputSource`는 **없음**.

## 설계

### 신규 타입 (GameFramework)

`baegames.GameFramework.Runtime`, `Runtime/Scripts/Game/`(`IPhysicsSimulator` 옆 — 짝 일관):

- **`IInputSource`** — 입력 처리 포트.
  ```csharp
  namespace GameFramework
  {
      /// <summary>
      /// 한 틱의 입력 처리를 구동하는 포트. 호스트가 구체 입력 경로(키보드 캡처/네트워크 큐)에
      /// 직결되지 않도록 주입한다. 클라는 PlayerInputManager(로컬 캡처+예측+송신),
      /// 서버는 NetworkInputSource(네트워크 입력 큐)가 구현한다.
      /// </summary>
      public interface IInputSource
      {
          /// <summary>이번 틱의 입력을 처리한다(읽기 → (클라)예측 적용 → 송신 등, 구현마다 다름).</summary>
          void ProcessInput();
      }
  }
  ```
  > **시그니처 = 무인자 `ProcessInput()`** (현 메서드 그대로). tick은 구현이 정적 `Runner.Time.tick`으로 읽음(LOP 관용) — 현 동작 0 변경. *`ProcessInput(long tick)`로 tick을 주입받아 정적 결합을 끊는 변형*은 더 port다우나 이번 wrap-only 범위 밖(Open Questions 참고, Stage④ 정련).

### 클라 호스트·구현 교체

**`PlayerInputManager` — `IInputSource` 직접 구현** (별도 래퍼 클래스 안 만듦):
- `ProcessInput()`을 `private` → `public`으로, `[RunnerListen(typeof(ProcessInput))]` 제거.
- `class PlayerInputManager` → `class PlayerInputManager : IInputSource`.
- 생성자에서 `IRunner runner` 파라미터 + `this.runner` 필드 + `runner.AddListener(this)` 제거(이제 직접 호출되므로 리스너 등록 불필요, `IRunner`는 이 용도뿐이라 같이 제거). `using LOP.Event.LOPRunner.Update;`가 더는 안 쓰이면 제거.
- **본문 로직·송신·예측·redundancy·`Runner.Time.tick` 사용은 그대로** — 동작 보존.

> 토폴로지 문서는 클라 구현명을 `PlayerInputManagerInputSource`로 *제안*했으나, 처리 로직(`ProcessInput`)이 이미 `PlayerInputManager`에 있어 **직접 구현**이 더 단순하고 pass-through 클래스를 피한다. (필요해지면 후속에 분리 가능.) 이 편차는 의도적.

**`LOPRunner` — 직접 포트 호출**:
- `[Inject] private IInputSource inputSource;` 필드 추가(`IPhysicsSimulator` 옆).
- `ProcessInput()` 페이즈 메서드 본문: `DispatchEvent<ProcessInput>();` → `inputSource.ProcessInput();`. (물리/이벤트싱크가 직접 호출인 것과 일관.)

### DI 등록

`GameLifetimeScope.cs:64` 변경:
```csharp
builder.Register<PlayerInputManager>(Lifetime.Singleton).AsSelf().As<IInputSource>();
```
- 같은 싱글톤을 `PlayerInputManager`(UI setter·seq 시드용)와 `IInputSource`(호스트 주입용) 양쪽으로 노출.
- **인스턴스화 시점**: 종전엔 `PlayerInputManager`를 처음 resolve하는 쪽(UI 등)이 생성 → ctor의 `AddListener`로 리스너 등록됐다. 신설 구조에선 `LOPRunner`가 `IInputSource`를 주입받으며 생성(빌드 콜백 `InjectSceneObjects` 시점) → 더 이르고 결정론적. UI resolve 순서에 의존하지 않음.

### 사라지는 것 / 남기는 것

- `ProcessInput` **이벤트 타입**(`LOP.Event.LOPRunner.Update.ProcessInput`)은 클라에서 더는 dispatch/구독되지 않아 *unused*가 된다. **이 슬라이스에선 제거하지 않는다**(다른 참조 여부 확인 필요 + 최소 변경 유지) — 잔여 정리는 Out of Scope.
- 다른 페이즈 이벤트(`Begin`/`End`/`BeforeEntityUpdate` 등)·`AddListener`/`DispatchEvent` 메커니즘 자체는 무변경.

## 동작 보존 / 검증

- **동작 보존:** `ProcessInput` 본문이 그대로 같은 시점(호스트 입력 페이즈)에 실행. 호출 경로만 이벤트 dispatch → 직접 호출. 이동/점프/발동 예측·서버 송신·redundancy·reconciliation 시퀀스 등록 전부 동일.
- **검증:** 클라 컴파일 클린(`read_console`). 실행: 룸 접속 후 **이동/점프/공격(어빌리티) 예측이 정상**, 서버로 입력 송신·reconciliation 무회귀, 무입력 틱 제동·redundancy 재전송 정상, GamePad setter·seq 시드 정상. 콘솔 신규 에러 0.
- **GameFramework 영향:** `IInputSource` 추가만 — 클·서 양쪽 컴파일되나(같은 file: 패키지) 서버는 미사용.
- **유닛 테스트 없음(의도적):** 동작 보존 리팩터. `IInputSource`는 trivial 포트, `PlayerInputManager`는 클라 Assembly-CSharp라 asmdef 테스트 불가. (자매 슬라이스 동일 정책.) seam의 페이오프는 Stage④(입력=읽기 전용 어댑터 + world 안 예측).

## 산업 표준 매핑

- `IInputSource`/`PlayerInputManager` = `IPhysicsSimulator`/`UnityPhysicsSimulator`, `IRandom`/`UnityRandom`과 동일한 port/adapter(헥사고날) 패턴, 짝 일관.
- 입력은 헥사고날의 **driving(주도) 포트** — 바깥(입력원)이 앱을 미는 쪽. driven 포트(물리/RNG/송출)와 대칭 자리.
- 호스트가 시뮬 주변 I/O를 포트로 조립하는 형태 = `world-core-connection-architecture.md`의 외각 호스트 스케치(`ProcessInput()` → `simulation.Tick()` → `eventSink.Emit()`)에 한 칸 더 근접.

## Out of Scope

- **진짜 어댑터화(분해):** 입력=읽기 전용(`GetInput`) + 예측 적용을 `world.Collection→Mutation`으로 이동 → Stage④(예측/롤백과 함께).
- **서버 `NetworkInputSource`:** 서버 입력 큐를 `IInputSource`로 — 별도 후속.
- **`ProcessInput` 이벤트 타입 제거** 등 잔여 정리.
- **`ProcessInput(long tick)` 시그니처(정적 `Runner.Time` 결합 제거):** 아래 Open Questions.
- **4e(얇은 호스트):** `SimulatePhysics`/`DriveAbilityEffects`/`UpdateEntity`를 world로 흡수 — 별도 spec.

## Open Questions (구현 plan 전 사용자 확인)

- **포트 시그니처 — `ProcessInput()`(무인자, 추천) vs `ProcessInput(long tick)`**: 무인자는 현 동작 0 변경(구현이 `Runner.Time.tick` 읽음). tick 주입형은 정적 facade 결합을 끊어 더 port답고 물리(`Simulate(dt)`)와 일관 — 단 본문 3곳(`:58,116,131`) 교체. **추천: 무인자(최소 seam), tick 주입은 Stage④ 정련.** 사용자 선호?

## 진행

- [x] 브레인스토밍 합의 (① wrap-only 포트, 클라 먼저, 분해는 Stage④)
- [x] 이 spec 작성
- [x] 사용자 리뷰 — **업계 표준 인터페이스 확인 결과 wrap-only는 비표준 모양 + reshape churn 위험 → 보류, Stage④ 이관 결정 (2026-07-01).** 상단 박스 참고.
- [ ] ~~구현 plan~~ — Stage④ spec에서 표준 provider(`Poll(tick)→데이터`)로 다룸.
