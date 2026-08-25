# 판치기 슬라이스 4 — 턴 루프

> **한 문장**: 판치기를 *칠 수는 있지만 이기고 지지는 않는* 상태에서 **끝나는 게임**으로 만든다 —
> 차례가 돌고, 동전이 멎으면 면을 세고, 다 뒤집히면 그 사람이 이긴다.

## 1. 왜 지금 이것인가

슬라이스 3이 끝난 뒤 판치기는 **아무나 아무 때나 치는 상태**다. 코드로 확인한 현재:

| | 실제 |
|---|---|
| 턴 | 없음 — 누구든 언제든 친다 |
| 정지 판정 | 없음 — `rest_speed_epsilon`/`rest_ticks` 노브만 있고 읽는 곳이 없다 |
| 면 세기 | 없음 |
| 매치 종료 | `LOPRunner.LateUpdate`의 **5분 하드코딩 타이머** |
| 등수 | 전원 공동 1등(자리만 채움) |
| 클라 UI | 전무 |
| 스폰 위치 | `new Vector3(0f, 1.5f, i * 0.5f)` **코드에 박힘**. `TbPanchigiSetup.formation`은 아무도 안 읽는다 |

규칙은 `2026-08-24-panchigi-game-mode-design.md`에 이미 잠겨 있다. 이 슬라이스는 그중
**턴 루프**만 만든다.

### 이번 슬라이스에 넣는 것

- 정지 판정 → 면 세기 → 턴 전환 → 종료 판정
- 조준 제한시간(넘기면 패스)
- 장외 동전 **제자리 복귀**
- 내 차례 UI(한 줄) + 입력 게이팅
- 스폰 위치를 **씬으로** 옮기기

### 미루는 것과 그 조건

| 미룬 것 | 언제 |
|---|---|
| **낙(落) 누적·탈락** | 다음 슬라이스. 복귀 자체는 이번에 만드므로 규칙의 절반이 이미 서 있다 |
| **전용 맵** | 지금은 FlapWang 맵 바닥이 같이 깔려 판 밖에서도 동전이 미끄러진다 |
| **일렬 아닌 대형** | 씬이 이름별 자리 목록을 들 수 있게 해 둔다. 이번에 채우는 건 `TbPanchigiSetup`이 이미 요구하는 두 벌(`FourInLine`·`SixInLine`)뿐이고, 원형·군집 같은 모양은 나중에 |
| **재접속 시 상태 복원** | 접속이 끊긴 사람의 차례는 조준 시간이 지나 자동 패스되므로 판이 멈추지는 않는다 |

> **장외 복귀를 미루지 않는 이유**: 종료 조건이 *"모든 동전이 반대 면"* 인데 판을 나간 동전은 다시
> 칠 수 없다 → 조건이 도달 불가능해지고 매번 턴 상한 무승부로 끝난다. **이 슬라이스의 성공 자체를
> 확인할 수 없게 된다.**

## 2. 상태 기계

서버만 안다.

```
        타격 수신
   ┌──────────────────────┐
   ▼                      │
Settling ──모두 정지──> Aiming ──제한시간 초과(패스)──┐
   │                      ▲                          │
   │                      └──────────────────────────┘
   │  (정지 직후 판정)
   └──> 종료 조건 충족 ──> Over
```

**Settling** — 물리가 도는 중. 판 시작 직후에도 동전이 떨어지므로 여기서 시작한다.
모두 멎으면 그 자리에서 순서대로: ① 장외 복귀 → ② 면 세기 → ③ 종료 판정.

**Aiming** — 지금 차례인 사람의 조준 시간(`aim_timeout_sec`). 진입할 때 마감 틱을 정해 클라에 알린다.
- 타격 수신 → Settling
- 시간 초과 → **패스** → 다음 사람 Aiming. 물리를 안 건드리므로 Settling을 거치지 않는다.

**Over** — `LOPRunner.EndMatch()`가 불리고 등수가 확정된다.

### 종료 조건

| 조건 | 결과 |
|---|---|
| 모든 동전이 **시작 면의 반대** | 방금 친 사람이 승자, 나머지 공동 꼴등 |
| 턴 수 > `match_turn_limit` | **전원 무승부**(동일 등수) |

턴 수는 **친 것과 패스한 것을 모두** 센다 — 안 그러면 전원이 계속 패스해 무한히 늘어진다.

