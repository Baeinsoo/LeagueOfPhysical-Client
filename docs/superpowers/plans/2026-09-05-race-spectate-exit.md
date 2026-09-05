# 관전·나가기 UI 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Flappy Race에서 완주·탈락한 뒤 다른 사람을 골라 보고, 판을 떠날 수 있게 한다.

**Architecture:** "지금 누구를 보는가"를 부품 하나(`FlappySpectate`)가 소유해 자동 추적과 수동
선택이 카메라를 두고 다투지 않게 한다. 조작은 기존 두 문구 화면과 분리된 세 번째 화면
(`RaceSpectateView`)에 둔다. 나가기는 앱 상태 머신에 `MatchLeft`를 더해 기존 로비 복귀 체인을
사람이 당기게 하는 것이다.

**Tech Stack:** Unity 6, UI Toolkit(UXML/USS), VContainer, UniTask, NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-05-race-spectate-exit-design.md`

## Global Constraints

- **레포는 클라 하나**(`LeagueOfPhysical-Client`)다. Shared·Server·MasterData는 안 건드린다.
- **main에 직접 커밋 금지.** 브랜치 `feature/race-spectate-exit`에서 작업한다(이미 있다 — spec과 로드맵 커밋이 올라가 있다).
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 `git status --short`로 확인한다.
- **워크트리 금지**(Unity). 일반 브랜치로 전환한다.
- **`.meta` 파일은 반드시 커밋한다 — 폴더 `.meta`도.** 직접 만들지 않고 유니티가 만든 것만 커밋한다.
- **테스트를 위한 어셈블리 이동 금지.**
- **`unity cmd`에는 항상 `--project-path`를 준다.** 테스트는 `--mode EditMode --async_tests true`로
  띄우고 `unity cmd test_status`로 폴링한다. **`run_tests` 응답의 `Total:0`은 무시한다**(비동기라 항상 0이다).
- **`run_tests`는 컴파일을 하지 않는다.** 반드시 `recompile` → `recompile_status` 완료 후 테스트를 돌린다.
- **새 View는 `Assets/UI/UIViewCatalog.asset`에 등록해야 한다.** 없으면 `WindowManager.Open<T>`가
  `LogError`를 찍고 화면에 안 붙은 View를 그대로 돌려준다.
- **테스트가 실제로 실패할 수 있는지 확인한다**(뮤테이션). 통과만으로 검증됐다고 하지 않는다.
- 커밋하지 않는 로컬 픽스처: `Assets/Art`(서브모듈 포인터), `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`,
  `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/ProjectSettings.asset`.
- **유니티 Play 모드를 임의로 멈추지 않는다.** EditMode 테스트를 돌리려면 사용자에게 멈춰 달라고 한다.

**프로젝트 경로**: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client` (이하 `$P`)

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Game/FlappySpectate.cs` (신규) | 후보 목록·현재 대상·앞뒤 이동. **"지금 누구를 보는가"의 유일한 주인** |
| `Assets/Tests/Editor/FlappySpectateTests.cs` (신규) | 위 규칙 전부 |
| `Assets/Scripts/Game/FlappyWatchTarget.cs` (삭제) | `FlappySpectate`로 흡수 |
| `Assets/Tests/Editor/FlappyWatchTargetTests.cs` (삭제) | 위로 이전 |
| `Assets/Scripts/UI/RaceSpectate/RaceSpectateViewModel.cs` (신규) | `관전 2 / 3` 문구, 앞뒤 커맨드 |
| `Assets/Scripts/UI/RaceSpectate/RaceSpectateView.cs` (신규) | 버튼 → 커맨드/콜백, 문구 갱신 |
| `Assets/UI/RaceSpectate/RaceSpectateView.uxml`·`.uss` (신규) | 레이아웃 |
| `Assets/Scripts/UI/LeaveMatchConfirm/LeaveMatchConfirmViewModel.cs` (신규) | 나가기 결과(`UniTask<bool>`) |
| `Assets/Scripts/UI/LeaveMatchConfirm/LeaveMatchConfirmView.cs` (신규) | 확인 팝업 |
| `Assets/UI/LeaveMatchConfirm/LeaveMatchConfirmView.uxml`·`.uss` (신규) | 레이아웃 |
| `Assets/Scripts/App/AppEvent.cs` (수정) | `MatchLeft` 추가 |
| `Assets/Scripts/App/States/InMatch.cs` (수정) | `MatchLeft → frontEnd()` |
| `Assets/Scripts/Game/FlappyHudCoordinator.cs` (수정) | 관전 화면 열기, 카메라 적용, 나가기 흐름 |
| `Assets/Scripts/Game/FlappyChaserView.cs` (수정) | 벽 시각을 `FlappySpectate.Current`로 |
| `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` (수정) | DI 등록 3개 + 뷰 팩토리 2개 |
| `Assets/UI/UIViewCatalog.asset` (수정) | 새 View 2개 등록 |

---

## Task 1: 관전 대상의 주인 — `FlappySpectate`

부품만 만든다. 아직 아무도 안 쓴다(`FlappyWatchTarget`은 Task 2까지 그대로 둔다).

**Files:**
- Create: `Assets/Scripts/Game/FlappySpectate.cs`
- Create: `Assets/Tests/Editor/FlappySpectateTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.EntityRegistry`, `IGameDataStore.userEntityId`,
  `EntityKind.Kind`(`EntityType.Character`), `GameFramework.World.Transform.Position.X`,
  `LOP.FinishState.Finished`, `LOP.FinishPlacement.Value`
- Produces:
  ```csharp
  public class FlappySpectate
  {
      public FlappySpectate(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore);
      public IReadOnlyList<string> Candidates { get; }   // x 내림차순, 같으면 id 내림차순
      public string Current { get; }                     // 볼 사람이 없으면 null
      public void Next();                                // 목록에서 한 칸 뒤로, 끝에서 되돌아옴
      public void Prev();                                // 목록에서 한 칸 앞으로, 끝에서 되돌아옴
      public void Refresh();                             // 매 틱. 후보 재구성 + 필요하면 재선택
  }
  ```

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/Editor/FlappySpectateTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// "지금 누구를 보고 있나"의 규칙. 카메라와 추격자 벽이 <b>같은 답</b>을 봐야 한다 —
    /// 벽은 보고 있는 새와 같은 시각으로 그려야 하는데(내 새는 앞, 남의 새는 뒤),
    /// 둘이 다른 새를 고르면 벽이 엉뚱한 시각에 그려진다.
    /// </summary>
    public class FlappySpectateTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private static Entity Bird(string id, float x)
        {
            var bird = new Entity(id);
            bird.Add(new EntityKind(EntityType.Character));
            bird.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, 0f) });
            return bird;
        }

        private static EntityRegistry Registry(params Entity[] entities)
        {
            var registry = new EntityRegistry();
            foreach (var entity in entities)
            {
                registry.Add(entity);
            }
            return registry;
        }

        private static FlappySpectate Spectate(EntityRegistry registry, string myEntityId)
        {
            var spectate = new FlappySpectate(registry, new FakeGameDataStore { userEntityId = myEntityId });
            spectate.Refresh();
            return spectate;
        }

        [Test]
        public void 내_새가_살아_있으면_내_새를_본다()
        {
            var spectate = Spectate(Registry(Bird("me", 50f), Bird("other", 10f)), "me");

            Assert.AreEqual("me", spectate.Current);
        }

        [Test]
        public void 내_새가_없으면_가장_뒤처진_새를_본다()
        {
            //  선두가 아니라 꼴찌를 본다 — 다음에 잡힐 사람이라 벽이 같은 화면에 있다.
            var spectate = Spectate(Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f)), "me");

            Assert.AreEqual("b", spectate.Current);
        }

        [Test]
        public void 같은_자리면_id가_작은_쪽을_본다()
        {
            //  레지스트리 순회 순서는 정해져 있지 않다. 안 정하면 프레임마다 카메라가 오간다.
            var spectate = Spectate(Registry(Bird("b", 10f), Bird("a", 10f)), "me");

            Assert.AreEqual("a", spectate.Current);
        }

        [Test]
        public void 새가_하나도_없으면_아무도_안_본다()
        {
            var spectate = Spectate(Registry(), "me");

            Assert.IsNull(spectate.Current);
            Assert.AreEqual(0, spectate.Candidates.Count);
            Assert.DoesNotThrow(() => spectate.Next());   // 눌러도 터지지 않는다
        }

        [Test]
        public void 새가_아닌_것은_세지_않는다()
        {
            //  아이템도 레지스트리에 있고 x가 더 작을 수 있다. 카메라가 그쪽으로 가면 안 된다.
            var item = new Entity("item");
            item.Add(new EntityKind(EntityType.Item));
            item.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(-100f, 0f, 0f) });

            var spectate = Spectate(Registry(item, Bird("bird", 10f)), "me");

            Assert.AreEqual("bird", spectate.Current);
        }

        [Test]
        public void 목록은_선두부터다()
        {
            var spectate = Spectate(Registry(Bird("b", 10f), Bird("a", 50f), Bird("c", 30f)), "watcher");

            CollectionAssert.AreEqual(new[] { "a", "c", "b" }, spectate.Candidates);
        }

        [Test]
        public void 다음을_누르면_한_칸_뒤로_가고_끝에서_되돌아온다()
        {
            var spectate = Spectate(Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f)), "watcher");
            //  내가 없으므로 꼴찌 "b"(마지막)에서 시작한다.
            Assert.AreEqual("b", spectate.Current);

            spectate.Next();
            Assert.AreEqual("a", spectate.Current, "끝에서 처음으로 되돌아온다");

            spectate.Next();
            Assert.AreEqual("c", spectate.Current);
        }

        [Test]
        public void 이전을_누르면_한_칸_앞으로_가고_처음에서_되돌아온다()
        {
            var spectate = Spectate(Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f)), "watcher");
            Assert.AreEqual("b", spectate.Current);

            spectate.Prev();
            Assert.AreEqual("c", spectate.Current);

            spectate.Prev();
            Assert.AreEqual("a", spectate.Current);

            spectate.Prev();
            Assert.AreEqual("b", spectate.Current, "처음에서 끝으로 되돌아온다");
        }

        [Test]
        public void 내_새가_완주하면_후보에서_빠진다()
        {
            //  내 새의 통과는 클라 시뮬이 직접 판정한다(FinishState).
            var mine = Bird("me", 50f);
            mine.Add(new FinishState { FinishedTick = 100 });

            var spectate = Spectate(Registry(mine, Bird("other", 10f)), "me");

            Assert.AreEqual("other", spectate.Current);
            CollectionAssert.DoesNotContain(spectate.Candidates, "me");
        }

        [Test]
        public void 남의_새가_완주하면_후보에서_빠진다()
        {
            //  남의 통과는 클라가 판정하지 않는다 — 서버가 스냅샷에 실어 보낸 등수로만 안다.
            var other = Bird("other", 50f);
            other.Add(new FinishPlacement { Value = 1 });

            var spectate = Spectate(Registry(other, Bird("last", 10f)), "watcher");

            Assert.AreEqual("last", spectate.Current);
            CollectionAssert.DoesNotContain(spectate.Candidates, "other");
        }

        [Test]
        public void 보던_사람이_사라지면_다시_고른다()
        {
            var registry = Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));
            var spectate = Spectate(registry, "watcher");
            spectate.Next();
            Assert.AreEqual("a", spectate.Current);

            registry.Remove("a");
            spectate.Refresh();

            Assert.AreEqual("b", spectate.Current, "사라졌으면 꼴찌 규칙으로 돌아간다");
        }

        [Test]
        public void 보던_사람이_그대로면_수동_선택이_유지된다()
        {
            //  이게 깨지면 자동 추적이 매 틱 수동 선택을 덮어써서 ◀ ▶ 가 아무 소용이 없다.
            var registry = Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));
            var spectate = Spectate(registry, "watcher");
            spectate.Next();
            Assert.AreEqual("a", spectate.Current);

            spectate.Refresh();
            spectate.Refresh();

            Assert.AreEqual("a", spectate.Current);
        }
    }
}
```

