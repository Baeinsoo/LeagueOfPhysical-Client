# 게임 모드 축 — 여러 하위 게임을 덩어리로 갈아끼우는 구조

LOP를 "게임 하나"에서 **"여러 하위 게임 중 하나를 골라 플레이하는 플랫폼"** 으로 바꾼다.
게임 모드가 씬 덩어리 단위로 로드/언로드되고, 룸(연결)은 그 아래에서 유지된다.

첫 입주자는 **Flappy Race**다. 기존 **FlapWang**은 두 번째 게임 타입으로 남아 축이 실제로
갈라지는지 증명하는 대조군 역할을 하고, 그 뒤 별도 슬라이스에서 걷어낸다.

> **범위**: 이 문서는 슬라이스 **A~D**(로비 선택 → 게임 조립 → 플레이 → 종료·결과 전달)를 다룬다.
> 매치 결과를 백엔드 DB에 영속화하는 **E**는 별도 spec — 무엇을 남길지(전적/레이팅/종목별 통계)가
> 아직 제품 결정 전이라 이 문서에 추측을 섞지 않는다.

---

## 1. 배경 — 왜 지금인가

LOP의 **백엔드와 마스터데이터는 이미 "여러 게임"을 전제로 설계돼 있다.** 그런데 유니티 런타임만
"게임은 하나"라고 믿고 있다. 이 문서는 그 간극을 메우는 일이다. 새 구조를 발명하는 게 아니다.

Flappy Race 프로토타입(`Assets/Scripts/FlappyRaceSlice/`)은 손맛 확인용 독립 스크립트로 만들어졌고,
`FlappyPlayer` 헤더가 그 사실을 명시한다 — *"정식 아키텍처(World Core/VContainer) 미통합"*.
이걸 정식 게임 타입으로 올리려면 축이 먼저 있어야 한다.

---

## 2. 목표 / 비목표

### 목표

- 로비에서 게임을 고르면, **그 게임의 씬 덩어리**가 로드되고 그 안에서 매치가 진행된다.
- 게임마다 **다른 월드 시뮬·룰·스폰·UI**를 가진다.
- 게임마다 **끝나는 조건이 다르고**, 종료 시 **순위**가 클라에 전달돼 결과 화면이 뜬다.
- Flappy Race가 LOP 넷코드(서버 권위 + 클라 예측 + 롤백) 위에서 실제로 돈다.

### 비목표 (이번에 하지 않는다)

| 항목 | 이유 |
|---|---|
| 매치 결과 DB 영속화 | 별도 spec(E). 제품 결정 전 |
| 한 매치 안 **여러 라운드 순차 진행** | 구조는 열어두되(`MatchRound[]`) 이번엔 `rounds[0]`만 소비 |
| FlapWang 제거 | 지금 유일한 넷코드 검증 베드다. Flappy가 그 역할을 넘겨받은 뒤 별도 슬라이스 |
| Flappy 게임성 요소 | 추격자·대시·부스트존·고도 지형·유령정지 — 축 증명에 불필요 (§9) |
| 매치 상태 복제(`GameState` 상당) | 지금 필요한 건 종료 시점 결과 하나뿐 |

---

## 3. 현재 상태

### 이미 있는 것

| 계층 | 상태 |
|---|---|
| `MatchmakingTicket.gameModeIds[]` (Prisma) | 유저가 원하는 게임 목록을 담아 티켓 발급 |
| `matchFunction.ts` (matchmaking-server) | **게임모드별로 매칭한다** — 모드마다 후보를 거르고 그 모드의 `maxPlayers`로 그룹 |
| `Match` + `MatchRound[]` (Prisma) | `index / gameModeId / mapId`. 스키마 주석: *"여러 게임을 연속으로 하되 최종 결과는 하나인 형태를 위해 목록으로 둔다"* |
| `TbQueue` | `AllowedGameModeIds`, `GameModeSelector`, `MapSelector` |
| `TbMap` | `GameModeId`, `ScenePath` — 맵이 게임에 속한다 |
| `TbGameMode` | `Id`, `Code`, `Name`, `Description`, `MinPlayers`, `MaxPlayers` |
| `IGameFactory` / `LOPGameFactory` (클·서) | **게임 씬을 Room 스코프의 자식으로 additive 로드하고 언로드한다** |
| 서버 `LOPRunner.ResolveScenePath()` | `rounds[0].mapId` → `TbMap.ScenePath` |
| `GameFramework.World.IWorld` | 월드 구현이 인터페이스로 주입된다 |
| `GameFramework.World.Simulated` 마커 | 시뮬 대상을 사이드별로 정하는 정책 훅 |