### 잠글 것

- **시작 면은 항상 +up이다.** 스폰이 `rotation=zero`라 규칙이 아니라 사실이고, 따로 저장하지 않는다.
  대형이 동전을 뒤집어 놓기 시작하면 그때 명시 필드가 필요해진다.
- **첫 Settling에는 승자가 없다.** 아무도 안 쳤고 동전은 전부 시작 면이라 종료 조건이 거짓이므로
  자연히 안전하다 — 별도 가드를 두지 않는다.

## 3. 판정 셋 — `PanchigiCoin`

동전 하나의 자세·속도·위치를 보고 참/거짓을 낸다. **계산이 아니라 판단**이다.

```csharp
PanchigiCoin.IsFlipped(rotation)                                 // dot(회전×up, up) < 0
PanchigiCoin.IsOutOfBoard(position, boardBounds)                 // x·z 범위 밖 또는 판 아래
PanchigiCoin.IsAtRest(linear, angular, speedEps, angularEps)     // 한 틱치 판단
```

- **모로 선 동전은 뒤집힌 것으로 치지 않는다**(내적 ≈ 0). 실제로 관측되는 상태라 미리 못박는다
  (슬라이스 3에서 겹침 판정을 고칠 때 모로 선 동전을 만들어 쟀다).
- **"연속 N틱"은 여기 없다.** 그건 판정이 아니라 *누적*이라 상태를 갖는 턴 시스템이 센다.

### 정지 판정이 연속이어야 하는 이유

튀어 오른 동전은 **정점에서 속도가 순간 0을 지난다.** 한 틱만 보면 공중에 뜬 동전을 "멎었다"고
오판한다. 각속도를 같이 보는 것도 같은 이유 — 미끄러지지 않고 제자리에서 도는 동전은 선속도가 0이다.
`rest_ticks`(현재 10)만큼 연속으로 문턱 아래여야 한다.

## 4. 스폰 위치는 무대에 둔다 — `PanchigiBoard`

서버 판치기 씬의 `Board`에 붙는 MonoBehaviour. **무대에 관한 값을 한 곳에 모은다**:

- **판 경계** — 자기 콜라이더. 지금 `PanchigiStrikeMessageHandler`의 `GameObject.Find("Board")`
  문자열 조회가 이걸로 대체된다.
- **동전 자리** — 대형 이름별로 *순서 있는* `Transform` 목록. 에디터에서 눈으로 배치한다.

`TbPanchigiSetup.formation`이 어느 대형을 쓸지 고른다. 그러면 **`coin_count`는 그 대형의 점
개수에서 나오므로 중복**이다 — 컬럼을 지운다. 지금은 두 값이 같은 것을 두 번 말하고 있고
(4개짜리 `FourInLine`), 어긋나면 어느 쪽이 맞는지 알 수 없다.

**스폰과 복귀가 같은 값을 본다.** 스폰할 때 i번째 동전을 i번째 자리에 놓고 `entityId → 자리 인덱스`만
기록한다. 장외 복귀는 그 인덱스의 자리를 다시 읽는다 — **위치를 코드가 기억하지 않는다.**

### 같이 메울 구멍 — 4인 행이 없다

규칙은 2~4명인데 `TbPanchigiSetup`에는 **2인·3인 행만 있다.** 4인 매치가 잡히면
`GetOrDefault(4)`가 null이라 `PanchigiRuleSystem`이 예외를 던지며 방이 죽는다(슬라이스 1~2에
심어 둔 명시적 throw라 조용히 깨지진 않는다). 어차피 이 테이블을 손대므로 **4인 행을 채운다.**

> 클라 씬에도 `Board`가 있지만(조준 레이캐스트용) **컴포넌트는 서버 씬에만** 붙인다. 스폰은 서버만
> 한다. 판의 위치·크기를 두 씬이 손으로 맞춰야 하는 상태는 그대로다 — 전용 맵 슬라이스의 몫.

### 복귀는 rb에 직접 쓴다

동전은 dynamic이라 **PhysX가 진실원본**이고 `PhysicsSimulationSystem`이 매 틱 rb → World로 읽어온다.
`World.Transform`에 써 봐야 다음 틱에 덮어써진다.

```
body.SetPosition(자리) · SetRotation(시작 자세) · SetVelocity(0) · SetAngularVelocity(0)
```

