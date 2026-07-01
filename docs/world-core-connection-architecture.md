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

## 용어와 두 축 — 먼저 못박기

이벤트 모델을 읽기 전에 어휘부터 고정한다. 이게 안 되면 "apply에서 죽음이 생기네?" 같은 혼동이 생긴다.

### Command(의도) vs Event(사실)

| | 정의 | 예 |
|---|---|---|
| **Command / Intent** | 아직 *해소되지 않은 요청* | "X에게 30 데미지 줘라" |
| **Event / Fact** | *해소된 불변 과거사실* | "X가 30 맞아 HP 70, 사망" |

원시 입력만 든 메시지는 *Command*다. HP를 실제로 깎아 `remaining`·`isDead`를 알아내는 *해소(resolve)* 를 거쳐야 비로소 *Event*가 된다.

### 세 계층 (Intent → Resolve → Project)

```
① Intent 수집            의도를 큐잉                                              — 상태 안 바꿈
② Resolve(=Generation)   상태 변경 + 파생 판정(HP≤0) + 결과 Event raise(Death 포함)   — 여기서만 Event 태어남
③ Projection/Egress      확정 Event를 read-state에 쓰고 / 바깥(프레젠테이션·wire)으로 송출   — 새 Event 0
```

**핵심 불변식:** *HP를 바꾸고 ≤0를 판정해 `DeathEvent`를 만드는 층은 — 이름이 "apply"든 뭐든 — 정의상 ②(Generation)다.* 순수 소비(이벤트 0)는 항상 그 아래 ③이다. 즉 **Event는 ②에서만 생성, ③은 절대 생성하지 않는다.** (CQRS: aggregate가 mutate+raise, projection은 consume만.)

### 두 직교 축 (혼동 주의)

| 축 | 정체 | 무엇을 위해 |
|---|---|---|
| **축 A** | Generation ↔ Application (결정/이벤트 ↔ 상태쓰기) | 멱등·재생·네트워킹·롤백 |
| **축 B** | Collection → Mutation → **Detection** 배리어 (의도 다 모음 → 다 적용 → *그 다음* 죽음 스캔, 중간 alive-guard 없음) | **동시죽음/트레이드 공정성·결정론** |

**트레이드(동시죽음)를 만드는 건 축 B다 — 축 A가 아니다.** 둘은 독립: 락스텝 RTS는 축 B만으로 트레이드를 얻고, CQRS 웹앱은 축 A만 쓴다. 우리 파이프라인은 둘을 함께 쓰지만 *서로를 함의하지 않는다.*

### 죽음은 4단계, 각기 다른 계층

| 단계 | 표준(목표) 모델 | 현재 코드 |
|---|---|---|
| **① 판정** (HP≤0?) | **Detection** (Generation): 데미지 다 적용된 최종상태 스캔 → `DeathEvent` | `LOPCombatSystem`이 `TakeDamage` 직후 `health.IsDead` 인라인 |
| **② 마크** (상태에 사망 기록) | **Application** (Projection): `DeathEvent` → 사망 마크 | 암묵적 — `Health.IsDead`가 HP≤0 파생 |
| **③ 후처리** (despawn·loot·exp) | **Cascade (Generation/Resolve)**: resolve 단계(egress 전)에서 despawn·loot 직접 처리. 풀 이벤트화(`DespawnEvent`+Application)는 deferred 인프라(Stage④) | **`LOPGame.HandleDeath` — egress(fan-out) 경로 ⚠️ #2** → resolve 단계로 이전(진행 중) |
| **④ 송출** (클라·VFX) | **Egress** (Projection): wire + presentation | `WorldEventSink`(클·서). 죽음 연출은 `DamageDealtEvent.IsDead`로 전달(별도 DeathEvent wire 없음) |

**서버권위:** 죽음 *판정·생성*은 서버 Resolve(②)에서만. 클라는 *받는다*(재해소 불가 — 공격자 스탯·RNG 모름). 그래서 "apply"가 서버=Resolve(생성층) / 클라=Projection(소비층)으로 의미가 다르다.

## 무엇을 deferred로 모으고, 무엇을 즉시 처리하나 (선택적 — "전부"가 아님)

위 4단계(Collection→Mutation→Detection→React)는 **모든 월드 갱신에 거는 게 아니라, 특정 종류에만** 건다. "전부 모았다가 한 번에 처리"는 과설계다 (Bevy 공식: *deferral is not free, only for infrequent mutations*; Photon Quantum은 게임플레이 mutation을 하나도 안 미루고 즉시 — 결정론은 *시스템 순서*로 잡는다). 업계는 종류별로 선을 긋는다:

| deferred (모았다 일괄) | inline (즉시) |
|---|---|
| **구조 변경** — 스폰/디스폰, 컴포넌트 추가/제거 (이터레이터 무결성·스레드 안전) | **이동·속도·위치** (이번 틱 충돌에 바로 필요) |
| **데미지/힐** — 교차 엔티티 + 죽음 연쇄 (OW `ModifyHealthQueue`) | **물리 시뮬** (PhysX 한 방) |
| **버프/디버프 적용** — 권위 확정·스택 순서 | **애니/방향** (연출, 순서대로) |
| **loot/exp/죽음 cascade** — 이미 확정된 사건의 *반응* | 자기완결·단일 소유 갱신 |

**선 긋는 기준:** ① 구조를 바꾸나? ② 여러 엔티티에 걸치고 심각한 부작용(죽음 연쇄)이 있나? ③ 동시성 공정성이 필요한가? → **deferred**. 자기 안에서 끝나고 순서대로면 → **inline**. (ECS 통설: 구조 변경은 ECB로 *항상* defer, 컴포넌트 *값* 쓰기는 inline.)

### alive-guard의 위치 (트레이드 공정성)

"이미 죽었으면 무시" 가드는 **②Mutation(데미지 적용)에 절대 두지 않는다** — 두면 실행 순서상 먼저 처리된 공격만 킬을 먹고 동시죽음(트레이드)이 깨진다(LoL·격겜·스타 동시죽음이 이 원리). 데미지는 가드 없이 *전부* 적용 → **③Detection이 HP≤0를 스캔**해 죽음을 만든다. "무시" 가드는 **①행동 자격**(기절·사망 중 행동 *시작* 불가)에만 둔다.

### React(연계) — 죽음→분노버프 같은 연쇄

④React가 만든 새 의도(분노버프 적용 등)는 **다음 틱의 ①Collection으로** 넣거나, 같은 틱 서브패스로 fixpoint 처리한다. 무한 연쇄(proc 폭주)는 **proc 마스크**(반응 체인에 이미 있으면 거부 — Unreal GAS 커뮤니티 표준)로 막는다.

> **YAGNI — 지금 짓지 않는다:** 풀 deferred 큐 인프라(OW `ModifyHealthQueue`급 — 모든 데미지/버프를 큐에 모아 일괄 확정)는 **버프·연계·광역 공격이 실제로 생길 때** 값을 한다. 현재 LOP는 단순 공격→데미지→죽음→디스폰뿐이라, 그 인프라는 **목표 모델**로만 남기고 즉시 짓지 않는다. 새 콘텐츠가 그 구조를 요구할 때 이 표를 기준으로 자라게 한다.