- [ ] **Step 2: 컴파일해서 빨간지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
```

기대: `FlappySpectate` 타입이 없어 **컴파일 에러**.

- [ ] **Step 3: 부품을 만든다**

`Assets/Scripts/Game/FlappySpectate.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 지금 누구를 보고 있나. 살아 있으면 내 새, 아니면 <b>아직 달리는 사람 중 꼴찌</b>다 —
    /// 다음에 잡힐 사람이라 추격자가 같은 화면 안에 있다(선두를 보면 벽이 화면 밖이라
    /// 아무 일도 안 일어난다). 완주·탈락한 뒤에는 사람이 <see cref="Next"/>/<see cref="Prev"/>로
    /// 직접 고를 수 있다.
    ///
    /// <para>카메라와 추격자 벽이 <b>같은 답</b>을 봐야 해서 주인을 하나로 둔다. 주인이 둘이면
    /// 자동 추적이 매 틱 수동 선택을 덮어쓴다.</para>
    /// </summary>
    public class FlappySpectate
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IGameDataStore gameDataStore;

        //  매 틱 도는 코드라 목록을 새로 만들지 않고 비워서 다시 쓴다.
        private readonly List<Ranked> scratch = new List<Ranked>();
        private readonly List<string> candidates = new List<string>();

        private static readonly Comparison<Ranked> LeaderFirst = CompareLeaderFirst;

        private struct Ranked
        {
            public string Id;
            public float X;
        }

        /// <summary>지금 볼 수 있는 사람. 선두가 0번이다.</summary>
        public IReadOnlyList<string> Candidates => candidates;

        /// <summary>지금 보는 사람. 볼 사람이 없으면 null.</summary>
        public string Current { get; private set; }

        public FlappySpectate(GameFramework.World.EntityRegistry entityRegistry, IGameDataStore gameDataStore)
        {
            this.entityRegistry = entityRegistry;
            this.gameDataStore = gameDataStore;
        }

        /// <summary>후보를 다시 만들고, 보던 사람이 사라졌으면 다시 고른다. 매 틱 부른다.</summary>
        public void Refresh()
        {
            Rebuild();

            if (candidates.Count == 0)
            {
                Current = null;
                return;
            }

            //  보던 사람이 그대로면 손대지 않는다 — 수동 선택이 살아남는 자리가 여기다.
            if (Current != null && candidates.Contains(Current))
            {
                return;
            }

            string mine = gameDataStore.userEntityId;
            Current = string.IsNullOrEmpty(mine) == false && candidates.Contains(mine)
                ? mine
                : candidates[candidates.Count - 1];   // 꼴찌 — 정렬 규칙 덕에 마지막이 곧 그 사람이다
        }

        public void Next() => Step(1);

        public void Prev() => Step(-1);

        private void Step(int delta)
        {
            int index = Current == null ? -1 : candidates.IndexOf(Current);
            if (index < 0)
            {
                return;
            }

            int count = candidates.Count;
            Current = candidates[((index + delta) % count + count) % count];
        }

        private void Rebuild()
        {
            scratch.Clear();
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }

                var body = entity.Get<GameFramework.World.Transform>();
                if (body == null || Finished(entity))
                {
                    continue;
                }

                scratch.Add(new Ranked { Id = entity.Id, X = body.Position.X });
            }

            scratch.Sort(LeaderFirst);

            candidates.Clear();
            for (int i = 0; i < scratch.Count; i++)
            {
                candidates.Add(scratch[i].Id);
            }
        }

        //  결승선을 넘었나. 답이 두 갈래인 이유: 클라 시뮬은 내 새만 굴리므로(Simulated),
        //  FinishState는 내 새에만 붙는다. 남의 통과는 서버가 스냅샷에 실어 준 등수로만 안다.
        private static bool Finished(GameFramework.World.Entity entity)
        {
            return (entity.Get<FinishState>()?.Finished ?? false)
                || (entity.Get<FinishPlacement>()?.Value ?? 0) > 0;
        }

        //  x 내림차순(선두가 0번). 같은 자리면 id 내림차순이라 <b>마지막이 "x 최소·id 최소"</b>가
        //  된다 — 꼴찌 폴백이 candidates[^1] 한 줄로 끝나고, 목록 순서와 폴백 규칙이 어긋날 수 없다.
        private static int CompareLeaderFirst(Ranked a, Ranked b)
        {
            int byX = b.X.CompareTo(a.X);
            return byX != 0 ? byX : string.CompareOrdinal(b.Id, a.Id);
        }
    }
}
```

- [ ] **Step 4: 컴파일하고 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P     # completed / failed:false 확인
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 45; unity cmd test_status --project-path $P
```

