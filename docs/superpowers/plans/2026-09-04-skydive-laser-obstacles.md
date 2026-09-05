# Skydive 레이저 장애물 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 코스에 닿으면 죽는 레이저를 놓고, 죽으면 마지막으로 지난 선반으로 되돌린다.

**Architecture:** 레이저는 상태 없이 `f(tick)`으로만 정의해 스냅샷·롤백 비용을 0으로 만든다. 맞았는지는
**Conservative Advancement**(Mirtich / PhysX / Bullet 표준)로 서버에서만 판정한다. 부활은 위치를 크게
옮기므로, 거리 문턱에 기대는 대신 **텔레포트 카운터**를 GameFramework에 범용 장치로 새로 만든다.

**Tech Stack:** Unity 6000.3.16f1, C#, VContainer, Mirror, Protobuf(proto3), NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-04-skydive-laser-obstacles-design.md`

## Global Constraints

- **주석은 최소로, 한국어로, "왜"만.** 코드로 자명한 것에는 달지 않는다. 전문용어를 던지지 말고 그 자리에서 풀어 쓴다.
- **LOP 측 파일에서 World 타입은 항상 풀 네임스페이스로 한정한다** — `GameFramework.World.Entity`, `GameFramework.World.Transform`. `using GameFramework.World;`를 추가하지 않는다(`UnityEngine.Component`와 이름이 겹친다).
- **시뮬 공유 코드는 구체 클래스를 공유한다.** 인터페이스 seam을 만들지 않는다. 인터페이스는 사이드가 달라야 하는 I/O 어댑터에만 쓴다.
- **컨텍스트 없는 순수 커널은 `static`이고 `*System` 이름을 붙이지 않는다.** 월드를 조작하는 것은 무상태 DI 인스턴스이며 `*System`이다.
- **레이저 판정은 서버에서만 돈다.** 클라는 그리기만 한다.
- **맵 씬에 붙는 새 MonoBehaviour는 공용 패키지(LeagueOfPhysical-Shared)에 둔다** — 한쪽에만 있으면 반대쪽에서 missing script가 되고 그 빈 컴포넌트가 씬 주입을 끊는다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고, 커밋 전 `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다.
- **Unity가 생성한 `.meta` 파일을 함께 커밋한다.** 직접 만들거나 고치지 않는다.
- 상수 값(스펙에서 그대로): `MaxIterations = 16` · `MaxAngularSpeedPerTick = 15°` · 무적 `2.0초` · 점멸 예고 `0.4초` · `EntitySnap`의 새 필드 번호 `22`
- **proto 재생성은 `Scripts/compile_protos.sh`만 돌린다.** `generate_message_ids.sh`를 돌리면 `MessageIds.cs`가 다시 매겨져 와이어가 깨진 전례가 있다. 재생성 후 `git diff --stat`으로 `MessageIds.cs`가 안 바뀐 것을 확인한다.

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `GameFramework/Runtime/Scripts/World/Components/Transform.cs` | `TeleportCount` 필드 추가 |
| `GameFramework/Runtime/Scripts/Extensions/EntityMotion.Extensions.cs` | `Teleport(entity, position)` 범용 API |
| `GameFramework/Runtime/Scripts/Netcode/TeleportTracker.cs` | (신규) 카운터가 바뀌었나를 판단하는 순수 클래스 |
| `GameFramework/Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs` | `OnTeleport()` 추가 |
| `LeagueOfPhysical-Shared/Protos/EntitySnap.proto` | `teleport_count = 22` |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Laser.cs` | (신규) 레이저 설정 데이터 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserGeometry.cs` | (신규) 순수 — 틱 → 선분, 점멸 여부 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserSweep.cs` | (신규) 순수 — CA 판정 + 3D 선분 거리 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserField.cs` | (신규) 한 판의 레이저 모음 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserVolume.cs` | (신규) 맵 마커 MonoBehaviour |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveCheckpoints.cs` | (신규) 순수 — 죽은 y → 부활 선반 y |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/SkydiveLaserSystem.cs` | (신규) 판정하고 부활시킨다 |
| `LeagueOfPhysical-Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs` | `Lasers` 표 · `Shelves`에 부활 지점 · 검사 3종 |
| `LeagueOfPhysical-Client/Assets/Scripts/Game/LaserView.cs` | (신규) 레이저를 그린다 |

---

## Task 1: 텔레포트 신호 (GameFramework)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/Components/Transform.cs`
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Extensions/EntityMotion.Extensions.cs`
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Netcode/TeleportTracker.cs`
- Test: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/World/TransformTeleportTests.cs`
- Test: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/Netcode/TeleportTrackerTests.cs`

**Interfaces:**
- Produces:
  - `GameFramework.World.Transform.TeleportCount` (int, 기본 0)
  - `GameFramework.World.EntityMotionExtensions.Teleport(this Entity e, UnityEngine.Vector3 value)` — 위치를 쓰고 카운터를 1 올린다
  - `GameFramework.Netcode.TeleportTracker` — `bool Observe(string entityId, int count)`, `void Forget(string entityId)`

- [ ] **Step 1: 실패하는 테스트를 쓴다 — Transform + Teleport 확장**

`GameFramework/Tests/World/TransformTeleportTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

public class TransformTeleportTests
{
    private static Entity MakeEntity()
    {
        var entity = new Entity("e1");
        entity.Add(new Transform());
        return entity;
    }

    [Test]
    public void 평범한_이동은_카운터를_올리지_않는다()
    {
        Entity entity = MakeEntity();

        entity.SetPosition(new Vector3(1f, 2f, 3f));

        Assert.AreEqual(0, entity.Get<Transform>().TeleportCount);
    }

    [Test]
    public void 텔레포트는_위치를_쓰고_카운터를_올린다()
    {
        Entity entity = MakeEntity();

        entity.Teleport(new Vector3(0f, 2200f, 0f));

        Assert.AreEqual(new Vector3(0f, 2200f, 0f), entity.GetPosition());
        Assert.AreEqual(1, entity.Get<Transform>().TeleportCount);
    }

    // 같은 자리로 텔레포트해도 "옮겼다"는 사실은 알려야 한다. SetPosition은 값이 같으면
    // 안 쓰고 빠져나가는데, 그 최적화에 카운터까지 딸려 가면 신호가 사라진다.
    [Test]
    public void 같은_자리로_텔레포트해도_카운터는_오른다()
    {
        Entity entity = MakeEntity();
        entity.SetPosition(new Vector3(5f, 0f, 0f));

        entity.Teleport(new Vector3(5f, 0f, 0f));

        Assert.AreEqual(1, entity.Get<Transform>().TeleportCount);
    }

    [Test]
    public void 여러_번_텔레포트하면_계속_오른다()
    {
        Entity entity = MakeEntity();

        entity.Teleport(new Vector3(1f, 0f, 0f));
        entity.Teleport(new Vector3(2f, 0f, 0f));
        entity.Teleport(new Vector3(3f, 0f, 0f));

        Assert.AreEqual(3, entity.Get<Transform>().TeleportCount);
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Unity 에디터에서 Test Runner(EditMode) 또는 CLI로 실행.
Expected: FAIL — `TeleportCount`와 `Teleport`가 없어 컴파일되지 않는다.

- [ ] **Step 3: `Transform`에 필드를 더한다**

`GameFramework/Runtime/Scripts/World/Components/Transform.cs`를 통째로 이 내용으로 바꾼다:

```csharp
using System.Numerics;

namespace GameFramework.World
{
    /// <summary>엔티티의 공간 포즈(위치+회전). 순수 데이터(Anemic) — 로직은 System에 둔다.</summary>
    public class Transform : Component
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; } = Quaternion.Identity;

        /// <summary>
        /// 순간이동한 횟수. 받는 쪽은 <b>값이 바뀌었는지</b>만 본다 — 늘 켜져 있는 표시가 아니라
        /// "직전에 본 것과 다른가"라서, 스냅샷 하나가 유실돼도 다음 스냅샷에서 알아챈다.
        /// </summary>
        public int TeleportCount { get; set; }
    }
}
```

- [ ] **Step 4: `Teleport` 확장을 더한다**

`GameFramework/Runtime/Scripts/Extensions/EntityMotion.Extensions.cs`의 `SetVelocity` 메서드 **뒤에**
다음을 넣는다(클래스 닫는 중괄호 앞):

```csharp
        /// <summary>
        /// 이어지지 않는 이동. 위치를 쓰고 <see cref="Transform.TeleportCount"/>를 올린다 —
        /// 받는 쪽이 이 이동을 녹이지 않고 즉시 반영하게 하려는 것이다.
        /// </summary>
        public static void Teleport(this Entity e, Vector3 value)
        {
            var t = e.Get<Transform>();
            t.Position = value.ToNumerics();   // SetPosition의 "같으면 안 쓰기"를 거치지 않는다
            t.TeleportCount++;
        }
```

- [ ] **Step 5: 테스트 통과를 확인한다**

Expected: `TransformTeleportTests` 4개 전부 PASS.

- [ ] **Step 6: 실패하는 테스트를 쓴다 — TeleportTracker**

`GameFramework/Tests/Runtime/Netcode/TeleportTrackerTests.cs`:

```csharp
using GameFramework.Netcode;
using NUnit.Framework;

public class TeleportTrackerTests
{
    // 처음 보는 엔티티는 텔레포트가 아니다 — 스폰이다. 여기서 true를 돌려주면 모든 스폰이
    // 텔레포트로 처리된다.
    [Test]
    public void 처음_본_엔티티는_텔레포트가_아니다()
    {
        var tracker = new TeleportTracker();

        Assert.IsFalse(tracker.Observe("e1", 0));
        Assert.IsFalse(tracker.Observe("e2", 7));
    }

    [Test]
    public void 값이_그대로면_텔레포트가_아니다()
    {
        var tracker = new TeleportTracker();
        tracker.Observe("e1", 3);

        Assert.IsFalse(tracker.Observe("e1", 3));
        Assert.IsFalse(tracker.Observe("e1", 3));
    }

    [Test]
    public void 값이_바뀌면_한_번만_텔레포트다()
    {
        var tracker = new TeleportTracker();
        tracker.Observe("e1", 3);

        Assert.IsTrue(tracker.Observe("e1", 4));
        Assert.IsFalse(tracker.Observe("e1", 4));
    }

    // 스냅샷이 유실돼 여러 번 오른 값이 한꺼번에 와도 알아채야 한다.
    [Test]
    public void 한꺼번에_여러_칸_올라도_알아챈다()
    {
        var tracker = new TeleportTracker();
        tracker.Observe("e1", 3);

        Assert.IsTrue(tracker.Observe("e1", 9));
    }

    [Test]
    public void 엔티티끼리_섞이지_않는다()
    {
        var tracker = new TeleportTracker();
        tracker.Observe("e1", 1);
        tracker.Observe("e2", 1);

        Assert.IsTrue(tracker.Observe("e1", 2));
        Assert.IsFalse(tracker.Observe("e2", 1));
    }

    [Test]
    public void 잊은_엔티티는_다시_처음_본_것이_된다()
    {
        var tracker = new TeleportTracker();
        tracker.Observe("e1", 1);
        tracker.Forget("e1");

        Assert.IsFalse(tracker.Observe("e1", 5));
    }
}
```

- [ ] **Step 7: 실패를 확인한다**

Expected: FAIL — `TeleportTracker`가 없다.

- [ ] **Step 8: `TeleportTracker`를 만든다**

`GameFramework/Runtime/Scripts/Netcode/TeleportTracker.cs`:

```csharp
using System.Collections.Generic;

namespace GameFramework.Netcode
{
    /// <summary>
    /// 엔티티마다 마지막으로 본 텔레포트 카운터를 기억해, 새 값이 그것과 다른지 답한다.
    ///
    /// <para>카운터를 쓰는 이유는 스냅샷이 유실될 수 있어서다 — "이번에 텔레포트함" 같은 일회성
    /// 표시는 그 패킷이 사라지면 영영 관측되지 않지만, 값이 남아 있으면 다음 스냅샷에서 알아챈다.</para>
    /// </summary>
    public class TeleportTracker
    {
        private readonly Dictionary<string, int> _seen = new Dictionary<string, int>();

        /// <summary>이 값이 텔레포트를 뜻하나. 처음 보는 엔티티는 스폰이므로 false다.</summary>
        public bool Observe(string entityId, int count)
        {
            if (_seen.TryGetValue(entityId, out int previous) == false)
            {
                _seen[entityId] = count;
                return false;
            }
            if (previous == count)
            {
                return false;
            }
            _seen[entityId] = count;
            return true;
        }

        public void Forget(string entityId) => _seen.Remove(entityId);

