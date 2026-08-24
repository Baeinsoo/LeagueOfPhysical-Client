# Flappy Race 시작 게이트 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 참가자가 다 모일 때까지(또는 상한까지) 새를 출발선에 세워 두고, 3초 카운트다운 뒤 모두 같은 틱에 출발시킨다.

**Architecture:** 서버가 순수 C# 게이트로 "몇 번 틱에 출발"을 정해 클라에 알리고, 양쪽 월드가 `tick >= GameplayStartTick`이라는 같은 계산으로 새를 굴릴지 정한다. 틱 루프는 처음부터 끝까지 멈추지 않는다.

**Tech Stack:** C# / Unity 6000.3.16f1 / VContainer / MessagePipe(OrderedMessageBroker) / R3 / Mirror / Protobuf / UI Toolkit

**Spec:** `docs/superpowers/specs/2026-08-25-flappy-race-start-gate-design.md`

## Global Constraints

- **브랜치**: 모든 레포에서 `feature/flappy-race-start-gate`. **`feature/flappy-ghost-extrapolation` 위에서 분기**한다(클라는 이미 그렇게 잡혀 있다). main에 직접 커밋 금지.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전에 `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다. 워킹트리에는 **의도적으로 커밋하지 않는 로컬 픽스처**가 늘 있다(`ConfigureRoomComponent.cs`, `ProjectSettings.asset`, URP 에셋, 폰트 에셋, `Assets/Art` 서브모듈 포인터).
- **`.meta` 파일은 반드시 함께 커밋**한다. 직접 만들지 말고 Unity가 생성한 것을 커밋한다.
- **테스트는 일부러 깨서 빨강을 확인한 뒤 커밋한다.** 직전 슬라이스에서 "초록인데 아무것도 안 지키는 테스트"가 여섯 개 나왔다.
- **World 타입은 항상 풀 네임스페이스로 한정**한다(`GameFramework.World.Entity`). `using GameFramework.World;`를 추가하지 않는다 — `Component`가 `UnityEngine.Component`와 겹친다.
- **주석은 최소화하고 쉽게 쓴다.** 코드로 자명한 것(무엇을)은 쓰지 않고, 비자명한 의도(왜)만 일상어로.
- **소프트웨어를 설치하지 않는다**(`brew install` 등). 필요하면 보고한다.
- **서브에이전트를 띄우지 않는다.**
- **입력 차단 코드를 넣지 않는다.** 카운트다운 중 탭은 `FlappyMoveSystem`이 아예 안 돌아 양쪽이
  똑같이 무시한다 — 어긋날 여지가 없어 **의도적으로 게이트를 두지 않았다.** 빠뜨린 것이 아니다.
  입력 스트림 자체는 계속 흘러야 한다(Phase 3c의 연속 command-frame — 끊으면 유실 복구가 깨진다).

### 스펙과 다른 점 (의도된 수정)

스펙 §9는 `RaceStartViewModelTests`(클라 EditMode)를 적었지만, **클라 `Assembly-CSharp`는 테스트
asmdef가 참조할 수 없다.** 그렇다고 그 계산을 다른 어셈블리로 옮기지 않는다 —
**테스트를 위한 어셈블리 이동은 하지 않는다.**

대신 지켜야 할 것과 아닌 것을 가른다:

| 무엇 | 틀리면 | 어디서 지키나 |
|---|---|---|
| `tick >= GameplayStartTick` | 클·서가 다른 틱에 출발 = 시뮬이 갈린다 | 월드 안. **Task 2에서 테스트** |
| 카운트다운 숫자(올림) | 화면에 "2, 1, 0"이 뜬다 | 태스크 8의 눈 검증 |

두 번째는 순전히 연출이고 화면을 켜면 즉시 보인다. 자동 테스트 없이 간다 —
`RaceStartViewModel` 안에 그대로 둔다.

### 테스트 실행 방법 (중요)

에디터가 떠 있는 상태에서는 배치모드 `unity test`가 **죽는다**(단독 점유 필요). 반드시:

```bash
export PATH="$HOME/.unity/bin:$PATH"

# 코드를 고친 뒤
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
unity command recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
# status가 completed / failed:false 가 될 때까지 다시 부른다. 알림은 오지 않는다.

unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
```

- **`--project-path`를 항상 붙인다.** 빼면 클·서 에디터가 둘 다 떠 있을 때 `Multiple Unity Editor instances found`로 멈추고, 하나만 떠 있을 때는 **조용히 다른 프로젝트로 라우팅된다.**
- 서버 테스트는 `--project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server`.
- GameFramework·LOP-Shared 패키지의 EditMode 테스트는 **클라/서버 테스트 실행에 함께 포함된다**(manifest `testables`). 별도 실행 경로가 없다.
- 결과 JSON이 크므로 요약만 뽑아 본다:
  ```bash
  unity command run_tests --project-path <경로> --no-banner 2>&1 | python3 -c "
  import sys,json
  s=sys.stdin.read(); i=s.find('{\"Summary\"')
  d,_=json.JSONDecoder().raw_decode(s[i:])
  print(d['Summary'])
  [print(' >',r['FullName'],(r['Message'] or '')[:300]) for r in d['Results'] if r['Status']!='Passed']"
  ```
- **에디터를 열어 둔 채 브랜치를 바꾸거나 패키지 파일이 바뀌면 반드시 `recompile`을 먼저 돌린다.** 안 그러면 낡은 어셈블리로 테스트가 돌아 가짜 실패가 난다(실제로 겪음).

### 기준선 (착수 전 값)

- 클라 **583 passed / 0 failed**
- 서버 **553 passed / 0 failed**

---

## 파일 구조

### GameFramework (`/Users/insoobae/workspace/LOP/GameFramework`)

| 파일 | 책임 |
|---|---|
| `Runtime/Scripts/Game/MatchPhase.cs` (신규) | 매치 단계 열거형. 서버 안에서만 산다 |
| `Runtime/Scripts/Game/MatchStartGate.cs` (신규) | 준비 집계·상한·출발틱 결정. 순수 C#, 이 슬라이스의 두뇌 |
| `Runtime/Scripts/World/IWorld.cs` (수정) | `GameplayStartTick` 노출 |
| `Runtime/Scripts/World/WorldBase.cs` (수정) | `GameplayStartTick` 보유 + `HasStarted` 헬퍼 |
| `Tests/Runtime/Game/MatchStartGateTests.cs` (신규) | 게이트 전 경우 |

### LeagueOfPhysical-Shared (`/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared`)

| 파일 | 책임 |
|---|---|
| `Runtime/Scripts/Game/FlappyWorld.cs` (수정) | 출발 전엔 새를 안 굴린다 |
| `Protos/MatchStartToC.proto` (신규) | 서버 → 클라: 출발틱 + 준비 현황 |
| `Protos/MatchReadyToS.proto` (신규) | 클라 → 서버: 준비됨 |
| `Runtime.Generated/**` (생성물) | `generate_protos.sh` 산출물 |
| `Tests/EditMode/FlappyWorldStartGateTests.cs` (신규) | 정지·출발 경계·결정론 |

### LeagueOfPhysical-Server (`/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server`)

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Game/TickSystems/MatchStartSystem.cs` (신규) | 게이트 구동 + 알림 + 월드에 출발틱 대입 |
| `Assets/Scripts/Game/GameplayInstaller.cs` (수정) | DI 등록 |
| `Assets/Scripts/Game/LOPRunner.cs` (수정) | 파이프라인 맨 앞 + 매치 시계 기준 교정 |
| `Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs` (수정) | 새 세션에게 즉시 현재 상태 |

### LeagueOfPhysical-Client (`/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client`)

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Room/LOPRoom.cs` (수정) | 준비 송신 |
| `Assets/Scripts/Game/MatchStartState.cs` (신규) | 화면용 R3 홀더 |
| `Assets/Scripts/Game/MessageHandler/MatchStartMessageHandler.cs` (신규) | 수신 → 월드·홀더 |
| `Assets/Scripts/UI/RaceStart/RaceStartView.cs` (신규) | 라벨 하나 갱신하는 얇은 바인더 |
| `Assets/Scripts/UI/RaceStart/RaceStartViewModel.cs` (신규) | 표시 문자열 |
| `Assets/UI/RaceStart/RaceStartView.uxml` / `.uss` (신규) | 가운데 큰 글자 |
| `Assets/UI/UIViewCatalog.asset` (수정) | View → uxml 매핑 |
| `Assets/Scripts/Game/GameplayInstaller.cs` (수정) | 핸들러·홀더 등록 |
| `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` (수정) | View 등록 |
| `Assets/Scripts/Game/FlappyHudCoordinator.cs` (수정) | 화면 열기 |

---

## Task 1: 출발 시점을 정하는 게이트 (GameFramework)

이 슬라이스에서 **틀릴 수 있는 것의 대부분이 여기 있다.** Unity를 참조하지 않는 순수 C#이라 EditMode로 전 경우를 돌린다.

**Files:**
- Create: `/Users/insoobae/workspace/LOP/GameFramework/Runtime/Scripts/Game/MatchPhase.cs`
- Create: `/Users/insoobae/workspace/LOP/GameFramework/Runtime/Scripts/Game/MatchStartGate.cs`
- Test: `/Users/insoobae/workspace/LOP/GameFramework/Tests/Runtime/Game/MatchStartGateTests.cs`

**Interfaces:**
- Consumes: (없음 — 이 태스크가 사슬의 시작)
- Produces:
  - `enum GameFramework.Runner.MatchPhase { WaitingForPlayers, Countdown, InProgress, Finished }`
  - `class GameFramework.Runner.MatchStartGate`
    - `MatchStartGate(int expectedPlayers, long waitCapTicks, long countdownTicks)`
    - `MatchPhase Phase { get; }`
    - `int ReadyCount { get; }`
    - `int ExpectedPlayers { get; }`
    - `long StartTick { get; }` — 확정 전엔 `long.MaxValue`
    - `void MarkReady(string userId)`
    - `void Tick(long tick)`
    - `void Finish()`

- [ ] **Step 1: 테스트 폴더를 만든다**

`Tests/Runtime/` 아래에 `Game` 폴더를 만든다. 별도 asmdef는 만들지 않는다 — `Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef`가 하위 폴더를 모두 덮는다(`Tests/Runtime/Netcode`, `Tests/Runtime/Auth`와 같은 구조).

```bash
mkdir -p /Users/insoobae/workspace/LOP/GameFramework/Tests/Runtime/Game
```

- [ ] **Step 2: 실패하는 테스트를 쓴다 — 게이트**

`Tests/Runtime/Game/MatchStartGateTests.cs`:

```csharp
using GameFramework.Runner;
using NUnit.Framework;

namespace GameFramework.Tests.Runner
{
    public class MatchStartGateTests
    {
        private const long WaitCap = 1500;      // 30초 @50Hz
        private const long Countdown = 150;     // 3초 @50Hz

        private static MatchStartGate Gate(int expected = 2)
            => new MatchStartGate(expected, WaitCap, Countdown);

        [Test]
        public void 처음엔_대기_상태이고_출발틱이_없다()
        {
            var gate = Gate();
            gate.Tick(1000);

            Assert.AreEqual(MatchPhase.WaitingForPlayers, gate.Phase);
            Assert.AreEqual(long.MaxValue, gate.StartTick);
        }

        [Test]
        public void 전원_준비하면_그_틱에_카운트다운이_시작된다()
        {
            var gate = Gate();
            gate.Tick(1000);

            gate.MarkReady("a");
            gate.MarkReady("b");
            gate.Tick(1001);

            Assert.AreEqual(MatchPhase.Countdown, gate.Phase);
            Assert.AreEqual(1001 + Countdown, gate.StartTick);
        }

        [Test]
        public void 일부만_준비했고_상한_전이면_계속_기다린다()
        {
            var gate = Gate();
            gate.Tick(1000);

            gate.MarkReady("a");
            gate.Tick(1000 + WaitCap - 1);

            Assert.AreEqual(MatchPhase.WaitingForPlayers, gate.Phase);
            Assert.AreEqual(1, gate.ReadyCount);
        }

        [Test]
        public void 상한이_지나면_안_온_사람을_두고_출발한다()
        {
            var gate = Gate();
            gate.Tick(1000);

            gate.MarkReady("a");
            gate.Tick(1000 + WaitCap);

            Assert.AreEqual(MatchPhase.Countdown, gate.Phase);
            Assert.AreEqual(1000 + WaitCap + Countdown, gate.StartTick);
        }

        [Test]
        public void 같은_사람이_여러_번_보내도_한_명으로_센다()
        {
            var gate = Gate();
            gate.Tick(1000);

            gate.MarkReady("a");
            gate.MarkReady("a");
            gate.MarkReady("a");
            gate.Tick(1001);

            Assert.AreEqual(1, gate.ReadyCount);
            Assert.AreEqual(MatchPhase.WaitingForPlayers, gate.Phase);
        }

        [Test]
        public void 카운트다운_중에_나머지가_준비해도_출발틱은_안_밀린다()
        {
            var gate = Gate();
            gate.Tick(1000);
            gate.MarkReady("a");
            gate.Tick(1000 + WaitCap);          // 상한 만료로 카운트다운 진입
            long announced = gate.StartTick;

            gate.MarkReady("b");
            gate.Tick(1000 + WaitCap + 10);

            Assert.AreEqual(announced, gate.StartTick);
            Assert.AreEqual(MatchPhase.Countdown, gate.Phase);
        }

        [Test]
        public void 출발틱_직전까지는_아직_카운트다운이다()
        {
            var gate = Gate();
            gate.Tick(1000);
            gate.MarkReady("a");
            gate.MarkReady("b");
            gate.Tick(1001);

            gate.Tick(gate.StartTick - 1);

            Assert.AreEqual(MatchPhase.Countdown, gate.Phase);
        }

        [Test]
        public void 출발틱에_진행으로_바뀐다()
        {
            var gate = Gate();
            gate.Tick(1000);
            gate.MarkReady("a");
            gate.MarkReady("b");
            gate.Tick(1001);

            gate.Tick(gate.StartTick);

            Assert.AreEqual(MatchPhase.InProgress, gate.Phase);
        }

        [Test]
        public void 진행_후에_온_준비는_아무것도_바꾸지_않는다()
        {
            var gate = Gate(expected: 3);
            gate.Tick(1000);
            gate.MarkReady("a");
            gate.Tick(1000 + WaitCap);
            long announced = gate.StartTick;
            gate.Tick(announced);

            gate.MarkReady("b");
            gate.Tick(announced + 1);

            Assert.AreEqual(MatchPhase.InProgress, gate.Phase);
            Assert.AreEqual(announced, gate.StartTick);
            Assert.AreEqual(1, gate.ReadyCount);
        }

        [Test]
        public void 아무도_없는_방은_기다리지_않고_바로_카운트다운한다()
        {
            var gate = Gate(expected: 0);
            gate.Tick(1000);

            Assert.AreEqual(MatchPhase.Countdown, gate.Phase);
            Assert.AreEqual(1000 + Countdown, gate.StartTick);
        }

        [Test]
        public void 끝난_매치는_더_이상_전이하지_않는다()
        {
            var gate = Gate();
            gate.Tick(1000);
            gate.MarkReady("a");
            gate.MarkReady("b");
            gate.Tick(1001);
            gate.Tick(gate.StartTick);

            gate.Finish();
            gate.Tick(gate.StartTick + 100);

            Assert.AreEqual(MatchPhase.Finished, gate.Phase);
        }
    }
}
```

> **왜 `아무도 없는 방` 케이스가 상한을 안 기다리나**: `expectedPlayers == 0`이면 "전원 준비" 조건이 처음부터 참이다. 서버가 빈 방에서 30초를 멍하니 서 있지 않게 하는 것이 의도다.

- [ ] **Step 3: 빨강을 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
```

기대: `recompile_status`가 `failed:true`이고 에러가 `MatchStartGate`를 찾을 수 없다고 한다. (아직 타입이 없으므로 테스트 실행 이전에 컴파일에서 막힌다 — 이게 이 단계의 빨강이다.)

- [ ] **Step 4: `MatchPhase`를 만든다**

`Runtime/Scripts/Game/MatchPhase.cs`:

```csharp
namespace GameFramework.Runner
{
    /// <summary>
    /// 매치 진행 단계. 언리얼 AGameMode::MatchState에 대응한다.
    /// 서버 안에서만 쓴다 — 와이어로는 "몇 틱에 출발"만 나간다(현재 상태를 보내면 앞서 달리는
    /// 클라에게는 이미 지난 정보가 되기 때문).
    /// </summary>
    public enum MatchPhase
    {
        WaitingForPlayers,
        Countdown,
        InProgress,
        Finished,
    }
}
```

- [ ] **Step 5: `MatchStartGate`를 만든다**

`Runtime/Scripts/Game/MatchStartGate.cs`:

```csharp
using System.Collections.Generic;

namespace GameFramework.Runner
{
    /// <summary>
    /// 언제 출발할지를 정한다. 전원이 준비하거나 대기 상한이 지나면 카운트다운에 들어가고,
    /// 그 순간 "몇 번 틱에 출발"이 확정된다. 언리얼 AGameMode의 ReadyToStartMatch + bDelayedStart에 해당.
    /// <para>
    /// 시간이 아니라 틱으로 세는 이유: 출발틱이 곧 결과물이고, 클·서가 같은 숫자를 봐야 한다.
    /// </para>
    /// </summary>
    public sealed class MatchStartGate
    {
        private readonly HashSet<string> _ready = new HashSet<string>();
        private readonly long _waitCapTicks;
        private readonly long _countdownTicks;

        //  대기 상한을 재기 시작한 틱. 첫 Tick에서 정해진다 — 생성자(DI 조립) 시점부터 재면
        //  맵 로딩 시간이 상한을 갉아먹는다.
        private long _armTick;
        private bool _armed;

        public MatchStartGate(int expectedPlayers, long waitCapTicks, long countdownTicks)
        {
            ExpectedPlayers = expectedPlayers;
            _waitCapTicks = waitCapTicks;
            _countdownTicks = countdownTicks;
        }

        public MatchPhase Phase { get; private set; } = MatchPhase.WaitingForPlayers;
        public int ExpectedPlayers { get; }
        public int ReadyCount => _ready.Count;

        /// <summary>게임플레이가 시작될 틱. 아직 확정 전이면 long.MaxValue.</summary>
        public long StartTick { get; private set; } = long.MaxValue;

        /// <summary>같은 사람이 여러 번 보내도 한 번으로 센다. 이미 출발했으면 무시.</summary>
        public void MarkReady(string userId)
        {
            if (Phase != MatchPhase.WaitingForPlayers || string.IsNullOrEmpty(userId))
            {
                return;
            }

            _ready.Add(userId);
        }

        /// <summary>
        /// 페이즈 전이는 여기서만 일어난다. 메시지가 도착한 순간에 바꾸면 틱 중간에 페이즈가 갈려,
        /// 같은 틱을 보는 시스템들이 서로 다른 답을 본다.
        /// </summary>
        public void Tick(long tick)
        {
            if (_armed == false)
            {
                _armTick = tick;
                _armed = true;
            }

            switch (Phase)
            {
                case MatchPhase.WaitingForPlayers:
                    if (_ready.Count >= ExpectedPlayers || tick - _armTick >= _waitCapTicks)
                    {
                        StartTick = tick + _countdownTicks;
                        Phase = MatchPhase.Countdown;
                    }
                    break;

                case MatchPhase.Countdown:
                    //  한 번 확정된 출발틱은 무슨 일이 있어도 밀지 않는다 —
                    //  이미 3-2-1을 보고 있는 사람이 배신당한다.
                    if (tick >= StartTick)
                    {
                        Phase = MatchPhase.InProgress;
                    }
                    break;
            }
        }

        /// <summary>매치가 끝났음을 알린다. 이후 Tick은 아무것도 바꾸지 않는다.</summary>
        public void Finish() => Phase = MatchPhase.Finished;
    }
}
```

- [ ] **Step 6: 초록을 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
# completed / failed:false 확인 후
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
```

기대: 실패 0. 총 개수는 583 + 11 = **594**.

- [ ] **Step 7: 테스트가 실제로 깨지는지 확인한다**

`MatchStartGate.Tick`의 카운트다운 분기를 `tick > StartTick`으로 잠깐 바꾸고 다시 돌린다.
기대: `출발틱에_진행으로_바뀐다`가 **빨강**. 확인했으면 되돌린다.

이어서 `StartTick = tick + _countdownTicks;`를 `WaitingForPlayers` 분기가 아니라 `Countdown` 분기에서 매번 다시 계산하도록 잠깐 바꾼다.
기대: `카운트다운_중에_나머지가_준비해도_출발틱은_안_밀린다`가 **빨강**. 되돌린다.

- [ ] **Step 8: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git status --short          # 의도한 파일 + .meta 만 있는지 확인
git add Runtime/Scripts/Game/MatchPhase.cs Runtime/Scripts/Game/MatchPhase.cs.meta \
        Runtime/Scripts/Game/MatchStartGate.cs Runtime/Scripts/Game/MatchStartGate.cs.meta \
        Tests/Runtime/Game
git commit -m "feat(runner): 언제 출발할지 정하는 매치 시작 게이트를 더한다"
```

---

## Task 2: 출발틱 전에는 새를 안 굴린다 (GameFramework + LOP-Shared)

**Files:**
- Modify: `/Users/insoobae/workspace/LOP/GameFramework/Runtime/Scripts/World/IWorld.cs`
- Modify: `/Users/insoobae/workspace/LOP/GameFramework/Runtime/Scripts/World/WorldBase.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`
- Test: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldStartGateTests.cs`

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `long GameFramework.World.IWorld.GameplayStartTick { get; set; }` — 기본 `long.MaxValue`
  - `protected bool GameFramework.World.WorldBase.HasStarted(long tick)`

> **왜 `WorldBase`에 두는가**: "게임플레이가 몇 틱에 시작하나"는 게임 종류와 무관한 숫자다. 서버는
> `MatchStartSystem`이, 클라는 메시지 핸들러가 같은 한 줄로 대입한다 — 양쪽 다 게임을 모른다.
> 무엇을 안 굴릴지는 각 월드의 `Mutation`이 정한다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/FlappyWorldStartGateTests.cs`:

```csharp
using GameFramework.Physics;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class FlappyWorldStartGateTests
    {
        //  이웃 테스트 파일들과 같은 모양의 "아무것도 안 맞는" 스텁. 공용 픽스처로 빼지 않은 것은
        //  이미 네 파일이 각자 갖고 있어, 여기만 바꾸면 오히려 어긋나 보이기 때문이다.
        private class EmptySkyQuery : ICollisionQuery
        {
            public CollisionHit CapsuleCast(UnityEngine.Vector3 p1, UnityEngine.Vector3 p2, float radius,
                UnityEngine.Vector3 direction, float distance, int layerMask)
                => CollisionHit.None;
        }

        private const float Dt = 0.02f;

        [Test]
        public void 출발틱이_안_정해졌으면_아무리_굴려도_안_움직인다()
        {
            var world = FlappyWorldFixture.Create(new EmptySkyQuery(), out var bird);

            for (long tick = 0; tick < 200; tick++)
            {
                world.Tick(tick, Dt);
            }

            Assert.AreEqual(System.Numerics.Vector3.Zero, bird.Get<GameFramework.World.Transform>().Position);
            Assert.AreEqual(System.Numerics.Vector3.Zero, bird.Get<Velocity>().Linear);
        }

        [Test]
        public void 출발틱_직전까지는_안_움직인다()
        {
            var world = FlappyWorldFixture.Create(new EmptySkyQuery(), out var bird);
            world.GameplayStartTick = 100;

            for (long tick = 0; tick < 100; tick++)
            {
                world.Tick(tick, Dt);
            }

            Assert.AreEqual(System.Numerics.Vector3.Zero, bird.Get<GameFramework.World.Transform>().Position);
        }

        [Test]
        public void 출발틱부터_움직인다()
        {
            var world = FlappyWorldFixture.Create(new EmptySkyQuery(), out var bird);
            world.GameplayStartTick = 100;

            for (long tick = 0; tick <= 100; tick++)
            {
                world.Tick(tick, Dt);
            }

            //  전진 속도가 붙어 x가 커지고, 중력으로 y가 내려간다.
            Assert.Greater(bird.Get<GameFramework.World.Transform>().Position.X, 0f);
            Assert.Less(bird.Get<GameFramework.World.Transform>().Position.Y, 0f);
        }

        [Test]
        public void 출발_경계를_가로질러_두_번_굴려도_결과가_같다()
        {
            System.Numerics.Vector3 Run()
            {
                var world = FlappyWorldFixture.Create(new EmptySkyQuery(), out var bird);
                world.GameplayStartTick = 50;
                for (long tick = 40; tick < 60; tick++)
                {
                    world.Tick(tick, Dt);
                }
                return bird.Get<GameFramework.World.Transform>().Position;
            }

            Assert.AreEqual(Run(), Run());
        }
    }
}
```

> `CollisionHit.None`이 없으면 이웃 파일(`FlappyWorldTests.EmptySkyQuery`)이 쓰는 것과 **똑같은 형태**로 맞춘다. 새 API를 만들지 말 것.

- [ ] **Step 2: 빨강을 확인한다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
```
기대: `world.GameplayStartTick`을 찾을 수 없다는 컴파일 에러.

- [ ] **Step 3: `IWorld`에 노출한다**

`GameFramework/Runtime/Scripts/World/IWorld.cs`의 `void Tick(long tick, float deltaTime);` 바로 아래에 추가:

```csharp
        /// <summary>
        /// 이 틱 전에는 게임플레이가 시작되지 않았다. 확정 전엔 long.MaxValue.
        /// 무엇을 멈출지는 각 월드가 정한다 — 부르는 쪽(넷코드·룰)은 숫자만 대입한다.
        /// </summary>
        long GameplayStartTick { get; set; }
```

- [ ] **Step 4: `WorldBase`에 구현한다**

`GameFramework/Runtime/Scripts/World/WorldBase.cs`의 `public WorldEventBuffer EventBuffer { get; }` 다음 줄에 추가:

```csharp
        public long GameplayStartTick { get; set; } = long.MaxValue;

        protected bool HasStarted(long tick) => tick >= GameplayStartTick;
```

- [ ] **Step 5: `FlappyWorld.Mutation` 앞에 가드를 둔다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/FlappyWorld.cs`의 `Mutation` 맨 앞:

```csharp
        protected override void Mutation(long tick, float deltaTime)
        {
            CollectBirds();

            if (HasStarted(tick) == false)
            {
                // 출발선에서 대기 중. 속도를 명시적으로 0으로 두는 이유는 스냅샷과 물리 팔로워가
                // 이 값을 읽기 때문이다 — 스폰 직후엔 어차피 0이지만 적어 두는 쪽이 안전하다.
                for (int i = 0; i < _birds.Count; i++)
                {
                    _birds[i].Get<GameFramework.World.Velocity>().Linear = System.Numerics.Vector3.Zero;
                }
                return;
            }

            // ...기존 본문(유령정지 → 속도 → 몸싸움 → 맵 통과)...
        }
```

**주의**: `CollectBirds()`는 가드보다 **먼저** 불러야 한다(위 목록을 채워야 얼릴 대상을 안다). 기존 본문 첫 줄이 `CollectBirds()`이므로 그 아래에 가드를 넣는다.

클래스 XML 주석의 "한 틱:" 설명에 한 줄을 더한다:

```
    /// ⓪ 출발틱 전이면 아무것도 굴리지 않고 속도만 0으로 둔다.
```

- [ ] **Step 6: 초록을 확인한다**

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
```
기대: 실패 0, 총 **598**.

- [ ] **Step 7: 테스트가 실제로 깨지는지 확인한다**

`HasStarted`를 `tick > GameplayStartTick`으로 잠깐 바꾼다.
기대: `출발틱부터_움직인다`가 **빨강**. 되돌린다.

- [ ] **Step 8: 커밋 (두 레포)**

```bash
cd /Users/insoobae/workspace/LOP/GameFramework
git status --short
git add Runtime/Scripts/World/IWorld.cs Runtime/Scripts/World/WorldBase.cs
git commit -m "feat(world): 게임플레이 시작 틱을 월드가 들고 있게 한다"

cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/FlappyWorld.cs Tests/EditMode/FlappyWorldStartGateTests.cs Tests/EditMode/FlappyWorldStartGateTests.cs.meta
git commit -m "feat(flappy): 출발틱 전에는 새를 굴리지 않는다"
```

---

## Task 3: 와이어 두 개 (LOP-Shared)

**Files:**
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Protos/MatchStartToC.proto`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Protos/MatchReadyToS.proto`
- Generated (커밋 대상): `Runtime.Generated/Scripts/Protobuf/*`, `Runtime.Generated/Scripts/MessageIds.cs`, `Runtime.Generated/Scripts/MessageInitializer.cs`

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `LOP.MatchStartToC` — `long StartTick`, `int ReadyCount`, `int TotalCount`
  - `LOP.MatchReadyToS` — 필드 없음
  - `LOP.MessageIds.MatchStartToC`, `LOP.MessageIds.MatchReadyToS`

> C# 프로퍼티 이름은 protoc가 `start_tick` → `StartTick`으로 만든다. 기존
> `InputTimingToC { AvgD, MaxD, PruneCount, ... }`와 같은 규칙이다.

- [ ] **Step 1: proto 두 개를 쓴다**

`Protos/MatchStartToC.proto`:

```proto
syntax = "proto3";

// @auto_generate
// 출발 예정 틱과 준비 현황. "지금 무슨 상태"가 아니라 "몇 틱에 출발"을 보내는 이유는,
// 클라가 서버보다 앞선 시계로 달리고 틱을 되감아 재시뮬하기 때문이다 — 현재값으로는
// "그 틱에 상태가 뭐였나"에 답할 수 없다. MatchEndedToC와 짝이 된다.
message MatchStartToC {
  int64 start_tick  = 1;   // 아직 안 정해졌으면 -1
  int32 ready_count = 2;
  int32 total_count = 3;
}
```

`Protos/MatchReadyToS.proto`:

```proto
syntax = "proto3";

// @auto_generate
// 맵도 떴고 내 새도 있고 시계도 안정됐다 — 이제 진짜 플레이 가능하다는 통보.
// 필드가 없다: 누가 보냈는지는 세션이 안다.
message MatchReadyToS {}
```

`// @auto_generate` 주석이 **`message` 줄 바로 위**에 있어야 `IMessage` 구현과 MessageId가 생성된다.

- [ ] **Step 2: 생성한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared/Scripts
./generate_protos.sh
```

- [ ] **Step 3: 결과를 확인한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
grep -n "MatchStartToC\|MatchReadyToS" Runtime.Generated/Scripts/MessageIds.cs
git diff --stat Runtime.Generated/Scripts/MessageIds.cs
```

기대:
- 두 줄이 새로 생겼고 **id 15, 16**이다(현재 마지막이 14).
- **기존 id 1~14는 하나도 바뀌지 않았다.** 바뀌었다면 멈춘다 — 배포본과 wire desync가 난다.

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
```
기대: 실패 0, 총 598 (와이어만 생겼으므로 개수 불변).

- [ ] **Step 4: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Protos/MatchStartToC.proto Protos/MatchStartToC.proto.meta \
        Protos/MatchReadyToS.proto Protos/MatchReadyToS.proto.meta \
        Runtime.Generated
git commit -m "feat(wire): 출발 예정 틱과 준비 통보 메시지를 더한다"
```

---

## Task 4: 서버가 출발을 정하고 알린다

**Files:**
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/MatchStartSystem.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/GameplayInstaller.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`

**Interfaces:**
- Consumes: `MatchStartGate`, `MatchPhase` (Task 1) · `IWorld.GameplayStartTick` (Task 2) · `MatchStartToC`, `MatchReadyToS` (Task 3)
- Produces:
  - `class LOP.MatchStartSystem : MessageHandlerBase, GameFramework.Runner.ITickSystem`
    - `MatchPhase Phase { get; }`
    - `long StartTick { get; }`
    - `void Tick(long tick, float deltaTime)`
    - `MatchStartToC BuildMessage()`
    - `void Finish()` (Task 5에서 쓴다)

- [ ] **Step 1: `MatchStartSystem`을 만든다**

`Assets/Scripts/Game/TickSystems/MatchStartSystem.cs`:

```csharp
using System.Collections.Generic;
using GameFramework;
using GameFramework.Runner;
using MessagePipe;

namespace LOP
{
    /// <summary>
    /// 언제 출발할지를 정해 전원에게 알리고, 확정된 출발틱을 월드에 꽂는다.
    /// 판단은 전부 <see cref="MatchStartGate"/>(순수 C#)에 있고 여기는 배선만 한다.
    /// </summary>
    public class MatchStartSystem : MessageHandlerBase, ITickSystem
    {
        //  50Hz 기준. 카운트다운 3초는 카트라이더·로켓리그 관례.
        private const long CountdownTicks = 150;
        //  실서비스: 모바일 콜드 로딩 + 맵 로드를 덮는 30초.
        private const long WaitCapTicks = 1500;
        //  로컬(에디터): 사람이 손으로 에디터 셋을 켜는 시간. 이 한 줄이 2인 검증 리그를 세운다.
        private const long StandaloneWaitCapTicks = 30000;

        private readonly IRoomDataStore roomDataStore;
        private readonly ISessionManager sessionManager;
        private readonly GameFramework.World.IWorld world;
        private readonly ISubscriber<ClientMessage<MatchReadyToS>> readySubscriber;

        private readonly List<ClientMessage<MatchReadyToS>> received = new List<ClientMessage<MatchReadyToS>>();

        private MatchStartGate gate;
        private long lastBroadcastStartTick = long.MinValue;
        private int lastBroadcastReadyCount = -1;

        public MatchStartSystem(
            IRoomDataStore roomDataStore,
            ISessionManager sessionManager,
            GameFramework.World.IWorld world,
            ISubscriber<ClientMessage<MatchReadyToS>> readySubscriber)
        {
            this.roomDataStore = roomDataStore;
            this.sessionManager = sessionManager;
            this.world = world;
            this.readySubscriber = readySubscriber;
        }

        public MatchPhase Phase => gate?.Phase ?? MatchPhase.WaitingForPlayers;
        public long StartTick => gate?.StartTick ?? long.MaxValue;

        protected override void Subscribe()
        {
            int expected = roomDataStore.match?.playerList?.Length ?? 0;
            long cap = EnvironmentSettings.active.Standalone ? StandaloneWaitCapTicks : WaitCapTicks;
            gate = new MatchStartGate(expected, cap, CountdownTicks);

            Track(readySubscriber.Subscribe(OnMatchReadyToS));
        }

        private void OnMatchReadyToS(ClientMessage<MatchReadyToS> message) => received.Add(message);

        public void Tick(long tick, float deltaTime)
        {
            for (int i = 0; i < received.Count; i++)
            {
                gate.MarkReady(received[i].Session.userId);
            }
            received.Clear();

            gate.Tick(tick);

            //  출발틱은 확정되면 안 바뀌지만, 준비 인원은 대기 중에 늘어난다("2/4" 표시).
            if (gate.StartTick == lastBroadcastStartTick && gate.ReadyCount == lastBroadcastReadyCount)
            {
                return;
            }

            lastBroadcastStartTick = gate.StartTick;
            lastBroadcastReadyCount = gate.ReadyCount;

            world.GameplayStartTick = gate.StartTick;

            foreach (var session in sessionManager.GetAllSessions())
            {
                //  놓치면 출발을 영영 모른다 — reliable로 보낸다.
                session.Send(BuildMessage());
            }
        }

        /// <summary>지금 붙은 세션에게 현재 상태를 알린다(입장 직후 1회).</summary>
        public MatchStartToC BuildMessage() => new MatchStartToC
        {
            //  와이어에서는 "미정"을 -1로 쓴다. long.MaxValue를 그대로 실으면 클라가 그 값을
            //  틱과 빼면서 넘침이 난다.
            StartTick = gate.StartTick == long.MaxValue ? -1 : gate.StartTick,
            ReadyCount = gate.ReadyCount,
            TotalCount = gate.ExpectedPlayers,
        };
    }
}
```

**참고할 실물** (베끼기 전에 열어 볼 것):
- `MessageHandlerBase`의 `Subscribe()`/`Track()` 사용 — 서버 `Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs`
- `ClientMessage<T>.Session`, `ISession.userId`, `ISession.Send(msg)` / `Send(msg, reliable: false)` — `Assets/Scripts/Game/TickSystems/InputTimingFeedbackSystem.cs`

- [ ] **Step 2: DI에 등록한다**

`Assets/Scripts/Game/GameplayInstaller.cs`의 틱 시스템 목록에 한 줄 더한다:

```csharp
            //  RegisterEntryPoint여야 MessageHandlerBase의 구독이 살아난다(스코프가 Initialize/Dispose 구동).
            //  AsSelf는 LOPRunner가 구체 타입으로 [Inject]해 Tick·Phase를 직접 쓰기 때문이다.
            builder.RegisterEntryPoint<MatchStartSystem>().AsSelf();
```

**둘 다 필요하다.** `builder.Register<>`만 쓰면 `Subscribe()`가 영영 안 불려 준비 메시지를 못 받고,
`RegisterEntryPoint<>`만 쓰면 `LOPRunner`의 `[Inject] MatchStartSystem`이 resolve에 실패한다.
(서버의 다른 핸들러들은 `RegisterEntryPoint<GameInfoMessageHandler>()`처럼 `AsSelf` 없이 등록돼 있는데,
그것들은 러너가 구체 타입을 안 잡고 `runner.RegisterSystem<End>(this)`로 스스로 붙기 때문이다.)

- [ ] **Step 3: 파이프라인 맨 앞에 넣는다**

`Assets/Scripts/Game/LOPRunner.cs`:

```csharp
        [Inject] private MatchStartSystem matchStartSystem;
```

```csharp
        protected override void UpdateRunner()
        {
            RunPhase<Begin>(tickUpdater.tick, (float)tickUpdater.deltaTime);
            //  이번 틱이 출발틱인지가 먼저 정해져야 월드가 그걸 보고 굴린다.
            matchStartSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            serverInputSystem.Tick(...);
            world.Tick(...);
            ...
        }
```

- [ ] **Step 4: 컴파일과 테스트**

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server --no-banner
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server --no-banner
```
기대: 실패 0. (서버 기준선 553 + Task 1·2가 더한 15 = **568**)

- [ ] **Step 5: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git status --short          # ConfigureRoomComponent.cs 등 픽스처가 섞이지 않았는지 확인
git add Assets/Scripts/Game/TickSystems/MatchStartSystem.cs Assets/Scripts/Game/TickSystems/MatchStartSystem.cs.meta \
        Assets/Scripts/Game/GameplayInstaller.cs Assets/Scripts/Game/LOPRunner.cs
git commit -m "feat(match): 서버가 출발 시점을 정해 전원에게 알린다"
```

---

## Task 5: 지각 입장자와 매치 시계 (서버)

**Files:**
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`

**Interfaces:**
- Consumes: `MatchStartSystem.BuildMessage()`, `.Phase`, `.StartTick` (Task 4)
- Produces: (없음 — 배선만)

- [ ] **Step 1: 입장 응답 바로 뒤에 현재 상태를 보낸다**

`GameInfoMessageHandler`에 `MatchStartSystem`을 주입하고, `session.Send(gameInfoToC);` 바로 다음 줄에:

```csharp
                //  이미 달리는 판에 붙은 사람도 이 한 줄로 출발틱을 받아 바로 참여한다 —
                //  지각 입장용 별도 경로가 필요 없다.
                session.Send(matchStartSystem.BuildMessage());
```

**순서가 중요하다.** `GameInfoToC`가 먼저 가야 클라가 엔티티를 만든 뒤 출발틱을 받는다. 둘 다
reliable이라 Mirror가 순서를 지킨다.

- [ ] **Step 2: 매치 시계를 출발틱 기준으로 센다**

`LOPRunner.cs`의 `LateUpdate`:

```csharp
        //  50Hz × 300초.
        private const long MatchDurationTicks = 15000;

        private void LateUpdate()
        {
            //  방이 부팅된 때가 아니라 출발한 때부터 잰다. 부팅 기준이면 참가자를 기다리는 동안
            //  판이 시작도 못 하고 끝난다(로컬 대기 상한이 600초라 특히).
            if (initialized
                && matchStartSystem.Phase == MatchPhase.InProgress
                && tickUpdater.tick - matchStartSystem.StartTick > MatchDurationTicks)
            {
                EndMatch();
            }
        }
```

`EndMatch()` 안에서 게이트에도 알린다 — `gameState = RunnerState.GameOver;` 앞에:

```csharp
            matchStartSystem.Finish();
```

이를 위해 `MatchStartSystem`에 위임 메서드를 더한다:

```csharp
        public void Finish() => gate?.Finish();
```

- [ ] **Step 3: 컴파일과 테스트**

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server --no-banner
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server --no-banner
```
기대: 실패 0, 568.

- [ ] **Step 4: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs \
        Assets/Scripts/Game/LOPRunner.cs \
        Assets/Scripts/Game/TickSystems/MatchStartSystem.cs
git commit -m "fix(match): 매치 시계를 출발 시점부터 세고 지각 입장자에게 출발틱을 준다"
```

---

## Task 6: 클라가 준비를 보내고 출발틱을 받는다

**Files:**
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Room/LOPRoom.cs`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/MatchStartState.cs`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/MatchStartMessageHandler.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/GameplayInstaller.cs`

**Interfaces:**
- Consumes: `MatchStartToC`, `MatchReadyToS` (Task 3) · `IWorld.GameplayStartTick` (Task 2)
- Produces:
  - `class LOP.MatchStartState` — `ReadOnlyReactiveProperty<long> StartTick`, `ReadOnlyReactiveProperty<int> ReadyCount`, `ReadOnlyReactiveProperty<int> TotalCount`, `void Update(long startTick, int readyCount, int totalCount)`
  - `StartTick`은 미정일 때 `long.MaxValue`(와이어의 -1을 여기서 바꾼다)

- [ ] **Step 1: 상태 홀더를 만든다**

`Assets/Scripts/Game/MatchStartState.cs`:

```csharp
using R3;

namespace LOP
{
    /// <summary>서버가 알려준 출발 예정과 준비 현황. 화면이 구독한다.</summary>
    public class MatchStartState
    {
        private readonly ReactiveProperty<long> startTick = new ReactiveProperty<long>(long.MaxValue);
        private readonly ReactiveProperty<int> readyCount = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<int> totalCount = new ReactiveProperty<int>(0);

        public ReadOnlyReactiveProperty<long> StartTick => startTick;
        public ReadOnlyReactiveProperty<int> ReadyCount => readyCount;
        public ReadOnlyReactiveProperty<int> TotalCount => totalCount;

        public void Update(long tick, int ready, int total)
        {
            startTick.Value = tick;
            readyCount.Value = ready;
            totalCount.Value = total;
        }
    }
}
```

`ReadOnlyReactiveProperty` 노출 방식은 코드베이스의 기존 R3 사용부와 맞춘다 — 다르면 그쪽을 따른다.

- [ ] **Step 2: 수신 핸들러를 만든다**

`Assets/Scripts/Game/MessageHandler/MatchStartMessageHandler.cs`:

```csharp
using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>출발 예정 틱을 월드에 꽂고 화면용 상태를 갱신한다.</summary>
    public class MatchStartMessageHandler : MessageHandlerBase
    {
        private readonly GameFramework.World.IWorld world;
        private readonly MatchStartState matchStartState;
        private readonly ISubscriber<MatchStartToC> subscriber;

        public MatchStartMessageHandler(
            GameFramework.World.IWorld world,
            MatchStartState matchStartState,
            ISubscriber<MatchStartToC> subscriber)
        {
            this.world = world;
            this.matchStartState = matchStartState;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(OnMatchStartToC));

        private void OnMatchStartToC(MatchStartToC message)
        {
            //  와이어의 -1(미정)을 월드가 쓰는 표현으로 바꾼다.
            long startTick = message.StartTick < 0 ? long.MaxValue : message.StartTick;

            world.GameplayStartTick = startTick;
            matchStartState.Update(startTick, message.ReadyCount, message.TotalCount);
        }
    }
}
```

- [ ] **Step 3: 등록한다**

`Assets/Scripts/Game/GameplayInstaller.cs`:

```csharp
            builder.RegisterEntryPoint<MatchStartMessageHandler>();
```
(다른 `RegisterEntryPoint<...MessageHandler>()` 줄들 옆에)

```csharp
            builder.Register<MatchStartState>(Lifetime.Singleton);
```
(`ReconciliationStats` 등록 줄 근처에)

- [ ] **Step 4: 준비를 보낸다**

`Assets/Scripts/Room/LOPRoom.cs`의 `Awake` 흐름 맨 끝:

```csharp
                await StartGameAsync();

                //  여기서 보내는 이유: 맵이 떴고, 내 새가 있고, 시계가 안정됐고, 러너가 돌고 있다.
                //  더 일찍 보내면 시계가 어긋난 채 출발선을 긋게 된다(WaitForClockSettleAsync 주석 참고).
                NetworkClient.Send(new CustomMirrorMessage { payload = new MatchReadyToS() });
```

`CustomMirrorMessage` 사용법은 같은 파일의 `JoinRoomServerAsync`를 그대로 따른다.

- [ ] **Step 5: 컴파일과 테스트**

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
```
기대: 실패 0, 598.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short          # Assets/Art, 폰트, ProjectSettings 픽스처가 섞이지 않았는지 확인
git add Assets/Scripts/Game/MatchStartState.cs Assets/Scripts/Game/MatchStartState.cs.meta \
        Assets/Scripts/Game/MessageHandler/MatchStartMessageHandler.cs Assets/Scripts/Game/MessageHandler/MatchStartMessageHandler.cs.meta \
        Assets/Scripts/Game/GameplayInstaller.cs Assets/Scripts/Room/LOPRoom.cs
git commit -m "feat(match): 클라가 준비를 알리고 출발틱을 받아 월드에 꽂는다"
```

---

## Task 7: 화면 — 가운데 큰 글자

**Files:**
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/UI/RaceStart/RaceStartViewModel.cs`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/UI/RaceStart/RaceStartView.cs`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/UI/RaceStart/RaceStartView.uxml`
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/UI/RaceStart/RaceStartView.uss`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/UI/UIViewCatalog.asset`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/FlappyHudCoordinator.cs`

**Interfaces:**
- Consumes: `MatchStartState` (Task 6)
- Produces: `RaceStartView : UIView` (`Layer => UILayer.Notification`)

> **여기는 자동 검증이 없다 — 의도된 것이다.** UI Toolkit View는 EditMode로 못 잡는다(직전
> 슬라이스에서 574개가 전부 초록인데 유령 연출이 런타임에 통째로 무효였던 그 공백). 그렇다고
> **테스트하려고 코드를 다른 어셈블리로 옮기지 않는다.**
>
> 대신 여기 있는 것이 무엇인지를 본다: 시뮬을 가르는 판단(`tick >= GameplayStartTick`)은 이미
> 월드에 있고 Task 2에서 테스트됐다. 이 파일에 남은 건 **카운트다운 숫자와 문구 고르기** —
> 틀리면 화면에 "2, 1, 0"이 뜨는 연출 문제이고, 태스크 8에서 눈으로 3-2-1을 확인할 때 바로 드러난다.
> 그 정도 위험에 어셈블리를 하나 건너거나 새로 만들 이유가 없다.

- [ ] **Step 1: ViewModel을 만든다**

`Assets/Scripts/UI/RaceStart/RaceStartViewModel.cs`:

```csharp
using System;

namespace LOP.UI
{
    /// <summary>대기 인원과 카운트다운을 화면 문구로 바꾼다.</summary>
    public class RaceStartViewModel
    {
        private readonly MatchStartState state;
        private readonly GameFramework.Runner.IRunner runner;

        public RaceStartViewModel(MatchStartState state, GameFramework.Runner.IRunner runner)
        {
            this.state = state;
            this.runner = runner;
        }

        /// <summary>지금 화면에 띄울 문구. 빈 문자열이면 아무것도 안 띄운다.</summary>
        public string CurrentText()
        {
            long startTick = state.StartTick.CurrentValue;
            if (startTick == long.MaxValue)
            {
                return $"{state.ReadyCount.CurrentValue} / {state.TotalCount.CurrentValue} 대기 중";
            }

            long remainingTicks = startTick - runner.tickUpdater.tick;
            if (remainingTicks <= 0)
            {
                return "GO!";
            }

            //  올림이라야 "3, 2, 1"이 각각 1초씩 보인다. 내림이면 3이 한 순간만 스친다.
            return ((int)Math.Ceiling(remainingTicks * runner.tickUpdater.interval)).ToString();
        }
    }
}
```

`IRunner`는 클라 `GameLifetimeScope`가 `builder.RegisterComponent(runner).As<IRunner>().AsSelf()`로
등록하므로 이 스코프에서 resolve된다.

`IRunner`를 이 스코프에서 resolve할 수 있는지 확인한다 — 못 하면 `ITickUpdater`를 직접 주입한다
(서버 `GameLifetimeScope`가 `builder.Register<ITickUpdater>(...)`로 등록하는 것과 같은 배선이
클라에도 있는지 볼 것).

- [ ] **Step 2: View를 만든다**

`Assets/Scripts/UI/RaceStart/RaceStartView.cs`:

```csharp
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 출발 전 안내를 화면 가운데 큰 글자로 띄운다. 아래 입력을 막지 않는다 —
    /// 카운트다운 중 탭은 월드가 어차피 무시하므로 굳이 차단할 이유가 없다.
    /// </summary>
    public class RaceStartView : UIView
    {
        private readonly RaceStartViewModel _viewModel;

        private Label _text;
        private IVisualElementScheduledItem _tick;

        public RaceStartView(RaceStartViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Notification;

        public override void OnOpen()
        {
            base.OnOpen();

            _text = Root.Q<Label>("race-start-text");

            //  UIView에는 Update가 없다 — 패널 스케줄러로 매 프레임 값을 가져온다
            //  (카운트다운은 변경 이벤트가 없는 샘플링 값이라 DebugHudView와 같은 방식).
            _tick = Root.schedule.Execute(_ =>
            {
                string text = _viewModel.CurrentText();
                _text.text = text;
                Root.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
            }).Every(0);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed == false)
            {
                _disposed = true;
                _tick?.Pause();
                _tick = null;
            }

            base.Dispose(disposing);
        }
    }
}
```

`Dispose` 형태는 `FlapPadView`를 그대로 따른다(먼저 자기 플래그로 가드, 끝에 `base` 호출).

- [ ] **Step 3: uxml과 uss를 만든다**

`Assets/UI/RaceStart/RaceStartView.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="race-start-root" class="race-start-root" picking-mode="Ignore">
        <ui:Label name="race-start-text" class="race-start-text" text="" picking-mode="Ignore" />
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/RaceStart/RaceStartView.uss`:

```css
.race-start-root {
    position: absolute;
    left: 0;
    right: 0;
    top: 0;
    bottom: 0;
    align-items: center;
    justify-content: center;
}

.race-start-text {
    font-size: 120px;
    -unity-font-style: bold;
    color: rgb(255, 255, 255);
    -unity-text-outline-width: 3px;
    -unity-text-outline-color: rgba(0, 0, 0, 0.7);
}
```

`picking-mode="Ignore"`가 **꼭 필요하다** — 전체화면 요소라 안 두면 아래 `FlapPadView`의 탭을 먹는다.

- [ ] **Step 4: Unity에 임포트시켜 `.meta`를 만든다**

```bash
export PATH="$HOME/.unity/bin:$PATH"
unity command import_asset --path Assets/UI/RaceStart --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
```

(`import_asset`의 정확한 인자는 `unity list --project-path ... | grep import`로 확인한다.)

- [ ] **Step 5: 카탈로그에 등록한다**

`UIViewCatalog`는 View 타입 이름 → uxml 매핑을 들고 있는 ScriptableObject다. 여기 없으면
`windowManager.Open<RaceStartView>()`가 **조용히 아무것도 안 한다.**

GUID를 읽어서:

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
grep guid Assets/UI/RaceStart/RaceStartView.uxml.meta
grep guid Assets/UI/RaceStart/RaceStartView.uss.meta
```

`Assets/UI/UIViewCatalog.asset`의 `entries:` 목록 끝에 추가한다. `fileID`는 다른 항목과 **같은 상수**를 쓴다:

```yaml
  - viewName: RaceStartView
    uxml: {fileID: 9197481963319205126, guid: <uxml의 guid>, type: 3}
    uss: {fileID: 7433441132597879392, guid: <uss의 guid>, type: 3}
```

- [ ] **Step 6: DI와 열기 배선**

`Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`의 `ConfigureGame`:

```csharp
            builder.Register<RaceStartViewModel>(Lifetime.Transient);
            builder.Register<RaceStartView>(Lifetime.Transient);
```

같은 파일의 `RegisterViewFactories`:

```csharp
            sink.Add(windowManager.RegisterViewFactory<RaceStartView>(() => container.Resolve<RaceStartView>()));
```

`Assets/Scripts/Game/FlappyHudCoordinator.cs`의 `OnEntityCreated`에서 `DebugHudView`를 여는 줄 다음:

```csharp
            windowManager.Open<RaceStartView>();
```

- [ ] **Step 7: 컴파일과 테스트**

```bash
unity command recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
unity command run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --no-banner
```
기대: 실패 0, 598.

- [ ] **Step 8: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/UI/RaceStart Assets/UI/RaceStart Assets/UI/UIViewCatalog.asset \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs Assets/Scripts/Game/FlappyHudCoordinator.cs
git commit -m "feat(ui): 대기 인원과 카운트다운을 화면 가운데 띄운다"
```

---

## Task 8: 로컬 2인 리그로 실제 확인 + 기록

이 슬라이스의 목적이 여기서 판정된다. **자동 테스트가 아니라 사람이 보는 단계**다.

**Files:**
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/docs/ROADMAP.md`
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/docs/superpowers/specs/2026-08-25-flappy-race-start-gate-design.md` (실측 결과 절 추가)

**Interfaces:**
- Consumes: 전부
- Produces: (문서)

- [ ] **Step 1: 사전조건을 확인한다**

```bash
# 클라·서버 둘 다 env가 local 인지
grep -n "useLocalRoomInstance" /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Resources/EnvironmentSettings/EnvironmentSettings.local.asset
# → 1 이어야 한다 (방 접속만 에디터 서버로 우회)

# 서버 픽스처에 두 계정 uuid가 있는지
grep -n "playerList" -A 5 /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs
```

**픽스처를 고쳐야 하면 사용자에게 보고한다.** 의도적으로 커밋하지 않는 사용자 소유 파일이다.

- [ ] **Step 2: 실행 순서**

1. 서버 에디터 재생 → 콘솔에서 대기 로그 확인
2. 클라 메인 에디터 재생 → 준비 인원이 1로 늘어남
3. **일부러 1분 이상 기다린 뒤** MPPM 가상 플레이어(Player 2) 재생 → 2/2 → 카운트다운 → GO

**확인할 것:**

| 무엇 | 기대 |
|---|---|
| 두 번째 클라가 붙기 전 | 새가 출발선에서 **가만히** 있다(떨어지지 않는다) |
| 대기 표시 | `1 / 2 대기 중` → `2 / 2 대기 중` |
| 카운트다운 | `3` → `2` → `1` → `GO!` 후 사라짐 |
| 출발 | 두 새가 **동시에** 움직이기 시작한다 |
| 1분 기다린 뒤에도 | 판이 끝나 있지 않다(매치 시계가 출발 기준) |

- [ ] **Step 3: 이어서 직전 슬라이스의 눈 검증을 한다**

리그가 서면 `feature/flappy-ghost-extrapolation`의 미완 검증을 같은 판에서 끝낸다:

| 확인 | 기대 |
|---|---|
| 유령정지(G2) | 맵에 부딪힌 새가 **청회색으로 변하고** 그 자리에 멈춘다 |
| 원격 새(G3) | 남의 새가 **순간이동이 아니라 매끄럽게** 움직인다 |
| 낑김 | 새끼리 겹쳤을 때 **카메라가 진동하지 않는다** |

**결과를 있는 그대로 기록한다.** 안 고쳐진 것이 있으면 고쳐졌다고 쓰지 않는다.

- [ ] **Step 4: 스펙에 실측 결과를 남긴다**

`docs/superpowers/specs/2026-08-25-flappy-race-start-gate-design.md` 끝에 `## 14. 실측 결과 (검증한 날짜)` 절을 만들고 위 표의 실제 관측을 적는다. 못 한 검증이 있으면 **못 했다고** 적는다.

- [ ] **Step 5: 로드맵을 갱신한다**

`docs/ROADMAP.md`의 "🟡 Flappy 유령정지 + 원격 외삽 — 코드 완료, 머지 대기" 절을 실제 상태로 고친다.
눈 검증이 통과했다면 머지 조건이 풀린 것이고, 안 됐다면 무엇이 남았는지 적는다.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add docs/ROADMAP.md docs/superpowers/specs/2026-08-25-flappy-race-start-gate-design.md
git commit -m "docs: 시작 게이트 실측 결과와 남은 일을 기록한다"
```

---

## 머지

**이 계획은 머지를 포함하지 않는다.** 8개 태스크가 끝나고 Task 8의 눈 검증 결과를 사용자가 본 뒤에
결정한다. 머지할 때는 `CLAUDE.md`의 "푸시 규약"을 **레포마다 한 줄씩** 밟는다:

```bash
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff <feature>
git push origin main
```

`feature/flappy-race-start-gate`는 `feature/flappy-ghost-extrapolation` 위에 있으므로 **유령 브랜치가
먼저** 올라가야 한다. `git push --force`는 어떤 경우에도 금지다.
