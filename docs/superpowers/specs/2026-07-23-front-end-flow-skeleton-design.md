# 프론트엔드 플로우 골격 설계 (네비게이션 아키텍처)

## 이 스펙의 범위 — 골격만

전체 게임 플로우(로그인 → 로비 → 매칭 → 인게임 → 결과)의 **네비게이션 아키텍처(골격)** 만 확정한다:
*어떤 화면(노드)이 있고, 어떻게 이어지며, 무엇이 전환을 구동하고, 각 조각이 어느 스코프에 사는가.*

**범위 밖(후속 스펙)**: 각 화면의 상세 내용·레이아웃(로비 홈에 뭐가 있나, 결과가 뭘 보여주나, 상점 품목 등),
하단 네비바 UI 디테일, 전환 애니메이션, 실제 게임 디자인(캐릭터 선택 유무 등). 골격이 서면 이런 화면들이
그 위에 얹힌다.

## 배경

현재 플로우는 씬 전환이 세 군데(`EntranceScene`·`LOPRoom`·`RoomConnector`)에 흩어져 있고, 프론트엔드에
**로비 홈**과 **결과 화면**이 없다(로비 씬이 Play 버튼 하나뿐). 세션형 PvP 게임(브롤스타즈류)의 표준
"허브 중심 고리"(로비에서 나갔다 돌아오는 반복 루프)를 세우려면 골격부터 정리해야 한다.

## 핵심 결정 — 3층 전환 모델

전환을 **책임이 다른 3개 층**으로 가른다. 이것이 설계의 뼈대다.

| 층 | 무엇을 전환하나 | 소유자 | 스코프 |
|---|---|---|---|
| **씬 페이즈** | Boot ↔ FrontEnd ↔ InMatch (큰 컨텍스트) | **`AppStateMachine`** (앱-플로우 게임 상태 머신) | **Root**(지속) |
| **매칭 하위흐름** | Idle → 대기 → 매치잡힘 | 기존 **`MatchStateMachine`** (신호만 발행) | FrontEnd 씬 |
| **윈도우 네비** | 로비 홈 ↔ 상점/설정/프로필/결과 | **`WindowManager`(레이어×스택) + 코디네이터** | FrontEnd 씬 |

## 화면(노드) 집합 — 세션게임 표준 세트

- **Boot/로그인** (기존)
- **로비 홈** (신규 — 허브. Play + 하단 네비)
- **상점 / 설정 / 프로필** (신규 노드 — 내용은 후속, 네비 구조만 선점)
- **매칭 대기** (기존 오버레이)
- **로딩** (기존)
- **인게임** (기존 HUD/게임패드/스탯/월드 UI)
- **결과** (신규 — 판 끝 → 로비 복귀)

## 씬 맵 & 스코프

```
[Boot 씬]      = 현 Entrance 씬 (경량 부트/로그인 시퀀스)
     │  로그인·마스터데이터 완료 신호
     ▼
[FrontEnd 씬]  = 현 Lobby 씬을 'meta 씬'으로 재정의 (한 씬 + 윈도우 스택)
     │           · 로비 홈(베이스 윈도우) + 상점/설정/프로필(푸시 윈도우)
     │           · 매칭 대기(Loading 밴드 오버레이) · 결과(윈도우)
     │           · MatchStateMachine·코디네이터·프론트엔드 VM = 이 씬 스코프
     │  '매치 잡힘' 신호
     ▼
[Match 씬]     = 현 Room / LOPGame(additive) (실제 인게임)
     │  '매치 종료' 신호
     └────────► FrontEnd 씬으로 복귀 + 결과 윈도우
```

- **Root 스코프(지속)**: `AppStateMachine`, `WindowManager`, 데이터 스토어들 — 씬 넘어 살아남음.
- **FrontEnd 씬 스코프**: 매칭 흐름·프론트엔드 화면 일체 → 매치 진입 시 파괴, 복귀 시 재생성.

> **front-end 세션 스코프 결정(앞서 미룬 것) 해소**: 프론트엔드를 **한 씬 + 윈도우**로 두므로,
> 매칭(FSM·코디네이터·VM)이 **FrontEnd 씬 스코프**에 살아 상점/설정/프로필 윈도우를 오가도 자연히
> 지속된다. 별도의 persistent 서브스코프를 만들 필요 없음(같은 씬 = 같은 스코프).