        public void Clear() => _seen.Clear();
    }
}
```

- [ ] **Step 9: 테스트 통과를 확인한다**

Expected: `TransformTeleportTests` 4개 + `TeleportTrackerTests` 6개 전부 PASS.

- [ ] **Step 10: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git status --short
git add Runtime/Scripts/World/Components/Transform.cs \
        Runtime/Scripts/Extensions/EntityMotion.Extensions.cs \
        Runtime/Scripts/Netcode/TeleportTracker.cs \
        Runtime/Scripts/Netcode/TeleportTracker.cs.meta \
        Tests/World/TransformTeleportTests.cs \
        Tests/World/TransformTeleportTests.cs.meta \
        Tests/Runtime/Netcode/TeleportTrackerTests.cs \
        Tests/Runtime/Netcode/TeleportTrackerTests.cs.meta
git commit -m "feat(netcode): 텔레포트를 카운터로 명시한다"
```

---

## Task 2: 렌더 스무더가 텔레포트를 안 녹인다

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs`
- Test: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `GameFramework.Netcode.RenderCorrectionSmoother.OnTeleport()`

**배경:** 지금 `OnCorrection`은 갭 크기로만 판단한다 — `gap < _minCorrection`(2.5cm)이면 무시하고,
`gap > _noSmoothDistance`(8m)면 즉시 채택하고, 그 사이는 녹인다. **3m짜리 텔레포트는 그 사이에 있어
미끄러진다.** 크기와 무관하게 즉시 채택하는 길이 필요하다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`GameFramework/Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs`의 클래스 안에 아래 세 테스트를
추가한다(기존 테스트는 그대로 둔다). 파일 맨 위의 `MakeSmoother`/상수 헬퍼를 그대로 쓴다.

```csharp
    // 3m 텔레포트는 NoSmooth(8m) 문턱 아래라, 카운터 신호가 없으면 녹아서 미끄러진다.
    [Test]
    public void 문턱_아래_텔레포트도_녹이지_않는다()
    {
        var smoother = MakeSmoother();
        smoother.Target(System.Numerics.Vector3.Zero);
        smoother.Advance(0.02f);
        smoother.Target(System.Numerics.Vector3.Zero);
        smoother.Advance(0.02f);

        smoother.OnTeleport();

        var moved = new System.Numerics.Vector3(3f, 0f, 0f);
        Assert.AreEqual(moved, smoother.Target(moved));
    }

    // 녹이던 도중에 텔레포트가 오면 그 블렌드를 버려야 한다. 안 버리면 옛 오차를 텔레포트한
    // 자리에 그대로 얹어 그린다.
    [Test]
    public void 녹이던_도중_텔레포트가_오면_블렌드를_버린다()
    {
        var smoother = MakeSmoother();
        smoother.Target(System.Numerics.Vector3.Zero);
        smoother.Advance(0.02f);
        smoother.Target(System.Numerics.Vector3.Zero);
        smoother.Advance(0.02f);
        smoother.OnCorrection(System.Numerics.Vector3.Zero,
                              new System.Numerics.Vector3(1f, 0f, 0f),
                              System.Numerics.Vector3.Zero, 0.02f);

        smoother.OnTeleport();

        var moved = new System.Numerics.Vector3(50f, 0f, 0f);
        Assert.AreEqual(moved, smoother.Target(moved));
    }

    // 2.5cm 미만이라 OnCorrection이라면 무시했을 크기여도, 텔레포트는 즉시 채택이 맞다.
    [Test]
    public void 아주_작은_텔레포트도_즉시_채택한다()
    {
        var smoother = MakeSmoother();
        smoother.Target(System.Numerics.Vector3.Zero);
        smoother.Advance(0.02f);

        smoother.OnTeleport();

        var moved = new System.Numerics.Vector3(0.01f, 0f, 0f);
        Assert.AreEqual(moved, smoother.Target(moved));
    }
```

- [ ] **Step 2: 실패를 확인한다**

Expected: FAIL — `OnTeleport`가 없다.

- [ ] **Step 3: `OnTeleport`를 더한다**

`RenderCorrectionSmoother.cs`의 `OnCorrection` 메서드 **뒤에** 넣는다:

```csharp
        /// <summary>
        /// 의도된 순간이동임을 알린다. 크기를 보지 않고 즉시 채택한다 — 거리 문턱
        /// (<see cref="_noSmoothDistance"/>)은 <b>큰 랙</b>을 위한 안전망이지 의도의 신호가 아니라서,
        /// 문턱 아래 짧은 텔레포트는 그대로 두면 미끄러진다.
        /// </summary>
        public void OnTeleport()
        {
            _smoothing = false;
            _elapsed = 0f;
        }
```

- [ ] **Step 4: 테스트 통과를 확인한다**

Expected: 새 3개 + 기존 테스트 전부 PASS.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git status --short
git add Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs \
        Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs
git commit -m "feat(netcode): 텔레포트는 크기와 무관하게 즉시 채택한다"
```

---

## Task 3: 와이어에 텔레포트 카운터를 싣는다

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Protos/EntitySnap.proto`
- 재생성 산출물: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/EntitySnap.cs`

**Interfaces:**
- Produces: `LOP.EntitySnap.TeleportCount` (int32, 필드 번호 22)

- [ ] **Step 1: proto에 필드를 더한다**

`Protos/EntitySnap.proto`의 `dash_charge = 21;` 줄 **뒤에**, 닫는 중괄호 앞에 넣는다:

```proto
	// 순간이동한 횟수. 받는 쪽은 직전에 본 값과 다른지만 본다 — 스냅샷이 유실돼도
	// 다음 스냅샷에서 알아챈다(일회성 표시라면 그 패킷과 함께 사라진다).
	int32 teleport_count = 22;
```

- [ ] **Step 2: 재생성한다 — MessageIds는 건드리지 않는다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
bash Scripts/compile_protos.sh
```

`generate_message_ids.sh`를 **돌리지 않는다.** 필드 추가는 MessageId를 바꾸지 않는데, 그 스크립트를
돌리면 `MessageIds.cs`가 다시 매겨져 와이어가 조용히 깨진 전례가 있다.

- [ ] **Step 3: MessageIds가 안 바뀐 것을 확인한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git diff --stat
```

Expected: `Protos/EntitySnap.proto`와 `Runtime.Generated/Scripts/Protobuf/EntitySnap.cs`만 바뀌어 있고
`MessageIds.cs`는 목록에 **없다**. 있으면 `git checkout -- Runtime.Generated/Scripts/MessageIds.cs`로
되돌린다.

- [ ] **Step 4: 컴파일을 확인한다**

클라 에디터에서 재컴파일하고 콘솔에 에러가 없는지 본다.
Expected: 에러 0.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Protos/EntitySnap.proto Runtime.Generated/Scripts/Protobuf/EntitySnap.cs
git commit -m "feat(wire): EntitySnap에 텔레포트 카운터를 싣는다"
```

---

## Task 4: 서버가 카운터를 채우고 클라가 알아챈다

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/UserEntitySnapshotSystem.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Netcode/Reconciler.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Entity/PredictedEntityInterpolator.cs`

**Interfaces:**
- Consumes: `Transform.TeleportCount` (Task 1) · `RenderCorrectionSmoother.OnTeleport()` (Task 2) · `TeleportTracker` (Task 1) · `EntitySnap.TeleportCount` (Task 3)
- Produces: `PredictedEntityInterpolator.OnCorrection(before, after, velocity, deltaTime, bool teleported)`

- [ ] **Step 1: 서버가 스냅에 카운터를 담는다**

두 서버 시스템에서 `EntitySnap`을 만드는 자리를 찾아(`new EntitySnap` 또는 필드를 채우는 블록),
`Stamina`/`Gliding` 같은 다른 필드를 채우는 줄 **옆에** 한 줄을 더한다:

```csharp
snap.TeleportCount = entity.Get<GameFramework.World.Transform>()?.TeleportCount ?? 0;
```

두 파일 모두에 넣어야 한다 — 하나만 넣으면 그 경로로 오는 스냅에서는 신호가 사라진다.

- [ ] **Step 2: 클라 인터폴레이터가 텔레포트를 구분해 받는다**

`PredictedEntityInterpolator.cs`의 `OnCorrection` 시그니처에 파라미터를 더하고, 스무더 호출을 가른다:

```csharp
        public void OnCorrection(System.Numerics.Vector3 before, System.Numerics.Vector3 after,
                                 System.Numerics.Vector3 velocity, float deltaTime, bool teleported)
        {
            if (teleported)
            {
                smoother.OnTeleport();
                return;
            }
            smoother.OnCorrection(before, after, velocity, deltaTime);
        }
```

기존 필드 이름이 `smoother`가 아니면 그 파일에서 쓰는 이름을 그대로 쓴다.

- [ ] **Step 3: Reconciler가 카운터를 관측해 넘긴다**

`Reconciler.cs`에 필드를 더한다(다른 필드들 옆):

```csharp
        private readonly GameFramework.Netcode.TeleportTracker teleportTracker
            = new GameFramework.Netcode.TeleportTracker();
        private readonly HashSet<string> teleportedThisBatch = new HashSet<string>();
```

**관측은 게이트보다 먼저 한다.** `bool allClose = true;` 줄 **앞에** 넣는다:

```csharp
            // 텔레포트 관측은 아래 errorGate보다 먼저다. 게이트는 "예측이 서버와 얼마나 먼가"로
            // 판단하는데, 짧은 텔레포트는 그 문턱 아래라 게이트가 닫히고 스냅을 적용하는 루프가
            // 통째로 안 돈다 — 그러면 신호가 사라진다.
            teleportedThisBatch.Clear();
            foreach (var pair in pendingSnaps)
            {
                if (teleportTracker.Observe(pair.Key, pair.Value.TeleportCount))
                {
                    teleportedThisBatch.Add(pair.Key);
                }
            }
```

**텔레포트가 있으면 게이트를 연다.** `if (allClose && statusMatches)`를 이렇게 고친다:

```csharp
            //  텔레포트는 정의상 이어지지 않는 이동이라, 거리가 작아도 그대로 채택해야 한다.
            if (allClose && statusMatches && teleportedThisBatch.Count == 0)
```

그리고 `NotifyRenderCorrections()` 안의 호출을 고친다:

```csharp
                    actor.GetComponent<PredictedEntityInterpolator>()?.OnCorrection(
                        pair.Value, after,
                        GameFramework.World.EntityMotionExtensions.GetVelocity(target).ToNumerics(),
                        deltaTime,
                        teleportedThisBatch.Contains(pair.Key));
```

- [ ] **Step 4: 클라도 카운터를 World에 반영한다**

같은 루프에서 `SetVelocity` 줄 **뒤에** 넣는다 — 클라의 Transform이 서버 값을 따라가게 한다.
(비교는 `teleportTracker`가 하므로 판단에 쓰이지는 않지만, 두 사이드의 같은 필드가 다른 값을
들고 있으면 나중에 읽는 쪽이 헷갈린다.)

```csharp
                var targetTransform = target.Get<GameFramework.World.Transform>();
                if (targetTransform != null)
                {
                    targetTransform.TeleportCount = snap.TeleportCount;
                }
```

- [ ] **Step 5: 짧은 텔레포트가 게이트를 연다는 것을 확인한다**

`Reconciler`는 Assembly-CSharp이라 단위 테스트가 어렵다. 대신 **관측이 게이트보다 앞이라는 것이
코드에서 보이는지** 를 눈으로 확인한다:

- `teleportedThisBatch.Clear()`가 `bool allClose = true;` **위**에 있다
- `if (allClose && statusMatches && teleportedThisBatch.Count == 0)`로 되어 있다

둘 중 하나라도 어긋나면 짧은 텔레포트가 조용히 무시된다 — 이 슬라이스가 막으려는 실패다.

- [ ] **Step 6: 컴파일과 기존 테스트를 확인한다**

클라 에디터에서 재컴파일 → 콘솔 에러 0 → EditMode 전체 실행.
Expected: 기존 테스트 전부 PASS(회귀 없음).

- [ ] **Step 7: 커밋 (레포 두 개)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/TickSystems/UserEntitySnapshotSystem.cs \
        Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs
git commit -m "feat(netcode): 서버가 스냅에 텔레포트 카운터를 담는다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Netcode/Reconciler.cs \
        Assets/Scripts/Entity/PredictedEntityInterpolator.cs
git commit -m "feat(netcode): 클라가 텔레포트를 알아채 녹이지 않는다"
```

---

## Task 5: 레이저 데이터와 기하 (LOP-Shared, 순수)

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/Laser.cs`
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserGeometry.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/LaserGeometryTests.cs`

**Interfaces:**
- Produces:
  - `LOP.Laser` (readonly struct) — `Pivot`(`System.Numerics.Vector3`), `Length`, `Radius`, `StartAngle`, `AngularSpeed`, `SweepHalfRange` (float), `Period`, `OnTicks`, `Phase` (int)
  - `LOP.LaserGeometry.Fold(float x, float half) → float`
  - `LOP.LaserGeometry.Angle(in Laser laser, float t) → float`
  - `LOP.LaserGeometry.SegmentAt(in Laser laser, float t, out Vector3 a, out Vector3 b)`
  - `LOP.LaserGeometry.Lit(in Laser laser, long tick) → bool`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/LaserGeometryTests.cs`:

```csharp
using System.Numerics;
using LOP;
using NUnit.Framework;

public class LaserGeometryTests
{
    private static Laser Rotating(float startAngle, float angularSpeed)
        => new Laser(Vector3.Zero, length: 10f, radius: 0.5f,
                     startAngle: startAngle, angularSpeed: angularSpeed,
                     sweepHalfRange: 0f, period: 0, onTicks: 0, phase: 0);

    [Test]
    public void 고정_빔은_각도가_변하지_않는다()
    {
        Laser laser = Rotating(0.5f, 0f);

        Assert.AreEqual(0.5f, LaserGeometry.Angle(laser, 0f), 1e-5f);
        Assert.AreEqual(0.5f, LaserGeometry.Angle(laser, 100f), 1e-5f);
    }

    [Test]
    public void 회전_빔은_틱에_비례해_돈다()
    {
        Laser laser = Rotating(0f, 0.1f);

        Assert.AreEqual(1.0f, LaserGeometry.Angle(laser, 10f), 1e-5f);
    }

    // 삼각파의 네 경계. 접는 지점이 틀리면 왕복이 튄다.
    [Test]
    public void 삼각파는_네_경계를_지난다()
    {
        const float half = 2f;

        Assert.AreEqual(0f, LaserGeometry.Fold(0f, half), 1e-5f);
        Assert.AreEqual(half, LaserGeometry.Fold(half, half), 1e-5f);
        Assert.AreEqual(0f, LaserGeometry.Fold(2f * half, half), 1e-5f);
        Assert.AreEqual(-half, LaserGeometry.Fold(3f * half, half), 1e-5f);
        Assert.AreEqual(0f, LaserGeometry.Fold(4f * half, half), 1e-5f);
    }

    [Test]
    public void 삼각파는_음수_입력에도_같은_주기를_돈다()
    {
        const float half = 2f;

        Assert.AreEqual(LaserGeometry.Fold(1f, half), LaserGeometry.Fold(1f - 4f * half, half), 1e-5f);
    }

    [Test]
    public void 삼각파는_범위를_벗어나지_않는다()
    {
        const float half = 3f;

        for (int i = 0; i < 200; i++)
        {
            float folded = LaserGeometry.Fold(i * 0.37f, half);
            Assert.That(folded, Is.InRange(-half - 1e-4f, half + 1e-4f));
        }
    }

    [Test]
    public void 왕복_빔은_시작각을_중심으로_흔들린다()
    {
        var laser = new Laser(Vector3.Zero, 10f, 0.5f,
                              startAngle: 1f, angularSpeed: 0.5f, sweepHalfRange: 1f,
                              period: 0, onTicks: 0, phase: 0);

        Assert.AreEqual(1f, LaserGeometry.Angle(laser, 0f), 1e-5f);
        Assert.AreEqual(2f, LaserGeometry.Angle(laser, 2f), 1e-5f);   // 진행각 1.0 = half → 정점
    }

    [Test]
    public void 선분은_피벗에서_길이만큼_뻗는다()
    {
        Laser laser = Rotating(0f, 0f);

        LaserGeometry.SegmentAt(laser, 0f, out Vector3 a, out Vector3 b);

        Assert.AreEqual(Vector3.Zero, a);
        Assert.AreEqual(10f, b.X, 1e-4f);
        Assert.AreEqual(0f, b.Y, 1e-4f);
        Assert.AreEqual(0f, b.Z, 1e-4f);
    }

    [Test]
    public void 주기가_없으면_늘_켜져_있다()
    {
        Laser laser = Rotating(0f, 0f);

        Assert.IsTrue(LaserGeometry.Lit(laser, 0));
        Assert.IsTrue(LaserGeometry.Lit(laser, 12345));
    }

    [Test]
    public void 점멸은_주기_안에서_켜졌다_꺼진다()
    {
        var laser = new Laser(Vector3.Zero, 10f, 0.5f, 0f, 0f, 0f,
                              period: 10, onTicks: 4, phase: 0);

        Assert.IsTrue(LaserGeometry.Lit(laser, 0));
        Assert.IsTrue(LaserGeometry.Lit(laser, 3));
        Assert.IsFalse(LaserGeometry.Lit(laser, 4));
        Assert.IsFalse(LaserGeometry.Lit(laser, 9));
        Assert.IsTrue(LaserGeometry.Lit(laser, 10));
    }

    [Test]
    public void 위상이_점멸을_밀어_준다()
    {
        var laser = new Laser(Vector3.Zero, 10f, 0.5f, 0f, 0f, 0f,
                              period: 10, onTicks: 4, phase: 5);

        Assert.IsFalse(LaserGeometry.Lit(laser, 0));
        Assert.IsTrue(LaserGeometry.Lit(laser, 5));
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Expected: FAIL — `Laser`와 `LaserGeometry`가 없다.

- [ ] **Step 3: `Laser`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/Laser.cs`:

```csharp
using System.Numerics;

namespace LOP
{
    /// <summary>
    /// 레이저 하나의 설정. <b>상태가 없다</b> — 틱을 넣으면 자세가 나오는 식의 계수일 뿐이라
    /// 스냅샷에 실을 것도, 롤백에서 되돌릴 것도 없다.
    ///
    /// <para>빔은 <see cref="Pivot"/>에서 한쪽으로만 뻗은 선분(시계바늘)이고, Y축을 중심으로
    /// 수평면에서 돈다. 통로를 가로지르는 빔은 Pivot을 벽 쪽에 둬서 만든다.</para>
    /// </summary>
    public readonly struct Laser
    {
        public readonly Vector3 Pivot;
        public readonly float Length;
        /// <summary>빔의 굵기(반지름). 캐릭터 반지름과 더해 허용 거리를 만든다.</summary>
        public readonly float Radius;
        public readonly float StartAngle;
        /// <summary>rad / 틱. 0이면 고정 빔이다.</summary>
        public readonly float AngularSpeed;
        /// <summary>0보다 크면 전회전 대신 이 폭만큼 왕복한다.</summary>
        public readonly float SweepHalfRange;
        /// <summary>점멸 주기(틱). 0 이하면 늘 켜져 있다.</summary>
        public readonly int Period;
        public readonly int OnTicks;
        public readonly int Phase;

        public Laser(Vector3 pivot, float length, float radius,
                     float startAngle, float angularSpeed, float sweepHalfRange,
                     int period, int onTicks, int phase)
        {
            Pivot = pivot;
            Length = length;
            Radius = radius;
            StartAngle = startAngle;
            AngularSpeed = angularSpeed;
            SweepHalfRange = sweepHalfRange;
            Period = period;
            OnTicks = onTicks;
            Phase = phase;
        }
    }
}
```

- [ ] **Step 4: `LaserGeometry`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserGeometry.cs`:

```csharp
using System;
using System.Numerics;

namespace LOP
{
    /// <summary>
    /// 틱을 넣으면 레이저의 자세가 나오는 식. 컨텍스트가 없는 순수 계산이라 static이다.
    ///
    /// <para>판정은 서버에서만 도므로(스펙 §4.6) <c>Cos</c>/<c>Sin</c>을 그대로 쓴다 — 클·서가
    /// 끝자리까지 같을 필요가 없다. 클라는 이 결과로 그리기만 한다.</para>
    /// </summary>
    public static class LaserGeometry
    {
        /// <summary>톱니를 접어 만든 삼각파. <c>[-half, +half]</c>를 주기 <c>4·half</c>로 왕복한다.</summary>
        public static float Fold(float x, float half)
        {
            if (half <= 0f)
            {
                return 0f;
            }
            float period = 4f * half;
            float m = x + half;
            m -= MathF.Floor(m / period) * period;
            return m <= 2f * half ? m - half : 3f * half - m;
        }

        /// <param name="t">틱. 정수가 아니어도 된다 — 한 틱 안을 훑을 때 소수로 들어온다.</param>
        public static float Angle(in Laser laser, float t)
        {
            float advance = laser.AngularSpeed * t;
            return laser.SweepHalfRange > 0f
                ? laser.StartAngle + Fold(advance, laser.SweepHalfRange)
                : laser.StartAngle + advance;
        }

        public static void SegmentAt(in Laser laser, float t, out Vector3 a, out Vector3 b)
        {
            float angle = Angle(laser, t);
            a = laser.Pivot;
            b = laser.Pivot + new Vector3(
                MathF.Cos(angle) * laser.Length, 0f, MathF.Sin(angle) * laser.Length);
        }

        /// <summary>
        /// 이 틱에 켜져 있나. 주기가 <b>정수 틱</b>이라 한 틱 안에서는 값이 변하지 않는다 —
        /// 그래서 판정은 틱 시작에 한 번만 보고 꺼져 있으면 통째로 건너뛸 수 있다.
        /// </summary>
        public static bool Lit(in Laser laser, long tick)
        {
            if (laser.Period <= 0 || laser.OnTicks >= laser.Period)
            {
                return true;
            }
            long m = (tick + laser.Phase) % laser.Period;
            if (m < 0)
            {
                m += laser.Period;
            }
            return m < laser.OnTicks;
        }
    }
}
```

- [ ] **Step 5: 테스트 통과를 확인한다**

Expected: `LaserGeometryTests` 10개 전부 PASS.

- [ ] **Step 6: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/Laser.cs Runtime/Scripts/Game/Laser.cs.meta \
        Runtime/Scripts/Game/LaserGeometry.cs Runtime/Scripts/Game/LaserGeometry.cs.meta \
        Tests/EditMode/LaserGeometryTests.cs Tests/EditMode/LaserGeometryTests.cs.meta
git commit -m "feat(skydive): 레이저를 틱의 함수로 정의한다"
```

---

## Task 6: 터널링 없는 판정 — Conservative Advancement

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserSweep.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/LaserSweepTests.cs`

**Interfaces:**
- Consumes: `LOP.Laser`, `LOP.LaserGeometry` (Task 5)
- Produces:
  - `LOP.LaserSweep.MaxIterations` (const int = 16)
  - `LOP.LaserSweep.HitTolerance` (const float = 0.01f)
  - `LOP.LaserSweep.SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2) → float`
  - `LOP.LaserSweep.Hit(in Laser laser, long tick, Vector3 bottomFrom, Vector3 topFrom, Vector3 bottomTo, Vector3 topTo, float capsuleRadius, out float timeOfImpact) → bool`
  - `LOP.LaserSweep.Hit(..., out float timeOfImpact, out bool exhausted) → bool` (오버로드 — 반복 상한까지 돌고도 결론을 못 내 관대하게 통과시켰는지)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/LaserSweepTests.cs`:

```csharp
using System.Numerics;
using LOP;
using NUnit.Framework;

public class LaserSweepTests
{
    private const float CapsuleRadius = 0.4f;
    private const float CapsuleHeight = 1.8f;

    // 원점을 지나 +X로 뻗은 고정 빔. 캐릭터는 X=0 근처를 세로로 지나간다.
    private static Laser FixedBeamAlongX()
        => new Laser(new Vector3(-20f, 1000f, 0f), length: 40f, radius: 0.3f,
                     startAngle: 0f, angularSpeed: 0f, sweepHalfRange: 0f,
                     period: 0, onTicks: 0, phase: 0);

    private static void Capsule(float y, out Vector3 bottom, out Vector3 top)
    {
        bottom = new Vector3(0f, y, 0f);
        top = new Vector3(0f, y + CapsuleHeight, 0f);
    }

    // 이 슬라이스의 존재 이유. 다이브 한 틱은 4.5m라 "틱 시작/끝만 보기"로는 얇은 빔을 그냥 지난다.
    [Test]
    public void 빠른_낙하가_얇은_빔을_통과하지_못한다()
    {
        Laser laser = FixedBeamAlongX();
        Capsule(1003f, out Vector3 bottomFrom, out Vector3 topFrom);
        Capsule(998.5f, out Vector3 bottomTo, out Vector3 topTo);

        bool hit = LaserSweep.Hit(laser, tick: 0, bottomFrom, topFrom, bottomTo, topTo,
                                  CapsuleRadius, out float toi);

        Assert.IsTrue(hit, "한 틱에 4.5m를 내려가며 y=1000의 빔을 지났는데 안 잡혔다");
        Assert.That(toi, Is.InRange(0f, 1f));
    }

    // 원 스펙의 서브스텝(캐릭터만 쪼개기)이 못 잡던 경우. 캐릭터는 멈춰 있고 빔이 훑고 지나간다.
    // 이 경우가 HitTolerance의 존재 이유이기도 하다 — 안전 전진 폭이 남은 거리에 비례해
    // 접촉 시각에 점점 가까워지기만 하므로, 허용 오차가 없으면 상한까지 돌다 놓친다.
    [Test]
    public void 정지한_캐릭터를_회전_빔이_훑으면_잡힌다()
    {
        // 캐릭터는 (10, 1000, 0)에 서 있고, 빔은 각도 -0.2에서 +0.2로 지나며 그 자리를 쓴다.
        var laser = new Laser(new Vector3(0f, 1000.5f, 0f), length: 20f, radius: 0.3f,
                              startAngle: -0.2f, angularSpeed: 0.4f, sweepHalfRange: 0f,
                              period: 0, onTicks: 0, phase: 0);
        var bottom = new Vector3(10f, 1000f, 0f);
        var top = new Vector3(10f, 1000f + CapsuleHeight, 0f);

        bool hit = LaserSweep.Hit(laser, tick: 0, bottom, top, bottom, top,
                                  CapsuleRadius, out _);

        Assert.IsTrue(hit, "빔이 한 틱 안에 캐릭터를 쓸고 지나갔는데 안 잡혔다");
    }

    [Test]
    public void 멀리_떨어져_있으면_안_맞는다()
    {
        Laser laser = FixedBeamAlongX();
        Capsule(1200f, out Vector3 bottomFrom, out Vector3 topFrom);
        Capsule(1195.5f, out Vector3 bottomTo, out Vector3 topTo);

        bool hit = LaserSweep.Hit(laser, tick: 0, bottomFrom, topFrom, bottomTo, topTo,
                                  CapsuleRadius, out _);

        Assert.IsFalse(hit);
    }

    [Test]
    public void 꺼진_레이저는_맞지_않는다()
    {
        var laser = new Laser(new Vector3(-20f, 1000f, 0f), 40f, 0.3f, 0f, 0f, 0f,
                              period: 10, onTicks: 4, phase: 0);
        Capsule(1003f, out Vector3 bottomFrom, out Vector3 topFrom);
        Capsule(998.5f, out Vector3 bottomTo, out Vector3 topTo);

        Assert.IsTrue(LaserSweep.Hit(laser, 0, bottomFrom, topFrom, bottomTo, topTo, CapsuleRadius, out _));
        Assert.IsFalse(LaserSweep.Hit(laser, 5, bottomFrom, topFrom, bottomTo, topTo, CapsuleRadius, out _));
    }

    // 캡슐 축은 세로, 빔은 가로라 3D에서 대개 어긋나 있다(2D에는 없는 경우). 2D 거리 공식을
    // 잘못 옮기면 여기서 걸린다.
    [Test]
    public void 어긋난_두_선분의_거리를_바르게_잰다()
    {
        var p1 = new Vector3(0f, 0f, 0f);
        var q1 = new Vector3(0f, 10f, 0f);      // 세로
        var p2 = new Vector3(-5f, 5f, 3f);
        var q2 = new Vector3(5f, 5f, 3f);       // 가로, z로 3만큼 어긋남

        float d = LaserSweep.SegmentDistance(p1, q1, p2, q2);

        Assert.AreEqual(3f, d, 1e-4f);
    }

    [Test]
    public void 끝점_밖에서는_끝점까지의_거리를_준다()
    {
        var p1 = new Vector3(0f, 0f, 0f);
        var q1 = new Vector3(0f, 1f, 0f);
        var p2 = new Vector3(0f, 5f, 0f);
        var q2 = new Vector3(0f, 9f, 0f);

        float d = LaserSweep.SegmentDistance(p1, q1, p2, q2);

        Assert.AreEqual(4f, d, 1e-4f);
    }

    [Test]
    public void 겹친_두_선분의_거리는_0이다()
    {
        var p1 = new Vector3(-1f, 0f, 0f);
        var q1 = new Vector3(1f, 0f, 0f);
        var p2 = new Vector3(0f, -1f, 0f);
        var q2 = new Vector3(0f, 1f, 0f);

        Assert.AreEqual(0f, LaserSweep.SegmentDistance(p1, q1, p2, q2), 1e-4f);
    }

    // 둘 다 멈춰 있고 닿지도 않으면 안전 전진 폭이 0이라 무한 반복이 될 수 있다.
    [Test]
    public void 아무것도_안_움직이면_바로_끝난다()
    {
        Laser laser = FixedBeamAlongX();
        Capsule(1050f, out Vector3 bottom, out Vector3 top);

        bool hit = LaserSweep.Hit(laser, 0, bottom, top, bottom, top, CapsuleRadius, out _);

        Assert.IsFalse(hit);
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Expected: FAIL — `LaserSweep`이 없다.

- [ ] **Step 3: `LaserSweep`을 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserSweep.cs`:

```csharp
using System;
using System.Numerics;

namespace LOP
{
    /// <summary>
    /// 한 틱 사이에 캐릭터가 레이저에 닿았는지 판정한다. 시간을 끊어 보면 다이브 속도에서
    /// 얇은 빔을 그냥 통과하므로(터널링), <b>확실히 안전한 만큼만 시간을 앞으로 감는</b>
    /// 방식으로 훑는다.
    ///
    /// <para>업계 표준 매핑: Mirtich의 Conservative Advancement(박사논문 §2.3.2, 3D 강체).
    /// PhysX가 자기 CCD를 <i>best-effort conservative advancement scheme</i>이라 문서에 적어
    /// 두었고, Bullet <c>btContinuousConvexCollision</c>도 같은 알고리즘이다.</para>
    ///
    /// <para>안전 전진 폭은 <c>(거리 − 허용) ÷ 최대 접근 속도</c>다. 최대 접근 속도는 캐릭터의
    /// 이동 거리에 <b>빔 끝이 그리는 호의 길이</b>(각속도 × 길이)를 더해 만든다 — 이는 CA의
    /// 표준 상한식(선속도 + 각속도 × 바운딩 스피어 반지름)을 우리 도형에 대입한 것이다.</para>
    /// </summary>
    public static class LaserSweep
    {
        /// <summary>스치면 수렴이 느려진다. 상한에 닿으면 통과로 본다 — 억울한 죽음이 더 나쁘다.</summary>
        public const int MaxIterations = 16;

        /// <summary>
        /// 이만큼 가까워지면 닿은 것으로 본다. <b>없으면 안 된다</b> — 안전 전진 폭이 남은 거리에
        /// 비례해서, 빔이 가로질러 오는 정상적인 경우에도 <c>d</c>가 허용 거리에 **점점 가까워지기만
        /// 하고 절대 닿지 않아** 상한까지 돌다 통과로 처리된다. Box2D도 같은 이유로 target에
        /// tolerance를 더해 멈춘다.
        /// </summary>
        public const float HitTolerance = 0.01f;

        /// <summary>
        /// 이 틱에 닿았나. <paramref name="timeOfImpact"/>는 틱 안에서의 시각(0~1)이다.
        /// </summary>
        public static bool Hit(in Laser laser, long tick,
                               Vector3 bottomFrom, Vector3 topFrom,
                               Vector3 bottomTo, Vector3 topTo,
                               float capsuleRadius, out float timeOfImpact)
            => Hit(laser, tick, bottomFrom, topFrom, bottomTo, topTo,
                   capsuleRadius, out timeOfImpact, out _);

        /// <param name="iterations">
        /// 돈 횟수. <see cref="MaxIterations"/>와 같으면 상한에 걸려 관대하게 통과시킨 것이다 —
        /// 이게 잦으면 레이저가 조용히 약해지므로 부르는 쪽이 세어 둔다.
        /// </param>
        public static bool Hit(in Laser laser, long tick,
                               Vector3 bottomFrom, Vector3 topFrom,
                               Vector3 bottomTo, Vector3 topTo,
                               float capsuleRadius, out float timeOfImpact, out int iterations)
        {
            timeOfImpact = 0f;
            iterations = 0;
            if (LaserGeometry.Lit(laser, tick) == false)
            {
                return false;
            }

            float allowed = capsuleRadius + laser.Radius;
            float moved = Vector3.Distance(bottomFrom, bottomTo);
            float tipArc = MathF.Abs(laser.AngularSpeed) * laser.Length;
            float closing = moved + tipArc;

            float t = 0f;
            for (int i = 0; i < MaxIterations; i++)
            {
                iterations = i + 1;
                Vector3 bottom = Vector3.Lerp(bottomFrom, bottomTo, t);
                Vector3 top = Vector3.Lerp(topFrom, topTo, t);
                LaserGeometry.SegmentAt(laser, tick + t, out Vector3 a, out Vector3 b);

                float d = SegmentDistance(bottom, top, a, b);
                if (d <= allowed + HitTolerance)
                {
                    timeOfImpact = t;
                    return true;
                }
                if (closing <= 1e-6f)
                {
                    return false;   // 둘 다 안 움직인다 — 지금 안 닿았으면 이 틱엔 안 닿는다
                }

                t += (d - allowed) / closing;
                if (t >= 1f)
                {
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 3D 선분 두 개의 최단거리. 세로 캡슐 축과 가로 빔은 대개 어긋나 있어(skew) 평면 공식으로는
        /// 안 된다. (Ericson, Real-Time Collision Detection §5.1.9)
        /// </summary>
        public static float SegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            const float eps = 1e-8f;

            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);

            float s, t;
            if (a <= eps && e <= eps)
            {
                return r.Length();
            }
            if (a <= eps)
            {
                s = 0f;
                t = Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= eps)
                {
                    t = 0f;
                    s = Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > eps ? Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Clamp01((b - c) / a);
                    }
                }
            }
            return Vector3.Distance(p1 + d1 * s, p2 + d2 * t);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
```

- [ ] **Step 4: 테스트 통과를 확인한다**

Expected: `LaserSweepTests` 8개 전부 PASS. 특히 `빠른_낙하가_얇은_빔을_통과하지_못한다`와
`정지한_캐릭터를_회전_빔이_훑으면_잡힌다`가 Step 2에서 **실패했다가** 여기서 통과해야 한다 —
둘 다 이 알고리즘을 고른 이유 그 자체다.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/LaserSweep.cs Runtime/Scripts/Game/LaserSweep.cs.meta \
        Tests/EditMode/LaserSweepTests.cs Tests/EditMode/LaserSweepTests.cs.meta
git commit -m "feat(skydive): 터널링 없는 레이저 판정(Conservative Advancement)"
```

---

## Task 7: 레이저 보관소와 맵 마커

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserField.cs`
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserVolume.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/LaserFieldTests.cs`

**Interfaces:**
- Consumes: `LOP.Laser` (Task 5)
- Produces:
  - `LOP.LaserField` — `void Add(Laser laser)`, `bool Remove(in Laser laser)`, `void Clear()`, `IReadOnlyList<Laser> All`
  - `LOP.LaserVolume` (MonoBehaviour) — public `Length`, `Radius`, `StartAngleDegrees`, `AngularSpeedDegreesPerTick`, `SweepHalfRangeDegrees`, `Period`, `OnTicks`, `Phase`

**참고 — `WindField`와 다른 점:** 바람은 겹친 볼륨의 합을 매 틱 계산해야 해서 정렬이 필요했지만,
레이저는 **각각 독립으로 판정**하므로 순서가 결과에 영향을 주지 않는다. 정렬하지 않는다.

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/LaserFieldTests.cs`:

```csharp
using System.Numerics;
using LOP;
using NUnit.Framework;

public class LaserFieldTests
{
    private static Laser Any(float startAngle)
        => new Laser(Vector3.Zero, 10f, 0.5f, startAngle, 0f, 0f, 0, 0, 0);

    [Test]
    public void 비어_있는_판에는_레이저가_없다()
    {
        var field = new LaserField();

        Assert.AreEqual(0, field.All.Count);
    }

    [Test]
    public void 넣은_순서대로_들어간다()
    {
        var field = new LaserField();
        field.Add(Any(1f));
        field.Add(Any(2f));

        Assert.AreEqual(2, field.All.Count);
        Assert.AreEqual(1f, field.All[0].StartAngle, 1e-5f);
        Assert.AreEqual(2f, field.All[1].StartAngle, 1e-5f);
    }

    // 라운드가 여러 판이면 맵을 다시 로드한다. 안 비우면 레이저가 두 배가 된다.
    [Test]
    public void 비우면_다_사라진다()
    {
        var field = new LaserField();
        field.Add(Any(1f));
        field.Add(Any(2f));

        field.Clear();

        Assert.AreEqual(0, field.All.Count);
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Expected: FAIL — `LaserField`가 없다.

- [ ] **Step 3: `LaserField`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserField.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 이 판에 놓인 레이저 전부. 맵 씬의 <see cref="LaserVolume"/> 마커가 로드될 때 스스로 들어온다.
    ///
    /// <para><see cref="WindField"/>와 달리 정렬하지 않는다 — 바람은 겹친 볼륨의 합을 구해야 해서
    /// 순서가 부동소수 합에 새어 들어갔지만, 레이저는 각각 독립으로 판정하므로 순서가 결과를
    /// 바꾸지 않는다.</para>
    /// </summary>
    public class LaserField
    {
        private readonly List<Laser> _lasers = new List<Laser>();

        public IReadOnlyList<Laser> All => _lasers;

        public void Add(Laser laser) => _lasers.Add(laser);

        /// <summary>
        /// 등록했던 레이저 하나를 뺀다. <see cref="Laser"/>가 값이라 참조가 아니라 <b>값으로</b>
        /// 찾는데, 완전히 같은 레이저가 둘이면 어느 쪽을 빼도 결과가 같아 문제되지 않는다.
        /// </summary>
        public bool Remove(in Laser laser) => _lasers.Remove(laser);

        public void Clear() => _lasers.Clear();
    }
}
```