**덩어리 메커니즘은 이미 뚫려 있다.** `LOPGameFactory`가 하는 일이 정확히 그것이고, 클라·서버가
동일한 코드다. 하드코딩된 건 한 줄뿐이다.

```csharp
private const string GameSceneName = "LOPGame";   // ← 축이 막혀 있는 유일한 지점
```

### 빠진 것

| 위치 | 지금 | 문제 |
|---|---|---|
| `LOPGameFactory` (클·서) | 씬 이름 상수 | 게임을 고를 수 없다 |
| `TbGameMode` | 씬 경로 컬럼 없음 | 고를 대상을 데이터로 표현할 수 없다 |
| `gameModeId` | **아무도 읽지 않는다** | 값은 흐르는데 아무것도 분기하지 않는다 |
| 클라 `LOPRunner.MapId` | 하드코딩 상수 | 서버는 데이터 주도인데 클라만 상수 (비대칭) |
| 클라 `MatchmakingViewModel.Play()` | `gameModeId = 1` 하드코딩 | 유저가 고를 수 없다 (코드 주석에 이미 "로비 선택 UI 슬라이스 몫"으로 표시됨) |
| 서버 `GameRuleSystem` | 단일 하드코딩 | 게임별 룰이 없다. 자기 주석에 *"⚠️ 임시 위치"* 로 이미 표시됨 |
| 서버 종료 조건 | `LateUpdate`에서 `elapsedTime > 60*5` | 게임마다 다른 종료 조건이 불가능 |
| `MatchEndedToC` | **빈 메시지** | 결과·순위를 전달할 수단이 없다 |
| `GameLifetimeScope` (클·서) | 단일 등록 세트 | 게임별 월드·룰·UI를 나눌 자리가 없다 |

---

## 4. 아키텍처 — 룸 프레임 / 게임 덩어리

```
Room 씬 ─────── 기본 프레임. 연결·세션이 여기 산다. 게임이 바뀌어도 끊기지 않는다.
  └ 게임 씬 ──── 덩어리. rounds[n].gameModeId 가 정한다. 통째로 로드/언로드.
      └ 맵 씬 ── rounds[n].mapId 가 정한다 (지금도 additive).
```

**왜 게임 씬과 맵 씬을 계속 분리하나.** `TbMap.GameModeId`가 이미 "한 게임에 여러 맵"을 상정하고
있다. 게임 덩어리(룰·월드·UI)는 한 벌이고 코스만 여러 개인 게 자연스러운 모양이라 분리를 유지한다.

**라운드 전환이 공짜로 따라온다.** 덩어리만 갈아끼우고 Room은 그대로 두면 된다. 이번 슬라이스에서
구현하지는 않지만(§2 비목표), 구조가 그 방향을 막지 않는다.

---

## 5. 무엇이 프레임에 남고 무엇이 덩어리로 가나

판단 기준 한 줄: **연결이 끊기면 안 되는 것은 프레임, 게임이 바뀌면 달라지는 것은 덩어리.**

| | 프레임(Room 스코프) | 덩어리(게임 씬 스코프) |
|---|---|---|
| 세션·네트워크 | `ISessionManager`, `LOPNetworkManager`, `IPlayerContext` | |
| 넷코드 시계 | `INetworkTime` (lead·dilation) — 연결 수명에 묶임 | |
| 매치 데이터 | `match.rounds`, `playerList` (`IRoomDataStore`) | |
| 월드 시뮬 | | `IWorld` 구현 + 그 게임의 시스템들 |
| 룰 | | `GameRuleSystem` 게임별 구현 (스폰·종료·순위) |
| 엔티티 생성 | | `CharacterCreator` / `FlappyBirdCreator` |
| 게임 UI·카메라 | | HUD, 게임패드, 카메라 컨트롤러 |
| 틱 파이프라인 | | `Runner` + 파이프라인 스텝, 스냅샷 히스토리, 시퀀스 버퍼 |

