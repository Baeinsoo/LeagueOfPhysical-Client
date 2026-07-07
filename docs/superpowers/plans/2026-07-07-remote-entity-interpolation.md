# 원격 엔티티 스냅샷 보간 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 원격 엔티티(남 캐릭·아이템)를 비표준 스프링-팔로우(`ServerStateReconciler`)에서 표준 receive-anchored 스냅샷 보간으로 교체해 대시 밀림이 끊기지 않게 한다.

**Architecture:** 받은 스냅을 버퍼에 쌓고, 공유 재생 시계(최신 스냅 소인 − 적응형 쿠션을 `ClockDilator`로 rate 추종)의 `renderTime`을 감싸는 두 스냅 사이를 Hermite(위치)+Slerp(회전)로 블렌드해 엔티티+비주얼에 쓴다. 순수 로직(Hermite/쿠션)은 GameFramework에 EditMode TDD, 클라 통합은 컴파일+플레이 검증.

**Tech Stack:** Unity 6, C#, VContainer(DI), Mirror(transport), GameFramework 패키지(`file:` 참조), UnityMCP(컴파일/테스트).

## Global Constraints

- **보간 시계 = receive-anchored**: `renderTime`은 `serverNow`(진짜 서버 now)도 `elapsedTime`(클라 앞선 시계)도 기준으로 쓰지 않는다. 오직 **최신 받은 스냅 소인 − 쿠션**을 target으로 한다.
- **재생 시계 = rate 추종**: `ClockDilator.Advance(current, target, dt)`로 전진, **절대 역행 없음**, 프레임당 정확히 1회 전진.
- **쿠션 = 핑 무관**: `N×전송간격 + K×지터`, clamp. 초기 N=2, K=2, min=1×interval, max=5×interval.
- **위치=Hermite(스냅 velocity 사용), 회전=Slerp**. **언더런=최신 스냅 hold(외삽 금지)**.
- **스냅 채널 = unreliable**(`reliable: false`). 범위 = **남 캐릭 + 아이템** 공통 `RemoteEntityInterpolator` 하나.
- **순수 로직(Hermite/estimator)은 GameFramework.Netcode에 두고 EditMode TDD**(System.Numerics). 클라 MonoBehaviour/DI는 컴파일+플레이 검증.
- **코드 컨벤션**: World 타입은 풀 네임스페이스 한정(`GameFramework.World.*`). 주석 최소·일상어. 새 `.cs`의 Unity 생성 `.meta`도 커밋.
- **Git**: 브랜치 `feature/remote-entity-interpolation`(이미 존재). 커밋 메시지 한국어 + trailer `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **컴파일 검증(클라/서버 태스크)**: UnityMCP `refresh_unity(scope=scripts, compile=request, mode=force, unity_instance=<대상>)` → `read_console(filter_text="error CS")` 0건. 클라 인스턴스는 `mcpforunity://instances`에서 `LeagueOfPhysical-Client@<hash>` 해석, 서버는 `LeagueOfPhysical-Server@<hash>`.

---

### Task 1: Hermite 위치 보간 순수 커널 (GameFramework)

**Files:**
- Create: `GameFramework/Runtime/Scripts/Netcode/Hermite.cs`
- Test: `GameFramework/Tests/Runtime/Netcode/HermiteTests.cs`

**Interfaces:**
- Produces: `static System.Numerics.Vector3 GameFramework.Netcode.Hermite.Position(Vector3 p0, Vector3 v0, Vector3 p1, Vector3 v1, float dt, float u)` — 두 스냅(위치+속도) 사이 큐빅 Hermite 위치. `dt`=구간 길이(초, 탄젠트 스케일), `u`=정규화 [0,1].

- [ ] **Step 1: Write the failing tests**

