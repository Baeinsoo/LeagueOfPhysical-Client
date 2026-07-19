# 클라 Assets/Scripts 폴더 재편 — 그루핑 + 업계 표준 네이밍

`LeagueOfPhysical-Client`의 `Assets/Scripts` 폴더 구조를 **개념 단위로 그루핑**하고 **업계 표준 용어로 네이밍 정정**한다. 코드 동작은 무변화 — 순수 파일/폴더 이동 + 폴더 개명뿐.

## 목표 / 비목표

**목표**
- 흩어진 엔티티 관련 코드(3폴더)를 `Entity/` 한 곳으로 통합
- 흩어진 넷코드 코드를 `Netcode/`로 신설·집결
- 오해 소지가 있거나 개념적으로 부정확한 폴더 이름을 업계 표준으로 정정
- 1파일/빈 껍데기/빈 폴더 정리

**비목표 (YAGNI)**
- Feature-based 구조(`Features/{Combat,…}/`)로의 전면 이행 — 이번엔 안 함
- `Entity/`·`Netcode/` 내부 하위폴더 세분 — 지금은 평면, 필요해지면 나중에 쪼갬
- 서버 repo 정리 — 이번은 **클라만**. 서버는 별도 세션에서 대칭 적용
- 코드 내용(네임스페이스/타입/DI 등록) 변경 — 0줄

## 왜 안전한가 (핵심 전제)

- **모든 파일이 평면 `namespace LOP`** — 폴더는 네임스페이스/타입 해석에 영향을 주지 않는다. 파일을 옮겨도 참조가 안 깨진다(코드 편집 0).
- **asmdef 없음** — 클라 전체가 단일 `Assembly-CSharp`. 어셈블리 참조 편집 불필요.
- **씬/프리팹 참조는 GUID 기반** — GUID는 `.meta`에 있다. `.cs`와 `.meta`를 **항상 함께** 옮기면(그리고 폴더 개명 시 폴더 자체의 `.meta`도 함께) MonoBehaviour/에셋 참조가 보존된다.
- 따라서 이 작업의 리스크는 "`.meta` 동반 이동 누락" 하나로 좁혀진다 — `git mv`로 `.cs`/`.meta`를 쌍으로 옮기면 방지된다.

## 업계 표준 네이밍 근거 (요약)

웹 리서치로 확인(출처는 대화 로그):

- **`Netcode/`** — Unity 공식 용어("Netcode for GameObjects/Entities"), 게임 넷코드 통용어. 흩어진 시간동기/보정/스냅샷을 여기로.
- **`Entity/`** — ECS/게임 도메인 표준.
- **`Stores/`** (← `Data/`) — `GameDataStore` 등은 런타임 상태를 캐시하는 **client-side state store**(Flux/Redux/MobX "store" 관용어)지 영속 repository가 아니다. `Data/`는 `Model/`과 헷갈려 개명.
- **`Domain/`** (← `Model/`) — 최상위 `Model/`은 `WebAPI/Dto/`를 AutoMapper로 매핑해 만든 **앱 내부 도메인 표현**(anemic domain model). "Model"은 도메인 모델/DTO 양쪽을 뜻해 모호 → `Domain/`으로 역할 명시. (단 스냅샷·enum 이물질은 정리 — 아래.)
- **`WebAPI/Dto/`** (← `WebAPI/Model/`) — 백엔드 응답 원본은 로직 없는 전송 객체 = **DTO**가 정확. `Model`이라 오히려 오해를 준다.
- **유지 결정**: `Mapper/`(AutoMapper 관용), `MatchMaking/`·`Entrance/`(사소한 표기/영어라 이번엔 손대지 않음 — 사용자 결정).

## 이동/개명 명세

> 규칙: 모든 항목은 `.cs`와 짝 `.meta`를 함께 옮긴다. 폴더 개명은 폴더 `.meta`(부모에 위치, 예 `Assets/Scripts/Data.meta`)도 함께 개명. 빈 폴더 삭제는 폴더 + 폴더 `.meta` 제거.

### ① Entity 통합 → `Entity/`
- `EntityCreator/CharacterCreator.cs` → `Entity/`
- `EntityCreator/ItemCreator.cs` → `Entity/`
- `Component/PhysicsFollower.cs` → `Entity/`
- `Game/EntityBinder.cs` → `Entity/`
- 빈 껍데기 폴더 삭제: `EntityCreator/`, `Component/`

**`Entity/` 최종(11)**: ActorRegistry, EntitySpawner, EntityBinder, Event.Entity, LOPActor, LOPEntityView, CharacterCreator, ItemCreator, CharacterCreationData, ItemCreationData, PhysicsFollower