**틱 파이프라인이 덩어리에 있는 이유.** 스냅샷 히스토리·시퀀스 버퍼는 *그 게임의 상태*라 덩어리와
함께 깨끗이 버려지는 게 맞다. 틱 번호 자체는 Room의 `INetworkTime`(서버 시계)에서 유도되므로
Runner가 새로 떠도 이어진다 — 서버는 이미 `runner.Run((long)(now / TICK_INTERVAL), ...)` 로
자기 시계에서 틱을 유도한다.

**체력·마나·어빌리티는 공통에 두지 않는다.** Flappy Race에는 체력이 없다. 공통은 *정말 모든 게임이
쓰는 것* 만 담는다.

### DI 분해 — VContainer `IInstaller`

상속이 아니라 설치자 조합으로 나눈다 (VContainer 관용 표현).

```
NetcodeInstaller     — 리컨사일러, 스냅샷 히스토리, 시퀀스 버퍼, 파이프라인 스텝   [모든 게임 공통]
WorldCoreInstaller   — EntityRegistry, WorldEventBuffer, IEventSink, 물리 포트    [모든 게임 공통]

FlapWangLifetimeScope   : builder.Install(공통 2종) + LOPWorld, 이동·어빌리티·체력, 캐릭터 HUD
FlappyRaceLifetimeScope : builder.Install(공통 2종) + FlappyWorld, 새 스폰, 레이스 HUD
```

클라·서버가 각자 자기 `NetcodeInstaller`를 갖는다(등록 대상이 사이드별로 다르다 — 클라는 예측·보간,
서버는 브로드캐스트). 게임 스코프의 *모양*은 양쪽이 동일하다.

---

## 6. 배선 흐름

클라·서버가 **같은 순서**로 흐른다.

```
룸 진입
 └ WebAPI.GetMatch → match.rounds[]
    │    클·서 모두 이미 IRoomDataStore.match에 저장돼 있다 (변경 불필요 — 아래 정정 참고)
    └ IGameFactory.CreateAsync()
       └ rounds[0].gameModeId → TbGameMode.ScenePath → 게임 씬 additive 로드     ← 이번에 추가
          └ 게임 씬 스코프 Configure — 그 게임의 월드·룰·UI 등록
             └ Runner.InitializeAsync()
                └ rounds[0].mapId → TbMap.ScenePath → 맵 additive 로드
                     서버: 이미 동작 / 클라: 상수 → 서버와 동일 경로로
```

### 변경 지점

| 대상 | 변경 |
|---|---|
| `infrastructure/table/Datas/#GameMode.xlsx` | **`scene_path` 컬럼 추가** (→ `TbGameMode.ScenePath`). `TbMap.ScenePath`와 같은 이름을 쓴다 — 다른 테이블이라 충돌이 없고, "이 행이 가리키는 씬"이라는 뜻이 양쪽에서 동일하다 |
| `LOPGameFactory` (클·서) | 상수 → `rounds[0].gameModeId` → `TbGameMode.ScenePath` |
| ~~클라 `LOPRoom.InitializeAsync`~~ | **정정 — 변경 불필요.** 착수 시 확인해보니 클라도 이미 저장하고 있다: `WebAPI.SendAsync`가 모든 응답을 메시지 파이프에 발행하고 `RoomDataStore.HandleGetMatch`가 받아 `match`에 넣는다. `RoomDataStore`는 RootLifetimeScope 싱글턴이라 앱 시작부터 살아 있고, `GetMatch` 호출은 `gameFactory.CreateAsync()` **직전**이다 |
| 클라 `LOPRunner` | `MapId` 상수 삭제 → 서버와 동일한 `ResolveScenePath()` |
| `GameLifetimeScope` (클·서) | 공통 → Installer 2종, 게임별 → 각 게임 씬 스코프 |
| 클라 `MatchmakingViewModel.Play()` | 하드코딩 `1` → 로비에서 고른 값 |

