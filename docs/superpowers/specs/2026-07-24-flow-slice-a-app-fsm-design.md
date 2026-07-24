# Slice A — 앱 FSM 씬 전환 일원화 (설계)

프론트엔드 플로우 골격(Slice A~D)의 마지막 조각. 흩어진 씬 로드를 **앱-플로우 상태 머신 한 곳**으로
모은다. 골격 스펙 `2026-07-23-front-end-flow-skeleton-design.md`의 "씬 페이즈 층"을 구현한다.

## 범위

**한다**: `AppStateMachine`(Root 스코프) 신설 → 씬 페이즈(`Boot`/`FrontEnd`/`InMatch`) 소유. 지금 4곳에
흩어진 `LoadScene` 호출을 이 FSM으로 모으고, 각 소스는 씬을 직접 로드하는 대신 도메인 신호
(`BootCompleted`/`MatchFound`/`MatchEnded`)만 발행한다.

**안 한다(범위 밖)**:
- 파킹된 "매치 종료 시 유저 위치 백엔드 desync"(로비 재진입 시 자동 재접속 루프)는 **그대로 파킹**한다.
  이 루프는 현재 코드에도 이미 있고 Slice A가 새로 만드는 게 아니다. 근본 해결은 백엔드(RoomServer/WebAPI)
  위치 정리 몫. → 로컬 전체 루프(play→매치→결과→로비) 플레이테스트 시 결과→로비 구간에 자동 재접속이 낄
  수 있음(예상된 제약).
- 각 화면 내용·UI·전환 애니메이션(골격 스펙의 "범위 밖"과 동일).

## 현재 씬 전환 지도 (검증됨)

씬: `Entrance`(부트) → `Lobby`(프론트엔드) → `Room`(매치, `LOPGame` additive 로드).

`LoadScene` 호출 5곳:

| # | 위치 | 로드 | 상태 |
|---|---|---|---|
| 1 | `EntranceScene.Start` | Lobby | ✅ 활성 (부트 → 로비) |
| 2 | `CheckLocationComponent` | Lobby/Room | ⛔ 비활성 — `EntranceLifetimeScope`에서 주석 처리(죽은 코드) |
| 3 | `RoomConnector.TryToEnterRoomById` | Room | ✅ 활성 — 매칭 `InGameRoom`이 호출(= "매치 잡힘") |
| 4 | `LOPRoom.Awake` catch | Lobby | ✅ 활성 (에러 복구) |
| 5 | `LOPRoom.OnGameStateChanged(GameOver)` | Lobby | ✅ 활성 (매치 종료, Slice D) |