기대: 통과. **`total`이 12 늘었는지 확인한다**(새 테스트 12개, 기존 `FlappyWatchTargetTests` 5개는 아직 살아 있다).

- [ ] **Step 5: 뮤테이션으로 세 곳을 확인한다**

한 번에 하나씩 바꾸고 돌린 뒤 되돌린다.

| 바꿀 것 | 기대 |
|---|---|
| `if (Current != null && candidates.Contains(Current)) return;` → 지운다 | `보던_사람이_그대로면_수동_선택이_유지된다` 빨강 |
| `candidates[candidates.Count - 1]` → `candidates[0]` | `내_새가_없으면_가장_뒤처진_새를_본다` 빨강 |
| `Finished`의 `\|\| (entity.Get<FinishPlacement>()?.Value ?? 0) > 0` → 지운다 | `남의_새가_완주하면_후보에서_빠진다` 빨강 |

- [ ] **Step 6: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/FlappySpectate.cs Assets/Scripts/Game/FlappySpectate.cs.meta \
        Assets/Tests/Editor/FlappySpectateTests.cs Assets/Tests/Editor/FlappySpectateTests.cs.meta
git status --short
git commit -m "feat(flappy): 관전 대상의 주인을 부품 하나로 만든다"
```

---

## Task 2: 배선 — 카메라와 벽이 같은 주인을 본다

`FlappySpectate`를 실제로 쓰게 하고 `FlappyWatchTarget`을 지운다. **화면 변화는 아직 없다** —
`Current`가 기존 규칙과 같은 답을 내므로 동작이 그대로여야 한다(회귀 검증).

**Files:**
- Modify: `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `Assets/Scripts/Game/FlappyHudCoordinator.cs`
- Modify: `Assets/Scripts/Game/FlappyChaserView.cs`
- Delete: `Assets/Scripts/Game/FlappyWatchTarget.cs` (+ `.meta`)
- Delete: `Assets/Tests/Editor/FlappyWatchTargetTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `FlappySpectate.Refresh()`, `FlappySpectate.Current` (Task 1)
- Produces: `FlappyHudCoordinator`가 `ITickable.Tick()`에서 카메라를 적용한다.
  `FlappyChaserView` 생성자에서 `IPlayerContext`가 빠지고 `FlappySpectate`가 들어온다.

- [ ] **Step 1: DI에 등록한다**

`FlappyRaceLifetimeScope.cs`의 `builder.RegisterEntryPoint<FlappyChaserView>().AsSelf();` **바로 위**에 넣는다:

```csharp
            //  카메라와 벽이 같은 답을 봐야 한다 — 주인이 하나여야 하므로 싱글턴.
            builder.Register<FlappySpectate>(Lifetime.Singleton);