- [ ] **Step 4: 테스트 통과를 확인한다**

Expected: `LaserFieldTests` 3개 PASS.

- [ ] **Step 5: `LaserVolume` 마커를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/LaserVolume.cs`:

```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 맵 씬에 놓는 레이저 표시. 맵이 올라올 때 <see cref="LaserField"/>를 주입받아 스스로 등록한다.
    ///
    /// <para><see cref="WindVolume"/>과 같은 이유로 <b>공용 패키지</b>에 있다: 맵 씬은 클라에서 굽고
    /// 서버가 읽는데, 스크립트가 한쪽에만 있으면 반대쪽에서 missing script가 되고 그 빈 컴포넌트가
    /// 씬 주입을 끊는다.</para>
    ///
    /// <para>각도를 <b>도(degree)</b>로 노출하는 것은 씬 인스펙터에서 사람이 읽고 고치기 때문이다.
    /// 라디안 변환은 여기서 한 번만 한다.</para>
    /// </summary>
    [SceneInjectMonoBehaviour]
    public class LaserVolume : MonoBehaviour
    {
        /// <summary>빔 길이. 이 오브젝트의 위치가 회전 중심(Pivot)이다.</summary>
        public float Length = 30f;

        /// <summary>빔 굵기(반지름).</summary>
        public float Radius = 0.6f;

        public float StartAngleDegrees = 0f;

        /// <summary>도 / 틱. 0이면 고정 빔이다.</summary>
        public float AngularSpeedDegreesPerTick = 0f;

        /// <summary>0보다 크면 전회전 대신 이 폭만큼 왕복한다.</summary>
        public float SweepHalfRangeDegrees = 0f;

        /// <summary>점멸 주기(틱). 0 이하면 늘 켜져 있다.</summary>
        public int Period = 0;
        public int OnTicks = 0;
        public int Phase = 0;

        public Laser ToLaser() => new Laser(
            transform.position.ToNumerics(),
            Length, Radius,
            StartAngleDegrees * Mathf.Deg2Rad,
            AngularSpeedDegreesPerTick * Mathf.Deg2Rad,
            SweepHalfRangeDegrees * Mathf.Deg2Rad,
            Period, OnTicks, Phase);

        private LaserField field;
        private Laser registered;
        private bool hasRegistered;

        [Inject]
        public void Construct(LaserField field)
        {
            this.field = field;
            registered = ToLaser();
            hasRegistered = true;
            field.Add(registered);
        }

        private void OnDestroy()
        {
            // 라운드가 여러 판이면 맵을 다시 로드한다 — 안 빼면 레이저가 두 배가 된다.
            // 등록할 때의 값을 그대로 들고 있다가 뺀다(그 사이 필드가 바뀌어도 짝이 맞게).
            if (hasRegistered && field != null)
            {
                field.Remove(registered);
            }
        }
    }
}
```

> `WindVolume`과 **같은 방식**으로 라운드 사이를 책임진다: 등록한 값을 캐시해 두었다가 `OnDestroy`에서
> 그것을 뺀다. 시스템이 첫 틱에 `Clear()`를 부르는 방식은 쓸 수 없다 — 등록은 맵 로드 시
> `InjectSceneObjects`에서 일어나고 그건 첫 틱보다 **앞서므로**, 첫 틱에 비우면 방금 등록한 것을
> 통째로 지운다.

- [ ] **Step 6: 컴파일을 확인한다**

Expected: 콘솔 에러 0. `[SceneInjectMonoBehaviour]`와 `ToNumerics()`가 해석되는지 본다
(`WindVolume.cs`가 같은 것을 쓴다).

- [ ] **Step 7: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/LaserField.cs Runtime/Scripts/Game/LaserField.cs.meta \
        Runtime/Scripts/Game/LaserVolume.cs Runtime/Scripts/Game/LaserVolume.cs.meta \
        Tests/EditMode/LaserFieldTests.cs Tests/EditMode/LaserFieldTests.cs.meta
git commit -m "feat(skydive): 레이저 보관소와 맵 마커"
```

---

## Task 8: 체크포인트 계산 (순수)

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveCheckpoints.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/SkydiveCheckpointsTests.cs`

**Interfaces:**
- Produces: `LOP.SkydiveCheckpoints.LastPassedShelfY(float deathY, IReadOnlyList<float> shelfYs, float spawnY) → float`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/SkydiveCheckpointsTests.cs`:

```csharp
using LOP;
using NUnit.Framework;

public class SkydiveCheckpointsTests
{
    private static readonly float[] Shelves =
        { 2600f, 2200f, 1800f, 1400f, 1000f, 600f, 200f };

    private const float SpawnY = 3000f;

    [Test]
    public void 지나온_선반_중_가장_낮은_것으로_돌아간다()
    {
        Assert.AreEqual(1800f, SkydiveCheckpoints.LastPassedShelfY(1500f, Shelves, SpawnY), 1e-4f);
    }

    [Test]
    public void 첫_선반_위에서_죽으면_스폰_고도다()
    {
        Assert.AreEqual(SpawnY, SkydiveCheckpoints.LastPassedShelfY(2800f, Shelves, SpawnY), 1e-4f);
    }

    [Test]
    public void 마지막_선반_아래에서_죽으면_마지막_선반이다()
    {
        Assert.AreEqual(200f, SkydiveCheckpoints.LastPassedShelfY(50f, Shelves, SpawnY), 1e-4f);
    }

    // 선반 고도에 정확히 있을 때. 그 선반을 "지났다"고 보면 제자리 부활이 되어 다시 그 레이저에
    // 걸린다 — 바로 위 선반으로 보낸다.
    [Test]
    public void 선반_고도에_정확히_있으면_그_위_선반으로_간다()
    {
        Assert.AreEqual(1800f, SkydiveCheckpoints.LastPassedShelfY(1400f, Shelves, SpawnY), 1e-4f);
    }

    [Test]
    public void 표의_순서가_뒤섞여_있어도_답이_같다()
    {
        var shuffled = new[] { 600f, 2600f, 200f, 1800f, 1000f, 2200f, 1400f };

        Assert.AreEqual(1800f, SkydiveCheckpoints.LastPassedShelfY(1500f, shuffled, SpawnY), 1e-4f);
    }

    [Test]
    public void 선반이_하나도_없으면_스폰_고도다()
    {
        Assert.AreEqual(SpawnY, SkydiveCheckpoints.LastPassedShelfY(1500f, new float[0], SpawnY), 1e-4f);
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Expected: FAIL — `SkydiveCheckpoints`가 없다.

- [ ] **Step 3: 구현한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveCheckpoints.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 죽은 고도로부터 되돌아갈 선반을 고른다. <b>저장할 상태가 없다</b> — 코스가 아래 한 방향이라
    /// y 하나로 "어디까지 왔나"가 정해진다(<see cref="SkydiveProgress"/>가 완주를 y로만 재는 것과 같다).
    /// </summary>
    public static class SkydiveCheckpoints
    {
        /// <summary>
        /// 마지막으로 지나온 선반의 고도. 지나온 선반이 없으면 <paramref name="spawnY"/>.
        ///
        /// <para>선반 고도에 정확히 있는 경우는 <b>아직 안 지난 것</b>으로 본다 — 지났다고 보면
        /// 제자리에 부활해 방금 맞은 레이저에 곧바로 다시 걸린다.</para>
        /// </summary>
        public static float LastPassedShelfY(float deathY, IReadOnlyList<float> shelfYs, float spawnY)
        {
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < shelfYs.Count; i++)
            {
                float y = shelfYs[i];
                if (y > deathY && y < best)
                {
                    best = y;
                    found = true;
                }
            }
            return found ? best : spawnY;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과를 확인한다**

Expected: `SkydiveCheckpointsTests` 6개 PASS.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/SkydiveCheckpoints.cs Runtime/Scripts/Game/SkydiveCheckpoints.cs.meta \
        Tests/EditMode/SkydiveCheckpointsTests.cs Tests/EditMode/SkydiveCheckpointsTests.cs.meta
git commit -m "feat(skydive): 죽은 고도에서 체크포인트를 계산한다"
```

---