`GameFramework/Tests/Runtime/Netcode/HermiteTests.cs`:
```csharp
using System.Numerics;
using NUnit.Framework;

namespace GameFramework.Tests.Netcode
{
    public class HermiteTests
    {
        [Test]
        public void U0_ReturnsStart_IgnoringVelocities()
        {
            var p0 = new Vector3(1, 2, 3);
            var r = Hermite.Position(p0, new Vector3(9, 9, 9), new Vector3(5, 5, 5), new Vector3(-9, 0, 0), 0.033f, 0f);
            Assert.AreEqual(p0.X, r.X, 1e-5f); Assert.AreEqual(p0.Y, r.Y, 1e-5f); Assert.AreEqual(p0.Z, r.Z, 1e-5f);
        }

        [Test]
        public void U1_ReturnsEnd_IgnoringVelocities()
        {
            var p1 = new Vector3(5, 5, 5);
            var r = Hermite.Position(new Vector3(1, 2, 3), new Vector3(9, 9, 9), p1, new Vector3(-9, 0, 0), 0.033f, 1f);
            Assert.AreEqual(p1.X, r.X, 1e-5f); Assert.AreEqual(p1.Y, r.Y, 1e-5f); Assert.AreEqual(p1.Z, r.Z, 1e-5f);
        }

        [Test]
        public void ZeroVelocities_EqualsLinearLerp()
        {
            var p0 = new Vector3(0, 0, 0);
            var p1 = new Vector3(10, 0, 0);
            var r = Hermite.Position(p0, Vector3.Zero, p1, Vector3.Zero, 0.033f, 0.25f);
            Assert.AreEqual(2.5f, r.X, 1e-5f);   // Lerp(0,10,0.25)
        }

        [Test]
        public void ConstantVelocity_TravelsStraightAtConstantSpeed()
        {
            // p1 = p0 + v*dt, v0=v1=v → 등속 직선. u=0.5 midpoint = p0 + 0.5*dt*v.
            float dt = 0.033f;
            var v = new Vector3(3, 0, 0);
            var p0 = new Vector3(0, 0, 0);
            var p1 = p0 + v * dt;
            var r = Hermite.Position(p0, v, p1, v, dt, 0.5f);
            Assert.AreEqual(0.5f * dt * 3f, r.X, 1e-5f);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Test Runner(EditMode) 또는 UnityMCP `run_tests(mode=EditMode, assembly_names=["baegames.GameFramework.Runtime.Tests"], unity_instance=<클라>)`.
Expected: FAIL — `Hermite` 타입 없음(컴파일 에러).

- [ ] **Step 3: Write the implementation**

`GameFramework/Runtime/Scripts/Netcode/Hermite.cs`:
```csharp
using System.Numerics;