```

- [ ] **Step 2: 벽이 같은 주인을 보게 한다**

`FlappyChaserView.cs`에서 `IPlayerContext`를 `FlappySpectate`로 바꾼다.

필드:
```csharp
        private readonly IPlayerContext playerContext;
```
→
```csharp
        private readonly FlappySpectate spectate;
```

생성자 파라미터 `IPlayerContext playerContext,` → `FlappySpectate spectate,`
그리고 `this.playerContext = playerContext;` → `this.spectate = spectate;`

`LateTick()`의 호출을 바꾼다:
```csharp
            X = FlappyChaserCurve.XAt(
                config, ElapsedSeconds(FlappyWatchTarget.Resolve(entityRegistry, playerContext.entityId)),
                stopAtX);
```
→
```csharp
            //  코디네이터가 ITickable에서 Refresh를 이미 돌렸다(Tick이 LateTick보다 먼저다).
            X = FlappyChaserCurve.XAt(config, ElapsedSeconds(spectate.Current), stopAtX);
```

`entityRegistry` 필드가 이제 안 쓰이면 생성자 파라미터째 지운다 — 컴파일러 경고 대신
`recompile_status`의 에러로 확인하지 말고, 파일을 읽어 다른 용처가 있는지 직접 본다.

- [ ] **Step 3: 코디네이터가 주인을 굴리고 카메라를 적용한다**

`FlappyHudCoordinator.cs`:

생성자에 `FlappySpectate spectate`를 더하고 필드로 보관한다(`private readonly FlappySpectate spectate;`).

`Tick()`을 바꾼다:
```csharp
        public void Tick()
        {
            UpdateFinish();
        }
```
→
```csharp
        public void Tick()
        {
            UpdateFinish();
            UpdateCamera();
        }

        //  보는 대상이 바뀌었을 때만 카메라를 옮긴다. SetTarget은 현재 카메라 위치로부터
        //  거리·각도를 다시 잡으므로 매 틱 부르면 조작감이 망가진다.
        private void UpdateCamera()
        {
            spectate.Refresh();

            if (spectate.Current == null || spectate.Current == _cameraTargetId)
            {
                return;
            }

            var visual = actorRegistry.Get(spectate.Current)?.visualGameObject;
            if (visual == null)
            {
                return;   // 아직 몸이 안 붙었다 — 다음 틱에 다시 본다
            }

            _cameraTargetId = spectate.Current;
            cameraController.SetTarget(visual.transform);
        }
```

`FollowNextRunner()` 메서드를 통째로 지우고, `OnEntityDestroyed`의 마지막 줄
`FollowNextRunner();`도 지운다(이제 `Tick`이 매 틱 본다).

- [ ] **Step 4: 옛 부품을 지운다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git rm Assets/Scripts/Game/FlappyWatchTarget.cs Assets/Scripts/Game/FlappyWatchTarget.cs.meta
git rm Assets/Tests/Editor/FlappyWatchTargetTests.cs Assets/Tests/Editor/FlappyWatchTargetTests.cs.meta
```

- [ ] **Step 5: 컴파일하고 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 45; unity cmd test_status --project-path $P
```

기대: 통과. **`total`이 5 줄었는지 확인한다**(`FlappyWatchTargetTests` 삭제).

- [ ] **Step 6: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/FlappyRaceLifetimeScope.cs \
        Assets/Scripts/Game/FlappyHudCoordinator.cs \
        Assets/Scripts/Game/FlappyChaserView.cs
git status --short
git commit -m "refactor(flappy): 카메라와 벽이 같은 관전 주인을 본다"
```

---

## Task 3: 관전 화면

`◀ 관전 2 / 3 ▶`. 나가기 버튼은 자리만 만들고 Task 4에서 연결한다.

**Files:**
- Create: `Assets/Scripts/UI/RaceSpectate/RaceSpectateViewModel.cs`
- Create: `Assets/Scripts/UI/RaceSpectate/RaceSpectateView.cs`
- Create: `Assets/UI/RaceSpectate/RaceSpectateView.uxml`, `.uss`
- Modify: `Assets/UI/UIViewCatalog.asset`
- Modify: `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `Assets/Scripts/Game/FlappyHudCoordinator.cs`
- Create: `Assets/Tests/Editor/RaceSpectateViewModelTests.cs`

**Interfaces:**
- Consumes: `FlappySpectate.Candidates`, `.Current`, `.Next()`, `.Prev()` (Task 1)
- Produces:
  ```csharp
  public class RaceSpectateViewModel
  {
      public RaceSpectateViewModel(FlappySpectate spectate);
      public string StatusText();   // "관전 2 / 3", 볼 사람이 없으면 ""
      public void Next();
      public void Prev();
  }

  public class RaceSpectateView : UIView
  {
      public override UILayer Layer => UILayer.Window;
      public void SetLeaveCallback(System.Action onLeave);
  }
  ```

- [ ] **Step 1: 실패하는 ViewModel 테스트를 쓴다**

`Assets/Tests/Editor/RaceSpectateViewModelTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;
using LOP.UI;