**포트에 두 메서드를 더한다** — `PhysicsBody.GetAngularVelocity()`(정지 판정) /
`SetAngularVelocity()`(복귀). 나머지(`GetPosition`/`GetRotation`/`GetVelocity`/`Set*`)는 이미 있다.

## 5. 배선

### `PanchigiTurnSystem` (LOP-Server)

`runner.RegisterSystem<End>(this)`로 매 틱 돈다. `End`는 **물리가 돈 뒤·스냅샷 송신 전**이라
"이번 틱 결과를 보고 턴을 정한 뒤 그 상태를 같이 보낸다"가 한 틱 안에 끝난다.
(`LOPAIController`가 쓰는 것과 같은 관례.)

드는 것: `PanchigiTurn` POCO · 정지 연속 틱 카운터 · `entityId → 자리 인덱스` · `PanchigiBoard`.

### 타격 수신은 지금 자리를 지킨다

`PanchigiStrikeMessageHandler`가 계속 받고, 기하 검증 **앞에** 한 줄이 붙는다:

```
차례인가?  →  아니면 버린다
기하 검증  →  임펄스
턴 시스템에 "쳤다" 통지  →  Aiming → Settling
```

핸들러는 턴 시스템에 *묻고 알리기만* 한다. 반대로 턴 시스템이 임펄스까지 주는 배선도 되지만,
그러면 두 시스템이 서로의 내부를 알게 된다.

### 종료 신호 — `IGameRuleSystem`에 `bool IsMatchOver` 한 줄

```csharp
// LOPRunner.LateUpdate
if (initialized && (gameRuleSystem.IsMatchOver || tickUpdater.elapsedTime > 60 * 5))
{
    EndMatch();
}
```

인터페이스의 주석은 이미 *"언제 끝내는지"* 가 자기 책임이라고 적어 놓고 **메서드가 없었다** —
러너의 5분 타이머가 그 자리를 대신하고 있었다. 다른 두 게임(FlapWang·FlappyRace)은 `=> false` 한 줄.
**5분 타이머는 지우지 않는다** — 다른 게임엔 아직 그것뿐이고, 판치기에도 백스톱이 된다.

### 방향은 한쪽이다

`PanchigiRuleSystem` → `PanchigiTurnSystem`만 참조한다. 룰 시스템이 `Initialize`에서 스폰하며 자리
인덱스를 턴 시스템에 넘기고, `IsMatchOver`/`ResolveOutcome`을 턴 시스템의 상태로 답한다.
턴 시스템은 룰 시스템을 모른다(순환 방지).

DI: 둘 다 `PanchigiLifetimeScope`에 Singleton. 턴 시스템은 `PanchigiStrikeMessageHandler`와 같은
`RegisterEntryPoint` 방식으로 생명주기를 받는다.

## 6. 와이어와 클라

### `PanchigiStateToC` (LOP-Shared proto, **reliable**, 바뀔 때만)

| 필드 | |
|---|---|
| `phase` | `Settling` / `Aiming` |
| `current_entity_id` | 지금 차례인 플레이어. `Settling`이면 빈 값 |
| `aim_deadline_tick` | 조준 마감. `Aiming`일 때만 유효 |

**남은 시간을 클라가 스스로 그린다** — 마감을 *틱*으로 보내면 클라가 공유 틱 시계로 매 프레임
계산한다. 주기 전송도, 초 단위 갱신 메시지도 필요 없다.

`Over`는 phase에 넣지 않는다. 매치 종료는 이미 기존 경로(`EndMatch` → 결과 화면)로 가므로 두 경로가
같은 사실을 말하게 된다.

`current_entity_id`로 보내는 이유는 클라가 이미 `playerContext.entityId`를 들고 있어 **이름 조회 없이
내 차례인지 판정**되기 때문이다. 3~4인일 때 "다른 사람 차례"가 누구인지는 안 보이는데, 이름을
붙이려면 표시명 조회가 필요해 이번 최소안에서는 뺐다.

> **메시지 id를 눈으로 확인한다.** 생성기가 은퇴한 번호를 앞에서부터 다시 채우기 때문에
> 새 메시지가 옛 번호를 물려받을 수 있다. 지금은 클라를 늘 새로 빌드해 무해하지만, 재생성 뒤
> `MessageIds.cs` diff에서 **기존 번호가 밀리지 않았는지**는 매번 봐야 한다
> (`[[proto-message-id-regen-gotcha]]`, ROADMAP 파킹).