Slice A는 활성 4곳(#1·#3·#4·#5)을 `AppStateMachine`으로 흡수한다. #2는 이미 죽은 코드.

## 핵심 — `AppStateMachine` (Root 스코프)

`MatchStateMachine`과 **같은 GameFramework `StateMachine<TEvent>` 인프라** 위에 얹는 앱-플로우 FSM.
같은 인프라의 두 번째 사용처.

### 상태 · 이벤트 · 전이표

`AppEvent { BootCompleted, MatchFound, MatchEnded }` — 골격 스펙 그대로 3개.

| 현재 상태 | 이벤트 | 다음 상태 | 부수효과(진입 시) |
|---|---|---|---|
| `Boot` (init) | `BootCompleted` | `FrontEnd` | (없음 — Entrance 씬은 이미 로드됨) |
| `FrontEnd` | `MatchFound` | `InMatch` | `Lobby` 씬 로드 |
| `InMatch` | `MatchEnded` | `FrontEnd` | `Room` 씬 로드 |

`initState = Boot`. 루프 구조: `Boot → FrontEnd → InMatch → FrontEnd → …`.

### 상태 클래스의 성격 — 외부 신호로만 전이

세 상태는 매칭 상태들과 달리 **자체 async 로직(폴링·네트워크)이 없다**. 전이는 전부 바깥에서 오는 신호
(EntranceScene / 매칭 FSM / 매치 종료)가 구동한다. 따라서 각 상태는:

- `OnEnter` → 자기 씬 로드(`ISceneLoader.Load`). `Boot`는 no-op.
- `GetNextState(ev)` → 순수 switch(위 표).
- `OnExecuteAsync`/`OnError` 미사용(base 기본값).

의존성을 주입받지 않으므로 매칭 상태처럼 per-state DI 등록(`RegisterState<T>` + `Func<T>`)이 **불필요**하다.
`AppStateMachine`이 내부에서 세 상태를 직접 조립(`Func<다음상태>` 람다로 전이 대상 연결)한다.

### 생명주기 — 언제 Start 하나

Root 싱글턴. 앱 시작 시 작은 Root 엔트리포인트(`IStartable`)가 `appStateMachine.Start()` → `Boot` 진입
(no-op OnEnter). Root 스코프는 앱 전역 부모라 가장 먼저 빌드되고, `EntranceScene`이 `BootCompleted`를
발행하는 시점(로그인·마스터데이터 등 async 컴포넌트 완료 후, 수 초 뒤)보다 훨씬 앞서 Start 된다.

## 씬 소유 — `ISceneLoader` 포트

씬 로드를 `ISceneLoader`(메서드 `void Load(string sceneName)`, non-additive) 포트 뒤에 둔다. Unity 구현
`SceneLoader`가 `SceneManager.LoadScene(sceneName)`을 감싼다.

**근거 3가지**:
1. 흩어진 씬 이름 리터럴을 한 곳에 모은다 — Slice A의 목적 자체.
2. FSM을 `UnityEngine.SceneManagement` static에서 분리한다.
3. 페이크 `ISceneLoader`로 "각 상태 진입 시 올바른 씬을 로드하는지" 테스트 가능.

아키텍처 문서의 `Infrastructure/SceneManagement`(`SceneLoader`, `SceneTransition`) 네이밍과 정합.

씬 이름은 `AppStateMachine`(또는 상태) 안의 `const string`으로 중앙화한다(`"Lobby"`, `"Room"`).
`Boot`는 로드하지 않으므로 Entrance 씬 이름은 `AppStateMachine`이 알 필요 없다.

> 대안(기각): 상태가 `SceneManager.LoadScene`을 직접 호출. 더 단순하지만 씬 이름이 다시 흩어지고 FSM이
> Unity static에 묶여 테스트 불가.

## 신호 재배선 (LoadScene 4곳 → Fire)

| 위치 | 지금 | Slice A 후 |
|---|---|---|
| `EntranceScene.Start` | `LoadScene("Lobby")` | `appFsm.Fire(BootCompleted)` |
| 매칭 `InGameRoom` | `RoomConnector` → `LoadScene("Room")` | 조인 확인 성공 시 `appFsm.Fire(MatchFound)` |
| `LOPRoom.OnGameStateChanged(GameOver)` | `LoadScene("Lobby")` | `appFsm.Fire(MatchEnded)` |
| `LOPRoom.Awake` catch (에러) | `LoadScene("Lobby")` | `appFsm.Fire(MatchEnded)` |

각 소스는 `AppStateMachine`을 주입받아(Root 싱글턴 → 자식 스코프에서 resolve 가능) Fire만 한다.

### 에러 복구는 `MatchEnded` 재사용

`LOPRoom.Awake` 실패(룸 접속 에러) 시에도 `MatchEnded`를 발행해 `FrontEnd`로 복귀한다. 정상 종료와
구분은 **결과 스토어에 값이 있는지**로 자연 처리된다:

- 정상 종료: `MatchEndedMessageHandler`가 결과를 스토어에 기록 → `FrontEndCoordinator`가 결과 창을 띄움.
- 에러: 결과 스토어가 비어 있음 → 결과 창 안 뜸.

별도 `MatchAborted` 이벤트가 불필요하므로 골격 스펙의 3-이벤트를 유지한다.

### `MatchEndedMessageHandler`는 그대로

결과 스토어 기록 + `runner.EndMatch()`만 담당(변경 없음). `EndMatch()`가 러너를 `GameOver`로 보내고,
`LOPRoom.OnGameStateChanged(GameOver)`가 위 표대로 `Fire(MatchEnded)`한다. 씬 전환은 그 경로로 흡수.

## `RoomConnector` — LoadScene만 제거

`TryToEnterRoomById`에서 `SceneManager.LoadScene("Room")` 한 줄을 삭제한다. 나머지(재시도 루프 +
`CheckRoomJoinable` + 성공 시 `RoomDataStore.room` 채움)는 유지. `InGameRoom`이 성공 반환을 받아
`MatchFound`를 Fire하고, 씬 로드는 `InMatch`가 한다.

### 순서 불변식 (Room 씬이 데이터를 제때 받는가)

`RoomDataStore`(Root 싱글턴)는 `RoomJoinableResponse`를 구독해 `room`을 채운다. `InGameRoom`이 조인 확인을
**await**(→ `room` 채워짐) 한 뒤에야 `MatchFound`를 Fire → `InMatch.OnEnter`가 `Room` 로드 → `LOPRoom`이
`roomDataStore.room`을 읽는다. `RoomDataStore`가 씬 전환을 넘어 살아남으므로(Root 싱글턴) 순서·데이터 모두
현재와 동일하게 보존된다.

### `InGameRoom` 정리

`InGameRoom`은 현재 `RoomConnector`(자체 10회 재시도)를 다시 10회 감싸 로드를 시도한다. Slice A에서는
`RoomConnector` 성공 시 즉시 `MatchFound`를 Fire하고 종료하도록 단순화한다(자체 재시도는 `RoomConnector`가
이미 함). 실패 시 `RecheckRequested`로 위치 재확인(현재와 동일). 씬 전환 후 `Lobby` 언로드로 매칭 FSM은
`LobbyLifetimeScope.OnDestroy`에서 정리되므로 `InGameRoom`은 `MatchFound` 발행 후 자기 전이가 불필요
(`OnExecuteAsync`가 null 반환).

## DI (RootLifetimeScope)

- `AppStateMachine`을 싱글턴 등록.
- `ISceneLoader → SceneLoader`를 싱글턴 등록.
- 앱 시작 시 `Start()`를 거는 Root 엔트리포인트(`IStartable`) 1개 등록(또는 `AppStateMachine` 자체를
  `IStartable`로 — 계획에서 결정).

자식 스코프(Entrance/Lobby/Room)는 부모 Root에서 `AppStateMachine`을 주입받는다.

## 테스트

- **FSM 메커니즘**(Fire/재진입 큐/전이): GameFramework EditMode `StateMachineTests`(7개)에 **이미 있음**.
  `AppStateMachine`이 그 위에 얹히므로 재사용 — 새로 검증할 필요 없음.
- **`AppStateMachine`의 3-엣지 전이표 + 씬 로드 배선**: 페이크 `ISceneLoader`로 "각 상태에서 올바른
  이벤트에 올바른 씬을 로드하는지 / 잘못된 이벤트는 무시되는지" 검증. 클라는 Assembly-CSharp라 EditMode
  불가 → PlayMode + 리플렉션 관용(`client-test-infra-constraint`). 그래프가 작아(3 상태·3 엣지) 가볍다.
- **엔드투엔드 루프**: 플레이테스트(로컬 서버) — Boot→로비→Play→매칭→인게임→GameOver→결과→로비 고리가
  도는지. ⚠️ 파킹된 백엔드 desync로 결과→로비 구간은 백엔드 위치 정리 전까진 자동 재접속이 낄 수 있음
  (예상된 제약, 위 "범위" 참고).

## 죽은 `CheckLocationComponent` 삭제 (범위 포함)

`CheckLocationComponent`는 부트 시 `GetUserLocation`으로 유저 위치를 물어 `GameRoom`이면 그 방으로 재접속,
아니면 로비로 보내던 컴포넌트다. 이미 `EntranceLifetimeScope`에서 등록이 주석 처리돼 **비활성(죽은 코드)**
이고, 라이브 참조가 0이다(클래스 파일 + 주석 처리된 등록 줄뿐).

이 역할은 **매칭 FSM에 완전히 흡수**됐다:

| CheckLocationComponent 분기 | 지금 담당 |
|---|---|
| `GameRoom` → 그 방으로 재접속 | 매칭 `CheckMatch` → `InGameRoom` |
| `WaitingRoom`/그 외 → 로비 | 로비 진입 + `CheckMatch` → `InWaitingRoom`/`Idle`(대기 중이면 폴링 재개 — 더 풍부) |

유일한 차이는 재접속이 한 단계 늦게(부트 직행이 아니라 `Boot → FrontEnd → CheckMatch → MatchFound → InMatch`
경유) 일어난다는 것뿐이며, 이는 이 슬라이스의 `AppStateMachine` 모델과 정확히 일치한다.

→ **Slice A에서 `CheckLocationComponent.cs`(+ `.meta`)와 `EntranceLifetimeScope`의 주석 처리된 등록 줄을
삭제**한다. `RoomConnector`를 부트 경로에서 쓰던 유일한(비활성) 소비자가 사라지므로, 삭제 후에도
`RoomConnector`의 라이브 소비자는 매칭 `InGameRoom` 하나만 남는다.

## 산업 표준 매핑

골격 스펙과 동일:
- **고수준 게임 플로우(부트↔메뉴↔인게임) = 상태 머신**: 언리얼 `GameMode`의 match 상태 머신 +
  맵 로드를 넘어 지속하는 `GameInstance` ↔ LOP의 Root 스코프 `AppStateMachine`.
- 씬 페이즈는 단순 루프라 push/pop 스택 불필요(스택은 윈도우 층 = 골격 스펙 Slice B/C의 `WindowManager`).

## 컴포넌트 요약

| 컴포넌트 | 신규/변경 | 책임 | 스코프 |
|---|---|---|---|
| `AppStateMachine` (+ `AppEvent`, 상태 `Boot`/`FrontEnd`/`InMatch`) | 신규 | 씬 페이즈 전환 소유, 씬 로드 일원화 | Root |
| `ISceneLoader` / `SceneLoader` | 신규 | 씬 로드 포트 + Unity 구현(씬 이름 중앙화) | Root |
| Root 엔트리포인트(`AppStateMachine.Start`) | 신규 | 앱 시작 시 FSM 기동 | Root |
| `EntranceScene` | 변경 | `LoadScene` 제거 → `Fire(BootCompleted)` | Entrance 씬 |
| 매칭 `InGameRoom` | 변경 | 로드 제거 → 조인 확인 후 `Fire(MatchFound)` | Lobby 씬 |
| `RoomConnector` | 변경 | `LoadScene` 한 줄 제거(조인 확인·스토어 채움 유지) | Root(Transient) |
| `LOPRoom` | 변경 | `GameOver`·에러 → `Fire(MatchEnded)` (2곳) | Room 씬 |
| `CheckLocationComponent` | 삭제 | 죽은 코드(역할은 매칭 FSM에 흡수됨) | — |