> **두 `ScenePath`는 이름이 같지만 해석하는 쪽이 다르다.**
>
> | 컬럼 | 로더 | 새로 추가할 때 필요한 것 |
> |---|---|---|
> | `TbGameMode.ScenePath` | `SceneManager.LoadSceneAsync` | **EditorBuildSettings에 씬 등록** |
> | `TbMap.ScenePath` | `AddressablesMapLoader` | **Addressable로 마킹** |
>
> 지금은 둘 다 `Assets/...` 꼴이라 같아 보이지만(Addressables 기본 주소가 에셋 경로다) 등록 요건이
> 다르다. 데이터만 채우고 등록을 빠뜨리면 유니티의 씬 로드 실패 메시지가 나오고, 그건 데이터를
> 가리키지 않아 원인을 찾기 어렵다. 새 게임·맵을 추가할 때 이 표를 먼저 볼 것.

**`gameModeId`가 지금 아무도 읽지 않는 값이라, 읽기 시작하는 것만으로 축이 생긴다.**

### 오류 처리

| 상황 | 처리 |
|---|---|
| `rounds`가 비어 있음 | 예외 — 매치가 성립할 수 없다 (서버는 이미 이렇게 함) |
| `TbGameMode`에 없는 `gameModeId` | 예외 + id를 메시지에 담는다 (서버 `ResolveScenePath`의 기존 패턴) |
| `ScenePath`가 빈 문자열 | 예외 — 데이터 누락은 조용히 넘어가면 안 된다 |
| 씬 로드 실패 | 기존 룸 초기화 실패 경로를 탄다 (`LOPRoom`이 `MatchEnded`로 로비 복귀) |

---

## 7. 종료와 결과 — 사이클 닫기

### 지금

```
서버 LOPRunner.LateUpdate: elapsedTime > 60*5 → EndMatch() → RunnerState.GameOver
  → LOPRoom.OnGameStateChanged → 전원에게 빈 MatchEndedToC → 룸 Closed
```

종료 조건이 호스트에 하드코딩돼 있고 결과가 없다.

### 바꾸는 것

**① 종료 판정을 룰로 옮긴다.** `GameRuleSystem`이 게임별 구현이 되므로 종료 조건도 거기 산다.
5분 타이머는 *FlapWang 룰의 조건*이 되고, Flappy Race는 *전원 완주 또는 시간 초과*가 된다.
호스트(`LOPRunner`)는 룰이 "끝났다"고 하면 `EndMatch()`를 부를 뿐이다.

**② 결과를 메시지에 싣는다.**

```proto
message MatchResultEntry {
  string user_id = 1;
  int32  rank    = 2;   // 1부터. 동률은 같은 값을 갖는다
}

message MatchEndedToC {
  repeated MatchResultEntry results = 1;
}
```

Flappy Race의 순위 규칙: **완주자를 통과 시각 오름차순으로 먼저 놓고, 미완주자를 진행 거리(x)
내림차순으로 그 뒤에 잇는다.**

**동률은 같은 순위를 준다.** 같은 틱에 결승선을 통과했으면 둘 다 공동 1위다 — 임의 기준(`entityId`
등)으로 우열을 가르지 않는다. 플레이어가 납득할 수 없는 차등이기 때문이다. 다음 순위는 인원수만큼
건너뛴다(스포츠 표준 경쟁 랭킹): 공동 1위가 둘이면 그다음은 3위.

이 규칙은 결정론적이다 — 같은 입력이면 같은 결과가 나오고, 순서 의존이 없다.

순위를 *어떻게 매기는지* 는 이렇게 게임별(룰)이고, 와이어 모양은 공통이다. 언리얼은 여기서
`AGameStateBase`(양쪽 복제되는 매치 상태)까지 두지만, 지금 필요한 건 종료 시점의 결과 하나뿐이라
**상태 복제는 만들지 않는다.**

**③ 클라가 결과 화면을 띄운다.** `MatchEndedMessageHandler`가 결과를 받아 결과 뷰를 열고,
닫으면 로비로 돌아간다. UI 테마(`Assets/UI/Theme/Theme.uss`)의 카드 컴포넌트를 쓴다.