### 받는 쪽은 홀더를 하나 둔다

`PanchigiStateStore`(최신 상태 보관) ← 메시지 핸들러가 채우고, UI가 R3로 구독.
UI가 아직 안 만들어졌을 때 도착한 상태를 잃지 않기 위해서다 — reliable은 *도착*을 보장하지만
*받을 준비*까지 보장하지 않는다.

### UI — 한 줄

`PanchigiTurnView` + `PanchigiTurnViewModel`(기존 게임 스코프 View 배선을 탄다).

```
Aiming  · 내 차례     →  "내 차례 · 12"
Aiming  · 남의 차례   →  "다른 사람 차례"
Settling             →  "동전이 멈추는 중"
```

**입력 게이팅**: `PanchigiStrikeInput`이 `Aiming && 내 차례`일 때만 조준을 시작한다. 아니면 눌러도
조준선이 안 뜬다 — 화면 문구와 짝이 맞아 왜 안 되는지 보인다.

## 7. 어셈블리 배치 — 판치기 규칙은 서버 것이다

토폴로지 결정 트리(`lop-repo-topology.md`)로 따진다:

> 2. LOP 도메인이고 **클·서 양쪽이 반드시 동일하게 보아야** 하는가? → LOP-Shared
> 4. **한쪽만 쓰는** I/O·View·정책인가? → 각자

판치기는 **클라 예측이 없어서**(게임 모드 설계 §3.2) 면 판정·정지 판정·턴 전이·힘 계산을 **서버만**
한다. 따라서:

| | 어디 |
|---|---|
| `PanchigiCoin` (면·장외·정지 판정) | **LOP-Server** |
| `PanchigiTurn` (전이 POCO) | **LOP-Server** |
| `PanchigiTurnSystem` · `PanchigiBoard` | **LOP-Server** |
| **`PanchigiStrike`** (구 `PanchigiStrikeKernel`) | LOP-Shared → **LOP-Server**로 이동 |
| `PanchigiStateToC` (proto) | LOP-Shared — **와이어는 양쪽이 같아야 한다** |

### 결정 — 테스트 편의가 배치를 정하지 않는다

`PanchigiStrikeKernel`은 슬라이스 3에서 **EditMode 테스트를 붙이려고** LOP-Shared에 뒀다. 그건
잘못된 이유였다. 서버 레포는 asmdef가 없어 테스트가 안 붙지만, **개념적으로 맞는 자리에 두는 것이
먼저**다.

**어셈블리로 떼어내지 않는다.** 진짜 피처 어셈블리(`LOP.Panchigi`)를 만들 수 없기 때문이다 —
판치기 서버 코드는 `IRoomDataStore`·`EntitySpawner`·`MessageHandlerBase`·`IGameRuleSystem`처럼
`Assembly-CSharp`에 있는 것들에 붙어 있고, asmdef는 `Assembly-CSharp`를 참조할 수 없다.
순수한 조각만 떼면 그건 피처가 아니라 "테스트 붙는 부분"이다.

**대가**: `PanchigiStrikeKernelTests` 13개가 사라진다. 그 자리는 §8의 에디터 검증 루틴이 메운다.

### 이름 — 주어를 클래스로, 동사를 메서드로

```csharp
PanchigiCoin.IsFlipped(rotation)
PanchigiStrike.ComputeImpulse(input, tuning, samples, liveCount, total)
```

`*System`은 **무상태 DI 인스턴스**(월드·컴포넌트를 조작, 컨텍스트를 안다)이고, 이것들은 **컨텍스트
없는 static 순수 함수**라 그 이름을 쓰지 않는다(`world-core-connection-architecture.md` 컨벤션).

`Kernel`은 버린다. 커널은 *데이터 뭉치에 같은 계산을 훑어 값을 내는 것*(GPU 커널·합성곱 커널)이라
`ComputeImpulse`에는 맞지만 `IsFlipped` 같은 술어에는 억지다. 한 피처 안에서 **주어 이름
(`PanchigiCoin`)과 메커니즘 이름(`...Kernel`)이 섞이는 것**이 아키텍처 가이드라인이 금지한
*"같은 개념에 다른 어휘를 섞지 않는다"* 에 정확히 걸린다.