### ② `Netcode/` 신설 — 흩어진 넷코드 집결
- `Game/`에서: LOPTickUpdater, MirrorNetworkTime, LeadState, ReconciliationStats, InputTimingStats, MatchSeed
- `Entity/`에서: Reconciler, LocalEntityInterpolator, RemoteEntityInterpolator, RemoteInterpolationClock
- `World/WorldEventSink.cs` → `Netcode/` (그 후 빈 `World/` 삭제)
- `Model/EntitySnap.cs`, `Model/LocalEntitySnap.cs` → `Netcode/` (스냅샷은 도메인 모델 아님)

**`Netcode/` 최종(13)**: LOPTickUpdater, MirrorNetworkTime, LeadState, ReconciliationStats, InputTimingStats, MatchSeed, Reconciler, LocalEntityInterpolator, RemoteEntityInterpolator, RemoteInterpolationClock, WorldEventSink, EntitySnap, LocalEntitySnap

### ③ `Data/` → `Stores/` (폴더 개명, 8파일 그대로)
GameDataStore, IGameDataStore, MatchMakingDataStore, IMatchMakingDataStore, RoomDataStore, IRoomDataStore, UserDataStore, IUserDataStore

### ④ `Model/` → `Domain/` (폴더 개명; ②에서 스냅샷 2개 빠진 뒤 11파일)
Enums, GameRoomLocationDetail, Location, LocationDetail, Match, Room, User, UserLocation, UserProfile, UserStats, WaitingRoomLocationDetail
- enum들(GameMode/Location/RoomStatus)은 도메인 enum이라 `Domain/`에 잔류(별도 하위폴더 안 만듦 — YAGNI).

### ⑤ `WebAPI/Model/` → `WebAPI/Dto/` (하위폴더 개명)

### ⑥ `Messaging/` → `Network/` 병합
- `Messaging/NetworkMessageDispatcher.cs` → `Network/`
- 빈 `Messaging/` 삭제

### ⑦ 빈 폴더 삭제 (파일 0개)
- `Extensions/`, `Popup/`, `Game/UI/`

### ⑧ 손대지 않음
- `Game/` 나머지(호스트/플레이어/카메라/데이터 프로바이더 + `MessageHandler/`), `UI/`, `Room/`, `Lobby/`, `Login/`, `MatchMaking/`, `Entrance/`, `Mapper/`, `Network/Mirror/`, `Application/`, `Scene/`, `Editor/`, `Network/`(Mirror 하위 유지)

## 검증

- **컴파일**: 각 이동 묶음 후(또는 전체 후) Unity 에디터 컴파일 클린 — UnityMCP `read_console`로 에러 0 확인(`unity_instance` = 클라 인스턴스).
- **`.meta` 무결성**: `git status`에 `.cs`만 옮겨지고 `.meta`가 남거나(orphan) 삭제된 항목이 없는지 확인. Unity가 "missing meta / meta without asset" 경고를 안 내야 함.
- **인게임 스모크**: 로그인→매칭→게임 진입→이동/충돌/아이템/넉백/데미지/원격보간/롤백 한 바퀴. 특히 MonoBehaviour(PhysicsFollower, 보간기, LOPActor, EntityBinder)의 씬/프리팹 참조가 GUID 보존으로 살아있는지.

## 리스크 & 완화

| 리스크 | 완화 |
|---|---|
| `.meta` 동반 이동 누락 → GUID 참조 깨짐 | `git mv`로 `.cs`/`.meta` 쌍 이동. 폴더 개명은 폴더 `.meta`도. 커밋 전 `git status`로 짝 확인 |
| 폴더 개명을 Unity 밖(git)에서 해 에디터가 혼란 | 이동 후 Unity가 도메인 리로드하며 재import. 컴파일·경고 0 확인이 게이트 |
| 이동 도중 부분 상태로 컴파일 깨짐 | 묶음(①~⑦) 단위로 이동하고 각 묶음 후 컴파일 확인, 또는 전체 이동 후 한 번에 |

## 작업 순서(제안)

파일 이동은 상호 독립적이라 순서 자유. 안전하게는: ⑦빈폴더삭제 → ①Entity → ②Netcode → ③Stores → ④Domain → ⑤Dto → ⑥Network 병합. 각 단계 `git mv` 쌍 이동, 마지막에 Unity 컴파일 + 인게임 검증. 완료 후 `docs/ROADMAP.md` Done 원장에 기록.