namespace GameFramework.Netcode
{
    /// <summary>두 스냅(위치+속도) 사이 큐빅 Hermite 위치 보간. 순수 — 프레임독립·EditMode 테스트.</summary>
    public static class Hermite
    {
        /// <param name="dt">구간 길이(초) = newerTime − olderTime. 속도(탄젠트)를 위치 단위로 스케일.</param>
        /// <param name="u">정규화 파라미터 [0,1].</param>
        public static Vector3 Position(Vector3 p0, Vector3 v0, Vector3 p1, Vector3 v1, float dt, float u)
        {
            float u2 = u * u;
            float u3 = u2 * u;
            float h00 = 2f * u3 - 3f * u2 + 1f;
            float h10 = u3 - 2f * u2 + u;
            float h01 = -2f * u3 + 3f * u2;
            float h11 = u3 - u2;
            return h00 * p0 + h10 * dt * v0 + h01 * p1 + h11 * dt * v1;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Expected: 4 PASS. (Unity가 생성한 `Hermite.cs.meta`, `HermiteTests.cs.meta` 확인.)

- [ ] **Step 5: Commit**

```bash
git add GameFramework/Runtime/Scripts/Netcode/Hermite.cs GameFramework/Runtime/Scripts/Netcode/Hermite.cs.meta \
        GameFramework/Tests/Runtime/Netcode/HermiteTests.cs GameFramework/Tests/Runtime/Netcode/HermiteTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(netcode): Hermite 위치 보간 순수 커널 + EditMode TDD

두 스냅(위치+속도) 사이 큐빅 Hermite. 등속 구간을 직선·등속으로 재현(빠른
넉백 코너 깎임 방지). 속도 0이면 선형 Lerp와 동일.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

> **참고**: GameFramework는 `file:` 패키지라 커밋은 `C:/Users/re5na/workspace/LOP/GameFramework` 저장소에서 수행. 이 저장소도 `feature/remote-entity-interpolation` 브랜치를 만들어 작업(`git checkout -b feature/remote-entity-interpolation`).

---

### Task 2: 적응형 쿠션 추정기 (GameFramework)

**Files:**
- Create: `GameFramework/Runtime/Scripts/Netcode/InterpolationDelayEstimator.cs`
- Test: `GameFramework/Tests/Runtime/Netcode/InterpolationDelayEstimatorTests.cs`

**Interfaces:**
- Produces:
  - `new InterpolationDelayEstimator(double sendInterval, double n = 2, double k = 2, double minCushion = 0, double maxCushion = double.MaxValue, double smoothing = 0.1)`
  - `void RecordArrival(double arrivalTime)` — 스냅 배치 도착(클라 실시간 초). 도착 간격 지터 갱신.
  - `double Cushion { get; }` — `clamp(n·sendInterval + k·jitter, min, max)`.

- [ ] **Step 1: Write the failing tests**

`GameFramework/Tests/Runtime/Netcode/InterpolationDelayEstimatorTests.cs`:
```csharp
using NUnit.Framework;

namespace GameFramework.Tests.Netcode
{
    public class InterpolationDelayEstimatorTests
    {
        [Test]
        public void NoArrivals_CushionIsBaseline()
        {
            var e = new InterpolationDelayEstimator(sendInterval: 0.033, n: 2, k: 2);
            Assert.AreEqual(2 * 0.033, e.Cushion, 1e-9);   // jitter 0
        }

        [Test]
        public void RegularArrivals_NoJitter_CushionStaysBaseline()
        {
            var e = new InterpolationDelayEstimator(0.033, 2, 2);
            double t = 0;
            for (int i = 0; i < 20; i++) { e.RecordArrival(t); t += 0.033; }
            Assert.AreEqual(2 * 0.033, e.Cushion, 1e-6);
        }

        [Test]
        public void JitteryArrivals_CushionGrowsAboveBaseline()
        {
            var e = new InterpolationDelayEstimator(0.033, 2, 2, smoothing: 0.5);
            double t = 0;
            double[] gaps = { 0.033, 0.010, 0.070, 0.005, 0.080, 0.015 };
            foreach (var g in gaps) { t += g; e.RecordArrival(t); }
            Assert.Greater(e.Cushion, 2 * 0.033);
        }

        [Test]
        public void Cushion_ClampedToMax()
        {
            var e = new InterpolationDelayEstimator(0.033, n: 2, k: 100, minCushion: 0, maxCushion: 0.15, smoothing: 1.0);
            e.RecordArrival(0);
            e.RecordArrival(0.5);   // 큰 지터
            Assert.AreEqual(0.15, e.Cushion, 1e-9);
        }

        [Test]
        public void Cushion_ClampedToMin()
        {
            var e = new InterpolationDelayEstimator(0.033, n: 0, k: 0, minCushion: 0.05, maxCushion: 1.0);
            Assert.AreEqual(0.05, e.Cushion, 1e-9);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

UnityMCP `run_tests(mode=EditMode, assembly_names=["baegames.GameFramework.Runtime.Tests"])`.
Expected: FAIL — `InterpolationDelayEstimator` 없음.

- [ ] **Step 3: Write the implementation**

`GameFramework/Runtime/Scripts/Netcode/InterpolationDelayEstimator.cs`:
```csharp
namespace GameFramework.Netcode
{
    /// <summary>
    /// 스냅 배치 도착 간격의 지터로 적응형 보간 쿠션을 산출. 순수 — EditMode 테스트.
    /// 쿠션 = clamp(n·sendInterval + k·jitter, min, max). jitter = |도착간격 − sendInterval|의 EWMA.
    /// 핑(레이턴시)은 여기 안 들어감 — 도착 "간격"만 본다.
    /// </summary>
    public class InterpolationDelayEstimator
    {
        private readonly double sendInterval;
        private readonly double n;
        private readonly double k;
        private readonly double minCushion;
        private readonly double maxCushion;
        private readonly double smoothing;

        private double lastArrival;
        private bool hasLast;
        private double jitter;

        public InterpolationDelayEstimator(double sendInterval, double n = 2, double k = 2,
            double minCushion = 0, double maxCushion = double.MaxValue, double smoothing = 0.1)
        {
            this.sendInterval = sendInterval;
            this.n = n;
            this.k = k;
            this.minCushion = minCushion;
            this.maxCushion = maxCushion;
            this.smoothing = smoothing;
        }

        public void RecordArrival(double arrivalTime)
        {
            if (hasLast)
            {
                double interval = arrivalTime - lastArrival;
                double deviation = System.Math.Abs(interval - sendInterval);
                jitter += smoothing * (deviation - jitter);
            }
            lastArrival = arrivalTime;
            hasLast = true;
        }

        public double Cushion =>
            System.Math.Clamp(n * sendInterval + k * jitter, minCushion, maxCushion);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Expected: 5 PASS. (`.meta` 2개 확인.)

- [ ] **Step 5: Commit**

```bash
git add GameFramework/Runtime/Scripts/Netcode/InterpolationDelayEstimator.cs GameFramework/Runtime/Scripts/Netcode/InterpolationDelayEstimator.cs.meta \
        GameFramework/Tests/Runtime/Netcode/InterpolationDelayEstimatorTests.cs GameFramework/Tests/Runtime/Netcode/InterpolationDelayEstimatorTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(netcode): 적응형 보간 쿠션 추정기 + EditMode TDD

스냅 배치 도착 간격 지터의 EWMA로 쿠션 = clamp(n·전송간격 + k·지터). 핑
무관(도착 간격만). 잔잔하면 baseline, 튀면 넓힘.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: 공유 재생 시계 `RemoteInterpolationClock` (Client) + DI 등록

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/RemoteInterpolationClock.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs:90` (등록 추가)

**Interfaces:**
- Consumes: `GameFramework.ClockDilator`, `GameFramework.Netcode.InterpolationDelayEstimator`, `IGameDataStore.gameInfo.Interval`(초/틱), `UnityEngine.Time`.
- Produces:
  - `new RemoteInterpolationClock(double sendInterval)`
  - `void RecordArrival(long serverTick, double clientTime)` — 스냅 배치 도착 시(메시지 핸들러) 1회.
  - `bool HasSnapshot { get; }`
  - `double RenderTime { get; }` — 프레임당 1회 자가 전진 후 반환.

- [ ] **Step 1: Write the implementation**

`LeagueOfPhysical-Client/Assets/Scripts/Entity/RemoteInterpolationClock.cs`:
```csharp
using GameFramework;
using GameFramework.Netcode;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 원격 엔티티 공유 재생 시계(receive-anchored). 최신 받은 스냅 배치 소인에서 적응형 쿠션만큼 뒤를
    /// 가리키며 ClockDilator로 rate 추종(역행 없음). RenderTime은 프레임당 1회만 전진(여러 인터폴레이터 공용).
    /// </summary>
    public class RemoteInterpolationClock
    {
        private readonly double sendInterval;
        private readonly ClockDilator dilator;
        private readonly InterpolationDelayEstimator estimator;

        private long newestTick;
        private bool hasSnapshot;
        private double renderTime;
        private int lastAdvancedFrame = -1;

        public RemoteInterpolationClock(double sendInterval)
        {
            this.sendInterval = sendInterval;
            // errorScale=sendInterval → 오차 1틱이면 최대 rate. snapThreshold=8틱(오래 굶다 재개 시만 점프).
            this.dilator = new ClockDilator(maxRate: 0.05, errorScale: sendInterval, snapThreshold: sendInterval * 8);
            this.estimator = new InterpolationDelayEstimator(
                sendInterval, n: 2, k: 2, minCushion: sendInterval, maxCushion: sendInterval * 5);
        }

        public bool HasSnapshot => hasSnapshot;

        public void RecordArrival(long serverTick, double clientTime)
        {
            estimator.RecordArrival(clientTime);
            if (hasSnapshot == false || serverTick > newestTick)
            {
                newestTick = serverTick;
            }
            if (hasSnapshot == false)
            {
                renderTime = Target();   // 첫 스냅에 시계 시드
                hasSnapshot = true;
            }
        }

        // 목표 = 최신 스냅 소인(서버 타임라인) − 쿠션. 스냅 timestamp = tick*interval과 같은 축.
        private double Target() => newestTick * sendInterval - estimator.Cushion;

        public double RenderTime
        {
            get
            {
                if (hasSnapshot == false)
                {
                    return 0;
                }
                if (Time.frameCount != lastAdvancedFrame)
                {
                    renderTime = dilator.Advance(renderTime, Target(), Time.unscaledDeltaTime);
                    lastAdvancedFrame = Time.frameCount;
                }
                return renderTime;
            }
        }
    }
}
```

- [ ] **Step 2: Register in DI**

`GameLifetimeScope.cs`에서 `builder.Register<Reconciler>(Lifetime.Singleton);`(90번째 줄) 바로 아래에 추가:
```csharp
            builder.Register(c => new RemoteInterpolationClock(c.Resolve<IGameDataStore>().gameInfo.Interval), Lifetime.Singleton);
```

- [ ] **Step 3: Verify compile (client)**

UnityMCP `refresh_unity(scope=scripts, compile=request, mode=force, unity_instance=<클라>)` → `read_console(filter_text="error CS", unity_instance=<클라>)`.
Expected: 0건. (`RemoteInterpolationClock.cs.meta` 생성 확인.)

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Entity/RemoteInterpolationClock.cs Assets/Scripts/Entity/RemoteInterpolationClock.cs.meta \
        Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "$(cat <<'EOF'
feat(netcode): 원격 공유 재생 시계 RemoteInterpolationClock + DI 등록

최신 스냅 소인−쿠션을 target으로 ClockDilator rate 추종(역행 없음), RenderTime
프레임당 1회 전진. 미러 localTimeline과 동일 방식.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: `RemoteEntityInterpolator` 컴포넌트 (Client)

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/RemoteEntityInterpolator.cs`

**Interfaces:**
- Consumes: `RemoteInterpolationClock`(주입), `GameFramework.Netcode.Hermite.Position`, `EntitySnap`(tick/position/rotation/velocity/timestamp), `LOPEntity`, `LOPEntityView`, `BoundedList<T>`.
- Produces: `void AddServerEntitySnap(EntitySnap snap)`; per-entity MonoBehaviour로 `entity`/`entityView` 세팅 후 동작.

- [ ] **Step 1: Write the implementation**

`LeagueOfPhysical-Client/Assets/Scripts/Entity/RemoteEntityInterpolator.cs`:
```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 원격 엔티티(남 캐릭·아이템)의 표준 스냅샷 보간. 공유 재생 시계의 renderTime을 감싸는 두 스냅 사이를
    /// Hermite(위치)+Slerp(회전)로 블렌드해 엔티티(월드 위치·kinematic 콜라이더)와 비주얼 메시에 쓴다.
    /// 예측·스프링 없음. 감쌀 쌍이 없으면 최신 스냅 hold(외삽 안 함).
    /// </summary>
    public class RemoteEntityInterpolator : MonoBehaviour
    {
        [Inject] private RemoteInterpolationClock clock;

        public LOPEntity entity { get; set; }
        public LOPEntityView entityView { get; set; }

        private readonly BoundedList<EntitySnap> snaps = new BoundedList<EntitySnap>(32);

        /// <summary>서버 스냅 수신. 타임스탬프 순으로만 추가, 최신보다 오래되거나 같은 건 무시(unreliable 순서역전 방지).</summary>
        public void AddServerEntitySnap(EntitySnap snap)
        {
            if (snaps.Count > 0 && snap.timestamp <= snaps[snaps.Count - 1].timestamp)
            {
                return;
            }
            snaps.Add(snap);
        }

        private void LateUpdate()
        {
            if (entityView.visualGameObject == null || snaps.Count == 0 || clock.HasSnapshot == false)
            {
                return;
            }

            double renderTime = clock.RenderTime;

            for (int i = snaps.Count - 1; i >= 1; i--)
            {
                EntitySnap newer = snaps[i];
                EntitySnap older = snaps[i - 1];
                if (older.timestamp <= renderTime && renderTime <= newer.timestamp)
                {
                    float dt = (float)(newer.timestamp - older.timestamp);
                    float u = dt > 0f ? Mathf.Clamp01((float)((renderTime - older.timestamp) / dt)) : 0f;

                    Vector3 pos = GameFramework.Netcode.Hermite.Position(
                        older.position.ToNumerics(), older.velocity.ToNumerics(),
                        newer.position.ToNumerics(), newer.velocity.ToNumerics(), dt, u).ToUnity();
                    Quaternion rot = Quaternion.Slerp(
                        Quaternion.Euler(older.rotation), Quaternion.Euler(newer.rotation), u);

                    Apply(pos, rot);
                    return;
                }
            }

            // 언더런(renderTime이 최신 스냅보다 앞 or 감쌀 쌍 없음) → 최신 스냅 hold.
            EntitySnap newest = snaps[snaps.Count - 1];
            Apply(newest.position, Quaternion.Euler(newest.rotation));
        }

        // 엔티티(월드 Transform → reactive로 kinematic 콜라이더 + 네임플레이트 등) + 비주얼 메시 둘 다 구동.
        private void Apply(Vector3 pos, Quaternion rot)
        {
            entity.position = pos;
            entity.rotation = rot.eulerAngles;
            entityView.visualGameObject.transform.position = pos;
            entityView.visualGameObject.transform.rotation = rot;
        }
    }
}
```

- [ ] **Step 2: Verify compile (client)**

`refresh_unity` + `read_console(filter_text="error CS")` → 0건. (`.meta` 확인.)
> 아직 아무도 이 컴포넌트를 부착하지 않으므로 동작 변화 없음(다음 태스크에서 배선). 컴파일만 통과하면 OK.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/Entity/RemoteEntityInterpolator.cs Assets/Scripts/Entity/RemoteEntityInterpolator.cs.meta
git commit -m "$(cat <<'EOF'
feat(netcode): RemoteEntityInterpolator — 표준 스냅샷 보간 컴포넌트

공유 재생 시계 renderTime을 감싸는 두 스냅 사이 Hermite+Slerp, 엔티티+비주얼
구동. 언더런=최신 hold. 스프링·예측·외삽 없음.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: 배선 — 크리에이터 + 메시지 핸들러 (Client)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/EntityCreator/CharacterCreator.cs:82-88`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/EntityCreator/ItemCreator.cs:60-63`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` (주입 + 배치 도착 피드 + 원격 dispatch 교체)

**Interfaces:**
- Consumes: `RemoteEntityInterpolator`(Task 4), `RemoteInterpolationClock`(Task 3).

- [ ] **Step 1: CharacterCreator — 원격 분기 교체**

`CharacterCreator.cs`의 `else { ServerStateReconciler ... }` 블록(82~88줄)을 교체:
```csharp
            else
            {
                RemoteEntityInterpolator interpolator = entity.gameObject.AddComponent<RemoteEntityInterpolator>();
                objectResolver.Inject(interpolator);
                interpolator.entity = entity;
                interpolator.entityView = view;
            }
```

- [ ] **Step 2: ItemCreator — 원격 컴포넌트 교체**

`ItemCreator.cs`의 `ServerStateReconciler` 부착 블록(60~63줄)을 교체:
```csharp
            RemoteEntityInterpolator interpolator = entity.gameObject.AddComponent<RemoteEntityInterpolator>();
            objectResolver.Inject(interpolator);
            interpolator.entity = entity;
            interpolator.entityView = view;
```

- [ ] **Step 3: 메시지 핸들러 — 주입 + 배치 도착 피드 + 원격 dispatch 교체**

`Game.Entity.MessageHandler.cs`:

(a) 필드 주입 추가(다른 `[Inject]` 옆, `private Reconciler reconciler;` 아래):
```csharp
        [Inject] private RemoteInterpolationClock remoteInterpolationClock;
```

(b) `OnEntitySnapsToC` 진입부 — `if (Runner.current == null) return;` 바로 아래에 배치 도착 1회 기록 추가:
```csharp
            remoteInterpolationClock.RecordArrival(entitySnapsToC.Tick, UnityEngine.Time.timeAsDouble);
```

(c) 원격 분기 마지막 줄 교체:
```csharp
// 기존:
//   entity.GetComponent<ServerStateReconciler>().AddServerEntitySnap(entitySnap);
// 변경:
                    entity.GetComponent<RemoteEntityInterpolator>().AddServerEntitySnap(entitySnap);
```

- [ ] **Step 4: Verify compile (client)**

`refresh_unity` + `read_console(filter_text="error CS")` → 0건.
> `ServerStateReconciler`는 아직 삭제 전이라 참조는 사라졌지만 클래스는 남아있어 컴파일 OK. 삭제는 Task 7.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs \
        Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs
git commit -m "$(cat <<'EOF'
feat(netcode): 원격 엔티티 배선을 RemoteEntityInterpolator로 교체

남 캐릭·아이템에 RemoteEntityInterpolator 부착, 메시지 핸들러가 배치 도착을
공유 재생 시계에 피드 + 스냅을 새 컴포넌트로 dispatch.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: 스냅 채널 unreliable (Server)

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs` (`EndUpdate`의 `EntitySnapsToC` 송신)

**Interfaces:**
- Consumes: `ISession.Send<T>(T message, bool reliable = true)`.

- [ ] **Step 1: EntitySnapsToC 송신을 unreliable로**

`LOPRunner.cs`의 `EndUpdate`에서 `EntitySnapsToC` 전송 줄:
```csharp
// 기존:
//   session.Send(entitySnapsToC);
// 변경:
                session.Send(entitySnapsToC, reliable: false);
```
> `UserEntitySnapToC`(HP/MP 등) 송신은 **건드리지 않는다**(이 작업 범위 밖 — 위치 스냅만).

- [ ] **Step 2: Verify compile (server)**

`refresh_unity(..., unity_instance=<서버>)` + `read_console(filter_text="error CS", unity_instance=<서버>)` → 0건.

- [ ] **Step 3: Commit (server repo)**

서버 저장소(`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`)에서 `feature/remote-entity-interpolation` 브랜치 생성 후:
```bash
git add Assets/Scripts/Game/LOPRunner.cs
git commit -m "$(cat <<'EOF'
feat(netcode): 엔티티 위치 스냅을 unreliable 채널로 전송

full-state 스냅이라 손실 OK, 오래된 스냅 재전송 무의미(표준). 클라 receive-
anchored 보간 버퍼+hold가 손실/순서역전 흡수.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: 레거시 삭제 — ServerStateReconciler / SnapInterpolator / remoteBackTime

**Files:**
- Delete: `LeagueOfPhysical-Client/Assets/Scripts/Entity/ServerStateReconciler.cs` (+ `.meta`)
- Delete: `LeagueOfPhysical-Client/Assets/Scripts/Entity/SnapInterpolator.cs` (+ `.meta`)
- Modify(조건부): `GameFramework/Runtime/Scripts/Game/Runner.cs`(`remoteBackTime` 프로퍼티), `GameFramework/Runtime/Scripts/Game/NetworkTimeExtensions.cs`(`RemoteBackTime`), `GameFramework/Tests/Runtime/NetworkTimeExtensionsTests.cs`

- [ ] **Step 1: 참조 없음 확인**

```bash
# 클라 저장소에서
grep -rn "ServerStateReconciler\|SnapInterpolator" Assets/Scripts   # → 0건이어야
# 두 저장소에서
grep -rn "remoteBackTime\|RemoteBackTime" .                          # → 정의/테스트 외 소비자 0건 확인
```
Expected: `ServerStateReconciler`/`SnapInterpolator` 참조 0건(Task 5로 제거됨). `remoteBackTime` 소비자는 삭제 예정 `SnapInterpolator`뿐 → 확인되면 함께 제거.

- [ ] **Step 2: 파일 삭제**

```bash
git rm Assets/Scripts/Entity/ServerStateReconciler.cs Assets/Scripts/Entity/ServerStateReconciler.cs.meta
git rm Assets/Scripts/Entity/SnapInterpolator.cs Assets/Scripts/Entity/SnapInterpolator.cs.meta
```

- [ ] **Step 3: remoteBackTime 정리(소비자 0 확인 시) — GameFramework 저장소**

`Runner.cs`에서 `public static double remoteBackTime => ...;` 줄 삭제. `NetworkTimeExtensions.cs`에서 `RemoteBackTime` 확장 메서드 삭제. `NetworkTimeExtensionsTests.cs`에서 `RemoteBackTime_IsHalfRtt` 테스트 삭제(그 테스트만이면 파일째, 아니면 해당 메서드만).
> `INetworkTime.ServerNow/PredictedTime/Rtt`는 **유지**(다른 넷코드가 씀). `RemoteBackTime`(파생 확장)만 제거.

- [ ] **Step 4: Verify compile (client + server)**

양쪽 `refresh_unity(scope=all, mode=force)` + `read_console(filter_text="error CS")` → 각 0건.
> `.cs` 삭제 후 클·서에 stale CS2001이 뜨면 `refresh_unity(scope=all, mode=force)`로 재스캔(알려진 이슈).

- [ ] **Step 5: Commit (양쪽 저장소 각각)**

```bash
# 클라
git commit -am "$(cat <<'EOF'
refactor(netcode): 레거시 원격 처리 삭제 — ServerStateReconciler/SnapInterpolator

스프링-팔로우 재조정기(남 캐릭·아이템 이관 완료)와 미사용 SnapInterpolator 제거.
RemoteEntityInterpolator가 대체.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
# GameFramework (소비자 0 확인 시)
git commit -am "$(cat <<'EOF'
refactor(netcode): 미사용 remoteBackTime(=RTT/2) 제거

유일 소비자 SnapInterpolator 삭제로 무용. ServerNow/PredictedTime/Rtt는 유지.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: 통합 플레이 검증 + 튜닝

**Files:** (코드 변경 없음 — 필요 시 Task 3의 N/K/rate 상수만 조정)

- [ ] **Step 1: 클·서 동시 플레이 준비**

두 에디터(클라+서버) 플레이. 로컬 픽스처(`ConfigureRoomComponent` 등)는 커밋하지 않는다. RTT 시뮬레이션(Latency Simulation)으로 50/150/300ms 주입.

- [ ] **Step 2: 밀림 시나리오 검증(핵심)**

내 캐릭으로 **다른 캐릭터를 대시로 밀기**. 확인:
- 밀리는 원격 캐릭이 **끊김("드르륵 뚝뚝") 없이 부드럽게** 밀리는지 (하드스냅 사라짐).
- RTT 50/150/300ms 모두에서 부드러운지, 특히 **고핑에서 얼어붙지 않는지**(receive-anchored라 핑 무관해야 함).

- [ ] **Step 3: 손실/지터 검증**

Latency Simulation에 **패킷 손실 20%** 추가. 밀림·이동 반복:
- 짧은 손실에 **잠깐 hold 후 재개**(순간이동/러버밴딩 없이). 심한 손실에도 발산 없음.

- [ ] **Step 4: 일반 이동·아이템 검증**

원격 캐릭 일반 걷기 + 아이템 이동이 매끄러운지(회귀 없음). 콘솔 에러 0.

- [ ] **Step 5: 튜닝(필요 시)**

코너 깎임/과한 지연이 보이면 `RemoteInterpolationClock`의 estimator `n`(기본 2)·`k`(기본 2)·`maxCushion`(5×), `ClockDilator` `maxRate`(0.05)만 조정. 조정 시 사유를 커밋 메시지에 남김.

- [ ] **Step 6: 최종 확인 후 마무리**

검증 통과 시 `finishing-a-development-branch`로 클라·서버·GameFramework 세 저장소를 각각 `--no-ff` main 머지 + push.

---

## Self-Review

**1. Spec coverage:**
- 컴포넌트 `RemoteEntityInterpolator`(남캐릭+아이템 통일) → Task 4·5 ✅
- receive-anchored 재생 시계(최신스냅−쿠션, ClockDilator rate 추종) → Task 3 ✅
- 적응형 쿠션(핑 무관) → Task 2·3 ✅
- Hermite 위치 + Slerp 회전 → Task 1·4 ✅
- 언더런 hold(외삽 없음) → Task 4 ✅
- 스냅 unreliable → Task 6 ✅
- 기존 2컴포넌트 + remoteBackTime 삭제 → Task 7 ✅
- 단일 보간(entity+비주얼 동시 구동) → Task 4 `Apply` ✅
- 순수 로직 GameFramework EditMode TDD → Task 1·2 ✅
- 검증(밀림/고핑/손실) → Task 8 ✅
- (범위 밖) 서버 lag compensation·LocalEntityInterpolator·Stage④ — plan에서 손대지 않음 ✅

**2. Placeholder scan:** 각 코드 스텝에 완전한 코드 포함. "적절히 처리" 류 없음. Task 8 튜닝 상수는 Task 3에 구체 초기값 존재(빈칸 아님).

**3. Type consistency:**
- `Hermite.Position(Vector3 p0, Vector3 v0, Vector3 p1, Vector3 v1, float dt, float u)` — Task 1 정의 ↔ Task 4 호출 시그니처 일치(System.Numerics.Vector3, `.ToNumerics()`로 변환).
- `RemoteInterpolationClock`: `RecordArrival(long, double)`/`RenderTime`/`HasSnapshot` — Task 3 정의 ↔ Task 4·5 사용 일치.
- `InterpolationDelayEstimator(sendInterval,n,k,minCushion,maxCushion,smoothing)`/`RecordArrival(double)`/`Cushion` — Task 2 정의 ↔ Task 3 사용 일치.
- `AddServerEntitySnap(EntitySnap)` — Task 4 정의 ↔ Task 5 호출 일치.

## Open Questions (구현 중 확인)
- `entity.position`을 매 프레임 쓰면 reactive 경로가 kinematic 리지드바디를 매끄럽게 갱신하는지(플레이서 원격과 로컬 대시 충돌 확인). 문제 시 비주얼만 프레임 갱신 + 콜라이더는 저빈도로 분리 검토.
- `RemoteInterpolationClock`의 `errorScale`/`snapThreshold` 초기값(1틱/8틱)이 rate 추종에 적정한지(Task 8 튜닝).
- 첫 스냅 도착 전(HasSnapshot=false) 원격 엔티티 렌더 억제가 스폰 순간 위치 팝을 만들지(스폰 시 creationData.position에 이미 배치되므로 대개 OK — 확인).