namespace LOP.Tests
{
    /// <summary>관전 화면의 문구. 몇 번째 사람을 보고 있는지가 유일한 정보다 —
    /// 인게임에 표시 이름이 없어 이름을 띄울 수 없다.</summary>
    public class RaceSpectateViewModelTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private static Entity Bird(string id, float x)
        {
            var bird = new Entity(id);
            bird.Add(new EntityKind(EntityType.Character));
            bird.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, 0f) });
            return bird;
        }

        private static RaceSpectateViewModel ViewModel(params Entity[] entities)
        {
            var registry = new EntityRegistry();
            foreach (var entity in entities)
            {
                registry.Add(entity);
            }
            var spectate = new FlappySpectate(registry, new FakeGameDataStore { userEntityId = "watcher" });
            spectate.Refresh();
            return new RaceSpectateViewModel(spectate);
        }

        [Test]
        public void 몇_번째를_보는지_알려준다()
        {
            //  내가 없으므로 꼴찌 "b"(3번째)에서 시작한다.
            var viewModel = ViewModel(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));

            Assert.AreEqual("관전 3 / 3", viewModel.StatusText());
        }

        [Test]
        public void 다음을_누르면_번호가_바뀐다()
        {
            var viewModel = ViewModel(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));

            viewModel.Next();

            Assert.AreEqual("관전 1 / 3", viewModel.StatusText(), "끝에서 처음으로 되돌아온다");
        }

        [Test]
        public void 볼_사람이_없으면_아무것도_안_띄운다()
        {
            var viewModel = ViewModel();

            Assert.AreEqual(string.Empty, viewModel.StatusText());
        }
    }
}
```

- [ ] **Step 2: 컴파일해서 빨간지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

기대: `RaceSpectateViewModel` 타입이 없어 **컴파일 에러**.

- [ ] **Step 3: ViewModel을 만든다**

`Assets/Scripts/UI/RaceSpectate/RaceSpectateViewModel.cs`:

```csharp
namespace LOP.UI
{
    /// <summary>
    /// 관전 화면 ViewModel. 지금 몇 번째 사람을 보는지만 알려준다 —
    /// 인게임에 표시 이름이 없어 이름은 띄울 수 없다(순위처럼 보이는 문구도 쓰지 않는다:
    /// 완주해서 앞서 있는 사람이 후보에서 빠지므로 거짓말이 된다).
    /// </summary>
    public class RaceSpectateViewModel
    {
        private readonly FlappySpectate _spectate;

        public RaceSpectateViewModel(FlappySpectate spectate)
        {
            _spectate = spectate;
        }

        /// <summary>화면에 띄울 문구. 볼 사람이 없으면 빈 문자열.</summary>
        public string StatusText()
        {
            int index = IndexOfCurrent();
            return index < 0 ? string.Empty : $"관전 {index + 1} / {_spectate.Candidates.Count}";
        }

        public void Next() => _spectate.Next();

        public void Prev() => _spectate.Prev();

