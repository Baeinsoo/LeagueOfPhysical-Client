# Skydive 슬라이스 3 — 자세가 보이고, 도착하면 끝난다

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 떨어지는 동안 **자세가 눈에 보이고**, 바닥에 닿으면 **판이 끝나며 등수가 나온다.**

**Architecture:** 자세→기울기는 **뷰 레이어만** 건드린다(시뮬·스냅샷·롤백 무변경). 결승은 판치기가 이미 쓰는 짝 구조를 그대로 따른다 — 순수 판정기(Shared) + 매 틱 도는 `ITickSystem`(서버)이 통과 순서를 기록하고, `IGameRuleSystem`이 그걸 읽어 종료·등수를 답한다.

**Tech Stack:** Unity 6.3, VContainer, GameFramework World Core, Mirror

**Spec:** `docs/superpowers/specs/2026-08-30-skydive-game-mode-design.md` (§3.6 체크포인트·결승, §8 슬라이스)

## 이 슬라이스가 생긴 이유

슬라이스 2를 실플레이한 사용자가 두 가지를 보고했고, 둘 다 실측으로 확인됐다:

1. **"뭐가 바뀌는지를 모르겠는데"** — `Bird.prefab`에는 **Animator가 아예 없다**(정적 메시). 게다가 `LOPEntityView.UpdateRunAnimation`은 접지 상태를 요구하는데 낙하 중엔 항상 false다. 그래서 세 자세가 화면에 **전혀** 드러나지 않는다. 슬라이스 2의 존재 이유가 "떨어지는 게 재밌나?"를 판정하는 것인데, 판정 자체가 불가능한 상태였다.
2. **"바닥 도착 후 너무 오래 기다려야 해"** — 코스가 **200m**뿐이라 대자 25m/s면 8초, 다이브 45m/s면 4.4초에 닿는다. 그 뒤 제한시간 60초까지 서 있게 된다. 스펙이 스스로 정한 "한 판 30~60초"(§3.6)와도 어긋난다.

## Global Constraints

- **결정론은 클·서가 같은 구체 클래스를 컴파일하는 데서 온다.** 시뮬 로직은 `LeagueOfPhysical-Shared`에 인터페이스 seam 없이 구체 타입으로 둔다. 인터페이스는 사이드가 달라야 하는 I/O 어댑터에만.
- **한국어 주석, 쉬운 말로.** 코드로 자명한 것은 적지 않고 **비자명한 의도(왜)**만 짧게. 전문용어를 설명 없이 던지지 않는다.
- **`GameFramework.World.Component`는 `UnityEngine.Component`와 이름이 겹친다.** 클라·서버 파일은 `using GameFramework.World;`를 **추가하지 않고** 항상 풀 네임스페이스로 한정한다. Shared 패키지 내부 파일은 해당 없음.
- **다른 게임 모드를 건드리지 않는다.** Flappy Race / FlapWang / 판치기가 `LOPEntityView`·`FinishLine`·`IGameRuleSystem`을 공유한다. 컴포넌트가 없는 엔티티에서는 조용히 no-op이어야 한다.
- **커밋은 바꾼 파일만 경로로 지정한다.** `git add -A` 금지 — 이 Unity 레포들엔 늘 의도적인 로컬 픽스처가 있고, 쓸어 담아 커밋해서 main을 깨뜨린 적이 있다.
- **`run_tests` 전에 `recompile_status`의 `errors`가 빈 것을 반드시 확인한다.** 컴파일 에러 상태에서 테스트를 돌리면 에디터 메인 스레드가 물려 재시작 외엔 복구가 안 된다.
- **테스트 판정은 개수가 아니라 이름으로 한다.** 낡은 결과가 "전부 통과"로 보일 수 있다.
- 브랜치 `feature/skydive-slice3`. 머지·푸시는 사용자 승인 후에만.

---

## Task 1: 도착 판정기 (순수 로직)

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveProgress.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/FinishLine.cs` (주석만)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveProgressTests.cs`

