# Flow Slice D — 결과 화면(매치 종료 → 결과 → 로비 복귀) 설계

프론트엔드 플로우 골격(`2026-07-23-front-end-flow-skeleton-design.md`)의 마지막 고리를 잇는다.
지금까지: Slice B(로비 홈 허브) → Slice C(상점/설정/프로필 네비). 이번 Slice D는 **판이 끝나면
로비로 돌아와 결과 화면을 띄우는 경로**를 만든다.

## 문제 — 끊긴 건 UI가 아니라 첫 칸

착수 전 코드 실측 결과, "판 끝 → 결과 → 로비"에서 **클라이언트가 매치 종료를 알 방법이 아예 없었다.**

| 지점 | 현재 상태 |
|---|---|
| 서버 종료 판정 | `LOPRunner.LateUpdate`가 `elapsedTime > 60*5`면 `gameState = GameOver.State` |
| 서버 후처리 | `LOPRoom.OnGameStateChanged`가 룸을 `Closed`로 갱신 — **클라에 알리지 않음** |
| 클라 종료 인지 | 없음. 클라 `LOPRunner`에는 `GameOver`로 가는 경로가 존재하지 않음 |
| 클라 로비 복귀 | `LOPRoom.OnGameStateChanged`의 `case GameOver → LoadScene("Lobby")`는 **아무도 트리거하지 않는 죽은 가지** |
| 결과 데이터 | 와이어 메시지 없음 |

즉 이 슬라이스의 본체는 화면이 아니라 **종료 통보 경로**다. 화면은 그 경로가 살아 있음을 보여주는 플레이스홀더다.

## 결정

| 질문 | 결정 | 근거 |
|---|---|---|
| 클라가 종료를 어떻게 아는가 | **서버가 종료 메시지를 보낸다**(`MatchEndedToC`) | 서버 권위 종료가 표준. 나중에 순위·점수를 실을 자리가 그대로 생긴다. 로컬 판정(클·서 이중 판정)이나 연결 끊김 감지(정상 종료와 장애 구분 불가)는 기각 |
| 결과 화면에 무엇을 보여주는가 | **플레이스홀더**("게임 종료" + 확인) | 골격 spec이 "결과 View/VM — 내용은 후속"으로 범위를 그었고, 점수 규칙이 될 게임 모드가 아직 없다 |
| 결과를 언제 띄우는가 | **로비 복귀 후** | 골격 spec의 목표 모델과 동일하고, 씬 전환 일원화(Slice A) 없이도 가능하다. Root 스코프 스토어 하나만 추가된다 |
| 러너 상태 family | **손대지 않는다** | `GameOver`는 `None/Initializing/…/Playing/Paused/GameOver` family의 멤버다. 한 멤버만 바꾸면 family가 깨진다 |

## 어휘 규약 — game과 match는 서로 다른 층이다

이번 슬라이스에서 가장 오래 남을 결정. 코드베이스 실측으로 확인한 현재 어휘:

| 어휘 | 가리키는 것 | 근거(실제 식별자) |
|---|---|---|
| **match** | 플레이어가 큐를 잡고 실제로 치르는 **판(세션)** | `Match { id, mapId, playerList }`, `matchId`, `MatchStateMachine`, `MatchSeed`, 와이어 `GameInfo.MatchSeed` |
| **room** | 그 판을 돌리는 **서버 인스턴스** | `Room { id, matchId, status, ip, port }`, `LOPRoom` |
| **game** | **러너/프레임워크의 그릇** — 실행 상태 머신 | `IRunner.gameState`(GameFramework), `IGameState` family, `GameLifetimeScope`, `GameInfo(ToC)` |

**규칙: 새로 만드는 LOP 도메인 이름은 match를 쓴다.** game은 러너 상태 family와 GameFramework 쪽에
이미 자리 잡은 기존 어휘로만 남는다. 두 어휘가 만나는 지점은 `LOPRunner.EndMatch()` 본문의 한 줄
(`gameState = GameOver.State`)뿐이다 — 도메인 사건(match)을 러너 상태(game)로 번역하는 자리.