        //  IReadOnlyList에는 IndexOf가 없다. 매 프레임 도는 코드라 LINQ 대신 직접 훑는다.
        private int IndexOfCurrent()
        {
            string current = _spectate.Current;
            if (current == null)
            {
                return -1;
            }

            var candidates = _spectate.Candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == current)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
```

- [ ] **Step 4: 레이아웃을 만든다**

`Assets/UI/RaceSpectate/RaceSpectateView.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="spectate-root" class="spectate-root" picking-mode="Ignore">
        <ui:Button name="spectate-leave" text="나가기" class="btn btn--secondary spectate-leave" />
        <ui:VisualElement name="spectate-bar" class="spectate-bar">
            <ui:Button name="spectate-prev" text="◀" class="btn spectate-arrow" />
            <ui:Label name="spectate-status" text="" class="spectate-status" picking-mode="Ignore" />
            <ui:Button name="spectate-next" text="▶" class="btn spectate-arrow" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

> 루트는 `picking-mode="Ignore"`다 — 전체화면이라 그대로 두면 아래 월드가 입력을 못 받는다.
> UI Toolkit에서 부모가 Ignore여도 **자식은 그대로 눌린다.**

`Assets/UI/RaceSpectate/RaceSpectateView.uss`:

```css
.spectate-root {
    flex-grow: 1;
}

/* 오른쪽 위 — ◀ ▶ 와 멀리 떼어 오탭을 줄인다 */
.spectate-leave {
    position: absolute;
    right: 24px;
    top: 24px;
}

.spectate-bar {
    position: absolute;
    left: 0;
    right: 0;
    bottom: 48px;
    flex-direction: row;
    justify-content: center;
    align-items: center;
}

.spectate-arrow {
    width: 88px;
    height: 72px;
    font-size: 32px;
}

.spectate-status {
    width: 180px;
    font-size: 28px;
    color: rgba(255, 255, 255, 0.85);
    -unity-text-align: middle-center;
}
```

- [ ] **Step 5: View를 만든다**

`Assets/Scripts/UI/RaceSpectate/RaceSpectateView.cs`:

```csharp
using System;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 완주·탈락한 뒤의 조작면. 남은 사람을 골라 보고, 판을 떠난다.
    ///
    /// <para>문구 화면(<see cref="RaceFinishView"/>·<see cref="RaceEliminatedView"/>)과 나눈 이유:
    /// 그 둘은 아래 월드를 가리지 않는 순수 오버레이라 버튼을 넣으면 규칙이 깨지고,
    /// 완주와 탈락이 필요로 하는 조작이 똑같아 한 곳에 두면 중복이 없다.</para>
    ///
    /// <para>나가기는 화면 교체(큰 흐름)라 여기서 처리하지 않고 콜백으로 넘긴다 —
    /// <see cref="MatchResultView"/>와 같은 짝이다.</para>
    /// </summary>
    public class RaceSpectateView : UIView
    {
        private readonly RaceSpectateViewModel _viewModel;

        private Label _status;
        private Button _prev;
        private Button _next;
        private Button _leave;
        private IVisualElementScheduledItem _tick;
        private Action _onLeave;

        public RaceSpectateView(RaceSpectateViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public void SetLeaveCallback(Action onLeave) => _onLeave = onLeave;

        public override void OnOpen()
        {
            base.OnOpen();

            _status = Root.Q<Label>("spectate-status");
            _prev = Root.Q<Button>("spectate-prev");
            _next = Root.Q<Button>("spectate-next");
            _leave = Root.Q<Button>("spectate-leave");

            _prev.clicked += OnPrevClicked;
            _next.clicked += OnNextClicked;
            _leave.clicked += OnLeaveClicked;

            //  보는 대상은 변경 알림이 없는 샘플링 값이라 매 프레임 읽는다(RaceStartView와 같은 방식).
            _tick = Root.schedule.Execute(_ => _status.text = _viewModel.StatusText()).Every(0);
        }

        public override void OnClose()
        {
            if (_prev != null) { _prev.clicked -= OnPrevClicked; }
            if (_next != null) { _next.clicked -= OnNextClicked; }
            if (_leave != null) { _leave.clicked -= OnLeaveClicked; }

            base.OnClose();
        }

        private void OnPrevClicked() => _viewModel.Prev();
        private void OnNextClicked() => _viewModel.Next();
        private void OnLeaveClicked() => _onLeave?.Invoke();

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed == false)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                    _tick = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
```

- [ ] **Step 6: 유니티에 임포트시켜 `.meta`를 만든다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
ls -a $P/Assets/UI/RaceSpectate/ $P/Assets/Scripts/UI/RaceSpectate/
ls $P/Assets/UI/RaceSpectate.meta $P/Assets/Scripts/UI/RaceSpectate.meta
```

`.uxml.meta`·`.uss.meta`와 **폴더 `.meta` 둘**이 생겨야 한다.

- [ ] **Step 7: 카탈로그에 등록한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
grep '^guid:' Assets/UI/RaceSpectate/RaceSpectateView.uxml.meta
grep '^guid:' Assets/UI/RaceSpectate/RaceSpectateView.uss.meta
```

`Assets/UI/UIViewCatalog.asset`의 `- viewName: RaceFinishView` 항목 **아래**에 같은 모양으로 넣는다
(`fileID`는 다른 항목과 같은 값):

```yaml
  - viewName: RaceSpectateView
    uxml: {fileID: 9197481963319205126, guid: <uxml guid>, type: 3}
    uss: {fileID: 7433441132597879392, guid: <uss guid>, type: 3}
```

- [ ] **Step 8: DI에 등록한다**

`FlappyRaceLifetimeScope.cs`의 `builder.Register<RaceFinishView>(Lifetime.Transient);` 아래:

```csharp
            builder.Register<RaceSpectateViewModel>(Lifetime.Transient);
            builder.Register<RaceSpectateView>(Lifetime.Transient);
```

`RegisterViewFactories`의 마지막 줄 아래:

```csharp
            sink.Add(windowManager.RegisterViewFactory<RaceSpectateView>(() => container.Resolve<RaceSpectateView>()));
```

- [ ] **Step 9: 코디네이터가 관전 화면을 연다**

`FlappyHudCoordinator.cs`에 필드를 더한다:

```csharp
        private RaceSpectateView _spectateView;
```

`UpdateFinish()`의 `windowManager.Open<RaceFinishView>();` 아래에:

```csharp
            OpenSpectate();
```

`OnEntityDestroyed()`의 `windowManager.Open<RaceEliminatedView>();` 아래에도 같은 줄을 넣는다.

그리고 메서드를 더한다:

```csharp
        //  완주했든 탈락했든 같은 조작면을 연다. 두 번 열리지 않게 자기 인스턴스를 본다.
        private void OpenSpectate()
        {
            if (_spectateView != null)
            {
                return;
            }

            _spectateView = windowManager.Open<RaceSpectateView>();
        }
```

- [ ] **Step 10: 컴파일하고 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 45; unity cmd test_status --project-path $P
```

기대: 통과. **`total`이 3 늘었는지 확인한다.**

- [ ] **Step 11: 뮤테이션으로 확인한다**

`StatusText()`의 `index + 1`을 `index`로 바꾸고 돌린다.
기대: `몇_번째를_보는지_알려준다`와 `다음을_누르면_번호가_바뀐다`가 빨강. 되돌린다.

- [ ] **Step 12: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/UI/RaceSpectate Assets/Scripts/UI/RaceSpectate.meta \
        Assets/UI/RaceSpectate Assets/UI/RaceSpectate.meta \
        Assets/UI/UIViewCatalog.asset \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs \
        Assets/Scripts/Game/FlappyHudCoordinator.cs \
        Assets/Tests/Editor/RaceSpectateViewModelTests.cs Assets/Tests/Editor/RaceSpectateViewModelTests.cs.meta
git status --short
git commit -m "feat(flappy): 완주·탈락 뒤 볼 사람을 고를 수 있게 한다"
```

---

## Task 4: 나가기

**Files:**
- Modify: `Assets/Scripts/App/AppEvent.cs`
- Modify: `Assets/Scripts/App/States/InMatch.cs`
- Create: `Assets/Scripts/UI/LeaveMatchConfirm/LeaveMatchConfirmViewModel.cs`
- Create: `Assets/Scripts/UI/LeaveMatchConfirm/LeaveMatchConfirmView.cs`
- Create: `Assets/UI/LeaveMatchConfirm/LeaveMatchConfirmView.uxml`, `.uss`
- Modify: `Assets/UI/UIViewCatalog.asset`
- Modify: `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`
- Modify: `Assets/Scripts/Game/FlappyHudCoordinator.cs`
- Create: `Assets/Tests/Editor/LeaveMatchConfirmViewModelTests.cs`

**Interfaces:**
- Consumes: `RaceSpectateView.SetLeaveCallback(Action)` (Task 3)
- Produces:
  ```csharp
  public class LeaveMatchConfirmViewModel : System.IDisposable
  {
      public Cysharp.Threading.Tasks.UniTask<bool> ResultAsync { get; }
      public void Confirm();   // true
      public void Cancel();    // false
      public void Dispose();   // 아직 안 정해졌으면 false — 백드롭으로 닫혀도 대기가 풀린다
  }

  public class LeaveMatchConfirmView : UIPopup, IResultView<bool> { }
  public enum AppEvent { BootCompleted, MatchFound, MatchEnded, MatchLeft }
  ```

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/Editor/LeaveMatchConfirmViewModelTests.cs`:

```csharp
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using LOP.UI;

namespace LOP.Tests
{
    /// <summary>
    /// 나가기 확인의 결과. <b>어떤 경로로 닫혀도 결과가 확정돼야 한다</b> —
    /// 안 그러면 WindowManager.OpenModalAsync의 대기가 영영 안 풀린다.
    /// </summary>
    public class LeaveMatchConfirmViewModelTests
    {
        //  결과를 읽기 전에 <b>끝났는지부터</b> 단언한다. 안 그러면 결과가 확정되지 않았을 때
        //  테스트가 실패가 아니라 <b>정지</b>로 끝나 뮤테이션 확인이 불가능하다.
        private static void AssertResult(LeaveMatchConfirmViewModel viewModel, bool expected)
        {
            var result = viewModel.ResultAsync;
            Assert.AreEqual(UniTaskStatus.Succeeded, result.Status, "결과가 확정되지 않았다");
            Assert.AreEqual(expected, result.GetAwaiter().GetResult());
        }

        [Test]
        public void 나가기를_누르면_참이다()
        {
            var viewModel = new LeaveMatchConfirmViewModel();

            viewModel.Confirm();

            AssertResult(viewModel, true);
        }

        [Test]
        public void 취소를_누르면_거짓이다()
        {
            var viewModel = new LeaveMatchConfirmViewModel();

            viewModel.Cancel();

            AssertResult(viewModel, false);
        }

