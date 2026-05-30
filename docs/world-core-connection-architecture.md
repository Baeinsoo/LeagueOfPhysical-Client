# World Core ↔ 프레젠테이션/네트워크 연결 아키텍처

순수 C# 시뮬레이션 코어(`GameFramework.World`: Entity / Component / System)와 Unity 프레젠테이션·네트워크 계층을 **어떻게 연결하는가**에 대한 결정. fast-paced(오버워치식) 예측/보정 + 결정론·롤백을 전제로 한다.

## 레이어와 의존성 방향

- **안쪽(inner)**: 순수 C# World Core — 데이터(`Component`) + 로직(`System`). 엔진/네트워크 비의존(`noEngineReferences`).
- **바깥(outer)**: Unity View/Controller, 물리, 네트워크 보정(Reconciler), UI.
- **의존성은 안쪽으로만 향한다**(클린/헥사고날). 코어는 바깥의 구체 타입을 **참조하거나 호출하지 않는다.**

## 정보가 경계를 넘어 바깥으로 가는 방법

두 경로만 사용한다 (둘 다 의존성은 안쪽 유지):

1. **연속 상태(위치/HP값/애니 등) → 바깥이 안쪽을 읽는다(pull).**
   View가 `World.Entity` 참조를 들고 매 프레임 읽어 갱신. 코어는 자신이 관찰됨을 모른다.

2. **사건(데미지/사망/레벨업 등) → 안쪽은 이벤트를 도메인 데이터 레코드(`WorldEvent`)로 `WorldEventBuffer`에 쌓기만** 하고, 바깥이 꺼내 처리한다. 콜백 호출 아님. 이벤트가 누구에 의해 만들어지는지(생성)와 어떻게 상태에 반영되는지(적용)의 분리는 다음 섹션에서 정의.

**금지**: 코어가 View/Controller를 참조하거나 eventbus를 직접 호출하는 것(= 안쪽→바깥 호출). 지금 LOP가 컴포넌트 setter에서 `EventBus.Publish` 하는 방식은 새 구조에서 버린다.

## Generation vs Application — 결정과 적용의 분리

이벤트 처리는 두 단계로 분리한다. 이 분리가 멀티플레이 구조, 동시 사망 의미론, 후일 예측·롤백을 모두 받친다.

### Generation (생성) — "무엇이 일어났나"를 결정

게임 로직이 상태와 룰을 읽어 이벤트를 만든다. 페이즈 파이프라인:

- **Phase 1 — Collection**: Input/AI/네트워크 메시지가 의도(`DamageInfo`, `AttackInfo` 등)를 큐에 append. 상태 변경 X.
- **Phase 2 — Mutation**: 큐의 모든 의도를 순회하며 상태에 적용. 결과 이벤트(`DamageDealtEvent`, `BuffAppliedEvent` 등)를 `WorldEventBuffer`에 append. **이 단계에 "alive 가드" 같은 분기 없음** — 모든 의도가 동등하게 적용되어 LoL/격투게임/CS의 트레이드(동시 사망)가 자연 성립.
- **Phase 3 — Detection**: 상태 스캔으로 파생 사건 발생 (`IsDead → DeathEvent`, `MaxHpExceeded → OverhealEvent` 등). 결과 이벤트를 버퍼에 append.
- **(확장) Phase 4 — Cascade Reactor**: 추가 cascade(`DeathEvent → LootDropEvent` 등)는 별도 reactor 시스템 추가로 확장. 한 시스템이 다른 시스템 영역까지 알지 않도록.

Generation의 출력 = `WorldEventBuffer`에 결정론적 순서로 누적된 이벤트 시퀀스.

### Application (적용) — 이벤트를 받아 "쓰기만"

`WorldEventBuffer`의 이벤트를 받아 상태를 데이터대로 쓴다. **결정·분기·계산 없음**:

- `DamageDealtEvent.remaining` 값을 `Health.Current`에 그대로 씀
- `DeathEvent`를 받으면 엔티티 사망 마크
- "이미 죽었나"·"한도 초과인가" 같은 가드 없음 — 이벤트 데이터가 곧 명령

Application 코드는 누가 이벤트를 만들었든(서버 Generation, 자기 Generation, 예측 Generation) 동일하게 동작.

### 모드별 책임

| 모드 | Generation | Wire | Application | 비고 |
|---|---|---|---|---|
| **서버 권위** | 서버가 풀 실행 | 서버 → 클라 (`WorldEventBatch` 폴리모픽 envelope) | 클라가 받은 이벤트 적용 + 서버도 자기 상태 동기화로 적용 | LOP 슬라이스 3 출발점 |
| **클라 단독(싱글)** | 클라가 풀 실행 | 없음 | 클라가 자기 이벤트 적용 | 같은 Application 코드 재사용, 분기 없음 |
| **클라 예측 (Stage ④)** | 클라가 자기 캐릭만 로컬 Generation(예측) + 서버 확정 수신 | 서버 → 클라 | 예측 이벤트 즉시 Application + 서버 확정과 비교 → 일치/롤백 | Application은 그대로, 예측 태그·롤백 레이어 추가 |

### 와이어 추상

서버↔클라 이벤트 전송은 **단일 폴리모픽 envelope** (`WorldEventBatch` — 한 Mirror 메시지)이 여러 `WorldEvent` 레코드를 운반. **개념별 패킷 타입 (`DamageEventToC`, `DeathEventToC` 등)을 와이어에 박지 않음**. 새 이벤트 타입 추가 시 와이어 포맷이 흔들리지 않음.

레거시 와이어(`DamageEventToC` 등)는 **수신 어댑터에서 `WorldEvent` 레코드로 변환**해 격리한다. 코어/Application/Bridge는 `WorldEvent`만 본다. 서버 와이어 포맷이 점진 전환되어도 안쪽 영향 0.

### 산업 표준 매핑

이 모델은 새 발명이 아니라 표준 조립:

- **Quake / Source / DOTA**: 서버 권위 Generation, 클라 Application + 화려한 연출. 싱글모드는 listen-server로 같은 코드.
- **Overwatch** (Tim Ford GDC 2017): 위 + 자기 캐릭 클라 Generation 예측 + 서버 확정 reconcile.
- **격투게임(SF, GGPO) / StarCraft lockstep**: 결정론적 Generation + 인풋 동기화만, 모든 피어가 같은 Application 실행.
- **CQRS / Event Sourcing** (소프트웨어 일반): Command(intent) → Event(decision) → Projection(state apply).

## Deferred — "모아서 확정 후 지연 처리" (구조적 핵심)

예측/롤백에선 **같은 틱이 여러 번 재시뮬**된다. 시뮬 중 이펙트(사운드/파티클/UI)를 동기로 실행하면 재시뮬마다 중복·가짜로 터진다. 따라서:

- 시스템은 사건을 **이벤트 데이터로 버퍼(큐)에 append만** 한다.
- **확정 게이트(commit gate)**: **확정된(committed) 틱의 이벤트만** 바깥으로 통과시킨다. 재시뮬 틱의 버퍼는 버리거나 중복 제거.
- **명확화 — 게이트의 대상**: 이 게이트는 **외부로 나가는 fan-out**(Bridge → eventbus → 프레젠테이션)을 통제. **Application(코어 상태 쓰기)는 이벤트가 버퍼에 append되는 같은 틱 안에서 즉시 실행** — 시뮬 결정론 보장. 롤백 시 Application 결과는 Snapshot/Restore로 되돌리고, 잘못 fan-out된 프레젠테이션은 확정만 송출하거나(보수) 예측 송출 후 보정(반응성).
- 바깥(브릿지/프레젠테이션)이 정해진 시점에 큐를 **드레인**해 처리하고, 거기서 eventbus(R3 등)로 팬아웃한다.
- **비동기/멀티스레드가 아니라 단일 스레드, 프레임 내 지연 드레인**(순서 결정론적).
- (뉘앙스) 내 캐릭터의 *예측* 사건은 반응성 위해 확정 전 즉시 표시 + 빗나가면 보정하는 변형 가능. 남/서버권위 사건은 확정분만.

