# EventBus → MessagePipe(R3 생태계) 이전 + 사용량 정리

**Date:** 2026-07-16
**Branch (제안):** `feature/eventbus-messagepipe-migration`
**Related:** [audit #8](../audit-2026-07-13-structure.md) · [ROADMAP #8](../../ROADMAP.md) · [아키텍처 가이드라인 "R3 통일"](../../architecture-guidelines.md) · [connection-architecture "연속 pull / 이산 event"](../../world-core-connection-architecture.md)

## Goal

전역 static 커스텀 이벤트 버스(`GameFramework.EventBus` + 앱별 `EventBus.Default`/`EventTopic`)를 **Cysharp MessagePipe** 기반의 **타입·keyed pub/sub + DI 스코프 브로커**로 이전하고, 그 과정에서 **버스에 태우는 이벤트 사용량을 정리**한다. 두 목표:

1. **R3/DI 통일** — architecture-guidelines의 "이벤트는 R3로 통일" + VContainer DI에 정합. 문자열 토픽·리플렉션 디스패치·전역 static 제거.
2. **룸 재입장 leak 구조적 해소** — 구독을 `IDisposable`로 소유 수명에 묶고(①), 게임플레이 버스를 게임/룸 스코프에 두어 룸 종료 시 통째로 폐기(②).

## 배경 / 동기 (audit #8)

현재 `GameFramework.EventBus`는 `Dictionary<topic, Dictionary<Type, SortedDictionary<priority, List<handler>>>>` 저장소에 리플렉션 `DynamicInvoke`로 디스패치하는 **전역 static 버스**다. 앱마다 `EventBus.Default = new GameFramework.EventBus()`(프로세스 단일 인스턴스) + `EventTopic` 헬퍼가 중복 존재한다.

전수 조사(2026-07-16) 결과:

- **호출처 약 40곳** (클라 ~30, 서버 ~10). GameFramework/Shared에는 `EventBus.Default` 호출처 0 — 전부 앱 프로젝트.
- **와일드카드 사용 0, 비-0 priority 사용 0** — 버스의 `*` 매칭·우선순위 기능을 *아무도 안 씀* → 폐기 가능.
- **토픽 5종 중 4종이 사실상 타입 라우팅** — 문자열 토픽이 잉여(아래 매핑).
- **leak 모델**: 프로세스 전역 static + 수동 Subscribe/Unsubscribe 짝. 현재 모든 구독자가 짝을 맞추고 있으나 *규약 의존*(구조적 보장 아님). `GameDataStore.Dispose` 주석이 위험을 명시.
- **R3 1.3.1 이미 설치**(클라 NuGet), UI ViewModel이 R3 사용 중. 단 R3 코어엔 UniRx의 전역 `MessageBroker.Default`가 없음 → 버스는 컴패니언 라이브러리나 자작 필요.

## 리서치 요약 (2026-07-16, 웹 검증)

"이 구조가 업계 표준인가"를 웹으로 확인한 결과:

- **패턴 자체(이벤트 애그리게이터/pub-sub/Observer)는 표준** — Godot signals, UnityEvents, UniRx/R3, C# events 모두 같은 계열. 버스를 *가진 것*은 비표준이 아님.
- **"전역 static + 문자열 토픽 + 광범위 사용" 형태는 일관되게 경고 대상** — hidden coupling("뭐가 뭐랑 통신하는지 추적 불가"), 디버깅 난해, 구독 boilerplate, leak 취약. *진짜 전역 이벤트에만* 쓰고 나머진 DI 스코프/타입이벤트/직접참조 권장.
- **타입 pub/sub > 문자열 토픽** — 컴파일 안전·리팩터·IDE 지원.
- **R3 생태계의 표준 답 = Cysharp MessagePipe** — DI 관리, **스코프별 브로커, 스코프 dispose 시 구독 자동 해제(leak 원천 차단)**, 타입·keyed pub/sub. 감사 #8 목표를 그대로 구현한 기성품(같은 저자 생태계).
- **게임 뉘앙스(LOP 자기 문서와 일치)**: 버스는 "변화"만 주고 "초기 상태"를 못 줌 → *모든 걸* 버스에 태우지 말고 연속 상태는 pull. connection-architecture의 "연속 pull / 이산 event"와 동일.
- 주의: 마이크로서비스 "이벤트 버스 안티패턴" 비판(CodeOpinion 등)은 *분산 EDA* 얘기라 인프로세스 게임 클라엔 절반만 적용.

출처: [Unity Discussions — Global Event Bus 논쟁](https://discussions.unity.com/t/is-global-event-bus-pattern-a-good-practice/938018) · [Endless While Loop — Stop using event buses](https://endlesswhileloop.com/blog/2015/06/11/stop-using-event-buses/) · [Febucci — Godot Signals Architecture](https://blog.febucci.com/2024/12/godot-signals-architecture/) · [Cysharp/MessagePipe](https://github.com/Cysharp/MessagePipe) · [Cysharp/R3](https://github.com/Cysharp/R3)

## 설계 결정 (브레인스토밍 합의)

| 축 | 결정 | 비고 |
|---|---|---|
| 범위 | **클라+서버 양쪽**, `EventBus`/`IEventBus`/`HandlerWrapper`(GameFramework) + `EventBus.Default`/`EventTopic`(앱) **완전 삭제** | 포팅 + 정리 같이 |
| 라이브러리 | **Cysharp MessagePipe** (양쪽 앱에 새 의존성) | ①+②를 기성품으로. R3 자작 대비 keyed pub/sub·스코프 auto-dispose 내장 |
| 라우팅 | **타입 기반** (`IPublisher<T>`/`ISubscriber<T>`). 엔티티별은 **keyed**(`IPublisher<string,T>`/`ISubscriber<string,T>`, 키=entityId) | 문자열 토픽 소멸. keyed가 `EntityId<LOPEntity>(id)`를 native로 해결 |
| leak 해소 | **① 구독 = `IDisposable`**(소유 수명에 묶음, 버스 전역/스코프 무관하게 구조적) + **② 게임/룸 스코프 브로커**(룸 종료 시 폐기 = 안전망) | 감사 통증(룸 재입장)에 ② 정조준 |
| 스코프 | **게임/룸 스코프 브로커**(게임플레이·네트워크·엔티티) + **앱/루트 스코프**(WebResponse 소수) | VContainer Root→Room→Game 계층 활용 |
| 정리 경계 | 아래 "사용량 정리 경계" | 물리 브릿지 얽힘은 안 건드림 |

## 토픽 → MessagePipe 매핑

| 현재 토픽 | 페이로드 | → MessagePipe | 스코프 |
|---|---|---|---|
| `nameof(IMessage)` | 모든 wire 메시지 타입(GameInfoToC/DamageEventToC/EntitySnapsToC/…) | 수신 경계가 **런타임 타입 디스패치**로 `IPublisher<TConcrete>` 발행, 핸들러는 `ISubscriber<TConcrete>` | 게임 |
| `EntityId<LOPEntity>(id)` | PropertyChange·EntityDamage·AbilityActivated·EntityHealth/Mana/Level/StatPoints/Stat Changed | **keyed** `IPublisher<string,T>`/`ISubscriber<string,T>` (키=entityId) | 게임 |
| `EventTopic.Entity` | ItemTouch | `IPublisher<T>`/`ISubscriber<T>` | 게임 |
| `nameof(EntityCreated)`/`nameof(EntityDestroyed)` | EntityCreated·EntityDestroyed | `IPublisher<T>`/`ISubscriber<T>` | 게임 |
| `EventTopic.WebResponse` | CreateUser/GetUser/GetMatch/… Response | `IPublisher<T>`/`ISubscriber<T>` | **앱/루트** |

### 네트워크 수신 경계 — 유일한 설계 주름

현재 `LOPRoom`이 Mirror 핸들러에서 `EventBus.Default.Publish(nameof(IMessage), message.payload)`(payload는 정적 `IMessage`)를 발행하고, 버스가 `data.GetType()`로 **런타임 타입 디스패치**한다. MessagePipe는 **컴파일 타임 타입** 모델이라 `IPublisher<T>.Publish`가 T를 정적으로 요구한다. 경계에서 `IMessage`의 *구체 타입*을 아는 시점은 런타임뿐 → **타입 디스패치 브릿지**가 필요하다:

- 구체 타입 → 타입별 publisher로 보내는 얇은 `NetworkMessageDispatcher`(또는 기존 `MessageFactory`/MessageIds 레지스트리 재사용)를 경계에 둔다. 메시지 타입 집합은 이미 `MessageIds`로 알려져 있어 등록 테이블로 구성 가능.
- 대안(각 핸들러가 `ISubscriber<IMessage>` 구독 + 타입 필터)은 매 메시지마다 전 핸들러가 깨어나 demux 이점을 잃으므로 채택 안 함. 특히 스냅샷은 고빈도.
- 구현 방식(등록 테이블 vs 소스젠 vs 리플렉션 캐시)은 plan에서 확정.

## 사용량 정리 경계 (`PropertyChange` 해부 근거)

`PropertyChange`(엔티티 position/rotation/velocity/visualId setter 발행) 소비자 3곳 실측:

| 소비자 | 실제 반응 | 판정 |
|---|---|---|
| `LOPEntityView` | `visualId`만(외형 리로드). 모션 무시, 속도는 이미 pull | 이산 `visualId`만 유효 |
| `PhysicsComponent` | `position`만 → `rb.position = entity.position`(facade로 위치 셋 시 rb 동기) | `position`은 좁지만 살아있음 |
| `LOPEntityController` | `visualId` case 빈 본문(no-op) | 죽은 구독 |

**정리 경계:**

| #8에 포함 | 별도 워크스트림(world-core 뷰 이행/Stage④) |
|---|---|
| **구조적(포팅에 딸려옴)**: 커스텀 버스 삭제 → wildcard/priority/DynamicInvoke/문자열토픽 소멸, 타입 라우팅으로 잉여 `nameof(IMessage)` 토픽 붕괴 | **`position` PropertyChange → pull 전환**: 물리 브릿지(SyncPhysics/PushMotion/kinematic 권위)와 얽힘. #8은 `position`·`visualId` PropertyChange를 keyed로 *그대로 포팅*하고 후속 플래그만 남김 |
| **명백한 죽은 코드 제거**: `rotation`/`velocity` PropertyChange 발행 삭제(소비자 0), `LOPEntityController`의 no-op PropertyChange 구독 삭제 | |
| **ViewModel 수신원 이전**: `CharacterHudViewModel`/`StatsViewModel`이 EventBus로 받던 데이터 소스를 MessagePipe `ISubscriber<string,T>`로 교체. VM→View의 R3 `ReactiveProperty` 바인딩은 정당한 MVVM이라 유지(이중 "수신원"만 R3측으로 단일화) | |

## DI 배선 (스코프별 브로커)

- **앱/루트 스코프**: WebResponse용 브로커. `RootLifetimeScope`(또는 상응)에서 MessagePipe 등록. 소비자 = `RoomDataStore`/`UserDataStore`.
- **게임/룸 스코프**: 게임플레이·네트워크·엔티티용 브로커. `GameLifetimeScope`(클라)/서버 상응에서 등록 → 룸 종료 시 스코프 dispose와 함께 브로커·구독 폐기(② 안전망).
- 구독자는 `IPublisher<...>`/`ISubscriber<...>`를 `[Inject]`로 받고, 구독 반환 `IDisposable`을 자기 `CompositeDisposable`/`DisposableBag`에 담아 소유 수명에 묶는다(①).
- MessagePipe 옵션(글로벌 vs 스코프 브로커 등록, open-generic 해석, keyed 등록)의 구체 API 배선은 plan에서 확정.

## 슬라이스 (구현 계획 스케치 — plan에서 확정)

각 슬라이스는 클·서 **같은 이벤트 패밀리**를 양쪽에서 함께 이전하고, 그 슬라이스 범위의 EventBus 호출처를 제거한다. 커스텀 버스 삭제는 마지막.

| 슬라이스 | 범위 | 비고 |
|---|---|---|
| **0** | MessagePipe 의존성 추가(양쪽 앱) + DI 스코프 브로커 등록 골격. 호출처 변경 0, 컴파일 baseline | 새 의존성 도입 |
| **1** | 타입 라우팅 단순 패밀리 — WebResponse(앱 스코프) + Entity 라이프사이클(EntityCreated/Destroyed) + Entity(ItemTouch) | 가장 안전 |
| **2** | 네트워크 수신(`nameof(IMessage)`) — 타입 디스패치 브릿지 + 전 MessageHandler를 `ISubscriber<T>`로 | 위 "주름" |
| **3** | 엔티티별 keyed 패밀리 — PropertyChange(position·visualId만)/EntityDamage/AbilityActivated/Health·Mana·Level·Stat + **죽은코드 트림** | keyed pub/sub |
| **4** | `EventBus`/`IEventBus`/`HandlerWrapper`/`EventTopic`/`EventBus.Default` 삭제(전 레포) + 최종 검증 | 완료 게이트 |

## 산업 표준 매핑

- **MessagePipe** = Cysharp의 DI 관리 인프로세스 메시징(.NET MediatR 계열의 pub/sub + 성능 지향). 스코프별 브로커 + 스코프 dispose 시 auto-unsubscribe = **DI 수명 스코프 표준**.
- **keyed pub/sub** = 키별 채널(엔티티 id 키) — 토픽-per-키의 타입 안전 버전.
- **타입 라우팅 > 문자열 토픽** = 컴파일 안전 pub/sub 베스트프랙티스.
- **버스는 이산 이벤트 / 연속 상태는 pull** = connection-architecture 채택 모델(Godot "shared variable" 중간형, Fiedler state sync와 정합).
- **R3** = UniRx 후속(코드베이스가 UI에서 이미 채택). MessagePipe는 R3와 별개 축(pub/sub 라우팅) — 필요 시 `MessagePipe.R3` 브릿지로 Observable 노출 가능(현 범위 밖, VM은 핸들러 구독으로 충분).

## Out of Scope

- **실제 구현** — plan에서 슬라이스 단위로 (`writing-plans`).
- **`position` 연속상태 PropertyChange → pull 전환** — 물리 권위 브릿지와 얽혀 world-core 뷰 이행/Stage④가 소유. #8은 그대로 포팅 + 후속 플래그만.
- **`MessagePipe.R3` Observable 노출로 VM 리팩터** — VM은 MessagePipe 핸들러 구독 → 기존 R3 `ReactiveProperty`로 충분. 스트림 컴포지션이 필요해지면 후속.
- **네트워크 *송신* 경로** — 이미 직접 Mirror `Send`라 버스 무관. 변경 없음.
- **UniRx 잔재 제거**(`LOPEntityView`/`LOPEntityController`의 미사용 `using UniRx`) — 기회 있을 때 별도.

## Open Questions (plan에서 해소)

- **네트워크 타입 디스패치 브릿지 구현 방식** — 등록 테이블(MessageIds 재사용) vs 소스젠 vs 리플렉션 캐시. 스냅샷 고빈도라 alloc/perf 확인.
- **MessagePipe 스코프 브로커 API** — VContainer 통합에서 게임 스코프 브로커 격리(글로벌과 분리) 정확한 등록. open-generic·keyed 해석 확인.
- **메시지 타입 위치** — 페이로드 타입(EntityDamage/PropertyChange/EntityCreated 등)은 앱 프로젝트 유지(현행). MessagePipe 등록은 앱별.
- **MessagePipe 설치 경로** — NuGetForUnity(R3처럼) vs UPM(git). 양쪽 앱 + IL2CPP/모바일 호환 확인.
- **서버 클린 컴파일** — 패키지 파일 삭제 시 stale CS2001(메모리 `[[deleting-package-files-cs2001]]`) → `refresh scope=all force`.

## 진행

- [x] EventBus 사용 전수 지도 (호출처·토픽·수명·와일드카드/priority·R3 존재)
- [x] 업계 표준 웹 리서치 (패턴 정당 / 전역-static 형태 비표준 / MessagePipe = R3 생태계 표준 답)
- [x] 브레인스토밍 합의 (양쪽+삭제 / MessagePipe / 타입·keyed / ①+② 스코프 / 정리 경계)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