> 와이어가 game 레이어라서 `GameOverToC`가 맞다고 볼 여지도 있었으나, 실측하면 **와이어는 이미
> match를 쓰고 있다**(`GameInfo.MatchSeed`). 와이어는 GameFramework가 아니라 LOP 도메인 레이어다.

### 산업 표준 매핑

언리얼이 정확히 같은 층 구분을 쓴다 — **클래스는 Game\*, 판의 생애는 Match\***:

| 언리얼 | LOP |
|---|---|
| `AGameMode` / `AGameState` (프레임워크 그릇) | `LOPRunner` / `IGameState` family |
| `AGameMode::EndMatch()` (판 종료 진입점) | `LOPRunner.EndMatch()` |
| `MatchState::WaitingPostMatch`, `HasMatchEnded()` | `MatchEndedToC` → `MatchResult` |

출처: [Game Mode and Game State in Unreal Engine](https://dev.epicgames.com/documentation/en-us/unreal-engine/game-mode-and-game-state-in-unreal-engine),
[AGameMode::HandleMatchHasEnded](https://docs.unrealengine.com/4.26/en-US/API/Runtime/Engine/GameFramework/AGameMode/HandleMatchHasEnded/)

## 흐름

```
[서버] elapsedTime > 5분
   └─ LOPRunner.EndMatch()            ← 기존 인라인 대입을 메서드로 (클·서 대칭)
        └─ gameState = GameOver.State
             └─ LOPRoom.OnGameStateChanged (case GameOver)
                  ├─ 모든 세션에 MatchEndedToC 송신   ← 룸을 Closed로 바꾸기 "전에"
                  └─ 룸 상태 Closed 갱신

[클라] MatchEndedMessageHandler (Game 스코프)
   ├─ MatchResultDataStore(Root)에 MatchResult { matchId } 기록   ← 씬을 넘어 살아남는 유일한 조각
   └─ runner.EndMatch()
        └─ gameState = GameOver.State
             └─ LOPRoom.OnGameStateChanged (case GameOver) → LoadScene("Lobby")   ← 죽은 가지가 살아남

[로비] FrontEndCoordinator.Start()
   └─ 스토어에 대기 중인 결과가 있으면 MatchResultView 열기
        └─ [확인] → 닫기 + 스토어 Clear → 로비 홈
```

## 컴포넌트

### 1. 와이어 — `MatchEndedToC` (LOP-Shared)

```proto
syntax = "proto3";

// @auto_generate
message MatchEndedToC
{
}
```

**필드 없음으로 시작한다.** 종료 사유 enum을 넣을까 검토했으나 ① 우리 proto 12개에 enum 전례가 없고
② proto3는 필드 추가가 하위호환이라 나중에 무료로 붙는다 ③ 플레이스홀더 화면이 사유를 쓰지 않는다.

생성 절차: `Scripts/generate_protos.sh` 실행. 이 스크립트는 `MessageIds.cs`를 **보존**한다(기존 파일을
읽어 ID를 유지 — 지우면 전 메시지가 재번호돼 wire desync). 그래도 생성 후 diff로 기존 ID 불변 +
새 ID만 추가됐는지 확인한다.

### 2. 서버 — 브로드캐스트

- `LOPRunner.LateUpdate`: `gameState = GameOver.State` → `EndMatch()` 호출로 교체(동작 동일).
- `LOPRunner.EndMatch()` 신설: `gameState = GameOver.State`. `RunnerBase.gameState`는 `protected set`
  (프레임워크 주석: "구체 상태 값은 use-side가 정의하고 전이한다")이라 use-side에 진입점을 두는 게 맞다.
- `LOPRoom.OnGameStateChanged`의 `case GameOver`: `sessionManager.GetAllSessions()`를 돌며
  `session.Send(new MatchEndedToC())`. **룸 Closed 갱신보다 먼저** 보낸다(닫으면 클라가 끊겨 못 받음).
  기본 reliable 채널로 보낸다 — 유실되면 클라가 매치에 갇힌다.

### 3. 클라 수신 — `MatchEndedMessageHandler`

- 위치·패턴: `Assets/Scripts/Game/MessageHandler/`의 기존 5종(`GameInfoMessageHandler` 등)과 동일한
  `MessageHandlerBase` 엔트리포인트. Game 스코프에 `RegisterEntryPoint`.
- 배선 2줄: `RootLifetimeScope`에 `RegisterMessageBroker<MatchEndedToC>(options)`,
  `NetworkMessageDispatcher` 생성자에 `IPublisher<MatchEndedToC>` 추가 + `Register`.
- 동작: `MatchResultDataStore.Set(new MatchResult(roomDataStore.room.matchId))` → `runner.EndMatch()`.

### 4. 클라 러너 — `LOPRunner.EndMatch()`

서버와 같은 이름·같은 본문(`gameState = GameOver.State`). 다른 것은 **호출 시점**뿐이다 —
서버는 자기 타임리밋, 클라는 서버 통보. 이 대칭이 "종료 판정은 서버, 클라는 통보받는다"를 코드로 드러낸다.

### 5. 결과 보관 — `MatchResultDataStore` (Root)

```csharp
public class MatchResult          // 순수 C# 레코드. 나중에 순위·점수가 붙는 자리.
{
    public string matchId;
}
```

- `IMatchResultDataStore : IDataStore` + `MatchResultDataStore` — 기존 `UserDataStore`/`RoomDataStore`와
  같은 `<도메인>DataStore` 패턴, `RootLifetimeScope`에 Singleton 등록(`.As<IMatchResultDataStore>()
  .As<IDataStore>().AsSelf()`).
- **Root에 두는 이유**: Match 씬의 Room/Game 스코프는 로비 씬 로드 시 파괴된다. 결과는 그 파괴를
  건너 살아남아야 하는 유일한 조각이다.
- `matchId`를 담는 이유: 와이어가 비어 있어도 레코드가 "어느 판의 결과인지" 스스로 말한다. 클라가
  이미 `roomDataStore.room.matchId`를 갖고 있어 추가 통신이 필요 없다.

### 6. 결과 화면 — `MatchResultView`

- `UIView` 직접 상속. Slice C의 `ShellView`는 **상속하지 않는다** — 셸은 "네비 목적지(뒤로)"이고 결과는
  "판이 끝났음을 알리고 닫는" 다른 성격이라, 묶으면 back/확인의 의미가 엉킨다. `MatchingWaitingView`·
  `GameLoadingView`처럼 독립 View가 이 자리의 기존 패턴이다.
- 밴드: `UILayer.Window`(로비 홈 위에 push). 전용 UXML/USS + `UIViewCatalog` 엔트리 1개.
- 내용: 제목 "결과" + "매치 종료" + [확인] 버튼. 확인 콜백은 `MatchingWaitingView.SetCancelCallback`과
  같은 방식으로 코디네이터가 주입한다.

### 7. 로비 배선 — `FrontEndCoordinator` 확장

- `Start()`에서 스토어를 확인해 대기 중인 결과가 있으면 `Open<MatchResultView>()` + 확인 콜백 배선.
- 확인 → `Close` + `MatchResultDataStore.Clear()`(두 번 뜨지 않게). 셸(`_currentShell`)과 **별개 필드**로
  들고, `Dispose`에서 함께 정리한다.
- `LobbyLifetimeScope`: `MatchResultView` Transient 등록 + `RegisterViewFactory<MatchResultView>` 기여
  (Slice C의 셸 3종과 동일한 방식, `OnDestroy`에서 해제).

> **실행 순서 — `Start()`에서 열어도 안전한 이유(비자명).** Slice C에서 이 코디네이터의 `Start()`는
> 구독만 해서 순서가 무관했지만, 이제는 창을 연다. 창을 열려면 ① 뷰 팩토리가 이미 등록돼 있어야 하고
> ② 로비 홈이 먼저 열려 있어야 한다(그래야 결과 창이 그 위에 쌓인다) — 둘 다 `LobbyLifetimeScope`의
> **빌드 콜백**에서 일어난다. VContainer는 빌드 콜백을 빌드 중 동기 실행하지만 `IStartable.Start()`는
> 플레이어 루프(Startup 타이밍)로 큐잉하므로(`EntryPointDispatcher` → `PlayerLoopHelper.Dispatch`),
> **빌드 콜백이 항상 먼저**다. 이 순서가 깨지면 팩토리 미등록 resolve 실패 또는 z-순서 역전이 되므로,
> 코디네이터가 창을 여는 로직을 빌드 콜백보다 앞선 훅(`IInitializable` 등)으로 옮기지 말 것.

## 파일 요약

| 레포 | 파일 | 변경 |
|---|---|---|
| Shared | `Protos/MatchEndedToC.proto` + 생성물(`.cs`, `MessageIds`, `MessageInitializer`) | 신규 |
| Server | `Game/LOPRunner.cs` (`EndMatch()` 신설, `LateUpdate`가 호출) | 수정 |
| Server | `Room/LOPRoom.cs` (`case GameOver`에서 브로드캐스트) | 수정 |
| Client | `Game/LOPRunner.cs` (`EndMatch()` 신설) | 수정 |
| Client | `Game/MessageHandler/MatchEndedMessageHandler.cs` | 신규 |
| Client | `Game/GameLifetimeScope.cs` (핸들러 등록) | 수정 |
| Client | `RootLifetimeScope.cs` (브로커 + 스토어 등록) | 수정 |
| Client | `Network/NetworkMessageDispatcher.cs` (퍼블리셔 1개) | 수정 |
| Client | `Stores/MatchResult.cs`, `IMatchResultDataStore.cs`, `MatchResultDataStore.cs` | 신규 |
| Client | `UI/MatchResult/MatchResultView.cs`, `Assets/UI/MatchResult/*.uxml/.uss`, 카탈로그 엔트리 | 신규 |
| Client | `UI/LobbyHome/FrontEndCoordinator.cs`, `Lobby/LobbyLifetimeScope.cs` | 수정 |

## 테스트

클라에 단위 테스트 인프라가 없다(전 코드가 Assembly-CSharp). Slice B/C와 동일하게 **각 단계 컴파일
0 errors(UnityMCP) + 마지막 플레이테스트**로 검증한다.

플레이테스트 시나리오:
1. 매칭 → 인게임 진입 → 매치 종료 → 로비 복귀 + 결과 창 표시 → [확인] → 로비 홈
2. 결과 창을 닫은 뒤 다시 매치를 돌지 않고 로비를 오갈 때 결과 창이 **다시 뜨지 않는지**(Clear 확인)
3. Slice C 네비(상점/설정/프로필)와 Slice B PLAY 흐름 보존

> 5분 타임리밋을 기다릴 수 없으므로, 검증 중에는 서버 `LOPRunner.LateUpdate`의 임계값을 짧게(예: 20초)
> 낮춰 확인하고 **원복**한다. 원복 누락은 이 슬라이스의 가장 현실적인 사고다 — 마지막 커밋 전 diff로 확인.

## 범위 밖

- 점수·순위·보상 표시 — 게임 모드 규칙이 정해진 뒤 별도 spec(골격 spec의 "결과 화면 상세 UI").
- Slice A(앱 FSM 씬 전환 일원화) — 이 슬라이스가 만드는 경로는 나중에 `MatchEnded` 앱 이벤트로 이관된다.
- 러너 상태 family 리네임(언리얼식 `MatchState`) — 필요해지면 별도 소규모 슬라이스.
- 종료 사유 구분, 중도 이탈·서버 다운 등 비정상 종료 처리.