## Task 9: 서버가 판정하고 부활시킨다

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/SkydiveLaserSystem.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`

**Interfaces:**
- Consumes: `LaserField`, `LaserSweep.Hit`, `SkydiveCheckpoints.LastPassedShelfY`,
  `EntityMotionExtensions.Teleport`, `SkydiveConfig`
- Produces: `LOP.SkydiveLaserSystem : GameFramework.Runner.ITickSystem`

**틱 순서:** `RunPhase<End>`에 등록한다. 그러면 `world.Tick`(이동) 뒤, `entitySnapshotBroadcastSystem`
(송신) 앞에 돌아 **부활이 같은 틱의 스냅샷에 실린다.** `FinishLineTrackingSystem`보다 **먼저**
등록해, 레이저에 맞은 그 틱에 결승 통과로도 잡히는 일이 없게 한다.

**직전 위치를 어떻게 아나:** 시스템이 매 틱 끝에 위치를 캐시해 두고, 다음 틱에 그것을 "이번 틱의
시작 위치"로 쓴다. 틱 N의 끝 == 틱 N+1의 시작이라 정확하다. 캐시가 없는 첫 틱은 건너뛴다.

- [ ] **Step 1: 시스템을 만든다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/SkydiveLaserSystem.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 레이저에 닿았는지 매 틱 판정하고, 닿았으면 마지막으로 지난 선반으로 되돌린다.
    ///
    /// <para><b>서버에서만 돈다.</b> 클라는 죽음을 예측하지 않는다 — 잘못 예측한 죽음은 되돌릴 때
    /// 훨씬 잔인하고, 스치는 판정에서 갈리면 그 대가가 선반 하나만큼의 위치 불일치다.
    /// (2026-07-12에 같은 이유로 클라 데미지 예측을 짓지 않기로 했다.)</para>
    /// </summary>
    public class SkydiveLaserSystem : GameFramework.Runner.ITickSystem
    {
        //  부활 지점 근처를 지나는 빔에 즉시 다시 죽는 고리를 막는다.
        private const float InvulnerableSeconds = 2.0f;

        //  같은 자리에 여러 명이 부활하면 서로 밀어낸다(캐릭터끼리는 단단한 벽이다).
        private const float RespawnSpreadRadius = 2f;
        private const int RespawnSpreadCount = 6;

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly LaserField laserField;
        private readonly SkydiveConfig config;
        private readonly IReadOnlyList<float> shelfYs;
        private readonly float spawnY;
        private readonly IReadOnlyDictionary<float, Vector3> respawnPoints;

        //  틱 N의 끝 위치 == 틱 N+1의 시작 위치. 이 캐시가 이번 틱에 지나온 경로를 만든다.
        private readonly Dictionary<string, Vector3> previousPositions = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, float> invulnerableUntil = new Dictionary<string, float>();
        private readonly Dictionary<float, int> respawnCounts = new Dictionary<float, int>();
        private readonly List<GameFramework.World.Entity> divers = new List<GameFramework.World.Entity>();
        private int cappedIterations;   // CA가 상한까지 돌아 관대하게 통과시킨 횟수

        public SkydiveLaserSystem(GameFramework.World.EntityRegistry entityRegistry,
                                  LaserField laserField,
                                  SkydiveConfig config,
                                  IReadOnlyList<float> shelfYs,
                                  float spawnY,
                                  IReadOnlyDictionary<float, Vector3> respawnPoints)
        {
            this.entityRegistry = entityRegistry;
            this.laserField = laserField;
            this.config = config;
            this.shelfYs = shelfYs;
            this.spawnY = spawnY;
            this.respawnPoints = respawnPoints;
        }

        public void Tick(long tick, float deltaTime)
        {
            CollectDivers();

            float now = tick * deltaTime;

            for (int i = 0; i < divers.Count; i++)
            {
                GameFramework.World.Entity diver = divers[i];
                Vector3 to = GameFramework.World.EntityMotionExtensions.GetPosition(diver);

                if (previousPositions.TryGetValue(diver.Id, out Vector3 from) == false)
                {
                    previousPositions[diver.Id] = to;
                    continue;   // 첫 틱은 지나온 경로가 없다
                }
                previousPositions[diver.Id] = to;

                if (invulnerableUntil.TryGetValue(diver.Id, out float until) && now < until)
                {
                    continue;
                }

                if (AnyLaserHits(tick, from, to))
                {
                    Respawn(diver, to.y, now);
                }
            }
        }

        private bool AnyLaserHits(long tick, Vector3 from, Vector3 to)
        {
            float radius = config.BodyRadius;
            float height = config.BodyHeight;

            var bottomFrom = new System.Numerics.Vector3(from.x, from.y, from.z);
            var topFrom = new System.Numerics.Vector3(from.x, from.y + height, from.z);
            var bottomTo = new System.Numerics.Vector3(to.x, to.y, to.z);
            var topTo = new System.Numerics.Vector3(to.x, to.y + height, to.z);

            IReadOnlyList<Laser> lasers = laserField.All;
            for (int i = 0; i < lasers.Count; i++)
            {
                bool hit = LaserSweep.Hit(lasers[i], tick, bottomFrom, topFrom, bottomTo, topTo,
                                          radius, out _, out bool exhausted);
                //  상한까지 돌고도 결론이 안 나면 관대하게 통과시킨다. 잦으면 레이저가 조용히
                //  약해지므로 센다.
                if (exhausted)
                {
                    cappedIterations++;
                    if (cappedIterations % 100 == 1)
                    {
                        Debug.LogWarning($"[Laser] CA 반복 상한 도달 누적 {cappedIterations}회 — " +
                                         "잦으면 MaxIterations를 올려야 한다");
                    }
                }
                if (hit)
                {
                    return true;
                }
            }
            return false;
        }

        private void Respawn(GameFramework.World.Entity diver, float deathY, float now)
        {
            float shelfY = SkydiveCheckpoints.LastPassedShelfY(deathY, shelfYs, spawnY);

            Vector3 basePoint = respawnPoints.TryGetValue(shelfY, out Vector3 point)
                ? point
                : new Vector3(0f, shelfY, 0f);

            respawnCounts.TryGetValue(shelfY, out int order);
            respawnCounts[shelfY] = order + 1;
            float angle = order % RespawnSpreadCount * (2f * Mathf.PI / RespawnSpreadCount);
            var spread = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * RespawnSpreadRadius;

            GameFramework.World.EntityMotionExtensions.Teleport(diver, basePoint + spread);
            GameFramework.World.EntityMotionExtensions.SetVelocity(diver, Vector3.zero);
            previousPositions[diver.Id] = basePoint + spread;

            var stamina = diver.Get<Stamina>();
            if (stamina != null)
            {
                stamina.Current = config.StaminaMax;
                stamina.EmergencyUsed = false;
                stamina.EmergencyRemaining = 0f;
            }

            //  펴진 채로 부활하면 조작이 끊긴 것처럼 보인다. 대자(Axis 0)로 되돌린다.
            var posture = diver.Get<Posture>();
            if (posture != null)
            {
                posture.Gliding = false;
                posture.Axis = 0f;
            }

            invulnerableUntil[diver.Id] = now + InvulnerableSeconds;

            Debug.Log($"[Laser] {diver.Id} 부활 — 죽은 고도 {deathY:F0} → 선반 {shelfY:F0}");
        }

        //  SkydiveWorld.CollectDivers와 같은 기준이어야 한다 — 다른 집합을 보면 판정과 시뮬이 어긋난다.
        private void CollectDivers()
        {
            divers.Clear();
            foreach (GameFramework.World.Entity entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                if (entity.Has<GameFramework.World.Simulated>() == false)
                {
                    continue;
                }
                divers.Add(entity);
            }
        }
    }
}
```

- [ ] **Step 2: DI에 등록한다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`의
`builder.Register<IGameRuleSystem, SkydiveRuleSystem>(Lifetime.Singleton);` **뒤에** 넣는다:

```csharp
            // 맵 씬의 LaserVolume 마커가 맵 로드 시 여기에 자기를 넣는다.
            builder.Register<LaserField>(Lifetime.Singleton);
            builder.Register(c => new SkydiveLaserSystem(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<LaserField>(),
                c.Resolve<SkydiveConfig>(),
                SkydiveCourseLayout.ShelfYs,
                SkydiveCourseLayout.SpawnY,
                SkydiveCourseLayout.RespawnPoints), Lifetime.Singleton);
```

그리고 `RegisterBuildCallback` 블록에서 **결승선보다 먼저** 물린다:

```csharp
            builder.RegisterBuildCallback(container =>
            {
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<SkydiveLaserSystem>());
                runner.RegisterSystem<LOP.Event.LOPRunner.Update.End>(
                    container.Resolve<FinishLineTrackingSystem>());
            });
```

- [ ] **Step 3: 코스 배치표를 공용 패키지에 만든다**

`SkydiveLaserSystem`은 선반 고도와 부활 지점을 알아야 하는데, 그 값은 지금 클라 에디터의
`SkydiveCourseBuilder`에만 있다(서버가 못 본다). **양쪽이 같은 표를 보도록** 공용 패키지로 옮긴다.

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveCourseLayout.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 코스의 뼈대 좌표. <b>굽는 쪽(클라 에디터)과 판정하는 쪽(서버)이 같은 값을 봐야</b> 해서
    /// 공용 패키지에 있다 — 한쪽에만 두면 표를 고칠 때 조용히 어긋난다.
    /// </summary>
    public static class SkydiveCourseLayout
    {
        public const float SpawnY = 3000f;

        /// <summary>선반 고도. 위에서 아래 순서.</summary>
        public static readonly IReadOnlyList<float> ShelfYs =
            new[] { 2600f, 2200f, 1800f, 1400f, 1000f, 600f, 200f };

        /// <summary>
        /// 선반 고도 → 그 선반 위 부활 지점. 규칙으로 유도하지 않고 적어 두는 이유는 선반마다
        /// 구멍 위치가 달라, 규칙 한 줄로 만들면 표를 고칠 때 구멍 위에 세우게 되기 때문이다.
        /// 전부 구멍 중심에서 40m 떨어져 있고 판(±100) 안이며 기둥(±60)과도 겹치지 않는다.
        /// </summary>
        public static readonly IReadOnlyDictionary<float, Vector3> RespawnPoints =
            new Dictionary<float, Vector3>
            {
                { 2600f, new Vector3(0f, 2600f, 40f) },
                { 2200f, new Vector3(30f, 2200f, 40f) },
                { 1800f, new Vector3(30f, 1800f, -10f) },
                { 1400f, new Vector3(-25f, 1400f, -10f) },
                { 1000f, new Vector3(-25f, 1000f, 10f) },
                { 600f, new Vector3(30f, 600f, 15f) },
                { 200f, new Vector3(0f, 200f, -15f) },
            };
    }
}
```

- [ ] **Step 4: 낡아진 주석을 고친다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`의 클래스 XML 주석 마지막 줄이
아직 이렇게 되어 있다:

```
    /// 레이저 판정은 Detection에 들어오지만(슬라이스 4) 지금은 비어 있다.
```

판정이 공유 월드가 아니라 **서버 시스템**으로 갔으므로(스펙 §8.1) 이 줄을 지운다. 남겨 두면 다음에
읽는 사람이 없는 코드를 찾는다.

- [ ] **Step 5: 컴파일과 기존 테스트를 확인한다**

클라 에디터 재컴파일 → 콘솔 에러 0 → EditMode 전체 실행.
Expected: 기존 테스트 전부 PASS.

- [ ] **Step 6: 커밋 (레포 두 개)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/SkydiveCourseLayout.cs Runtime/Scripts/Game/SkydiveCourseLayout.cs.meta         Runtime/Scripts/Game/SkydiveWorld.cs
git commit -m "feat(skydive): 코스 뼈대 좌표를 클·서가 함께 본다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/TickSystems/SkydiveLaserSystem.cs \
        Assets/Scripts/Game/TickSystems/SkydiveLaserSystem.cs.meta \
        Assets/Scripts/Game/SkydiveLifetimeScope.cs
git commit -m "feat(skydive): 레이저에 맞으면 체크포인트로 되돌린다"
```

---

## Task 10: 코스에 레이저를 놓고, 막힌 배치는 굽기를 거절한다

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Tests/Editor/SkydiveLaserBuildTests.cs`

**Interfaces:**
- Consumes: `Laser`, `LaserGeometry`, `LaserSweep`, `SkydiveCourseLayout` (Tasks 5·6·9)
- Produces:
  - `SkydiveCourseBuilder.LaserSpec` (internal readonly struct) — `Name`, `Pivot`(Vector3), `Length`, `Radius`, `StartAngleDegrees`, `AngularSpeedDegreesPerTick`, `SweepHalfRangeDegrees`, `Period`, `OnTicks`, `Phase`
  - `SkydiveCourseBuilder.Lasers` (internal static readonly LaserSpec[])
  - `SkydiveCourseBuilder.FindBlockedGate() → string` / `FindBlockedGate(IReadOnlyList<LaserSpec>) → string`
  - `SkydiveCourseBuilder.FindInvalidRespawn() → string` (검사 대상은 `LOP.SkydiveCourseLayout.RespawnPoints`)
  - `SkydiveCourseBuilder.FindShelfLayoutDrift() → string`
  - `SkydiveCourseBuilder.FindTooFastLaser(IReadOnlyList<LaserSpec>) → string`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Client/Assets/Tests/Editor/SkydiveLaserBuildTests.cs`:

```csharp
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;

public class SkydiveLaserBuildTests
{
    // 표대로 구웠을 때 모든 구멍이 언젠가 열려야 한다. 안 열리면 에러 없이 판이 안 끝난다.
    [Test]
    public void 모든_구멍이_언젠가_열린다()
    {
        string failure = SkydiveCourseBuilder.FindBlockedGate();

        Assert.IsNull(failure, failure);
    }

    // 위 테스트는 통과하는 배치만 본다 — 진짜로 막는 배치를 던져도 걸리는지는 아무것도 검증되지
    // 않는다. 구멍을 통째로 덮는 굵은 고정 빔으로 확인한다.
    [Test]
    public void 구멍을_통째로_덮는_빔은_걸린다()
    {
        var blocking = new[]
        {
            new SkydiveCourseBuilder.LaserSpec(
                "Test_Block", new Vector3(30f, 2200f, 0f),
                length: 40f, radius: 40f,
                startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f, sweepHalfRangeDegrees: 0f,
                period: 0, onTicks: 0, phase: 0),
        };

        string failure = SkydiveCourseBuilder.FindBlockedGate(blocking);

        Assert.IsNotNull(failure);
        StringAssert.Contains("2200", failure);
    }

    [Test]
    public void 부활_지점은_모두_판_위이고_구멍_밖이다()
    {
        string failure = SkydiveCourseBuilder.FindInvalidRespawn();

        Assert.IsNull(failure, failure);
    }

    // 굽는 쪽(빌더)과 판정하는 쪽(서버)이 다른 선반 표를 보면 부활이 허공에 사람을 세운다.
    [Test]
    public void 빌더와_서버가_같은_선반_표를_본다()
    {
        string failure = SkydiveCourseBuilder.FindShelfLayoutDrift();

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 너무_빨리_도는_레이저는_걸린다()
    {
        var tooFast = new[]
        {
            new SkydiveCourseBuilder.LaserSpec(
                "Test_Spin", new Vector3(0f, 1000f, 0f),
                length: 30f, radius: 0.6f,
                startAngleDegrees: 0f, angularSpeedDegreesPerTick: 40f, sweepHalfRangeDegrees: 0f,
                period: 0, onTicks: 0, phase: 0),
        };

        string failure = SkydiveCourseBuilder.FindTooFastLaser(tooFast);

        Assert.IsNotNull(failure);
        StringAssert.Contains("Test_Spin", failure);
    }

    [Test]
    public void 표의_레이저는_모두_읽을_수_있는_속도다()
    {
        string failure = SkydiveCourseBuilder.FindTooFastLaser(SkydiveCourseBuilder.Lasers);

        Assert.IsNull(failure, failure);
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Expected: FAIL — `LaserSpec`·`Lasers`·검사 함수들이 없다.

- [ ] **Step 3: `LaserSpec`과 표를 더한다**

`SkydiveCourseBuilder.cs`의 `WindSpec`/`Winds` 블록 **뒤에** 넣는다:

```csharp
        internal readonly struct LaserSpec
        {
            public readonly string Name;
            public readonly Vector3 Pivot;
            public readonly float Length;
            public readonly float Radius;
            public readonly float StartAngleDegrees;
            public readonly float AngularSpeedDegreesPerTick;
            public readonly float SweepHalfRangeDegrees;
            public readonly int Period;
            public readonly int OnTicks;
            public readonly int Phase;

            public LaserSpec(string name, Vector3 pivot, float length, float radius,
                             float startAngleDegrees, float angularSpeedDegreesPerTick,
                             float sweepHalfRangeDegrees, int period, int onTicks, int phase)
            {
                Name = name;
                Pivot = pivot;
                Length = length;
                Radius = radius;
                StartAngleDegrees = startAngleDegrees;
                AngularSpeedDegreesPerTick = angularSpeedDegreesPerTick;
                SweepHalfRangeDegrees = sweepHalfRangeDegrees;
                Period = period;
                OnTicks = onTicks;
                Phase = phase;
            }

            public LOP.Laser ToLaser() => new LOP.Laser(
                new System.Numerics.Vector3(Pivot.x, Pivot.y, Pivot.z),
                Length, Radius,
                StartAngleDegrees * Mathf.Deg2Rad,
                AngularSpeedDegreesPerTick * Mathf.Deg2Rad,
                SweepHalfRangeDegrees * Mathf.Deg2Rad,
                Period, OnTicks, Phase);
        }

        // 한 틱에 이보다 크게 돌면 다음 자리를 눈으로 예측할 수 없다 — 피할 수 없는 것은
        // 장애물이 아니라 주사위다.
        private const float MaxAngularSpeedDegreesPerTick = 15f;

        // 코스 설계 그 자체. 구간마다 어법이 다르다: 문지기 → 격자 → 합침.
        // 문지기는 구멍 중심을 피벗으로 삼아 구멍 위를 쓸고, 격자는 통로를 가로지른다.
        internal static readonly LaserSpec[] Lasers =
        {
            // 2600 위: 없음 — 조작을 익히는 자리

            // 2200 구멍(30,0) 문지기 — 느린 회전. 반대편으로 들어가면 된다.
            new LaserSpec("Laser_2200_Gate", new Vector3(30f, 2215f, 0f),
                          length: 26f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 4f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),

            // 1800 구멍(30,30) 문지기 — 왕복. 오는 것이 보인다.
            new LaserSpec("Laser_1800_Gate", new Vector3(30f, 1815f, 30f),
                          length: 24f, radius: 0.6f,
                          startAngleDegrees: 90f, angularSpeedDegreesPerTick: 6f,
                          sweepHalfRangeDegrees: 70f, period: 0, onTicks: 0, phase: 0),

            // 1400~1800 통로: 격자 연습 — 벽에서 뻗은 고정 빔 두 층
            new LaserSpec("Laser_1650_Bar", new Vector3(-100f, 1650f, 0f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_1500_Bar", new Vector3(100f, 1500f, 40f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 180f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),

            // 1400 구멍(-25,30) 문지기 + 통로 격자
            new LaserSpec("Laser_1400_Gate", new Vector3(-25f, 1415f, 30f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 45f, angularSpeedDegreesPerTick: 7f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_1250_Bar", new Vector3(-100f, 1250f, -20f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),

            // 1000 구멍(-25,-30) 문지기 + 점멸 격자(리듬)
            new LaserSpec("Laser_1000_Gate", new Vector3(-25f, 1015f, -30f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 200f, angularSpeedDegreesPerTick: 8f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_900_Blink", new Vector3(100f, 900f, 0f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 180f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 40, onTicks: 20, phase: 0),
            new LaserSpec("Laser_800_Blink", new Vector3(-100f, 800f, 30f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 40, onTicks: 20, phase: 20),

            // 600 구멍(30,-25) 문지기 — 빠르게
            new LaserSpec("Laser_600_Gate", new Vector3(30f, 615f, -25f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 11f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_450_Blink", new Vector3(100f, 450f, -40f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 180f, angularSpeedDegreesPerTick: 0f,
                          sweepHalfRangeDegrees: 0f, period: 30, onTicks: 15, phase: 0),

            // 200 구멍(0,25) — 셋을 합친다
            new LaserSpec("Laser_200_Gate", new Vector3(0f, 215f, 25f),
                          length: 22f, radius: 0.6f,
                          startAngleDegrees: 120f, angularSpeedDegreesPerTick: 12f,
                          sweepHalfRangeDegrees: 0f, period: 0, onTicks: 0, phase: 0),
            new LaserSpec("Laser_320_Sweep", new Vector3(-100f, 320f, 0f),
                          length: 150f, radius: 0.6f,
                          startAngleDegrees: 0f, angularSpeedDegreesPerTick: 5f,
                          sweepHalfRangeDegrees: 40f, period: 0, onTicks: 0, phase: 0),
        };
```

- [ ] **Step 4: `Shelf`에 부활 지점을 더한다**

`Shelf` struct와 `Shelves` 표를 이 내용으로 바꾼다:

```csharp
        private readonly struct Shelf
        {
            public readonly float Y;
            public readonly float HoleX;
            public readonly float HoleZ;
            public readonly float HoleHalf;

            public Shelf(float y, float holeX, float holeZ, float holeSide)
            {
                Y = y;
                HoleX = holeX;
                HoleZ = holeZ;
                HoleHalf = holeSide * 0.5f;
            }
        }

        // 코스 설계 그 자체. 위에서 아래 순서로 적는다.
        // 부활 지점은 여기 없다 — 서버가 그 값으로 사람을 세우므로 LOP.SkydiveCourseLayout이
        // 진실원본이고, 여기서 또 적으면 두 곳이 조용히 어긋난다.
        private static readonly Shelf[] Shelves =
        {
            new Shelf(2600f, 0f, 0f, 30f),      // 스폰 바로 아래 — 아무것도 안 해도 지나간다
            new Shelf(2200f, 30f, 0f, 24f),     // 옆으로 가는 걸 가르치는 구간(다이브로도 닿는다)
            new Shelf(1800f, 30f, 30f, 20f),
            new Shelf(1400f, -25f, 30f, 20f),   // 여기부터 넷은 다이브로 곧장 가면 못 닿는다
            new Shelf(1000f, -25f, -30f, 16f),
            new Shelf(600f, 30f, -25f, 16f),
            new Shelf(200f, 0f, 25f, 16f),
        };
```

- [ ] **Step 5: 검사 셋을 더한다**

`FindImpassableSection` 메서드들 **뒤에** 넣는다:

```csharp
        // 구멍을 얼마나 촘촘히 훑을지. 한 변을 이만큼 나눈다.
        private const int GateGridSteps = 12;
        // 몇 틱까지 봐야 "언젠가 열린다"를 말할 수 있나. 표의 가장 긴 주기보다 넉넉히 크게.
        private const int GateSampleTicks = 240;
        // 통과하려면 몸이 들어갈 자리가 있어야 한다.
        private const float BodyRadiusForGateCheck = 0.4f;

        /// <summary>
        /// 어느 선반의 구멍이 <b>한 번도 안 열리면</b> 그 설명을, 다 열리면 null을 준다.
        /// 이걸 놓치면 에러 하나 없이 판이 안 끝난다.
        /// </summary>
        internal static string FindBlockedGate() => FindBlockedGate(Lasers);

        internal static string FindBlockedGate(IReadOnlyList<LaserSpec> lasers)
        {
            for (int i = 0; i < Shelves.Length; i++)
            {
                Shelf shelf = Shelves[i];
                if (GateEverOpens(shelf, lasers) == false)
                {
                    return $"선반 {shelf.Y:0}의 구멍이 한 번도 열리지 않는다";
                }
            }
            return null;
        }

        private static bool GateEverOpens(in Shelf shelf, IReadOnlyList<LaserSpec> lasers)
        {
            var beams = new List<LOP.Laser>();
            for (int i = 0; i < lasers.Count; i++)
            {
                beams.Add(lasers[i].ToLaser());
            }

            for (int tick = 0; tick < GateSampleTicks; tick++)
            {
                if (HoleHasClearPoint(shelf, beams, tick))
                {
                    return true;
                }
            }
            return false;
        }

        // 구멍 안을 격자로 훑어, 켜져 있는 모든 빔에서 충분히 떨어진 점이 하나라도 있으면 열린 것이다.
        private static bool HoleHasClearPoint(in Shelf shelf, List<LOP.Laser> beams, int tick)
        {
            float step = shelf.HoleHalf * 2f / GateGridSteps;

            for (int ix = 0; ix <= GateGridSteps; ix++)
            {
                for (int iz = 0; iz <= GateGridSteps; iz++)
                {
                    float x = shelf.HoleX - shelf.HoleHalf + ix * step;
                    float z = shelf.HoleZ - shelf.HoleHalf + iz * step;
                    var point = new System.Numerics.Vector3(x, shelf.Y, z);

                    bool clear = true;
                    for (int b = 0; b < beams.Count; b++)
                    {
                        LOP.Laser beam = beams[b];
                        if (LOP.LaserGeometry.Lit(beam, tick) == false)
                        {
                            continue;
                        }
                        LOP.LaserGeometry.SegmentAt(beam, tick, out var a, out var bb);
                        float d = LOP.LaserSweep.SegmentDistance(point, point, a, bb);
                        if (d <= BodyRadiusForGateCheck + beam.Radius)
                        {
                            clear = false;
                            break;
                        }
                    }
                    if (clear)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 부활 지점이 판 밖이거나 구멍 안이면 그 설명을, 다 멀쩡하면 null을 준다.
        /// 검사 대상은 <b>서버가 실제로 쓰는 표</b>(<c>LOP.SkydiveCourseLayout.RespawnPoints</c>)다 —
        /// 빌더 안에 사본을 두고 그걸 검사하면, 정작 사람을 세우는 값은 아무도 안 본 것이 된다.
        /// </summary>
        internal static string FindInvalidRespawn()
        {
            for (int i = 0; i < Shelves.Length; i++)
            {
                Shelf shelf = Shelves[i];

                if (LOP.SkydiveCourseLayout.RespawnPoints.TryGetValue(shelf.Y, out Vector3 point) == false)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점이 SkydiveCourseLayout에 없다";
                }

                if (Mathf.Abs(point.x) > SlabHalf || Mathf.Abs(point.z) > SlabHalf)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점이 판 밖이다";
                }

                if (Mathf.Abs(point.y - shelf.Y) > 0.001f)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점 고도가 선반과 다르다";
                }

                bool insideHole = Mathf.Abs(point.x - shelf.HoleX) <= shelf.HoleHalf
                               && Mathf.Abs(point.z - shelf.HoleZ) <= shelf.HoleHalf;
                if (insideHole)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점이 구멍 안이다 — 세우자마자 빠진다";
                }

                //  기둥에 겹치면 부활한 몸이 기둥에 박힌다.
                bool onPillar = Mathf.Abs(Mathf.Abs(point.x) - PillarOffset) < PillarSide
                             && Mathf.Abs(Mathf.Abs(point.z) - PillarOffset) < PillarSide;
                if (onPillar)
                {
                    return $"선반 {shelf.Y:0}의 부활 지점이 기둥과 겹친다";
                }
            }
            return null;
        }

        /// <summary>
        /// 빌더의 선반 고도와 <c>LOP.SkydiveCourseLayout.ShelfYs</c>가 어긋나면 그 설명을 준다.
        /// 굽는 쪽과 판정하는 쪽이 다른 코스를 보면 부활이 허공에 사람을 세운다.
        /// </summary>
        internal static string FindShelfLayoutDrift()
        {
            var layout = LOP.SkydiveCourseLayout.ShelfYs;
            if (layout.Count != Shelves.Length)
            {
                return $"선반 개수가 다르다 — 빌더 {Shelves.Length}, SkydiveCourseLayout {layout.Count}";
            }
            for (int i = 0; i < Shelves.Length; i++)
            {
                bool found = false;
                for (int j = 0; j < layout.Count; j++)
                {
                    if (Mathf.Abs(layout[j] - Shelves[i].Y) < 0.001f)
                    {
                        found = true;
                        break;
                    }
                }
                if (found == false)
                {
                    return $"선반 {Shelves[i].Y:0}이 SkydiveCourseLayout에 없다";
                }
            }
            return null;
        }

        /// <summary>한 틱에 너무 크게 도는 레이저가 있으면 그 설명을, 없으면 null을 준다.</summary>
        internal static string FindTooFastLaser(IReadOnlyList<LaserSpec> lasers)
        {
            for (int i = 0; i < lasers.Count; i++)
            {
                float speed = Mathf.Abs(lasers[i].AngularSpeedDegreesPerTick);
                if (speed > MaxAngularSpeedDegreesPerTick)
                {
                    return $"{lasers[i].Name}가 한 틱에 {speed:0.#}° 돈다 — " +
                           $"{MaxAngularSpeedDegreesPerTick:0.#}°를 넘으면 눈으로 못 읽는다";
                }
            }
            return null;
        }
```

- [ ] **Step 6: 굽기 전에 검사를 건다**

`Build()`의 `string impassable = FindImpassableSection();` 블록 **뒤에**, 옛 코스를 지우기 전에 넣는다:

```csharp
            string blockedGate = FindBlockedGate();
            if (blockedGate != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {blockedGate}. 씬은 바뀌지 않았다.");
                return;
            }

            string drift = FindShelfLayoutDrift();
            if (drift != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {drift}. 씬은 바뀌지 않았다.");
                return;
            }

            string invalidRespawn = FindInvalidRespawn();
            if (invalidRespawn != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {invalidRespawn}. 씬은 바뀌지 않았다.");
                return;
            }

            string tooFast = FindTooFastLaser(Lasers);
            if (tooFast != null)
            {
                Debug.LogError($"[Skydive] 굽지 않는다 — {tooFast}. 씬은 바뀌지 않았다.");
                return;
            }
```

- [ ] **Step 7: 테스트 통과를 확인한다**

Expected: `SkydiveLaserBuildTests` 5개 PASS. 통과하지 않으면 **표를 고쳐서** 통과시킨다 —
검사기를 느슨하게 바꾸지 않는다. 검사기가 하는 일이 바로 그것이다.

- [ ] **Step 8: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Editor/SkydiveCourseBuilder.cs \
        Assets/Tests/Editor/SkydiveLaserBuildTests.cs \
        Assets/Tests/Editor/SkydiveLaserBuildTests.cs.meta
git commit -m "feat(skydive): 코스에 레이저를 놓고 막힌 배치를 거절한다"
```

---

## Task 11: 씬에 레이저를 굽는다

> **실행 중 분할(Ruling 16).** 원래 이 태스크는 그림까지 포함했으나, 뷰가 **권위 틱**을 알아야
> 한다는 것이 실행 중에 드러났다(그린 빔과 판정된 빔이 다른 자리면 플레이어가 엉뚱한 것을 피한다).
> 정적 틱 통로가 없어 DI가 필요하고, 그러면 뷰를 어디에 둘지가 설계 결정이 된다 — 계획서가 쓴
> "맵 씬에 클라 전용 컴포넌트를 붙인다"는 **틀렸다**(서버도 그 씬을 읽어 missing script가 난다).
> 그래서 이 태스크는 **마커를 굽는 것까지만** 하고, 그림은 별도 슬라이스로 뺀다.
> ⚠ 그림이 없으면 레이저가 보이지 않는다 — 그 상태로 플레이테스트하면 바람 슬라이스의 실패를
> 그대로 반복한다. 플레이 전에 반드시 그림이 들어와야 한다.

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/LaserView.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Editor/SkydiveCourseBuilder.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Modify (아트 서브모듈): `Assets/Art/Scenes/SkydiveMap.unity`, `Assets/Art/Materials/SkydiveLaser*.mat`

**Interfaces:**
- Consumes: `LaserVolume` (Task 7), `LaserGeometry` (Task 5)

- [ ] **Step 1: 레이저 머티리얼 두 장을 만든다**

`LeagueOfPhysical-Client/Assets/Scripts/Editor/SkydiveLaserAssets.cs`:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LOP.EditorTools
{
    /// <summary>
    /// 레이저가 쓰는 머티리얼 두 장. <see cref="SkydiveWindAssets"/>와 같은 이유로 <b>아트
    /// 서브모듈</b>에 둔다 — 맵 씬이 거기 있고 서버도 같은 서브모듈을 읽는다.
    /// </summary>
    internal static class SkydiveLaserAssets
    {
        private const string MaterialFolder = "Assets/Art/Materials";
        private const string OnPath = MaterialFolder + "/SkydiveLaserOn.mat";
        private const string TelegraphPath = MaterialFolder + "/SkydiveLaserTelegraph.mat";

        private static readonly Color Beam = new Color(1f, 0.16f, 0.22f);

        internal static LaserVisualAssets EnsureAssets()
        {
            var assets = new LaserVisualAssets
            {
                On = EnsureMaterial(OnPath, 0.92f),
                Telegraph = EnsureMaterial(TelegraphPath, 0.22f),
            };
            AssetDatabase.SaveAssets();
            return assets;
        }

        // 새로 CreateAsset을 부르면 GUID가 바뀌어 씬 참조가 통째로 끊긴다. 있으면 내용만 덮는다.
        private static Material EnsureMaterial(string path, float alpha)
        {
            Material generated = CreateBeamMaterial(alpha);
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material CreateBeamMaterial(float alpha)
        {
            //  Unlit이다 — 빔은 스스로 빛나는 것이라 조명을 받으면 각도에 따라 어두워져 오히려 안 읽힌다.
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(Beam.r, Beam.g, Beam.b, alpha));

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }
    }

    internal sealed class LaserVisualAssets
    {
        public Material On;
        public Material Telegraph;
    }
}
```

- [ ] **Step 2: 빌더가 `LaserVolume`을 굽는다**

`SkydiveCourseBuilder.Build()`의 `var winds = new GameObject("Winds");` 블록 **뒤에** 넣는다:

```csharp
            LaserVisualAssets laserAssets = SkydiveLaserAssets.EnsureAssets();
            var lasers = new GameObject("Lasers");
            lasers.transform.SetParent(root.transform, worldPositionStays: false);
            for (int i = 0; i < Lasers.Length; i++)
            {
                CreateLaserVolume(lasers.transform, Lasers[i], laserAssets);
            }
```

그리고 메서드를 더한다:

```csharp
        internal static GameObject CreateLaserVolume(Transform parent, in LaserSpec spec,
                                                    LaserVisualAssets assets)
        {
            var go = new GameObject(spec.Name);
            if (parent != null)
            {
                go.transform.SetParent(parent, worldPositionStays: false);
            }
            go.transform.localPosition = spec.Pivot;

            var marker = go.AddComponent<LOP.LaserVolume>();
            marker.Length = spec.Length;
            marker.Radius = spec.Radius;
            marker.StartAngleDegrees = spec.StartAngleDegrees;
            marker.AngularSpeedDegreesPerTick = spec.AngularSpeedDegreesPerTick;
            marker.SweepHalfRangeDegrees = spec.SweepHalfRangeDegrees;
            marker.Period = spec.Period;
            marker.OnTicks = spec.OnTicks;
            marker.Phase = spec.Phase;

            var view = go.AddComponent<LOP.LaserView>();
            var so = new SerializedObject(view);
            so.FindProperty("onMaterial").objectReferenceValue = assets.On;
            so.FindProperty("telegraphMaterial").objectReferenceValue = assets.Telegraph;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }
```

- [ ] **Step 3: `LaserView`를 만든다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/LaserView.cs`:

```csharp
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 레이저를 그린다. 판정과 <b>같은 식</b>(<see cref="LaserGeometry"/>)으로 자세를 구하므로
    /// 화면과 판정이 어긋나지 않는다.
    ///
    /// <para>점멸 빔은 켜지기 전에 <b>가는 선으로 예고</b>한다 — 예고 없이 켜지면 피할 수 없고,
    /// 피할 수 없는 것은 장애물이 아니라 주사위다. 예고선은 그림일 뿐 판정에 들어가지 않는다.</para>
    /// </summary>
    [RequireComponent(typeof(LaserVolume))]
    public class LaserView : MonoBehaviour
    {
        private const float TelegraphSeconds = 0.4f;

        [SerializeField] private Material onMaterial;
        [SerializeField] private Material telegraphMaterial;

        private LaserVolume volume;
        private Laser laser;
        private Transform beam;
        private MeshRenderer beamRenderer;
        private GameFramework.Runner.IRunner runner;

        [Inject]
        public void Construct(GameFramework.Runner.IRunner runner)
        {
            this.runner = runner;
        }

        private void Awake()
        {
            volume = GetComponent<LaserVolume>();
            laser = volume.ToLaser();

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Beam";
            Destroy(cube.GetComponent<Collider>());
            beam = cube.transform;
            beam.SetParent(transform, worldPositionStays: false);
            beamRenderer = cube.GetComponent<MeshRenderer>();
        }

        private void LateUpdate()
        {
            if (runner?.tickUpdater == null)
            {
                return;   // 맵이 먼저 뜨고 러너가 뒤에 물릴 수 있다
            }

            long tick = runner.tickUpdater.tick;
            bool lit = LaserGeometry.Lit(laser, tick);
            bool telegraphing = lit == false && WillLightSoon(tick);

            beamRenderer.enabled = lit || telegraphing;
            if (beamRenderer.enabled == false)
            {
                return;
            }

            beamRenderer.sharedMaterial = lit ? onMaterial : telegraphMaterial;

            float angle = LaserGeometry.Angle(laser, tick);
            float thickness = lit ? volume.Radius * 2f : volume.Radius * 0.5f;

            beam.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            beam.localScale = new Vector3(volume.Length, thickness, thickness);
            //  큐브는 가운데가 원점이라 절반만큼 밀어야 피벗에서 뻗어 나간다.
            beam.localPosition = beam.localRotation * new Vector3(volume.Length * 0.5f, 0f, 0f);
        }

        private bool WillLightSoon(long tick)
        {
            if (volume.Period <= 0 || volume.OnTicks >= volume.Period)
            {
                return false;
            }
            int ahead = Mathf.Max(1, Mathf.RoundToInt(TelegraphSeconds / Mathf.Max(0.001f, (float)runner.tickUpdater.interval)));
            for (int i = 1; i <= ahead; i++)
            {
                if (LaserGeometry.Lit(laser, tick + i))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
```

> 머티리얼 두 장은 **빌더가 채운다**(Step 2의 `CreateLaserVolume`). 런타임에 `Resources`로 찾지
> 않는 이유는 경로 의존이 생기고, 서버 빌드에서 없는 파일을 찾게 되기 때문이다.

- [ ] **Step 4: 클라 DI에 `LaserField`를 등록한다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`의
`builder.Register<WindDriftSystem>(Lifetime.Singleton);` **뒤에** 넣는다:

```csharp
            // 맵 씬의 LaserVolume 마커가 맵 로드 시 여기에 자기를 넣는다. 클라는 판정하지 않지만
            // 마커의 [Inject]가 이걸 요구하므로 등록이 있어야 씬 주입이 끊기지 않는다.
            builder.Register<LaserField>(Lifetime.Singleton);
```

- [ ] **Step 5: 씬을 다시 굽는다**

클라 에디터에서 `Assets/Art/Scenes/SkydiveMap.unity`를 열고 메뉴 `LOP/Skydive/코스 굽기`를 실행한 뒤
씬을 저장한다.

- [ ] **Step 6: 구운 결과를 센다**

에디터에서 확인:
- `LaserVolume` 개수 == `SkydiveCourseBuilder.Lasers.Length` (13)
- `Lasers` 아래 오브젝트에 **콜라이더가 하나도 없다** (있으면 캐릭터가 레이저 위에 착지한다)
- `Course` 아래 `BoxCollider` 개수가 재굽기 전과 같다

- [ ] **Step 7: EditMode 전체를 돌린다**

Expected: 전부 PASS. 특히 `SkydiveWindBuildTests.구운_맵의_바람에는_콜라이더가_없다`가 계속
통과해야 한다(레이저를 얹어도 바람이 안 깨졌다는 뜻).

- [ ] **Step 8: 커밋 (아트 서브모듈 먼저)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Art
git status --short
git add Materials/SkydiveLaserOn.mat Materials/SkydiveLaserOn.mat.meta \
        Materials/SkydiveLaserTelegraph.mat Materials/SkydiveLaserTelegraph.mat.meta \
        Scenes/SkydiveMap.unity
git commit -m "feat(skydive): 코스에 레이저를 굽는다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/LaserView.cs Assets/Scripts/Game/LaserView.cs.meta \
        Assets/Scripts/Editor/SkydiveLaserAssets.cs Assets/Scripts/Editor/SkydiveLaserAssets.cs.meta \
        Assets/Scripts/Editor/SkydiveCourseBuilder.cs \
        Assets/Scripts/Game/SkydiveLifetimeScope.cs \
        Assets/Art
git commit -m "feat(skydive): 레이저를 그리고 코스에 굽는다"
```

---

## 마무리 — 실플레이로만 확인되는 것

코드와 테스트가 끝나도 다음 넷은 **플레이해야** 안다. 스펙 §12의 위험 목록과 같다.

1. 레이저가 실제로 **결정을 만드는가** — "뚫을까/기다릴까"가 생기는가
2. 부활 벌이 **너무 무르지 않은가** — 레이저를 무시하고 뚫는 것이 최적해면 장치가 죽는다
3. CA 반복 상한(`MaxIterations = 16`)에 **얼마나 자주 걸리는가** — 잦으면 레이저가 조용히 약해진다
4. 왕복 지연만큼 **늦는 부활**이 거슬리는가