**Interfaces:**
- Consumes: (없음 — 순수 C#)
- Produces: `LOP.SkydiveProgress` — `SkydiveProgress(float finishY)`, `bool HasFinished(float y)`, `bool AllFinished(IReadOnlyList<float> ys)`

### 왜 별도 클래스인가

`FlappyRaceProgress`와 **같은 자리, 반대 축**이다. Flappy는 +x로 달려서 `x >= finishX`, Skydive는 아래로 떨어져서 `y <= finishY`다. 한 클래스에 축을 매개변수로 넣지 않는 이유는 두 게임의 "진행"이 앞으로 갈라질 것이기 때문이다(Skydive엔 체크포인트 복귀가 붙는다).

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Tests/EditMode/SkydiveProgressTests.cs`:

```csharp
using NUnit.Framework;
using System.Collections.Generic;

namespace LOP.Tests
{
    public class SkydiveProgressTests
    {
        [Test]
        public void 결승고도보다_아래면_통과다()
        {
            var progress = new SkydiveProgress(10f);
            Assert.IsTrue(progress.HasFinished(9.9f));
        }

        [Test]
        public void 결승고도에_정확히_있으면_통과다()
        {
            var progress = new SkydiveProgress(10f);
            Assert.IsTrue(progress.HasFinished(10f));
        }

        [Test]
        public void 결승고도보다_위면_아직이다()
        {
            var progress = new SkydiveProgress(10f);
            Assert.IsFalse(progress.HasFinished(10.1f));
        }

        [Test]
        public void 전원이_내려와야_전원완주다()
        {
            var progress = new SkydiveProgress(10f);
            Assert.IsTrue(progress.AllFinished(new List<float> { 5f, 9f }));
            Assert.IsFalse(progress.AllFinished(new List<float> { 5f, 11f }));
        }

        [Test]
        public void 아무도_없으면_전원완주가_아니다()
        {
            // 스폰 직전(몸이 아직 없을 때) "전원 완주"로 끝내면 시작하자마자 판이 끝난다.
            var progress = new SkydiveProgress(10f);
            Assert.IsFalse(progress.AllFinished(new List<float>()));
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는 것을 확인한다** — `SkydiveProgress`가 없어 컴파일 실패면 된다.

- [ ] **Step 3: `SkydiveProgress`를 만든다**

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Skydive의 완주 판정. 물리도 엔티티도 모르고 <b>y 좌표만</b> 받아 답한다.
    /// 코스가 아래 한 방향이고 떨어진 사람은 다시 올라가지 않아서 한 축이면 충분하다.
    /// (<see cref="FlappyRaceProgress"/>와 같은 자리, 반대 축이다.)
    /// </summary>
    public class SkydiveProgress
    {
        private readonly float finishY;

        public SkydiveProgress(float finishY)
        {
            this.finishY = finishY;
        }

        /// <summary>결승 고도에 정확히 있는 것도 통과로 본다 — 선을 밟은 순간이 통과다.</summary>
        public bool HasFinished(float y) => y <= finishY;

        /// <summary>
        /// 남아 있는 사람 전원이 내려왔나. <b>비어 있으면 false</b> — 아무도 없는 판을
        /// "전원 완주"로 끝내면 스폰 직전에 판이 끝난다.
        /// </summary>
        public bool AllFinished(IReadOnlyList<float> ys)
        {
            if (ys.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < ys.Count; i++)
            {
                if (HasFinished(ys[i]) == false)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
```

- [ ] **Step 4: `FinishLine` 주석을 고친다**

이 마커는 이제 게임이 둘이고 **읽는 축이 다르다.** 현재 주석은 "판정은 x 한 축만 본다"라고 단언하는데 그건 이제 거짓이다. 그 문장을 바꿔라 — 어느 축을 읽을지는 **게임 룰이 정하고** 마커는 좌표만 제공한다는 뜻으로. Flappy는 x, Skydive는 y를 읽는다는 것을 예로 적어라.

- [ ] **Step 5: 테스트를 돌려 통과를 확인하고 커밋한다**

```bash
git add Runtime/Scripts/Game/SkydiveProgress.cs Runtime/Scripts/Game/SkydiveProgress.cs.meta Runtime/Scripts/Game/FinishLine.cs Tests/EditMode/SkydiveProgressTests.cs Tests/EditMode/SkydiveProgressTests.cs.meta
git commit -m "feat(skydive): 바닥 도착을 판정하는 순수 로직을 더한다"
```

---

## Task 2: 통과 순서를 기록하는 틱 시스템 (서버)

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/SkydiveFinishSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.SkydiveProgress`(Task 1), `GameFramework.World.EntityRegistry`, `GameFramework.Runner.ITickSystem`(`void Tick(long tick, float deltaTime)`), `LOP.FinishLine`
- Produces: `LOP.SkydiveFinishSystem` —
  - `void Watch(string entityId)` (룰이 스폰하며 등록)
  - `void Reset()`
  - `IReadOnlyList<string> FinishedOrder { get; }` (먼저 도착한 순, 엔티티 id)
  - `bool AllWatchedFinished { get; }`

### 왜 룰 시스템이 아니라 별도 틱 시스템인가

`IGameRuleSystem`에는 틱이 없다. `IsMatchOver`는 **속성**이고 러너가 매 프레임 읽지만, 그 getter 안에서 순서를 기록하면 "값을 묻는 것"이 상태를 바꾸게 되어 폴링 빈도에 정답이 딸려간다. 판치기가 이미 같은 문제를 **`PanchigiRuleSystem`(룰) + `PanchigiTurnSystem`(틱)** 짝으로 풀었으므로 그 모양을 그대로 따른다.

- [ ] **Step 1: `SkydiveFinishSystem`을 만든다**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 누가 먼저 바닥에 닿았는지 매 틱 지켜보고 순서를 적어 둔다.
    /// 룰(<see cref="SkydiveRuleSystem"/>)이 이걸 읽어 종료와 등수를 답한다 — 순서를 세는 일과
    /// 판을 끝내는 일을 나눈 이유는 룰에는 틱이 없어서다(판치기의 룰/턴 짝과 같은 구조).
    /// </summary>
    public class SkydiveFinishSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private readonly List<string> watched = new List<string>();
        private readonly List<string> finishedOrder = new List<string>();
        private readonly HashSet<string> finishedSet = new HashSet<string>();

        // 결승 고도는 맵이 정한다. 맵 씬은 나중에 로드되므로 생성자에서 찾으면 못 찾는다 —
        // 첫 틱까지 미뤘다가 그때 한 번만 찾는다.
        private SkydiveProgress progress;

        public SkydiveFinishSystem(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public IReadOnlyList<string> FinishedOrder => finishedOrder;

        public void Watch(string entityId) => watched.Add(entityId);

        public void Reset()
        {
            watched.Clear();
            finishedOrder.Clear();
            finishedSet.Clear();
            progress = null;
        }

        public void Tick(long tick, float deltaTime)
        {
            EnsureProgress();

            for (int i = 0; i < watched.Count; i++)
            {
                string entityId = watched[i];
                if (finishedSet.Contains(entityId))
                {
                    continue;   // 등수는 처음 통과한 순간이 정답이다
                }

                // 나간 사람의 몸은 이미 없다
                var entity = entityRegistry.Get(entityId);
                if (entity == null)
                {
                    continue;
                }

                if (progress.HasFinished(entity.Get<GameFramework.World.Transform>().Position.Y))
                {
                    finishedOrder.Add(entityId);
                    finishedSet.Add(entityId);
                }
            }
        }

        /// <summary>
        /// 남아 있는 사람이 전원 내려왔나. <b>아무도 없으면 false</b> — 스폰 직전에 판이 끝나는 것을 막는다.
        /// </summary>
        public bool AllWatchedFinished
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < watched.Count; i++)
                {
                    if (entityRegistry.Get(watched[i]) == null)
                    {
                        continue;   // 나간 사람은 세지 않는다. 세면 한 명 나간 판이 절대 안 끝난다
                    }
                    alive++;
                    if (finishedSet.Contains(watched[i]) == false)
                    {
                        return false;
                    }
                }
                return alive > 0;
            }
        }

        private void EnsureProgress()
        {
            if (progress != null)
            {
                return;
            }

            var markers = UnityEngine.Object.FindObjectsByType<FinishLine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (markers.Length == 1)
            {
                progress = new SkydiveProgress(markers[0].transform.position.y);
                return;
            }

            // Flappy는 같은 상황에서 예외를 던지지만 여기서는 판이 이미 굴러가는 중이다 —
            // 던지면 방 전체가 죽으므로, 크게 알리고 바닥을 결승선으로 삼아 판은 끝나게 둔다.
            Debug.LogError($"[Skydive] 맵에 FinishLine 마커가 정확히 하나 있어야 한다 (발견: {markers.Length}개). 바닥(y=0)을 결승선으로 쓴다");
            progress = new SkydiveProgress(0f);
        }
    }
}
```

주의: `EnsureProgress`가 마커를 못 찾아 폴백을 쓴 경우에도 `progress`가 채워지므로 **매 틱 다시 찾지 않는다.** 맵이 로드되기 전에 첫 틱이 돌면 폴백에 갇힐 수 있으니, 첫 틱이 맵 로드 뒤인지 확인하라 — 아니라면 `progress`를 마커를 찾은 경우에만 채우고 그 전까지는 판정을 건너뛰도록 바꿔라.

- [ ] **Step 2: 스코프에 등록한다**

`SkydiveLifetimeScope.ConfigureGame`에 판치기와 **같은 모양**으로:

```csharp
            builder.Register<SkydiveFinishSystem>(Lifetime.Singleton);
            // 도착 감시를 러너의 End 페이즈에 문다. 시스템이 스스로 IRunner를 잡으면
            // 러너→룰→도착→러너로 고리가 생겨 컨테이너가 아예 안 만들어진다.
            builder.RegisterBuildCallback(container =>
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(container.Resolve<SkydiveFinishSystem>()));
```

- [ ] **Step 3: 서버 컴파일을 확인하고 커밋한다**

```bash
unity command recompile --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
unity command recompile_status --project-path C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
```

`status`가 `completed`이고 `errors`가 비어야 한다. `up_to_date`는 **재컴파일을 안 한 것**이다.

---

## Task 3: 룰이 도착으로 끝내고 등수를 답한다 (서버)

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveRuleSystem.cs`

**Interfaces:**
- Consumes: `LOP.SkydiveFinishSystem`(Task 2)
- Produces: (없음 — 기존 `IGameRuleSystem` 구현을 채운다)

- [ ] **Step 1: 몸을 사람으로 바꾼다**

`BodyVisualId`를 `"Assets/Art/Characters/Knight/Knight.prefab"`으로 바꾼다.

주석도 함께 고쳐라. 현재 주석은 *"자세가 생기는 슬라이스 2에서 자세별 애니메이션이 있는 몸으로 바꾼다"* 라고 약속하고 있고, 지금이 그 시점이다. 새 주석에는 **왜 새를 버렸는지**를 남겨라: `Bird.prefab`에는 Animator가 없어 어떤 포즈도 취할 수 없고, Knight는 리그가 있어 지금은 기울기만 쓰지만 나중에 진짜 스카이다이빙 클립을 얹을 자리가 된다.

- [ ] **Step 2: 스폰할 때 도착 감시에 등록한다**

생성자에 `SkydiveFinishSystem`을 받고, `Initialize`의 스폰 루프에서 `finishSystem.Watch(entityId)`를 부른다. `Deinitialize`에서 `finishSystem.Reset()`.

같은 루프에서 **entityId → userId 대응표**(`Dictionary<string, string>`)도 기록해 두어라 — Step 4가 도착 순서(엔티티 id)를 등수(userId)로 옮길 때 필요하다.

- [ ] **Step 3: `IsMatchOver`를 채운다**

```csharp
        /// <summary>남아 있는 사람이 전원 바닥에 닿으면 끝난다. 시간 상한은 러너가 따로 본다.</summary>
        public bool IsMatchOver => finishSystem.AllWatchedFinished;
```

- [ ] **Step 4: `ResolveOutcome`을 실제 등수로 바꾼다**

무작위 셔플을 **지운다.** 순서 규칙:

1. **먼저 도착한 순서**대로 1등부터. `finishSystem.FinishedOrder`를 대응표로 userId에 옮긴다.
2. **시간 상한으로 끝난 판에는 도착 못 한 사람이 남는다.** 도착자 뒤에 붙이되 **더 낮게 내려간 사람이 앞**이다(현재 y 오름차순). 그러지 않으면 "가만히 있던 사람"과 "거의 다 온 사람"이 같은 등수가 된다.
3. 몸이 사라진 사람(나간 사람)은 **맨 뒤**로 보낸다.

`placement`는 1부터 연속으로 매긴다.

- [ ] **Step 5: `MatchDurationTicks` 주석을 고친다**

값 `3000`(50Hz × 60초)은 **그대로 둔다.** 다만 주석의 근거가 낡았다("200m를 40m/s 상한으로 떨어지면 10초 남짓"). Task 5에서 코스가 1000m가 되므로 새 근거로 바꿔라: 다이브 45m/s면 22초, 대자 25m/s면 40초라 60초 안에 들어오고, **패러세일만 붙들고 있으면 1000/6 ≈ 166초가 걸려 시간 상한에 걸린다 — 그게 활공의 대가다.**

- [ ] **Step 6: 서버 컴파일 확인 후 커밋한다**

---

## Task 4: 자세가 눈에 보인다 (클라)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/LOPEntityView.cs`

**Interfaces:**
- Consumes: `LOP.Posture`(`float Axis`, `bool Gliding`)
- Produces: (없음 — 뷰 내부)

### 왜 뷰의 자식만 기울이나

기울기는 **연출이지 시뮬이 아니다.** `World.Transform.Rotation`에 넣으면 스냅샷과 롤백에 딸려 들어가 넷코드가 실어 나를 값이 하나 늘어난다. 그래서 **엔티티가 아니라 비주얼 자식 오브젝트의 로컬 회전**만 만진다. 시뮬·와이어 변경 0.

이 파일이 이미 쓰는 방식(**컴포넌트가 있으면 읽고 없으면 no-op**)을 그대로 따른다 — `UpdateRunAnimation`이 Animator 없으면 빠지고 `UpdateAbilityAnimation`이 `Abilities` 없으면 빠지는 것과 같다. 다른 게임 모드엔 `Posture`가 없으므로 자동으로 아무 일도 안 한다.

- [ ] **Step 1: 각도 상수를 정한다**

```csharp
        // 자세는 속도만 바꿔서는 눈에 안 보인다 — 실루엣으로 읽히게 몸을 기울인다.
        // 젤다의 세 자세를 각도로 옮긴 것: 대자는 배를 살짝 아래로, 다이브는 머리부터 수직,
        // 패러세일은 매달린 것처럼 뒤로 눕는다. 셋이 서로 확실히 다른 각도여야 구분된다.
        private const float SpreadPitch = 25f;
        private const float DivePitch = 85f;
        private const float GlidePitch = -15f;

        // 기울기가 붙는 속도(초당 도). 자세는 즉시 바뀌어도 몸은 따라가는 데 시간이 걸리는 게
        // 자연스럽고, 입력이 튈 때 몸이 덜덜거리는 것도 막는다.
        private const float PitchDegreesPerSecond = 360f;
```

- [ ] **Step 2: `UpdatePostureTilt`를 더하고 갱신 루프에서 부른다**

`UpdateRunAnimation(); UpdateAbilityAnimation();` 옆에 `UpdatePostureTilt();`를 더한다. 동작:

1. `entityId`/`visualGameObject`가 없으면 반환.
2. `entityRegistry.Get(entityId)?.Get<Posture>()`가 null이면 반환(다른 모드).
3. 목표 각도 = `Gliding`이면 `GlidePitch`, 아니면 `Mathf.Lerp(SpreadPitch, DivePitch, Axis)`.
4. 현재 각도를 `Mathf.MoveTowards`로 목표까지 `PitchDegreesPerSecond * Time.deltaTime`만큼 옮긴다. **현재 각도는 필드에 들고 있어라** — `visualGameObject.transform.localEulerAngles.x`에서 되읽으면 오일러 각이 0~360으로 정규화돼 음수(`GlidePitch = -15`)가 345로 튀어 몸이 한 바퀴 돈다.
5. `visualGameObject.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);`

`visualGameObject`가 교체될 때(`UpdateVisual`) 들고 있던 각도를 **`SpreadPitch`로 되돌려라** — 안 그러면 새 모델이 옛 각도를 물려받아 한 프레임 어긋난 자세로 나타난다.

- [ ] **Step 3: 스펙에 이번 결정을 적는다**

`docs/superpowers/specs/2026-08-30-skydive-game-mode-design.md` §2에 짧은 항목을 더한다. 스펙은 세 자세를 **숫자로만** 정의하고 있고 화면에 어떻게 보이는지는 한 줄도 없다 — 그래서 슬라이스 2가 "보이지 않는 자세"를 만들고도 계획대로 끝났다. 같은 일이 반복되지 않게 다음을 남겨라:

- 자세는 **몸의 기울기**로 보인다(대자 25° / 다이브 85° / 패러세일 −15°). 시뮬이 아니라 뷰다.
- 몸은 `Knight.prefab`이고, 새(`Bird.prefab`)는 Animator가 없어 버렸다.
- 기울기는 **자리표시**다 — 리그가 있으므로 나중에 진짜 스카이다이빙 클립으로 승격한다.

§11 Open Decisions의 **낙하 카메라 항목은 그대로 둔다** — 이 슬라이스에서 하지 않는다.

- [ ] **Step 4: 클라 컴파일을 확인하고 커밋한다**

이 태스크는 눈으로 봐야 하는 것이라 자동 테스트를 만들지 않는다. **대신 리뷰가 확인할 것**: `World.Transform.Rotation`을 건드리지 않았는가, `Posture` 없는 엔티티에서 no-op인가, 각도를 트랜스폼에서 되읽지 않는가.

---

## Task 5: 코스를 1000m로 세우고 결승선을 찍는다 (맵)

**Files:**
- Modify: `LeagueOfPhysical-Art/Scenes/SkydiveMap.unity` (**아트 레포**)
- Modify: `LeagueOfPhysical-Client/Assets/Art` (서브모듈 포인터)

### 반드시 알아야 할 것

**아트 체크아웃이 두 개다.** `LOP/LeagueOfPhysical-Art`(독립 클론)와 `LOP/LeagueOfPhysical-Client/Assets/Art`(서브모듈)가 서로 다른 커밋일 수 있다. **에디터가 여는 것은 서브모듈 쪽**이다. 독립 클론에만 고치면 에디터가 영영 못 본다. Art 레포 내부 경로는 `Scenes/…`이지 `Assets/Art/Scenes/…`가 아니다.

- [ ] **Step 1: 스폰 높이를 올린다**

`SkydiveMap.unity`의 `SpawnPoint`들의 y를 **200 → 1000**으로 바꾼다. 씬 저작은 손편집 말고 `unity command`로 하는 편이 안전하다(`set_serialized_field` / `eval` + `EditorSceneManager.SaveScene`). x·z 간격은 그대로 둔다.

- [ ] **Step 2: `FinishLine` 마커를 바닥에 찍는다**

빈 GameObject를 만들고 `FinishLine` 컴포넌트를 붙인 뒤 **y = 2** 정도에 둔다. 바닥(y=0)에 정확히 두지 않는 이유: 판정이 발밑이 아니라 **몸 원점**의 y를 보므로, 원점이 바닥에 닿기 전에 캡슐이 이미 지면에 서 버린다. 몸 높이의 절반쯤 위에 두면 "닿았다"와 "통과했다"가 어긋나지 않는다. `SkydiveConfig.BodyHeight`를 보고 맞춰라.

- [ ] **Step 3: 아트 레포에 커밋하고 main에 올린다**

`CLAUDE.md`의 푸시 규약을 따른다. **이 커밋이 main에 올라가야** 다음 단계가 가능하다.

- [ ] **Step 4: 클라의 서브모듈 포인터를 그 SHA로 올린다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Art
git fetch origin && git checkout <아트 main SHA>
cd ../..
git add Assets/Art
git commit -m "chore(art): Skydive 코스를 1000m로 올린 맵을 가리킨다"
```

`git add`는 **이 경로만.** 다른 픽스처를 쓸어 담지 말 것.

**포인터 커밋이 없으면 CI가 새 맵을 못 본다** — `content-deploy`는 CI가 클라 레포를 체크아웃해 돌리므로, 포인터가 안 올라가면 새 맵이 빌드 머신에 **존재하지 않고** 번들이 옛 내용으로 나간다.

- [ ] **Step 5: 맵이 실제로 바뀌었는지 확인한다**

**서브모듈 쪽 경로**(`Assets/Art/Scenes/SkydiveMap.unity`)에서 y 좌표를 다시 읽어 1000이 들어갔는지, `FinishLine`이 정확히 하나인지 확인한다.

---

## Task 6: 머지·배포·실플레이

**사용자 승인 후에만 실행한다.**

- [ ] **Step 1: 4레포 컴파일·테스트 최종 확인** (Art·Shared·Client·Server)

- [ ] **Step 2: 규약대로 머지·푸시** — `CLAUDE.md`의 푸시 규약이 유일한 기준이다(force push 금지).

- [ ] **Step 3: `content-deploy` (클라 레포)** — **맵이 바뀌었으므로 필수다.** 빠뜨리면 서버가 옛 200m 맵을 계속 받아 클라와 코스가 어긋난다.

- [ ] **Step 4: `gameserver-deploy` (environment=local)** — 서버 코드가 바뀌었다.

- [ ] **Step 5: 배포가 실제로 반영됐는지 확인한다**

워크플로 success만 믿지 마라. 지난 슬라이스에서 **ArgoCD가 `Synced Healthy`인데 태그 bump 커밋을 못 본 상태**였다. 확인 순서:

1. infrastructure의 `GAME_SERVER_IMAGE` 태그 == 서버 main SHA
2. ArgoCD `Application`의 `status.sync.revision` == 그 bump 커밋 (아니면 `refresh=hard`)
3. room-server 파드 안에서 `printenv GAME_SERVER_IMAGE` — **파드를 이름으로 지정할 것.** 종료 중 파드도 `phase=Running`이라 `--field-selector`로는 옛 파드가 잡힌다.