`Rules`도 버린다 — `IGameRuleSystem`/`PanchigiRuleSystem`과 충돌한다.
`Util`/`Helper`도 버린다 — 도메인 없는 잡동사니에 붙이는 이름이라 나중에 아무거나 들어온다.

### 산업 표준 매핑

- **주어 static 클래스**: 유니티 자신의 관례 — `Physics.Raycast`, `Mathf.Abs`, `Input.GetKey`.
  맨명사 클래스에 동사 메서드.
- **턴 시스템을 게임 쪽에 두는 것**: 언리얼 `GameMode`(스폰·승패 규칙)가 엔진이 아니라 게임에 있는 것과 같다.
- **`IGameRuleSystem.IsMatchOver`**: 언리얼 `AGameMode::ReadyToEndMatch()`에 대응한다.

> **턴 시스템은 판치기 로컬로 둔다.** 두 번째 게임 모드가 턴제를 필요로 할 때 GameFramework로
> 승격한다 — 지금 올리면 사용자가 하나뿐인 추상이 된다.

## 8. 검증

### 에디터 검증 루틴 (`Assets/Editor/PanchigiVerification.cs`, `[MenuItem]`)

`Assembly-CSharp-Editor`는 **`Assembly-CSharp`를 참조할 수 있다** — asmdef를 안 만들어도 판치기
코드에 그대로 닿는다. `unity cmd menu`로 **헤드리스 재실행**이 되므로 다음에 누가 손대도 표를 다시
뽑아 비교할 수 있다.

| 대상 | 뽑는 표 |
|---|---|
| `PanchigiCoin.IsFlipped` | 기울기 0°/45°/**89°/90°/91°**/135°/180° |
| `PanchigiCoin.IsOutOfBoard` | 판 경계 안팎 격자 + 판 아래 |
| `PanchigiCoin.IsAtRest` | (선속도, 각속도) 조합 — **각속도만 큰 경우** 포함 |
| `PanchigiTurn` | 전이 다섯 줄 + 한 바퀴 순서 + 상한 무승부 |
| `PanchigiStrike` | 덮임 계수 — 평평/모로/공중/포갬. **잃은 테스트 13개 자리를 메운다** |

### 실플레이(두 클라)

순수 로직으로 못 잡는 것만:

- 내 차례가 아니면 조준선이 안 뜬다
- 조준 시간이 지나면 다음 사람에게 넘어간다
- 모든 동전을 뒤집으면 그 사람이 결과 화면에 승자로 뜬다
- 장외 동전이 제자리로 돌아오고 **그 뒤 다시 칠 수 있다**
- 서버 예외 0

### 정직하게 남길 한계

에디터 루틴은 **CI가 안 돌린다** — 사람이 부를 때만 돈다. 서버 레포에 피처 asmdef가 생기면
이걸 EditMode로 승격하는 것이 다음 자리다(ROADMAP 파킹의 *"테스트가 필요한 조각부터 asmdef로"* 참고).

## 9. 배포

에셋·씬·마스터데이터가 모두 바뀌므로 **세 갈래 중 둘**이 필요하다:

- `gameserver-deploy`(local) — 서버 코드·씬
- **`content-deploy`(gameserver)** — 씬이 어드레서블 원격 그룹에 걸리면 필수.
  `[[addressables-remote-delivery]]`
- `backend-deploy` — 불필요(판치기 노브는 Luban group `m`에 안 닿는다)

## 10. 전체 그림

```
서버                                        클라
─────────────────────────────────────       ──────────────────────
PanchigiRuleSystem
  ├ 스폰: PanchigiBoard의 자리 목록
  └ ResolveOutcome / IsMatchOver ──┐
                                   │
PanchigiTurnSystem (End 페이즈)     │
  ├ PanchigiCoin.IsAtRest × N틱     │
  ├ PanchigiCoin.IsOutOfBoard ──> rb에 자리 복귀
  ├ PanchigiCoin.IsFlipped ──> 전부? ──> PanchigiTurn
  ├ 조준 마감 초과 ──> 패스        │
  └ 상태 바뀜 ──> PanchigiStateToC ────────> PanchigiStateStore
                                   │              └─> PanchigiTurnView ("내 차례 · 12")
PanchigiStrikeMessageHandler        │              └─> PanchigiStrikeInput (게이팅)
  ├ 차례인가? ──────────────────────┘
  ├ 기하 검증 ──> PanchigiStrike.ComputeImpulse
  └ "쳤다" 통지
```