## 프론트엔드 구조 결정 — 한 씬 + 윈도우 스택

메뉴 화면(로비 홈·상점·설정·프로필)은 **별도 씬이 아니라 한 FrontEnd 씬 안의 윈도우/위젯**으로 둔다.
씬은 "프론트엔드 ↔ 실제 매치" 같은 **큰 컨텍스트 전환**에만 쓴다.

- **로비 홈** = `Window` 밴드 베이스 화면.
- **상점/설정/프로필** = 로비 홈 위 push 윈도우(`Window` 밴드 스택). 나가면 pop → 로비 홈.
- **매칭 대기** = `Loading` 밴드 오버레이 — 기존 `MatchMakingCoordinator`가 그대로 담당.
- **결과** = FrontEnd 복귀 시 로비 홈 위로 자동 open되는 윈도우(닫으면 로비 홈).

## `AppStateMachine` (씬 페이즈 소유)

Root에 사는 앱 상태 머신. `MatchStateMachine`과 **같은 GameFramework FSM 인프라**(`StateMachine<TEvent>`).

- **상태**: `Boot → FrontEnd → InMatch → (다시) FrontEnd` … 루프.
- **각 상태가 씬 로드를 소유**: `FrontEnd.OnEnter` → FrontEnd 씬 로드 / `InMatch.OnEnter` → Match 씬 로드 /
  복귀 시 `FrontEnd.OnEnter` 재진입.
- **입력 = 도메인 신호**: `BootCompleted` / `MatchFound` / `MatchEnded` 세 이벤트.
- 흩어져 있던 3개 `LoadScene`을 **여기로 일원화**.

## `FrontEndCoordinator` (신규 — 프론트엔드 윈도우 네비)

FrontEnd 씬 스코프에 사는 코디네이터. 로비 홈의 네비 신호(상점/설정/프로필 열기)를 구독해 `WindowManager`로
윈도우 push/pop, 그리고 결과 윈도우 open을 담당.

- **방금 만든 `MatchMakingCoordinator`가 이 패턴의 첫 사례** — 그것을 템플릿으로 확장한다
  (VM은 신호만 노출 / 코디네이터가 윈도우 네비).
- 기존 `PlayerHudCoordinator`(Game 스코프)도 같은 코디네이터 패턴의 선례.

## `MatchStateMachine`의 새 역할 — 신호만, LoadScene 안 함

- 지금 매칭 FSM의 `InGameRoom` 상태가 **직접 방 씬을 로드**(`RoomConnector.TryToEnterRoomById`)한다.
- 새 구조: 매칭 FSM은 **매칭 로직만**(위치 폴링·방 배정 확인) 수행하고, "매치 잡힘"에 도달하면
  **앱 FSM에 `MatchFound` 신호만** 보낸다. **씬 로드는 앱 FSM(`InMatch`)이** 수행.
- 결과: 매칭 FSM은 씬을 모르게 되고(관심사 분리), 씬 전환 소유가 앱 FSM 한 곳으로 모인다.

## 엔드투엔드 루프

```
Boot(로그인)
   └▶[BootCompleted]─▶ FrontEnd(로비 홈)
                          ├─ 상점/설정/프로필 (윈도우 push/pop, 씬 그대로)
                          └─ Play ─▶ 매칭 FSM(대기 오버레이) ─[MatchFound]─▶
        InMatch(Match 씬: 인게임) ─[MatchEnded]─▶
   FrontEnd 복귀 ─▶ 결과 윈도우 ─▶ 로비 홈  (고리 완성)
```

## 현재 코드 대비 변화 (골격이 실제로 건드릴 것)

| 항목 | 지금 | 골격 후 |
|---|---|---|
| Entrance 씬 | 로그인 후 `LoadScene("Lobby")` | 로그인 후 **앱 FSM에 `BootCompleted` 신호** |
| Lobby 씬 | MatchMakingView(Play만) | **FrontEnd 씬**: 로비 홈 + 상점/설정/프로필 윈도우 |
| 씬 로드 위치 | 3곳 흩어짐(Entrance·LOPRoom·RoomConnector) | **`AppStateMachine` 한 곳** |
| 매칭 `InGameRoom` | 직접 방 씬 로드 | **`MatchFound` 신호만** 발행 |
| 결과 화면 | 없음 | FrontEnd 복귀 시 윈도우 |
| 상점/설정/프로필 | 없음 | 윈도우 노드(내용은 후속) |