**순위는 서버 권위다.** 클라는 통보만 받는다 — 기존 `EndMatch` 주석의 원칙(*"종료 판정은 서버
권위이고, 클라는 통보를 받는다"*)을 그대로 따른다.

---

## 8. 로비 게임 선택

로비에 게임 선택 화면을 두고, 고른 값이 `IMatchmakingDataStore.gameModeId`로 들어간다. 그 뒤
경로(`RequestMatchmaking` → `MatchmakingRequest` → 백엔드)는 **이미 동작한다.**

최소 형태는 게임 2개를 나란히 놓은 카드/버튼이다. 각 카드의 표시 텍스트는 `TbGameMode.Name`과
`Description`에서 온다 — 이미 있는 컬럼이라 새로 만들 것이 없다.

**확인 필요 항목**: 클라 `MatchmakingRequest`는 `gameModeId`(단수)를 보내는데 백엔드 티켓은
`gameModeIds[]`(복수)다. 백엔드가 단수를 배열로 감싸는지, DTO를 배열로 바꿔야 하는지 슬라이스 C에서
확인한다. "아무거나" 옵션(빈 배열 = 전체 허용)은 이번에 만들지 않는다.

---

## 9. Flappy Race 최소 껍데기

이번 슬라이스의 Flappy는 **이동만 있고 게임성은 없다.** 목적이 "축이 갈라지는가 + 넷코드 위에서
도는가"의 증명이기 때문이다.

| 넣는 것 | 빼는 것 |
|---|---|
| 고정 전진 + 플랩 + 중력 | 대시 충전, 부스트존 |
| 새끼리 몸싸움(밀어내기 + 세로속도 교환) | 고도 지형, 이동 파이프 |
| 결승선 도달 순서 = 순위 | 유령정지 페널티 |
| 시간 초과 = 미완주(뒤 순위) | **추격자** |

**추격자를 뺀 이유.** 끝나는 조건은 결승선 + 타임아웃으로 충분하다. 추격자는 게임을 *재미있게*
만드는 요소지 *성립시키는* 요소가 아니다. D 이후 별도 슬라이스에서 붙인다.

**클라 예측은 넣는다.** 빼면 플랩이 RTT만큼 늦게 반응해 게임이 성립하지 않고, 무엇보다
*"LOP 넷코드 위에서 진짜 도는가"* 가 증명되지 않는다. 축만 갈라지고 넷코드가 안 돌면 반쪽이다.

다행히 비용이 거의 없다 — 최소 이동은 `World.Transform` + `World.Velocity`만 쓰므로 기존 스냅샷·
롤백 경로에 그대로 얹힌다. `Reconciler`는 월드 구현을 모르고 `world.Tick` 재생만 하므로,
`FlappyWorld`가 `Simulated` 마커 규약(서버=전원 / 클라=내 새만)을 지키면 된다.

### 구성

| 컴포넌트 | 책임 |
|---|---|
| `FlappyWorld : WorldBase` | `Mutation` — 플랩 입력 반영 → 전진·중력 → 새끼리 분리 |
| `FlappyMoveSystem` | 순수 이동 커널. 클·서 공유 구체 클래스 (인터페이스 seam 금지) |
| `FlappyBirdCreator` | 새 엔티티 생성 — `Transform`/`Velocity`/`Ownership`/`Simulated`/`InputBuffer` |
| `FlappyRaceRule` | 스폰, 결승선 통과 감지, 순위 확정, 종료 판정 |

**입력 매핑**: 플랩 = `InputCommand.Jump`. 이미 있는 필드라 와이어 변경이 없다.

**결승선은 맵이 제공한다.** 맵 씬에 결승선 마커 오브젝트를 두고, 게임 스코프가 그 x좌표를 읽어
룰에 넘긴다. 프로토타입은 코스 오브젝트 중 최대 x를 훑어 `finishX`를 구했는데(`FlappyRaceManager`),
그 방식은 맵 구성에 따라 값이 흔들려 클·서가 다르게 볼 수 있다. **명시적 마커**로 바꿔 양쪽이
같은 값을 읽게 한다.

---

## 10. 씬·이름 정리

`LOPGame`이라는 이름 자체가 "게임은 하나" 전제를 담고 있어 슬라이스 A에서 정리한다.

| 역할 | 지금 | 이후 |
|---|---|---|
| 게임 덩어리 | `Assets/Scenes/LOPGame.unity` | `Assets/Scenes/FlapWang.unity`<br>`Assets/Scenes/FlappyRace.unity` |
| 맵 | `Assets/Art/Scenes/FlapWangMap.unity` | 그대로 + `Assets/Art/Scenes/FlappyRaceMap.unity` |
| 프로토타입 개발 씬 | `Assets/Art/Scenes/FlappyRace.unity` | `FlappyRaceMap.unity`로 승격(rename). 프로토 전용 스크립트(오토파일럿·심저지·플레이 레코더)는 비활성 |

> 게임 덩어리 씬(`Scenes/FlappyRace.unity`)과 프로토타입 씬(`Art/Scenes/FlappyRace.unity`)이
> 이름 충돌하므로 후자를 `FlappyRaceMap`으로 rename한다. 코스 에셋을 그대로 재사용하기 위함이다.

Unity `.meta` 파일은 반드시 함께 커밋한다 (rename은 GUID 유지가 중요하므로 에디터에서 수행).

---

## 11. 슬라이스

| | 무엇 | 어디 | 끝났다는 기준 |
|---|---|---|---|
| **A** | 게임 씬을 데이터로 고른다 | 클·서 + 마스터데이터 | **지금과 똑같이 동작한다.** 단 씬 이름이 상수가 아니라 `TbGameMode`에서 온다 |
| **B** | Flappy Race 최소 껍데기 | 클·서 + 마스터데이터 | `gameModeId`를 2로 바꾸면 완전히 다른 게임이 뜨고, 여러 명이 예측 위에서 난다 |
| **C** | 로비 게임 선택 | 클라 UI | 로비에서 고른 게임으로 실제 입장한다 |
| **D** | 게임별 종료 + 순위 전달 | 클·서 + proto | 게임마다 다른 조건으로 끝나고 순위가 화면에 뜬다 |

순서: **A → B → (C, D는 서로 독립) → E(별도 spec)**

**A가 가장 중요하다.** 화면은 그대로고 씬 이름만 데이터에서 온다 — 순수 리팩터라 **회귀가 없다는
걸 확실히 검증할 수 있는 유일한 시점**이다. B부터는 새 코드가 섞여 원인을 가리기 어려워진다.

**C는 백엔드 작업이 거의 없다.** `matchFunction.ts`가 이미 게임모드별로 매칭하고
`MatchmakingTicket.gameModeIds[]`도 이미 있다.

---

## 12. 테스트

| 대상 | 방법 |
|---|---|
| `gameModeId → 씬 경로` 해석 | EditMode 단위 테스트 — 정상 조회 / 없는 id 예외 / 빈 `ScenePath` 예외 |
| 순위 산정 | EditMode — 완주 순서 / **동시 완주 시 공동 순위 + 다음 순위 건너뛰기** / 미완주 섞임 / 전원 미완주 |
| 종료 판정 | EditMode — 전원 완주 / 타임아웃 / 인원 이탈 |
| 예측·롤백 회귀 | **슬라이스 A 직후 FlapWang으로 확인.** 순수 리팩터라 여기서 안 깨지면 축 배선은 무죄 |
| 두 게임 전환 | 2 에디터(클·서) + MPPM 가상 플레이어 수동 |

순수 로직(순위·종료)은 기존 `FlappyChaserCurveTests` / `FlappyChaserOutcomeTests`처럼
엔진 비의존 클래스로 뽑아 EditMode에서 검증한다.

---

## 13. 산업 표준 매핑

이 설계는 새 발명이 아니라 표준 조립이다.

| 표준 | 대응 |
|---|---|
| **언리얼 `AGameModeBase`** — 서버 전용, 스폰·룰·승패 판정 소유 | `GameRuleSystem` 게임별 구현. 자리는 이미 같았고 다형화만 한다 |
| **언리얼 맵→GameMode 매핑** (World Settings의 GameMode Override) | `TbMap.GameModeId` — 이미 있음 |
| **언리얼 `DefaultPawnClass`** | 게임 스코프의 엔티티 크리에이터 (`CharacterCreator` / `FlappyBirdCreator`) |
| **언리얼 `AGameStateBase`** (양쪽 복제 매치 상태) | 이번엔 만들지 않음 — 종료 결과만 필요 |
| **Photon Quantum `SystemsConfig`** (어떤 시스템을 돌릴지 목록으로 정의) | 게임 씬 스코프의 등록 세트 |
| **Fall Guys식 라운드 로테이션** | `MatchRound[]` — 데이터는 이미 있고, 소비는 이번에 `rounds[0]`만 |
| **VContainer `IInstaller`** | 공통 등록 추출 (`NetcodeInstaller` / `WorldCoreInstaller`) |

`TbGameMode`라는 이름이 이미 언리얼 GameMode와 정렬돼 있어, 런타임 쪽도 그 어휘를 따른다.

---

## 14. Open Decisions

- [ ] **`MatchmakingRequest`의 `gameModeId` 단수 ↔ 백엔드 `gameModeIds[]` 복수** — 백엔드가
  감싸는지 DTO를 배열로 바꿀지. 슬라이스 C에서 확인 후 결정.
- [ ] **FlapWang 제거 시점** — Flappy Race가 넷코드 검증 베드 역할을 넘겨받은 뒤. D 이후 별도 슬라이스.
- [ ] **라운드 로테이션** — `rounds[1..]`을 실제로 소비하는 시점. 미니게임이 3개 이상 생긴 뒤.
- [ ] **레거시 string 키 테이블 정리** — `TbGameMode`/`TbMap`은 이미 int `id` 기본키라 규약에 맞다.
  `TbCharacter`/`TbAction` 등 string `code` 키는 이 슬라이스 범위 밖 (`lop-repo-topology.md`의
  "마스터데이터 키 규약" 참고).
- [ ] **`TbMap.scene_path` 그룹 정규화** — `##group`이 빈 칸이라 매치메이킹 생성물에도 들어가 있다.
  무해하지만 `lop-repo-topology.md`의 DTO 격리 원칙과 어긋난다. `c,s`로 맞추는 게 맞으나 셀 하나에
  4개 저장소 커밋이 걸려 별도 슬라이스로 둔다.

### 슬라이스 A 리뷰에서 미뤄둔 후속 (병합 블로커 아님)

- [ ] **`LOPRoom.DeinitializeAsync`의 null `runner` 가드** — `CreateAsync`가 `runner` 대입 전에 던지면
  이후 `OnDestroy` → `DeinitializeAsync`가 첫 줄에서 NRE로 죽고, 그 아래 `gameFactory.DestroyAsync()`와
  `roomDataStore.Clear()`가 건너뛰어져 **로비로 돌아간 뒤 스토어에 지난 매치가 남는다.** 선행 결함이지만
  슬라이스 A가 그 경로를 결정론적으로 도달 가능하게 만들었다(잘못된 `gameModeId`). `if (runner != null)` 한 줄.
- [ ] **씬 언로드를 경로 대신 `Scene` 핸들로** — `LOPGameFactory.DestroyAsync`가 `GetSceneByPath`로 다시
  찾는데, 이건 정확 일치라 마스터데이터의 대소문자가 하나만 달라도 **로드는 되고 언로드는 no-op**이 된다.
  매치를 거듭할수록 씬이 쌓인다. 로드 직후 `Scene` 핸들을 붙잡아 두면 없어지는 문제.
- [ ] **`FlapWang.unity`의 루트 GameObject 이름이 아직 `LOPGame`** — 코드는 이름으로 찾지 않아 기능 영향은
  없다. 슬라이스 B에서 이 씬을 어차피 열게 되므로 그때 함께.
- [ ] **`MatchSceneResolver` 이름 재고** — 지금은 아무것도 resolve하지 않고(조회는 호출자 몫) 검증 가드만
  갖고 있다. `MatchSceneRules` 쪽이 정직하나, 슬라이스 D의 순위·종료 순수 로직이 같은 자리에 들어올
  예정이라 그때 전체 이름을 다시 보는 게 낫다 — 지금 rename은 churn.
- [ ] **클·서 `LOPGameFactory.cs` 두 벌의 동일성을 지켜줄 장치 없음** — 지금 md5까지 같지만 이를 강제하는
  테스트나 도구가 없다. `RoomLifetimeScope`/`GameLifetimeScope` 같은 사이드 전용 타입을 참조해 공유로
  옮길 수도 없다. **슬라이스 B에서 게임별 스코프를 가를 때가 두 파일이 갈라질 첫 지점**이다.

---

## 15. 관련 문서

- `docs/world-core-connection-architecture.md` — World Core ↔ 프레젠테이션/넷코드 연결, `IWorld`·`Simulated`
- `docs/lop-repo-topology.md` — 레포 경계, 마스터데이터 파이프라인, 키 규약
- `docs/entity-system-design.md` — Entity/Component 모델
- `docs/netcode-redesign.md` — 예측·보정 구조