        [Test]
        public void 백드롭으로_닫혀도_거짓으로_끝난다()
        {
            //  백드롭 클릭은 Close → View.Dispose → ViewModel.Dispose로 온다. 여기서 결과를
            //  확정하지 않으면 나가기를 누른 쪽이 영원히 기다린다.
            var viewModel = new LeaveMatchConfirmViewModel();

            viewModel.Dispose();

            AssertResult(viewModel, false);
        }

        [Test]
        public void 확정된_뒤에_닫혀도_답이_안_바뀐다()
        {
            var viewModel = new LeaveMatchConfirmViewModel();

            viewModel.Confirm();
            viewModel.Dispose();

            AssertResult(viewModel, true);
        }
    }
}
```

- [ ] **Step 2: 컴파일해서 빨간지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

기대: `LeaveMatchConfirmViewModel` 타입이 없어 **컴파일 에러**.

- [ ] **Step 3: ViewModel을 만든다**

`Assets/Scripts/UI/LeaveMatchConfirm/LeaveMatchConfirmViewModel.cs`:

```csharp
using System;
using Cysharp.Threading.Tasks;

namespace LOP.UI
{
    /// <summary>
    /// 나가기 확인 ViewModel. 결과를 1회성으로 확정한다(확정 = 모달 닫기 신호).
    /// 나가면 true, 취소하거나 백드롭으로 닫으면 false.
    /// </summary>
    public class LeaveMatchConfirmViewModel : IDisposable
    {
        private readonly UniTaskCompletionSource<bool> _result = new();

        private bool _disposed;

        public UniTask<bool> ResultAsync => _result.Task;

        public void Confirm() => _result.TrySetResult(true);