> 산업 근거: [OW ModifyHealthQueue 분석(Alibaba/GDC2017)](https://topic.alibabacloud.com/a/on-the-ecs-architecture-in-the-overwatch_8_8_31063753.html) · [Unreal GAS — GASDocumentation](https://github.com/tranek/GASDocumentation) · [Unity DOTS ECB](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-entity-command-buffers.html) · [Bevy Commands/Deferred](https://deepwiki.com/bevyengine/bevy/2.3-commands-and-deferred-operations) · [Photon Quantum — Signals vs Events](https://doc.photonengine.com/quantum/current/manual/quantum-ecs/game-events) · [Nystrom — Event Queue](https://gameprogrammingpatterns.com/event-queue.html)

## Generation vs Application — 결정과 적용의 분리

이벤트 처리는 두 단계로 분리한다(축 A). 이 분리는 멀티플레이 구조와 후일 예측·롤백을 받친다. (동시 사망/트레이드 의미론은 이 분리가 아니라 Collection→Mutation→Detection 배리어[축 B] 소관 — 위 "두 직교 축" 참고.)

> **⚠️ "apply" 용어 + 현재 코드 정합 (2026-06-22 갱신).** 이벤트소싱에서 **"apply"는 *과거 이벤트를 상태에 되먹여 재구성*(rehydration/fold)** 을 뜻한다 — 아래 "Application"이 바로 그것이고, 우리가 *지운* `WorldEventApplicator`가 이 fold였다. 의도를 상태로 *확정하는* 단계(②Mutation)는 업계어로 **Handle/Decide/Resolve**이지 "apply"가 아니다. **서버 권위 모드에선 서버가 ②에서 상태를 *직접 한 번* mutate하고, 별도 Application(이벤트→상태 재적용)은 없다**(중복이라 삭제 — backlog #1). durable 값은 **스냅샷**으로 클라에 간다(이벤트 아님 — backlog #3). **Application(이벤트 fold)은 클라 *예측*(Stage④)에서만** 되살아난다: 클라가 자기 로컬 이벤트를 적용해 예측하고 스냅샷과 reconcile. 아래 "Application" 절은 그 **Stage④ 목표 모델**로 읽을 것 — 현재 서버·클라 런타임엔 없다.

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

| 모드 | 상태 확정 (Resolve = ②Mutation) | durable → 클라 | 연출 event | Application(이벤트 fold) | 비고 |
|---|---|---|---|---|---|
| **서버 권위 (현재)** | 서버가 ②에서 상태 **직접 한 번** mutate | **스냅샷** (단일 권위) | 서버 → 클라 (연출만) | **없음** — 서버 in-place mutate | HP 슬라이스 후 |
| **클라 단독(싱글)** | 클라가 직접 mutate | — (로컬) | 로컬 fan-out | 없음 | 같은 Resolve 코드 |
| **클라 예측 (Stage ④)** | 클라가 자기 캐릭만 로컬 Resolve(예측) | 서버 → 클라 스냅샷 | 로컬 + 서버 | **여기서만** — 예측 이벤트 즉시 fold + 스냅샷과 reconcile → 일치/롤백 | Stage④에서 추가 |

### 와이어 추상

서버↔클라 이벤트 전송은 **단일 폴리모픽 envelope** (`WorldEventBatch` — 한 Mirror 메시지)이 여러 `WorldEvent` 레코드를 운반. **개념별 패킷 타입 (`DamageEventToC`, `DeathEventToC` 등)을 와이어에 박지 않음**. 새 이벤트 타입 추가 시 와이어 포맷이 흔들리지 않음.

레거시 와이어(`DamageEventToC` 등)는 **수신 어댑터에서 `WorldEvent` 레코드로 변환**해 격리한다. 코어/Application/Bridge는 `WorldEvent`만 본다. 서버 와이어 포맷이 점진 전환되어도 안쪽 영향 0.

### 산업 표준 매핑

이 모델은 새 발명이 아니라 표준 조립:

- **Quake / Source / DOTA**: 서버 권위 Generation, 클라 Application + 화려한 연출. 싱글모드는 listen-server로 같은 코드.
- **Overwatch** (Tim Ford GDC 2017): 위 + 자기 캐릭 클라 Generation 예측 + 서버 확정 reconcile.
- **격투게임(SF, GGPO) / StarCraft lockstep**: 결정론적 Generation + 인풋 동기화만, 모든 피어가 같은 Application 실행.
- **CQRS / Event Sourcing** (소프트웨어 일반): Command(intent) → Event(decision) → Projection(state apply).

## 와이어 표현 — Snapshot vs Event (역할 분리 hybrid)

World 상태가 네트워크를 건널 때 *값을 snapshot(상태)으로 보낼지 event(사건)로 보낼지* 의 결정 기준.

### 결정 기준: "잃으면 영구 desync 되나?"

| | 유실 시 영구 desync? | → 표현 |
|---|---|---|
| HP · Transform · MP · Stats | **예** (durable·정확 필수) | **Snapshot** — 권위, 자가보정(다음 스냅이 덮음) |
| 데미지 숫자 · 크리/회피 · 킬피드 · 히트마커 | **아니오** (transient 연출) | **Event** — cosmetic, fire-and-forget, *클라가 state로 안 씀* |

> 묻는 건 "연속/이산"이 아니라 **"durable이냐"** 다. HP는 *이산적으로* 바뀌지만 durable이라 snapshot(안 바뀐 틱엔 안 보내는 델타압축). 연속/이산은 *빈도*(압축 여부)만 정한다.

### 결정 — 역할 분리 hybrid

- **Snapshot = 모든 durable 값의 단일 권위** (Transform/HP/MP/Lv/Stats). 클라 HP의 진실원본.
- **Event = 연출/귀속 전용** (데미지 숫자·크리·회피·death 연출). **클라는 event로 authoritative 상태를 쓰지 않는다.**
- *순수 snapshot 아님*: 크리/회피/누가-때렸나 같은 attribution을 snapshot delta로 복원 어려움.
- *순수 event 아님*: event는 lossy → durable HP가 desync (Source의 "durable state = entity-state, not message" 규칙).

### 산업 정렬 (개념 vs 와이어 — 분리해서)

- **계층 *개념*(Resolve가 이벤트 생성 / Projection은 소비)** = 업계 표준 (CQRS/ES/DDD).
- ***와이어*: durable=snapshot이 AAA 정석** — Overwatch ECS 컴포넌트 스냅샷 / Source entity-state / Fiedler 3학파(lockstep·snapshot·state-sync). *도메인 이벤트 레코드로 연속상태 전송*은 비전형(이산 사실엔 OK).
- ⚠️ **Overwatch는 우리 *개념*과 *state 와이어*를 지지하지, *fat-event-record 와이어*를 지지하지 않는다.** 근거 인용 시 분리할 것. (OW 1차자료는 GDC 유료 → 2차자료 기반.)

> 출처: [Fiedler — State Synchronization](https://gafferongames.com/post/state_synchronization/) · [Valve — Source Multiplayer Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking) · [MS Learn — Domain Events](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/domain-events-design-implementation) · [Gambetta — Client-Server Architecture](https://www.gabrielgambetta.com/client-server-game-architecture.html)

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
- View/Controller MonoBehaviour가 `World.Entity` 참조를 보유(생성 시 attach/link). **이 참조가 곧 연결.** 연속 상태(위치/HP값)는 이 참조로 *pull*(매 프레임 읽기), 이산 사건(데미지/사망)은 `WorldEventBuffer` fan-out 구독으로 받는다. — **월드는 component+system + (연속 pull / 이산 event)이며, UI 레이어의 MVVM 데이터 바인딩과 다른 축이다.**
- **뷰 스포너/바인딩 시스템**(*MVP의 "프레젠터"가 아님* — 프레젠터는 UI 용어)이 `EntityRegistry`의 **수명(add/remove)**을 보고 **entityId로** GameObject+View를 생성/연결/파괴한다. ECS/Entitas의 *view-resolver / 리액티브 뷰 시스템*에 해당하며, 이 "뷰 수명" 역할은 `WorldEventBuffer`(이산 게임플레이 사건 fan-out)와 **별개 축**이다.
- **분리형 구조**: 엔티티(데이터, `EntityRegistry`)와 뷰를 분리하고, **뷰 스포너가 엔티티 수명(add/remove)을 보고 GameObject+View를 생성/연결/파괴**한다 — ECS/Entitas의 view-resolver 정석. (로직+뷰를 한 프리팹에 합쳐 스폰하는 *합체형*도 업계 통용 대안이나, LOP는 분리형을 따른다.)
- eventbus(R3)는 **바깥(브릿지)에** 둔다. 확정 이벤트 버퍼가 그걸 먹인다. **코어 안엔 두지 않는다.**
- **`WorldEventBuffer`** — 코어 측 단일 폴리모픽 큐. Generation이 append, egress sink(`WorldEventSink`)가 dequeue해서 연출 fan-out. (서버 Application 소비자는 제거됨 — durable은 스냅샷. Application[이벤트 fold]은 Stage④ 클라 예측에서 복귀.)
- **와이어 envelope** (`WorldEventBatch`): 서버↔클라 이벤트 전송은 단일 폴리모픽 Mirror 메시지에 여러 `WorldEvent` 레코드를 담아 보낸다. 개념별 패킷 타입 신설 안 함.

## Engine ↔ Simulation 책임 분리 (Composition)

시뮬 코어와 외각 호스트를 *컴포지션*으로 분리한다. 양쪽이 동일한 시뮬 구현체를 *그대로* 사용 → 결정론 자동 강제.

### 시뮬 (`LOPGameSimulation`, LOP-Shared)

**책임**: "인풋 → 이벤트 생성" *그 자체*. 양쪽 공통, 결정론, 부수효과 없음.

- 보유: `EntityRegistry`, `WorldEventBuffer`
- 진입점: `Tick(int tick, float dt)` — Collection → Mutation → Detection → Application 흐름
- 외부 노출: 상태 access(`EntityRegistry`, `EventBuffer`)만. 정책(Snapshot/Restore/예측/롤백/보간/fan-out)은 *외부 책임*.
- **갖지 않는 것**: View, 네트워크 송수신, 예측 정책, 보정 정책, fan-out 정책.

### 외각 (`LOPGameEngine`, LOP-Client / LOP-Server 각자)

**책임**: 사이드/도메인 *정책* 일체.

- **클라**: 입력 캡처, 서버 snap 수신, **Snapshot/Restore + 입력 replay**(Stage ④), 보간(`SnapInterpolator`), 이벤트 fan-out(`WorldEventSink`), VFX
- **서버**: 입력 수신, AI 의도 push(`LOPAIController`), 와이어 송신(`WorldEventSink`), 권위 판정

### GameFramework 추상 (재사용 골격)

| 추상 | 책임 |
|---|---|
| `IGameSimulation` / `GameSimulationBase` | 시뮬 코어 추상 — `Tick(int, float)`, 상태 access properties, Phase 훅(`protected abstract`/`virtual`). 공통 구현은 `EntityRegistry`/`WorldEventBuffer` hold. |
| `IGameEngine` / `GameEngineBase` (기존) | 외각 호스트 추상 — `UpdateEngine()` 라이프사이클, 초기화/해제 |
| `ITickUpdater` / `TickUpdaterBase` | 틱 시간 계산 — 클라(Mirror NetworkTime smoothing) vs 서버(자기 클럭) 분기 |
| `IInputSource` | 인풋 어댑터 — 클라(키보드/마우스) vs 서버(네트워크 큐) |
| `IEventSink` | 이벤트 fan-out 어댑터 — 클·서 각자 `WorldEventSink` 구현 (클=`EventBus` publish / 서=wire 송신) |
| `IPhysicsSimulator` | PhysX 호출 추상 (양쪽 동일 구체) |
| `INetworkSession` | 네트워크 송수신 추상 (Mirror NetworkClient/Server 어댑터로 구현) |

> **`IGameSimulation`의 책임 경계**: Snapshot/Restore *메서드*는 두지 않는다. *상태 access*만 노출하고, 보관·복원 정책은 외각의 책임. 자세한 위치는 [netcode-redesign.md](netcode-redesign.md) 참조.

### 호스트 코드 스케치

```csharp
// LOP-Client
public class LOPGameEngine : GameEngineBase
{
    [Inject] LOPGameSimulation simulation;
    [Inject] IEventSink eventSink;          // WorldEventSink (클)
    [Inject] SnapshotHistory history;       // 클라 전용 (Stage ④)

    public override void UpdateEngine() {
        ProcessNetworkMessage();              // 클·서 다름
        if (snapReceived) {                   // 클라 전용
            history.RestoreSimulationTo(simulation, snap.tick);
            ReplayInputs(snap.tick + 1, currentTick);
        }
        ProcessInput();                       // 클·서 다름
        simulation.Tick(currentTick, dt);     // *공통*
        history.Record(simulation, currentTick);   // 클라 전용
        eventSink.Emit(simulation.EventBuffer.Snapshot);
        simulation.EventBuffer.Clear();
    }
}

// LOP-Server
public class LOPGameEngine : GameEngineBase
{
    [Inject] LOPGameSimulation simulation;
    [Inject] IEventSink eventSink;          // WorldEventSink (서)

    public override void UpdateEngine() {
        ProcessNetworkMessage();              // 서버 입력 수신
        PushAIIntents();                      // 서버 전용
        simulation.Tick(currentTick, dt);     // *공통*
        eventSink.Emit(simulation.EventBuffer.Snapshot);
        simulation.EventBuffer.Clear();
    }
}
```

### 산업 표준 매핑

- **Quake 3**: `game.qvm`/`cgame.qvm`(shared game module) ↔ `quake3.exe`(engine host)
- **Source / CS:GO**: shared C++ game code ↔ engine binary
- **Overwatch**: 단일 ECS World 코드(시뮬) ↔ server/client process(외각). Tim Ford GDC 2017.
- **Photon Quantum**: `QuantumGame`(시뮬) ↔ `QuantumRunner`(외각 호스트)

LOP 매핑: `LOPGameSimulation`(Shared) ↔ `LOPGameEngine`(각 사이드).

## 동기화 모델 (fast-paced, 오버워치식) — 상세는 Stage ④에서

- **내 캐릭터**: +N틱 예측(prediction) + 입력 리플레이 보정(reconciliation). (LOP `SnapReconciler`)
- **남/서버권위 객체**: 과거 보간(interpolation), 미래 예측 안 함. (LOP `ServerStateReconciler`)
- **판정 공정성**: 서버 lag compensation(favor-the-shooter) — 남을 내가 본 과거 시점으로 되감아 판정.
- 내가 **단독·결정론적으로** 유발하는 기믹만 "예측 버블"에 넣어 내 캐릭과 함께 롤백/리시뮬. 경합/비결정적이면 서버 권위 대기.
- **액션/공격 예측 확장**: 위치 예측(`SnapReconciler`)뿐 아니라 공격·피격·버프 같은 액션 이벤트도 같은 모델로 예측 가능. 내 캐릭 인풋이 클라 Generation(`AttackSystem` 등)에서 로컬 이벤트 발생 → 즉시 Application + 프레젠테이션 → 서버 확정 도착 시 비교 후 일치/롤백. Generation/Application 분리가 이 확장의 토대.

## 코어에 요구되는 능력 (이 모델을 받치기 위해)

- 결정론적 `Tick(int tick, float dt)` 진입점 (`IGameSimulation`).
- 외부에 *상태 access* 노출 (`EntityRegistry`, `WorldEventBuffer`). **`Snapshot()`/`Restore(snap)` 메서드는 코어에 두지 않는다** — 보관·복원 정책은 클라 외각의 책임([netcode-redesign.md](netcode-redesign.md) 참조).
- 이벤트는 **데이터로 출력**(버퍼). 코어는 이벤트 없이 순수·결정론 유지.
- **Generation/Application 시그니처 컨벤션**: 시스템은 Generation 측 메서드(`TakeDamage(Health, int amount)` 같은 의도/결정 로직)와 Application 측 메서드(`ApplyDamageDealt(Health, DamageDealtEvent)`, `ApplyDeath(Entity, DeathEvent)` 같은 쓰기 전용)를 별도 시그니처로 노출. 의도 메서드는 룰 적용·이벤트 발행, 적용 메서드는 데이터 그대로 반영.
- I/O 어댑터 추상 — `IInputSource`, `IEventSink`, `ITickUpdater`, `IPhysicsSimulator`, `INetworkSession`. 각 어댑터의 구체는 클라/서버 각자 보유. 시뮬은 어댑터를 *알 수 있어도(의존 주입)* 구체 타입을 알지 않는다.

## 참고 링크

- 🎥 오버워치 GDC 2017 (ECS + 예측 + 결정론, 우리 모델의 원형): [YouTube](https://www.youtube.com/watch?v=W3aieHjyNvw) / [GDC Vault](https://www.gdcvault.com/play/1024001/-Overwatch-Gameplay-Architecture-and)
- 예측/보정/보간 기본기: [Gambetta Fast-Paced Multiplayer](https://www.gabrielgambetta.com/client-server-game-architecture.html)
- lag compensation: [Valve Source Networking](https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking), [Lag Compensation](https://developer.valvesoftware.com/wiki/Lag_Compensation)
- 스냅샷 보간/네트워크 물리: [Gaffer On Games](https://gafferongames.com/post/snapshot_interpolation/)
- deferred(롤백): [SnapNet](https://www.snapnet.dev/blog/netcode-architectures-part-2-rollback/), [coherence](https://docs.coherence.io/manual/advanced-topics/competitive-games/determinism-prediction-rollback)
- deferred(ECS 커맨드버퍼): [Unity DOTS ECB](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/systems-entity-command-buffers.html)
- 도메인 이벤트(일반): [Fowler, Domain Event](https://martinfowler.com/eaaDev/DomainEvent.html)
- CQRS / Event Sourcing: [Fowler, CQRS](https://martinfowler.com/bliki/CQRS.html)
- "apply" 모호성(ES fold vs command handle): [Marten — Command Handler Workflow](https://martendb.io/scenarios/command_handler_workflow.html), [Daniel Whittaker — CQRS Command vs GoF Command](https://danielwhittaker.me/2015/05/25/is-a-cqrs-command-gof-command/)
- 선택적 deferral / reactive trigger: [OW ModifyHealthQueue(Alibaba/GDC2017)](https://topic.alibabacloud.com/a/on-the-ecs-architecture-in-the-overwatch_8_8_31063753.html), [Unreal GAS Documentation](https://github.com/tranek/GASDocumentation), [Bevy Commands/Deferred](https://deepwiki.com/bevyengine/bevy/2.3-commands-and-deferred-operations), [Photon Quantum — Signals vs Events](https://doc.photonengine.com/quantum/current/manual/quantum-ecs/game-events), [Nystrom — Event Queue](https://gameprogrammingpatterns.com/event-queue.html)

## 프로젝트 컨벤션

**네임스페이스 풀 한정 (LOP 측 파일)**: `GameFramework.World.Component`는 `UnityEngine.Component`와 이름이 겹친다. LOP 측 코드 파일은 거의 모두 `using UnityEngine;`을 쓰므로, **`using GameFramework.World;`를 추가하지 않고 World 타입은 항상 풀 네임스페이스로 한정한다.**

- 예: `GameFramework.World.Entity worldEntity = ...;`, `[Inject] GameFramework.World.EntityRegistry entityRegistry;`
- 이렇게 하면 `Component` 모호성이 자연스럽게 회피되고 컴파일러가 강제하지 않아도 됨.
- `noEngineReferences`인 World 어셈블리 내부 코드는 `UnityEngine`을 쓰지 않으므로 평소처럼 짧은 이름을 사용해도 충돌 없음.

**임시 명명 + 재논의 노트**: 현재 외각 호스트는 `LOPGameEngine`, 시뮬 코어는 `LOPGameSimulation`이라는 이름을 *임시로* 쓴다. *엄밀한 업계 정의*는 "engine = 인프라/플랫폼"이라 LOP의 host 자리에는 *어긋남* — Photon Quantum의 `Runner`(외각 호스트 통용) 같은 후보를 Slice 4 흡수 완료 후 재논의한다. 자세한 결정 항목은 `lop-repo-topology.md`의 Open Decisions 참조.

## 알려진 괴리 — 모델 vs 현재 코드 (cleanup backlog)

모델은 위와 같고, 현재 코드는 slice-3 단순화라 어긋난 곳이 있었다. **#1·#3은 해소됨**(2026-06-22, HP 스냅샷 단일권위 슬라이스), **#2는 남음**:

1. ~~**이중 apply**~~ ✅ **해소(2026-06-22)** — 서버 Generation이 `TakeDamage`로 mutate한 뒤 Application(`WorldEventApplicator`)이 `remaining`으로 재적용하던 중복을 제거. 서버·클라 양쪽 event-apply 삭제, 호출처가 사라진 `WorldEventApplicator`/`HealthSystem.ApplyDamageDealt`도 삭제. **Application 코드는 Stage④(Death 컴포넌트·클라 예측-apply)에서 새 모양으로 복귀.**
2. **despawn이 fan-out에 (진행 중 — 위치 교정 슬라이스)** — `LOPGame.HandleDeath`(디스폰+경험치)가 egress(`ProcessEvent`의 `Emit` 뒤 `reactor.React`) 경로에서 상태를 바꿈 = "egress가 새 사실 생성" 안티패턴. → **죽음 cascade를 resolve 단계(egress 전)로 이전** + reactor/EventBus 왕복 제거(직접 처리). 풀 이벤트화(`DespawnEvent`+Application 적용)는 deferred 큐 인프라가 필요해 **Stage④로 미룸** — 지금은 *위치 교정만*(위 "선택적 deferral" 절 YAGNI).
3. ~~**이중 HP 경로**~~ ✅ **해소(2026-06-22)** — 클라가 `DamageDealtEvent.remaining`(event)과 `UserEntitySnap.CurrentHP`(state) 둘로 HP를 받던 것을 **HP 권위 = 스냅샷 state 단일화**로 정리(클라 event-apply 삭제). 이벤트는 연출 전용(숫자·크리·HP바·HUD). 단 HP **UI**가 아직 그 연출 이벤트에서 값을 읽는 잔여(scope B: UI를 스냅샷-fed 모델로 이전 + 와이어 이벤트에서 HP 흔적 제거)는 다음 슬라이스.

## 상태

모델 확정. 구현은 슬라이스 단위로 진행 중:

- **슬라이스 1 (완료)**: `EntityRegistry`, `Entity`, `Component`, `Health`, `HealthSystem` (Generation Phase 2 측 `TakeDamage`만). Generation/Application 분리는 없고 슬라이스 분리만.
- **슬라이스 2 (완료)**: `LOPEntityManager.DestroyMarkedEntities`에서 `EntityRegistry.Remove` 연결, 라이프사이클 닫기.
- **슬라이스 3 (완료)**: Generation Phase 2/3 + `WorldEventBuffer` + Bridge(fan-out)로 **데미지 → 사망 경로의 첫 끝-끝 구현**. 와이어는 레거시 `DamageEventToC`를 어댑터로 격리하고 내부는 새 `WorldEvent` 추상으로 통일. 확정 게이트는 trivial(매 드레인=commit), 클라 측 Generation은 없음(서버 권위). *(후속 HP 슬라이스에서 중복 Application 제거 — backlog #1/#3 해소. Bridge/Broadcaster → `WorldEventSink`[`IEventSink`]로 리네임.)*
- **LOP-Shared 도입 (Slice 0/1, 진행 예정)**: 별도 git 저장소 `LeagueOfPhysical-Shared`(패키지 `com.baegames.lop.shared`)를 만들어 클·서 *진짜 공통* 코드(wire/proto + 메시지 인프라)를 추출. 자세한 토폴로지·범위는 `lop-repo-topology.md` 참조.
- **Slice 4(a~e) — Engine ↔ Simulation 추출**: 호스트(`LOPRunner`, 구 `LOPGameEngine`)의 시뮬 흐름을 공유 시뮬(`LOPWorld`, 구 `LOPGameSimulation`)로 점진 흡수. *(Runner/World 리네임 완료.)*
  - **4a (완료)**: GameFramework 추상 — `IWorld`/`WorldBase`(구 `IGameSimulation`), `ITickUpdater`/`TickUpdaterBase`, `IEventSink`, `IPhysicsSimulator`, `IRandom`. (`IInputSource`·`INetworkSession`은 미생성 — 아래 참고.)
  - **4b (완료)**: `LOPWorld` 골격 + `UnityPhysicsSimulator`. 호스트가 빈 `LOPWorld`를 매 틱 `Tick`.
  - **4c (완료)**: 이동 커널 공유(`MovementSystem.ProcessMovement`) + 어빌리티/StatusEffect 틱을 `LOPWorld.Mutation`으로 이전.
  - **어댑터 (완료)**: `IEventSink`(`WorldEventSink`)·`IPhysicsSimulator`·`IRandom` 도입 — wrap-only 모양이 *이미 최종 표준*(구현만 후일 심화)이라 안전.
  - **⛔ 4d (입력 어댑터) — Stage④로 이관 (2026-07-01).** 업계 표준 `IInputSource`는 *틱별 입력 데이터를 제공/폴링하는 provider*(`Poll(tick)→데이터`; Quantum `PollInput`/`SetInput`, Unity Netcode for Entities `IInputComponentData`/`ICommandData`)다. 이 모양은 *적용(예측 mutate)·송신을 source 밖으로* 빼야 성립 = 클라 예측 = **Stage④**. 현재 `PlayerInputManager.ProcessInput`은 capture+예측+송신을 묶은 *비표준 verb*라, 지금 wrap-only 포트로 박으면 Stage④에서 인터페이스를 깨는 reshape(임의명명→표준 rename churn)이 된다 — RNG/물리(모양이 이미 최종)와 비대칭. 상세: `docs/superpowers/specs/2026-06-30-slice4-input-source-port-design.md`(보류 표시).
  - **⛔ 4e (얇은 호스트) — Stage④에 막힘 (2026-07-01).** 호스트 잔여 페이즈 중 **지금 `LOPWorld.Tick`으로 깨끗이 옮길 수 있는 게 없음**: ① `UpdateEntity`(=`LOPEntity.UpdateEntity→Status`)는 클라 MonoBehaviour 프레젠테이션이라 *영구히 호스트*. ② `DriveAbilityEffects`·③ `SimulatePhysics`는 **velocity 권위가 아직 Rigidbody(side)** 라 막힘 — effect 핸들러(`MotionEffectHandler`)가 클라 전용 `PhysicsComponent`/`Rigidbody.velocity`에 쓰고, after-physics 동기(`LOPEntityController.SyncPhysics`/`WorldMotionSync`)가 호스트 리스너. 공유 `LOPWorld`는 이 클라 타입들을 참조 불가. **keystone = velocity 권위를 Rigidbody → `World.Entity`로 이전**(Stage④, "접근 B") — 이게 풀리면 ②③가 자연히 `LOPWorld.Tick`으로 들어가 4e가 닫힌다.
  - **요지:** Slice 4의 *분리 가능한* 작업은 완료. 잔여(4d·4e)는 전부 Stage④ 결정(예측·롤백·velocity 권위)에 묶여 있어 **Slice 4는 Stage④ 전 단계에선 사실상 닫힘** — 다음 실제 작업은 Stage④다.
- **Stage ④ (다음 — brainstorm 예정)**: 확정 게이트 본격(틱 스탬프 + 롤백 폐기), **velocity 권위 이전(Rigidbody → `World.Entity`, 4e 잠금 해제 + 예측 전제)**, Snapshot/Restore(클라 외각 책임 — `netcode-redesign.md` 참조), 클라 측 Generation(예측), 결정론적 RNG, `IInputSource` 표준 provider(4d), reconciliation 재설계(delta-replay → Snapshot/Restore + input replay).