이 패턴의 출처:
- **롤백 넷코드** — "롤백 때는 결정론적 내부 상태만 재시뮬하고, 오디오/비주얼 이펙트는 최종 확정 상태까지 미룬다": [SnapNet Rollback](https://www.snapnet.dev/blog/netcode-architectures-part-2-rollback/), [coherence Determinism/Prediction/Rollback](https://docs.coherence.io/manual/advanced-topics/competitive-games/determinism-prediction-rollback)
- **ECS 커맨드버퍼** — "기록은 해두고 `Playback()` 시점에 일괄 재생(deferred)": [Unity DOTS EntityCommandBuffer](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-entity-command-buffers.html)

## 연결 배선

- **World 컨테이너 = `GameFramework.World.EntityRegistry`** (id-키 엔티티 보관소, 코어 측 진실원본). 게임 측(LOP)이 VContainer DI로 한 인스턴스를 받아 사용 — **씬 생명주기(`SceneLifetimeScope`)에 Singleton 등록**(월드는 씬에 속함, 앱 전역 아님).
- 엔티티 생성 시 게임 측 크리에이터가 `World.Entity`를 만들어 `EntityRegistry.Add(entity)`로 등록하고, 파괴 시 `Remove(id)`로 해제.
- View/Controller MonoBehaviour가 `World.Entity` 참조를 보유(생성 시 `Bind`). **이 참조가 곧 연결.**
- 프레젠터/매니저가 `EntityRegistry`를 보고 **entityId로** GameObject+View를 생성/바인드/파괴.
- eventbus(R3)는 **바깥(브릿지)에** 둔다. 확정 이벤트 버퍼가 그걸 먹인다. **코어 안엔 두지 않는다.**
- **`WorldEventBuffer`** — 코어 측 단일 폴리모픽 큐. Generation이 append, Application이 dequeue해서 상태 쓰기, Bridge가 같은 시퀀스를 dequeue해서 프레젠테이션 fan-out. 두 소비자(Application, Bridge)는 같은 데이터를 다른 책임으로 본다.
- **와이어 envelope** (`WorldEventBatch`): 서버↔클라 이벤트 전송은 단일 폴리모픽 Mirror 메시지에 여러 `WorldEvent` 레코드를 담아 보낸다. 개념별 패킷 타입 신설 안 함.

## 동기화 모델 (fast-paced, 오버워치식) — 상세는 Stage ④에서

- **내 캐릭터**: +N틱 예측(prediction) + 입력 리플레이 보정(reconciliation). (LOP `SnapReconciler`)
- **남/서버권위 객체**: 과거 보간(interpolation), 미래 예측 안 함. (LOP `ServerStateReconciler`)
- **판정 공정성**: 서버 lag compensation(favor-the-shooter) — 남을 내가 본 과거 시점으로 되감아 판정.
- 내가 **단독·결정론적으로** 유발하는 기믹만 "예측 버블"에 넣어 내 캐릭과 함께 롤백/리시뮬. 경합/비결정적이면 서버 권위 대기.
- **액션/공격 예측 확장**: 위치 예측(`SnapReconciler`)뿐 아니라 공격·피격·버프 같은 액션 이벤트도 같은 모델로 예측 가능. 내 캐릭 인풋이 클라 Generation(`AttackSystem` 등)에서 로컬 이벤트 발생 → 즉시 Application + 프레젠테이션 → 서버 확정 도착 시 비교 후 일치/롤백. Generation/Application 분리가 이 확장의 토대.

## 코어에 요구되는 능력 (이 모델을 받치기 위해)

- 결정론적 `Step(input, dt)` 진입점.
- **Snapshot / Restore** (되감기·리플레이용).
- 이벤트는 **데이터로 출력**(버퍼). 코어는 이벤트 없이 순수·결정론 유지.
- **Generation/Application 시그니처 컨벤션**: 시스템은 Generation 측 메서드(`TakeDamage(Health, int amount)` 같은 의도/결정 로직)와 Application 측 메서드(`ApplyDamageDealt(Health, DamageDealtEvent)`, `ApplyDeath(Entity, DeathEvent)` 같은 쓰기 전용)를 별도 시그니처로 노출. 의도 메서드는 룰 적용·이벤트 발행, 적용 메서드는 데이터 그대로 반영.

## 참고 링크

- 🎥 오버워치 GDC 2017 (ECS + 예측 + 결정론, 우리 모델의 원형): [YouTube](https://www.youtube.com/watch?v=W3aieHjyNvw) / [GDC Vault](https://www.gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and)
- 예측/보정/보간 기본기: [Gambetta Fast-Paced Multiplayer](https://www.gabrielgambetta.com/client-server-game-architecture.html)
- lag compensation: [Valve Source Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking), [Lag Compensation](https://developer.valvesoftware.com/wiki/Lag_Compensation)
- 스냅샷 보간/네트워크 물리: [Gaffer On Games](https://gafferongames.com/post/snapshot_interpolation/)
- deferred(롤백): [SnapNet](https://www.snapnet.dev/blog/netcode-architectures-part-2-rollback/), [coherence](https://docs.coherence.io/manual/advanced-topics/competitive-games/determinism-prediction-rollback)
- deferred(ECS 커맨드버퍼): [Unity DOTS ECB](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-entity-command-buffers.html)
- 도메인 이벤트(일반): [Fowler, Domain Event](https://martinfowler.com/eaaDev/DomainEvent.html)
- CQRS / Event Sourcing: [Fowler, CQRS](https://martinfowler.com/bliki/CQRS.html)

## 프로젝트 컨벤션

**네임스페이스 풀 한정 (LOP 측 파일)**: `GameFramework.World.Component`는 `UnityEngine.Component`와 이름이 겹친다. LOP 측 코드 파일은 거의 모두 `using UnityEngine;`을 쓰므로, **`using GameFramework.World;`를 추가하지 않고 World 타입은 항상 풀 네임스페이스로 한정한다.**

- 예: `GameFramework.World.Entity worldEntity = ...;`, `[Inject] GameFramework.World.EntityRegistry entityRegistry;`
- 이렇게 하면 `Component` 모호성이 자연스럽게 회피되고 컴파일러가 강제하지 않아도 됨.
- `noEngineReferences`인 World 어셈블리 내부 코드는 `UnityEngine`을 쓰지 않으므로 평소처럼 짧은 이름을 사용해도 충돌 없음.

## 상태

모델 확정. 구현은 슬라이스 단위로 진행 중:

- **슬라이스 1 (완료)**: `EntityRegistry`, `Entity`, `Component`, `Health`, `HealthSystem` (Generation Phase 2 측 `TakeDamage`만). Generation/Application 분리는 없고 슬라이스 분리만.
- **슬라이스 2 (완료)**: `LOPEntityManager.DestroyMarkedEntities`에서 `EntityRegistry.Remove` 연결, 라이프사이클 닫기.
- **슬라이스 3 (진행)**: Generation Phase 2/3 + `WorldEventBuffer` + Application(쓰기) + Bridge(fan-out)로 **데미지 → 사망 경로의 첫 끝-끝 구현**. 와이어는 레거시 `DamageEventToC`를 어댑터로 격리하고 내부는 새 `WorldEvent` 추상으로 통일. 확정 게이트는 trivial(매 드레인=commit), 클라 측 Generation은 없음(서버 권위).
- **Stage ④ (예정)**: 확정 게이트 본격(틱 스탬프 + 롤백 폐기), Snapshot/Restore, 클라 측 Generation(예측), 결정론적 RNG, reconciliation.