        public void Cancel() => _result.TrySetResult(false);

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            //  백드롭으로 닫혔을 수도 있다. 아직 안 정해졌으면 취소로 끝낸다 —
            //  안 그러면 나가기를 누른 쪽이 영원히 기다린다.
            _result.TrySetResult(false);
        }
    }
}
```

- [ ] **Step 4: 레이아웃을 만든다**

`Assets/UI/LeaveMatchConfirm/LeaveMatchConfirmView.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="leave-root" class="scrim leave-root">
        <ui:VisualElement name="leave-card" class="card leave-card">
            <ui:Label text="판을 떠납니다" class="title leave-title" />
            <ui:Label text="다시 들어올 수 없고, 결과 화면도 볼 수 없습니다." class="card-text leave-message" />
            <ui:VisualElement name="leave-actions" class="leave-actions">
                <ui:Button name="leave-cancel" text="취소" class="btn btn--secondary leave-button" />
                <ui:Button name="leave-confirm" text="나가기" class="btn btn--primary leave-button" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

`Assets/UI/LeaveMatchConfirm/LeaveMatchConfirmView.uss`:

```css
.leave-root {
    flex-grow: 1;
    justify-content: center;
    align-items: center;
}

.leave-card {
    width: 460px;
}

.leave-actions {
    flex-direction: row;
    justify-content: flex-end;
    margin-top: 16px;
}

.leave-button {
    margin-left: 12px;
}
```

- [ ] **Step 5: View를 만든다**

`Assets/Scripts/UI/LeaveMatchConfirm/LeaveMatchConfirmView.cs`:

```csharp
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 나가기 확인 팝업. 버튼을 ViewModel 커맨드로 넘기고 결과를 포워딩한다.
    ///
    /// <para>백드롭을 눌러도 닫힌다(<see cref="UIPopup.AutoClose"/> 기본값) — 되돌릴 수 없는
    /// 행동이라 실수로 닫히는 편이 실수로 나가는 것보다 안전하다. 그때도 결과가 확정되는 것은
    /// ViewModel의 Dispose가 보장한다.</para>
    /// </summary>
    public class LeaveMatchConfirmView : UIPopup, IResultView<bool>
    {
        private readonly LeaveMatchConfirmViewModel _viewModel;

        private Button _confirmButton;
        private Button _cancelButton;

        public LeaveMatchConfirmView(LeaveMatchConfirmViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public UniTask<bool> ResultAsync => _viewModel.ResultAsync;

        public override void OnOpen()
        {
            base.OnOpen();

            _confirmButton = Root.Q<Button>("leave-confirm");
            _cancelButton = Root.Q<Button>("leave-cancel");

            _confirmButton.clicked += OnConfirmClicked;
            _cancelButton.clicked += OnCancelClicked;
        }

        public override void OnClose()
        {
            if (_confirmButton != null) { _confirmButton.clicked -= OnConfirmClicked; }
            if (_cancelButton != null) { _cancelButton.clicked -= OnCancelClicked; }

            base.OnClose();
        }

        private void OnConfirmClicked() => _viewModel.Confirm();
        private void OnCancelClicked() => _viewModel.Cancel();

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed == false)
            {
                _disposed = true;

                if (disposing)
                {
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
```

- [ ] **Step 6: 앱 상태 머신에 사건을 더한다**

`Assets/Scripts/App/AppEvent.cs`:

```csharp
namespace LOP
{
    public enum AppEvent
    {
        BootCompleted,
        MatchFound,
        MatchEnded,
        //  사람이 판을 떠났다. 목적지는 MatchEnded와 같지만 이름이 사실을 말해야 한다 —
        //  판은 남은 사람들끼리 계속 돌아간다.
        MatchLeft,
    }
}
```

`Assets/Scripts/App/States/InMatch.cs`의 `GetNextState`:

```csharp
            return ev switch
            {
                AppEvent.MatchEnded => frontEnd(),
                AppEvent.MatchLeft => frontEnd(),
                _ => this,
            };
```

- [ ] **Step 7: 유니티에 임포트시켜 `.meta`를 만든다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
ls -a $P/Assets/UI/LeaveMatchConfirm/ $P/Assets/Scripts/UI/LeaveMatchConfirm/
ls $P/Assets/UI/LeaveMatchConfirm.meta $P/Assets/Scripts/UI/LeaveMatchConfirm.meta
```

- [ ] **Step 8: 카탈로그와 DI에 등록한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
grep '^guid:' Assets/UI/LeaveMatchConfirm/LeaveMatchConfirmView.uxml.meta
grep '^guid:' Assets/UI/LeaveMatchConfirm/LeaveMatchConfirmView.uss.meta
```

`Assets/UI/UIViewCatalog.asset`의 `- viewName: RaceSpectateView` 아래에:

```yaml
  - viewName: LeaveMatchConfirmView
    uxml: {fileID: 9197481963319205126, guid: <uxml guid>, type: 3}
    uss: {fileID: 7433441132597879392, guid: <uss guid>, type: 3}
```

`FlappyRaceLifetimeScope.cs`의 `builder.Register<RaceSpectateView>(Lifetime.Transient);` 아래:

```csharp
            builder.Register<LeaveMatchConfirmViewModel>(Lifetime.Transient);
            builder.Register<LeaveMatchConfirmView>(Lifetime.Transient);
```

`RegisterViewFactories` 마지막 줄 아래:

```csharp
            sink.Add(windowManager.RegisterViewFactory<LeaveMatchConfirmView>(() => container.Resolve<LeaveMatchConfirmView>()));
```

- [ ] **Step 9: 코디네이터가 나가기를 처리한다**

`FlappyHudCoordinator.cs` 맨 위에 `using Cysharp.Threading.Tasks;`를 더한다.

생성자에 `AppStateMachine appStateMachine`을 더하고 필드로 보관한다
(`private readonly AppStateMachine appStateMachine;`).

`OpenSpectate()`에 콜백 연결을 더한다:

```csharp
            _spectateView = windowManager.Open<RaceSpectateView>();
            _spectateView.SetLeaveCallback(OnLeaveRequested);
```

그리고 메서드를 더한다:

```csharp
        //  나가기는 화면 교체(큰 흐름)라 View가 아니라 여기서 처리한다.
        private void OnLeaveRequested() => AskAndLeaveAsync().Forget();

        private async UniTaskVoid AskAndLeaveAsync()
        {
            bool leave = await windowManager.OpenModalAsync<LeaveMatchConfirmView, bool>();
            if (leave == false)
            {
                return;
            }

            //  서버에는 아무것도 안 보낸다. 씬이 내려가며 연결이 끊기고, 서버는 이미 나간 사람을
            //  제대로 처리한다 — 완주 기록은 FinishOrderTracker가, 탈락 기록은 추격자 시스템이 들고 있다.
            appStateMachine.Fire(AppEvent.MatchLeft);
        }
```

- [ ] **Step 10: 컴파일하고 테스트가 초록인지 본다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 45; unity cmd test_status --project-path $P
```

기대: 통과. **`total`이 4 늘었는지 확인한다.**

- [ ] **Step 11: 뮤테이션으로 확인한다**

`LeaveMatchConfirmViewModel.Dispose()`의 `_result.TrySetResult(false);`를 지우고 돌린다.
기대: `백드롭으로_닫혀도_거짓으로_끝난다`가 빨강 — `AssertResult`의 첫 단언
(`UniTaskStatus.Succeeded`)에서 "결과가 확정되지 않았다"로 실패한다. 되돌린다.

- [ ] **Step 12: 커밋한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/UI/LeaveMatchConfirm Assets/Scripts/UI/LeaveMatchConfirm.meta \
        Assets/UI/LeaveMatchConfirm Assets/UI/LeaveMatchConfirm.meta \
        Assets/UI/UIViewCatalog.asset \
        Assets/Scripts/App/AppEvent.cs Assets/Scripts/App/States/InMatch.cs \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs \
        Assets/Scripts/Game/FlappyHudCoordinator.cs \
        Assets/Tests/Editor/LeaveMatchConfirmViewModelTests.cs Assets/Tests/Editor/LeaveMatchConfirmViewModelTests.cs.meta
git status --short
git commit -m "feat(flappy): 판을 떠날 수 있게 한다"
```

---

## Task 5: 라이브 검증과 머지

**Files:**
- Modify: `docs/ROADMAP.md`

- [ ] **Step 1: 유니티 Play 모드가 멈춰 있는지 확인하고 최종 테스트를 돌린다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path $P
unity cmd recompile_status --project-path $P
unity cmd run_tests --project-path $P --mode EditMode --async_tests true
sleep 45; unity cmd test_status --project-path $P
```

기대: 전부 통과.

- [ ] **Step 2: 사용자에게 2인 라이브 검증을 요청한다**

서버 에디터 + 클라 MPPM으로 한 판. 확인할 여섯 가지:

1. 완주 후 `◀ ▶`로 남은 사람을 순환하고 **카메라가 따라간다**
2. 수동으로 딴 사람을 봐도 **추격자 벽이 튀지 않는다**(벽이 보는 대상의 시각으로 그려진다)
3. `나가기` → 확인창 → **로비로 간다**
4. 확인창에서 `취소` → 화면 그대로, 관전 계속
5. **탈락했을 때도** 같은 조작면이 뜬다
6. **레이스 중 동작이 그대로다**(회귀 — 카메라가 내 새를 따라가고 벽이 정상)

서버 콘솔의 `[Finish]`·`[Chaser]` 로그와 양쪽 콘솔 에러 0도 함께 본다.

- [ ] **Step 3: 로드맵을 갱신한다**

`docs/ROADMAP.md`의 열린 항목 `**관전·나가기 UI**` 행을 지우고, "이번 세션에 닫힌 것" 표
맨 위에 `✅` 행을 넣는다. 라이브에서 **실제로 본 것만** 적는다.

- [ ] **Step 4: 커밋하고 머지·푸시한다**

한 줄씩 결과를 확인하며 진행한다. `&&`로 이어 붙이지 않는다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add docs/ROADMAP.md
git commit -m "docs(roadmap): 관전·나가기 UI를 닫는다"
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/race-spectate-exit
git push origin main
```

**푸시가 거절되면 `--force`를 쓰지 않는다.** 다시 `fetch` → 리베이스 → 재시도한다.
머지 후 형제 레포(GameFramework·Shared 등)가 뒤처지지 않았는지 확인한다 —
지난 슬라이스에서 그것 때문에 컴파일이 깨졌다.

---

## 마지막 확인

- [ ] `FlappyWatchTarget.cs`와 그 테스트가 **지워졌다**(`.meta` 포함)
- [ ] 새 폴더 넷의 `.meta`가 전부 커밋됐다 — `Assets/Scripts/UI/RaceSpectate`,
      `Assets/UI/RaceSpectate`, `Assets/Scripts/UI/LeaveMatchConfirm`, `Assets/UI/LeaveMatchConfirm`
- [ ] `UIViewCatalog.asset`에 View 둘이 등록됐다
- [ ] 뮤테이션을 다섯 번 돌리고 **전부 되돌렸다**
- [ ] 클라 레포 하나만 바뀌었다(Shared·Server·MasterData 무변경)