> Match 종료 → FrontEnd 복귀의 시작점(현 `LOPRoom.LoadScene("Lobby")`)도 앱 FSM `MatchEnded` 신호로 대체.

## 컴포넌트 요약 (골격이 도입/변경하는 단위)

| 컴포넌트 | 신규/변경 | 책임 | 스코프 |
|---|---|---|---|
| `AppStateMachine` (+ `AppEvent`, 상태 `Boot`/`FrontEnd`/`InMatch`) | 신규 | 씬 페이즈 전환 소유, 씬 로드 일원화 | Root |
| `FrontEndCoordinator` | 신규 | 프론트엔드 윈도우 네비(상점/설정/프로필/결과) | FrontEnd 씬 |
| 로비 홈 View/VM | 신규 | 허브 화면(Play + 네비 신호 노출) | FrontEnd 씬 |
| 결과 View/VM | 신규 | 판 결과 표시(내용은 후속) | FrontEnd 씬 |
| 상점/설정/프로필 View/VM | 신규(빈 껍데기) | 네비 노드 선점 | FrontEnd 씬 |
| `MatchStateMachine` | 변경 | 씬 로드 제거, `MatchFound` 신호 발행 | FrontEnd 씬 |
| `EntranceScene` | 변경 | `LoadScene` 제거, `BootCompleted` 신호 | Boot 씬 |

## 테스트

- **`AppStateMachine`**: GameFramework EditMode로 상태 전이 단위 테스트(순수 로직 — `BootCompleted`/`MatchFound`/
  `MatchEnded`에 따라 올바른 상태로 전이하는지). `MatchStateMachine`과 같은 방식.
- **씬 전환·윈도우 네비**: 플레이테스트(로컬 서버 필요). Boot→로비→상점/설정 왕복→Play→매칭→인게임→결과→로비
  고리가 도는지, 매칭이 상점/설정 넘어 유지되는지.

## 산업 표준 매핑

이 골격은 새 발명이 아니라 검증된 표준의 조립이다:

- **고수준 게임 플로우(메뉴↔게임플레이) = 상태 머신**: [Game Programming Patterns — State](https://gameprogrammingpatterns.com/state.html),
  [Nuclex — Game State Management](http://blog.nuclex-games.com/tutorials/cxx/game-state-management/),
  [gamedevgeek — Managing Game States](http://gamedevgeek.com/tutorials/managing-game-states/). "Coordinator"는
  iOS/앱 **UI 네비게이션** 용어라 게임 씬 층엔 비표준 → 씬 층은 **FSM**, 윈도우 층은 코디네이터로 명명.
- **언리얼**: `GameMode`가 match 상태를 **state machine**으로(`EnteringMap→WaitingToStart→InProgress`), 플로우
  지속 상태는 **`GameInstance`**(맵 로드 넘어 지속)에 → LOP의 **Root 스코프 `AppStateMachine`** 과 1:1 대응.
- **프론트엔드 = 단일 맵 + 위젯 스택**: 언리얼 CommonUI/Lyra 프론트엔드 맵 + activatable 위젯 스택,
  UnityScreenNavigator Page/Modal/Sheet 스택. 메뉴는 씬이 아니라 윈도우.
- **스택(pushdown)은 윈도우 층**: 중첩 메뉴·pop 복귀용(우리 `WindowManager` 레이어×스택). 씬 페이즈는 단순
  루프라 평범한 FSM으로 충분(스택 불필요).

## 후속 (이 골격 위에 얹힐 것들)

각각 별도 spec:
- 로비 홈 화면 상세 UI (허브 내용 + 하단 네비바)
- 결과 화면 상세 UI (정산·보상 표시)
- 상점 / 설정 / 프로필 화면 내용
- (게임 디자인이 구체화되면) 캐릭터/모드 선택 등
