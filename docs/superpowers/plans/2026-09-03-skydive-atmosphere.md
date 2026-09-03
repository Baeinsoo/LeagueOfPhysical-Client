# Skydive 대기(하늘·안개·구름) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 3000m를 떨어지는 동안 속도와 진행도가 화면으로 읽히게 만든다 — 안개로 깊이를,
구름 층으로 속도를, 고도에 따른 색조로 진행도를.

**Architecture:** 전부 프레젠테이션이다. 순수 C# 두 개(고도→색 곡선, 구름 층 표)가 값을
정하고, VContainer 엔트리포인트 하나가 매 프레임 그 값을 `RenderSettings`에 쓴다. 구름
판때기는 코스 빌더가 맵 씬에 함께 굽는다. 시뮬(LOP-Shared)과 서버는 건드리지 않는다.

**Tech Stack:** Unity 6000.3 · URP 17.3 · VContainer · NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-09-03-skydive-atmosphere-design.md`

## Global Constraints

- **LOP-Shared와 서버 레포를 수정하지 않는다.** 수정이 필요해지면 설계가 틀린 것이니 멈추고 보고한다.
- 안개 기준 밀도 **0.0013**, `FogMode.ExponentialSquared` (스펙 §3.1).
- 코스: 스폰 y=3000, 선반 y=2600·2200·1800·1400·1000·600·200, 폭 ±100m.
- **구름에는 콜라이더가 없어야 한다.** 붙으면 그 위에 착지한다 (스펙 §8).
- `Assets/Art/Skybox/skybox.mat`은 **네 모드가 공유하므로 수정 금지.** 새로 만든다 (스펙 §8).
- 클라 EditMode 테스트는 `Assets/Tests/Editor/`(asmdef 없음)에 둔다 — Assembly-CSharp 타입을 그대로 쓴다.
- 코드 주석은 한국어로, **왜**만 짧게. 코드로 자명한 것은 달지 않는다.
- 커밋은 피처 브랜치에서. `git add -A` 금지 — 바꾼 파일만 경로로 지정한다.

### 스펙에서 바꾼 것 하나

스펙 §4.3.1은 "밀도의 기준값은 씬에 저장된다"고 했지만, **드라이버가 상수로 들고 매 프레임
쓴다**로 바꾼다. 씬에서 읽으면 맵이 additive로 늦게 로드될 때 기준값을 0으로 물을 수 있고,
읽는 쪽과 쓰는 쪽이 같아져 단일 writer가 깨진다. 씬은 **켜기/모드/색 기본값**만 갖는다.

---

## File Structure

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Game/SkydiveSkyGradient.cs` | 고도 → 안개색·하늘 틴트 (순수 static) |
| `Assets/Scripts/Game/SkydiveCloudLayers.cs` | 구름 층의 높이 표 + 밀도 배수 (순수 static) |
| `Assets/Scripts/Game/SkydiveAtmosphere.cs` | 매 프레임 `RenderSettings`에 쓰는 엔트리포인트 |
| `Assets/Scripts/Game/SkydiveLifetimeScope.cs` | 위 엔트리포인트 등록 (수정) |
| `Assets/Scripts/Editor/SkydiveCourseBuilder.cs` | 사암 머티리얼 + 구름 판때기 굽기 (수정) |
| `Assets/Scripts/Editor/SkydiveLookCapture.cs` | 고정 시점 캡처 메뉴 (전/후 비교) |
| `Assets/Tests/Editor/SkydiveSkyGradientTests.cs` | 색 곡선 |
| `Assets/Tests/Editor/SkydiveCloudLayersTests.cs` | 층 판정·밀도 배수 |
| `Assets/Tests/Editor/SkydiveAtmosphereTests.cs` | 틱이 실제로 RenderSettings를 쓰는가 |
| `Assets/Tests/Editor/SkydiveCloudColliderTests.cs` | 구름에 콜라이더가 없는가 |
| (Art) `Assets/Art/Materials/SkydiveStone.mat` | 사암 선반 |
| (Art) `Assets/Art/Materials/SkydiveCloud.mat` | 반투명 구름 |
| (Art) `Assets/Art/Skybox/skydive_sky.mat` | Skydive 전용 하늘 |
| (Art) `Assets/Art/Scenes/SkydiveMap.unity` | 안개 켜기·앰비언트 (수정) |

---

### Task 1: 고도 → 색 곡선

**Files:**
- Create: `Assets/Scripts/Game/SkydiveSkyGradient.cs`
- Test: `Assets/Tests/Editor/SkydiveSkyGradientTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `LOP.SkydiveSkyGradient.Evaluate(float altitude) → SkydiveSkyGradient.Colors`
  (필드 `Color fog`, `Color skyTint`), `SkydiveSkyGradient.TopAltitude = 3000f`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using LOP;
using NUnit.Framework;
using UnityEngine;

public class SkydiveSkyGradientTests
{
    private const float Tol = 0.01f;

    [Test]
    public void 꼭대기에서는_서늘한_하늘색이다()
    {
        var c = SkydiveSkyGradient.Evaluate(3000f);

        Assert.That(c.fog.b, Is.GreaterThan(c.fog.r + 0.08f), "파랑이 빨강보다 확실히 높아야 서늘하다");
    }

    [Test]
    public void 지면_근처에서는_따뜻한_크림빛이다()
    {
        var c = SkydiveSkyGradient.Evaluate(0f);

        Assert.That(c.fog.r, Is.GreaterThan(c.fog.b + 0.08f), "빨강이 파랑보다 높아야 따뜻하다");
    }

    [Test]
    public void 중간_고도는_두_끝_사이에_있다()
    {
        var top = SkydiveSkyGradient.Evaluate(3000f);
        var mid = SkydiveSkyGradient.Evaluate(1500f);
        var bottom = SkydiveSkyGradient.Evaluate(0f);

        Assert.That(mid.fog.r, Is.GreaterThan(top.fog.r).And.LessThan(bottom.fog.r));
    }

    [Test]
    public void 범위_밖_고도는_양끝으로_고정된다()
    {
        Assert.That(SkydiveSkyGradient.Evaluate(9000f).fog.r,
                    Is.EqualTo(SkydiveSkyGradient.Evaluate(3000f).fog.r).Within(Tol));
        Assert.That(SkydiveSkyGradient.Evaluate(-500f).fog.r,
                    Is.EqualTo(SkydiveSkyGradient.Evaluate(0f).fog.r).Within(Tol));
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Run: `unity command run_tests --project-path <클라> --json -- --mode EditMode --async_tests true`
그다음 `test_status`를 폴링한다.
Expected: 컴파일 실패 — `SkydiveSkyGradient` 없음

- [ ] **Step 3: 최소 구현**

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 고도에 따라 대기 색을 정한다. 높이에 따라 하늘이 변하면 HUD 숫자 없이도 얼마나
    /// 내려왔는지가 읽힌다 — 레퍼런스에서 가장 인상적이었던 부분이다.
    /// </summary>
    public static class SkydiveSkyGradient
    {
        public const float TopAltitude = 3000f;

        // 위는 공기가 얇은 느낌의 서늘한 하늘색, 아래는 지면의 따뜻한 아지랑이.
        private static readonly Color Top = new Color(0.72f, 0.80f, 0.90f);
        private static readonly Color Bottom = new Color(0.94f, 0.89f, 0.78f);

        public readonly struct Colors
        {
            public readonly Color fog;
            public readonly Color skyTint;

            public Colors(Color fog, Color skyTint)
            {
                this.fog = fog;
                this.skyTint = skyTint;
            }
        }

        public static Colors Evaluate(float altitude)
        {
            float t = Mathf.Clamp01(1f - altitude / TopAltitude);   // 0=꼭대기, 1=지면
            Color fog = Color.Lerp(Top, Bottom, t);

            // 하늘은 안개보다 덜 변한다 — 같이 움직이면 화면이 통째로 색만 바뀐 것처럼 보인다.
            Color sky = Color.Lerp(Top, Bottom, t * 0.55f);
            return new Colors(fog, sky);
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

Expected: 위 네 테스트 PASS. 기존 테스트 수가 줄지 않았는지 **이름으로** 확인한다.

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Game/SkydiveSkyGradient.cs Assets/Scripts/Game/SkydiveSkyGradient.cs.meta \
        Assets/Tests/Editor/SkydiveSkyGradientTests.cs Assets/Tests/Editor/SkydiveSkyGradientTests.cs.meta
git commit -m "feat(skydive): 고도에 따라 대기 색을 정하는 곡선"
```

---

### Task 2: 구름 층 표와 밀도 배수

**Files:**
- Create: `Assets/Scripts/Game/SkydiveCloudLayers.cs`
- Test: `Assets/Tests/Editor/SkydiveCloudLayersTests.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `LOP.SkydiveCloudLayers.Altitudes` (`float[]`), `.HalfThickness` (`float`),
  `.BaseFogDensity` (`float`), `.DensityAt(float altitude) → float`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using LOP;
using NUnit.Framework;

public class SkydiveCloudLayersTests
{
    [Test]
    public void 층은_코스_안에만_놓인다()
    {
        foreach (float y in SkydiveCloudLayers.Altitudes)
        {
            Assert.That(y, Is.GreaterThan(0f).And.LessThan(3000f));
        }
        Assert.That(SkydiveCloudLayers.Altitudes.Length, Is.GreaterThan(8));
    }

    [Test]
    public void 층_밖에서는_기준_밀도다()
    {
        // 층 사이의 한가운데. 간격(170)보다 반두께(40)가 작아 여기는 확실히 바깥이다.
        // "반두께의 몇 배"로 잡으면 오히려 다음 층 안으로 들어간다.
        float between = (SkydiveCloudLayers.Altitudes[0] + SkydiveCloudLayers.Altitudes[1]) * 0.5f;

        Assert.That(SkydiveCloudLayers.DensityAt(between),
                    Is.EqualTo(SkydiveCloudLayers.BaseFogDensity).Within(1e-6f));
    }

    [Test]
    public void 층_사이에는_틈이_있다()
    {
        // 반두께가 간격의 절반보다 크면 코스 전체가 구름 속이 되어 버린다.
        float gap = SkydiveCloudLayers.Altitudes[0] - SkydiveCloudLayers.Altitudes[1];

        Assert.That(SkydiveCloudLayers.HalfThickness * 2f, Is.LessThan(gap),
                    "층 두께가 간격보다 작아야 들어갔다 나오는 게 느껴진다");
    }

    [Test]
    public void 층_한가운데가_가장_짙다()
    {
        float mid = SkydiveCloudLayers.Altitudes[1];

        Assert.That(SkydiveCloudLayers.DensityAt(mid),
                    Is.GreaterThan(SkydiveCloudLayers.BaseFogDensity * 2f));
    }

    [Test]
    public void 층을_연달아_지나도_값이_누적되지_않는다()
    {
        // 같은 고도를 여러 번 물어도 답이 같아야 한다 — 자기가 쓴 값을 기억하면 안 된다.
        float mid = SkydiveCloudLayers.Altitudes[2];
        float first = SkydiveCloudLayers.DensityAt(mid);

        for (int i = 0; i < 50; i++)
        {
            SkydiveCloudLayers.DensityAt(mid);
        }

        Assert.That(SkydiveCloudLayers.DensityAt(mid), Is.EqualTo(first).Within(1e-6f));
    }

    [Test]
    public void 가장자리에서는_부드럽게_들어간다()
    {
        float mid = SkydiveCloudLayers.Altitudes[1];
        float edge = mid + SkydiveCloudLayers.HalfThickness * 0.98f;

        float atEdge = SkydiveCloudLayers.DensityAt(edge);
        Assert.That(atEdge, Is.GreaterThan(SkydiveCloudLayers.BaseFogDensity));
        Assert.That(atEdge, Is.LessThan(SkydiveCloudLayers.DensityAt(mid)));
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — `SkydiveCloudLayers` 없음

- [ ] **Step 3: 최소 구현**

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 구름 층의 높이와, 층 안에서 안개가 얼마나 짙어지는지.
    ///
    /// 선반은 깊이 단서가 못 된다 — 위 선반이 아래를 완전히 가려서 겹겹이 멀어지는 그림이
    /// 안 나온다. 그래서 낙하 속도감은 구름이 전담한다.
    ///
    /// 굽는 쪽(에디터 빌더)과 보는 쪽(런타임)이 같은 표를 봐야 하므로 여기 하나만 둔다.
    /// </summary>
    public static class SkydiveCloudLayers
    {
        // 60m/s로 떨어지면 층 사이가 약 2.8초. 층을 지나는 데는 약 1.3초 걸린다.
        private const float Spacing = 170f;
        private const float Lowest = 150f;
        private const float Highest = 2900f;

        /// <summary>층의 반두께(m). 이 범위 안에 있으면 구름 속이다.</summary>
        public const float HalfThickness = 40f;

        /// <summary>층 밖에서 쓰는 기준 밀도. 400m에서 24%, 1000m에서 81% 씻긴다.</summary>
        public const float BaseFogDensity = 0.0013f;

        // 한가운데에서 기준의 몇 배가 되는가. 너무 올리면 발밑 선반까지 사라져
        // "윤곽은 비쳐 보임"이 아니라 "완전히 가림"이 된다.
        private const float PeakMultiplier = 3.4f;

        public static readonly float[] Altitudes = Build();

        private static float[] Build()
        {
            int count = Mathf.FloorToInt((Highest - Lowest) / Spacing) + 1;
            var list = new float[count];
            for (int i = 0; i < count; i++)
            {
                list[i] = Highest - i * Spacing;
            }
            return list;
        }

        /// <summary>그 고도에서 써야 할 안개 밀도. 고도만 보고 답하므로 호출해도 상태가 안 남는다.</summary>
        public static float DensityAt(float altitude)
        {
            float nearest = 0f;
            for (int i = 0; i < Altitudes.Length; i++)
            {
                float d = Mathf.Abs(altitude - Altitudes[i]);
                if (i == 0 || d < nearest)
                {
                    nearest = d;
                }
            }

            if (nearest >= HalfThickness)
            {
                return BaseFogDensity;
            }

            // 가장자리에서 한가운데로 갈수록 부드럽게 짙어진다(경계에서 툭 끊기면 눈에 띈다).
            float t = 1f - nearest / HalfThickness;
            float eased = t * t * (3f - 2f * t);
            return BaseFogDensity * Mathf.Lerp(1f, PeakMultiplier, eased);
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Game/SkydiveCloudLayers.cs Assets/Scripts/Game/SkydiveCloudLayers.cs.meta \
        Assets/Tests/Editor/SkydiveCloudLayersTests.cs Assets/Tests/Editor/SkydiveCloudLayersTests.cs.meta
git commit -m "feat(skydive): 구름 층의 높이와 안개 밀도 표"
```

---

### Task 3: 대기 드라이버 — 매 프레임 RenderSettings에 쓴다

**Files:**
- Create: `Assets/Scripts/Game/SkydiveAtmosphere.cs`
- Modify: `Assets/Scripts/Game/SkydiveLifetimeScope.cs` (등록 한 줄)
- Test: `Assets/Tests/Editor/SkydiveAtmosphereTests.cs`

**Interfaces:**
- Consumes: `SkydiveSkyGradient.Evaluate`, `SkydiveCloudLayers.DensityAt`,
  `LOP.IPlayerContext.entityId`, `GameFramework.World.EntityRegistry.Get(string)`
- Produces: `LOP.SkydiveAtmosphere` (VContainer `ITickable`), `public void Apply(float altitude)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class SkydiveAtmosphereTests
{
    private sealed class FakeContext : IPlayerContext
    {
        public GameFramework.ISession session { get; set; }
        public string entityId { get; set; }
        public LOPActor actor { get; set; }
    }

    private static (SkydiveAtmosphere sut, Entity e) Make(float altitude)
    {
        var registry = new EntityRegistry();
        var e = new Entity("me");
        e.Add(new GameFramework.World.Transform
        {
            Position = new System.Numerics.Vector3(0f, altitude, 0f)
        });
        registry.Add(e);
        var ctx = new FakeContext { entityId = "me" };
        return (new SkydiveAtmosphere(ctx, registry), e);
    }

    [Test]
    public void 틱이_고도에_맞는_안개색을_쓴다()
    {
        var high = Make(3000f);
        high.sut.Tick();
        Color atTop = RenderSettings.fogColor;

        var low = Make(0f);
        low.sut.Tick();
        Color atBottom = RenderSettings.fogColor;

        Assert.That(atBottom.r, Is.GreaterThan(atTop.r + 0.05f), "아래가 더 따뜻해야 한다");
    }

    [Test]
    public void 구름_속에서는_밀도가_올라간다()
    {
        float between = (SkydiveCloudLayers.Altitudes[0] + SkydiveCloudLayers.Altitudes[1]) * 0.5f;
        var outside = Make(between);
        outside.sut.Tick();
        float baseDensity = RenderSettings.fogDensity;

        var inside = Make(SkydiveCloudLayers.Altitudes[0]);
        inside.sut.Tick();

        Assert.That(RenderSettings.fogDensity, Is.GreaterThan(baseDensity * 2f));
    }

    [Test]
    public void 구름을_나오면_기준으로_돌아온다()
    {
        var sut = Make(SkydiveCloudLayers.Altitudes[1]).sut;
        sut.Tick();

        sut.Apply((SkydiveCloudLayers.Altitudes[1] + SkydiveCloudLayers.Altitudes[2]) * 0.5f);

        Assert.That(RenderSettings.fogDensity,
                    Is.EqualTo(SkydiveCloudLayers.BaseFogDensity).Within(1e-6f));
    }

    [Test]
    public void 내_엔티티가_아직_없으면_아무것도_안_한다()
    {
        RenderSettings.fogDensity = 0.5f;
        var registry = new EntityRegistry();
        var sut = new SkydiveAtmosphere(new FakeContext { entityId = null }, registry);

        sut.Tick();

        Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.5f).Within(1e-6f),
                    "참가 전에는 손대지 않아야 한다");
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — `SkydiveAtmosphere` 없음

- [ ] **Step 3: 최소 구현**

```csharp
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 대기(안개색·밀도·하늘 틴트)를 내 고도에 맞춰 매 프레임 갱신한다.
    ///
    /// 월드에서 높이를 <b>읽기만</b> 한다 — 시뮬은 자신이 관찰되는 것을 모른다.
    /// 연속 상태라 이벤트가 아니라 pull이다(world-core-connection-architecture.md).
    ///
    /// 안개 밀도는 여기서만 쓴다. 씬에서 읽어 오지 않는 이유 — 맵이 additive로 늦게 로드되면
    /// 기준값을 0으로 물을 수 있고, 읽는 쪽과 쓰는 쪽이 같아져 값이 누적된다.
    /// </summary>
    public class SkydiveAtmosphere : ITickable
    {
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public SkydiveAtmosphere(IPlayerContext playerContext,
                                 GameFramework.World.EntityRegistry entityRegistry)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void Tick()
        {
            if (string.IsNullOrEmpty(playerContext.entityId))
            {
                return;   // 아직 참가 전 — 손대지 않는다
            }

            var entity = entityRegistry.Get(playerContext.entityId);
            var transform = entity?.Get<GameFramework.World.Transform>();
            if (transform == null)
            {
                return;
            }

            Apply(transform.Position.Y);
        }

        /// <summary>고도 하나로 대기 전체가 정해진다. 테스트가 이 문으로 들어온다.</summary>
        public void Apply(float altitude)
        {
            var colors = SkydiveSkyGradient.Evaluate(altitude);

            RenderSettings.fogColor = colors.fog;
            RenderSettings.fogDensity = SkydiveCloudLayers.DensityAt(altitude);

            Material sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Tint"))
            {
                sky.SetColor("_Tint", colors.skyTint);
            }
        }
    }
}
```

- [ ] **Step 4: 통과를 확인한다**

- [ ] **Step 5: 씬 스코프에 등록한다**

`Assets/Scripts/Game/SkydiveLifetimeScope.cs`의 `RegisterEntryPoint<SkydiveHudCoordinator>()` 바로 아래에 넣는다:

```csharp
            builder.RegisterEntryPoint<SkydiveAtmosphere>();
```

- [ ] **Step 6: 컴파일과 전체 테스트를 확인한다**

`recompile` → `recompile_status`가 `completed`인지 + **DLL 시각이 소스보다 새로운지** 대조한다
(`recompile_status`는 거짓 통과를 준다). 그다음 EditMode 전체.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Game/SkydiveAtmosphere.cs Assets/Scripts/Game/SkydiveAtmosphere.cs.meta \
        Assets/Scripts/Game/SkydiveLifetimeScope.cs \
        Assets/Tests/Editor/SkydiveAtmosphereTests.cs Assets/Tests/Editor/SkydiveAtmosphereTests.cs.meta
git commit -m "feat(skydive): 고도에 따라 대기를 갱신하는 드라이버"
```

---

### Task 4: 구름 판때기를 코스 빌더가 굽는다

**Files:**
- Modify: `Assets/Scripts/Editor/SkydiveCourseBuilder.cs`
- Test: `Assets/Tests/Editor/SkydiveCloudColliderTests.cs`

**Interfaces:**
- Consumes: `SkydiveCloudLayers.Altitudes`
- Produces: `LOP.EditorTools.SkydiveCourseBuilder.CreateCloudQuad(Transform, string, Vector3, float, Material) → GameObject`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

```csharp
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;

public class SkydiveCloudColliderTests
{
    [Test]
    public void 구름에는_콜라이더가_없다()
    {
        // 콜라이더가 붙으면 키네마틱 이동이 벽으로 인식해 구름 위에 착지한다.
        GameObject quad = SkydiveCourseBuilder.CreateCloudQuad(
            null, "cloud-test", new Vector3(0f, 1000f, 0f), 120f, null);

        try
        {
            Assert.That(quad.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(quad);
        }
    }

    [Test]
    public void 구름은_수평으로_눕는다()
    {
        GameObject quad = SkydiveCourseBuilder.CreateCloudQuad(
            null, "cloud-test", new Vector3(0f, 1000f, 0f), 120f, null);

        try
        {
            Vector3 up = quad.transform.up;
            Assert.That(Mathf.Abs(up.y), Is.GreaterThan(0.99f), "판이 수평이어야 층으로 보인다");
        }
        finally
        {
            Object.DestroyImmediate(quad);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다** — `CreateCloudQuad` 없음

- [ ] **Step 3: 최소 구현**

`SkydiveCourseBuilder`에 추가한다(같은 클래스, `AddBox` 아래):

```csharp
        // 구름 판. 콜라이더를 반드시 지운다 — 남으면 키네마틱 이동이 벽으로 보고
        // 그 위에 착지한다.
        internal static GameObject CreateCloudQuad(Transform parent, string name,
                                                   Vector3 center, float size, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            if (parent != null)
            {
                quad.transform.SetParent(parent, worldPositionStays: false);
            }
            quad.transform.localPosition = center;
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 수평으로 눕힌다
            quad.transform.localScale = new Vector3(size, size, 1f);

            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            if (material != null)
            {
                quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            return quad;
        }
```

- [ ] **Step 4: 통과를 확인한다**

- [ ] **Step 5: 굽기에 층을 넣는다**

`Build()`의 선반 루프 아래에 넣는다:

```csharp
            Material cloud = AssetDatabase.LoadAssetAtPath<Material>(CloudMaterialPath);
            if (cloud == null)
            {
                Debug.LogWarning($"[Skydive] 구름 머티리얼이 없다 — {CloudMaterialPath}. 판만 굽는다.");
            }

            var clouds = new GameObject("Clouds");
            clouds.transform.SetParent(root.transform, worldPositionStays: false);
            for (int i = 0; i < SkydiveCloudLayers.Altitudes.Length; i++)
            {
                float y = SkydiveCloudLayers.Altitudes[i];

                // 한 층을 판 세 장으로 겹쳐 놓는다 — 한 장이면 옆에서 볼 때 종잇장이라 층이 안 된다.
                for (int k = 0; k < 3; k++)
                {
                    float dy = (k - 1) * SkydiveCloudLayers.HalfThickness * 0.6f;
                    CreateCloudQuad(clouds.transform, $"Cloud_{y:0}_{k}",
                                    new Vector3(0f, y + dy, 0f), 460f, cloud);
                }
            }
```

그리고 클래스 상단 상수에 추가한다:

```csharp
        private const string CloudMaterialPath = "Assets/Art/Materials/SkydiveCloud.mat";
```

- [ ] **Step 6: 컴파일과 전체 테스트를 확인한다**

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Editor/SkydiveCourseBuilder.cs \
        Assets/Tests/Editor/SkydiveCloudColliderTests.cs Assets/Tests/Editor/SkydiveCloudColliderTests.cs.meta
git commit -m "feat(skydive): 코스 빌더가 구름 층을 함께 굽는다"
```

---

### Task 5: 사암 선반

**Files:**
- Create (Art): `Assets/Art/Materials/SkydiveStone.mat`
- Modify: `Assets/Scripts/Editor/SkydiveCourseBuilder.cs:83`

- [ ] **Step 1: 머티리얼을 만든다**

Unity 에디터에서 `Assets/Art/Materials/`에 URP Lit 머티리얼을 만들고 이름을 `SkydiveStone`으로
한다. Base Map 없이 Base Color만 **`#CFAC80`**(따뜻한 사암), Smoothness 0.15.

> 왜 새로 만드나 — 지금 선반은 유니티 기본 머티리얼을 쓰고 있어 색을 못 준다.
> 근경이 따뜻해야 원경이 안개로 씻긴 것이 보인다(스펙 §2).

- [ ] **Step 2: 빌더가 그것을 물게 한다**

`SkydiveCourseBuilder.cs:83`을 바꾼다:

```csharp
            Material material = AssetDatabase.LoadAssetAtPath<Material>(StoneMaterialPath);
            if (material == null)
            {
                Debug.LogWarning($"[Skydive] 선반 머티리얼이 없다 — {StoneMaterialPath}. 기본색으로 굽는다.");
            }
```

상수를 추가한다:

```csharp
        private const string StoneMaterialPath = "Assets/Art/Materials/SkydiveStone.mat";
```

- [ ] **Step 3: 코스를 다시 굽는다**

`SkydiveMap` 씬을 열고 메뉴 `LOP/Skydive/코스 굽기` → 콘솔에 에러가 없는지 → 씬 저장.

- [ ] **Step 4: 커밋 (레포 둘)**

```bash
git -C Assets/Art add Materials/SkydiveStone.mat Materials/SkydiveStone.mat.meta Scenes/SkydiveMap.unity
git -C Assets/Art commit -m "feat(skydive): 사암 선반 머티리얼과 구름 층을 구운 맵"
git add Assets/Scripts/Editor/SkydiveCourseBuilder.cs
git commit -m "feat(skydive): 선반에 사암 머티리얼을 물린다"
```

> ⚠️ Art는 서브모듈이라 **먼저 push하고**, 클라 레포에서 포인터를 따로 커밋해야 한다(Task 7).

---

### Task 6: 하늘·안개 씬 설정과 캡처 도구

**Files:**
- Create (Art): `Assets/Art/Skybox/skydive_sky.mat`, `Assets/Art/Materials/SkydiveCloud.mat`
- Modify (Art): `Assets/Art/Scenes/SkydiveMap.unity`
- Create: `Assets/Scripts/Editor/SkydiveLookCapture.cs`

- [ ] **Step 1: 캡처 도구를 만든다 (전/후 비교용)**

```csharp
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 늘 같은 시점에서 게임뷰를 찍는다. 아트는 유닛테스트가 안 되므로, 같은 자리에서 찍은
    /// 전/후를 나란히 놓는 것이 유일하게 재현 가능한 확인 방법이다.
    /// </summary>
    public static class SkydiveLookCapture
    {
        // 코스 옆 위쪽 — 선반 두어 장과 그 아래 구름 층이 한 화면에 들어온다.
        private static readonly Vector3 Eye = new Vector3(230f, 2760f, -230f);
        private static readonly Vector3 Look = new Vector3(0f, 2200f, 0f);

        [MenuItem("LOP/Skydive/보기 캡처")]
        public static void Capture()
        {
            var go = new GameObject("__LookCam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.farClipPlane = 8000f;
                cam.nearClipPlane = 1f;
                cam.fieldOfView = 55f;
                go.transform.position = Eye;
                go.transform.LookAt(Look);

                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var shot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                shot.Apply();
                RenderTexture.active = null;

                string path = $"Temp/skydive-look-{System.DateTime.Now:HHmmss}.png";
                System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());

                // 대화상자를 쓰지 않는다 — 모달은 메인 스레드를 잡아 CLI 자동화를 멈춘다.
                Debug.Log($"[Skydive] 캡처: {path}");

                Object.DestroyImmediate(shot);
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
```

- [ ] **Step 2: "전" 사진을 찍는다**

`SkydiveMap` 씬을 열고 메뉴 `LOP/Skydive/보기 캡처`. 나온 경로를 적어 둔다.

- [ ] **Step 3: 구름 머티리얼을 만든다**

`Assets/Art/Materials/SkydiveCloud.mat` — URP **Unlit**, Surface Type **Transparent**,
Base Color `#FFFFFF` 알파 **0.34**, Render Face **Both**.

> Unlit인 이유 — 구름이 빛을 받아 어두워지면 층이 회색 판처럼 보인다.

- [ ] **Step 4: 하늘 머티리얼을 만든다**

`Assets/Art/Skybox/skydive_sky.mat` — 셰이더 `Skybox/Procedural`,
Sun Size 0.04, Atmosphere Thickness 1.0, Sky Tint `#9EBCDC`, Ground Color `#C7D4DC`, Exposure 1.2.

> ⚠️ **기존 `Assets/Art/Skybox/skybox.mat`을 고치지 마라** — FlappyRace·FlapWang·Panchigi·
> Skydive 네 모드가 공유한다.
>
> Ground Color를 갈색이 아니라 창백한 하늘색으로 두는 이유 — 구름 위에서는 내려다봐도
> 하늘색이어야 한다.

- [ ] **Step 5: 맵 씬의 렌더 설정을 바꾼다**

`SkydiveMap` 씬에서 `Window > Rendering > Lighting`:

| 항목 | 값 |
|---|---|
| Skybox Material | `skydive_sky` |
| Fog | 켬 |
| Fog Mode | Exponential Squared |
| Fog Density | 0.0013 |
| Fog Color | `#B8CCE6` (드라이버가 매 프레임 덮어쓰므로 에디터 미리보기용) |
| Environment Lighting Source | Color, `#7A8899` |

- [ ] **Step 6: 코스를 다시 굽고 "후" 사진을 찍는다**

메뉴 `LOP/Skydive/코스 굽기` → 씬 저장 → `LOP/Skydive/보기 캡처`.
전/후를 나란히 보고, 발밑 선반은 선명하고 아래 선반이 뿌연지 확인한다.

- [ ] **Step 7: 커밋 (레포 둘)**

```bash
git -C Assets/Art add Materials/SkydiveCloud.mat Materials/SkydiveCloud.mat.meta \
                      Skybox/skydive_sky.mat Skybox/skydive_sky.mat.meta Scenes/SkydiveMap.unity
git -C Assets/Art commit -m "feat(skydive): 전용 하늘과 구름 머티리얼, 안개 켠 맵"
git add Assets/Scripts/Editor/SkydiveLookCapture.cs Assets/Scripts/Editor/SkydiveLookCapture.cs.meta
git commit -m "chore(skydive): 늘 같은 시점에서 찍는 캡처 메뉴"
```

---

### Task 7: 내보내기와 실플레이 확인

**Files:** 없음 (배포와 확인만)

- [ ] **Step 1: Art를 push한다**

```bash
git -C Assets/Art log --oneline -3
git -C Assets/Art push origin main
```

- [ ] **Step 2: 클라 레포에 서브모듈 포인터를 커밋한다**

```bash
git add Assets/Art
git status --short
git commit -m "chore(art): Skydive 대기 에셋으로 포인터를 올린다"
```

> 빠뜨리면 CI가 옛 맵을 본다.

- [ ] **Step 3: 클라 main에 머지·push한다**

`CLAUDE.md`의 푸시 규약을 **한 줄씩** 따른다(`&&` 금지, force 금지).

- [ ] **Step 4: 어드레서블을 올린다**

클라 레포의 `content-deploy`를 실행한다.

> ⚠️ 빠뜨리면 **에디터에선 멀쩡한데 폰·서버만 옛 하늘**이 나온다. 어드레서블은 S3에서 온다.

- [ ] **Step 5: 두 클라로 실제로 떨어져 본다**

확인할 것:

| | |
|---|---|
| 발밑 선반은 선명하고, 400m 아래는 뿌옇다 | 안개가 도는가 |
| 구름 층을 지날 때 잠깐 흐려졌다 돌아온다 | 밀도가 누적되지 않는가 |
| **구름 위에 착지하지 않는다** | 콜라이더가 없는가 |
| 내려갈수록 화면이 따뜻해진다 | 고도 색조가 도는가 |
| **러버밴딩이 없다** | 시뮬에 영향이 없는가 |

- [ ] **Step 6: 폰에서 프레임을 확인한다**

APK를 굽고 프레임을 본다. 떨어지면 첫 손잡이는 `SkydiveCloudLayers`의 층 수(간격 170 → 240)다.

---

## Self-Review

**스펙 대응:**

| 스펙 | 태스크 |
|---|---|
| §4.1 안개 | Task 6 Step 5 (씬) + Task 2 (기준 밀도) |
| §4.2 고도 색조 | Task 1, 3 |
| §4.3 구름 층 | Task 2, 4 + Task 6 Step 3 |
| §4.3.1 안개 소유권 | Task 3 (한 곳에서만 쓴다) |
| §4.4 사암 선반 | Task 5 |
| §7 검증 | Task 1·2·3 테스트, Task 4 콜라이더, Task 6 캡처, Task 7 실플레이 |
| §8 함정 | Task 4(콜라이더) · Task 6 Step 4(공유 스카이박스) · Task 7 Step 2·4(배포) |
| §9 배포 사슬 | Task 7 |

**타입 일관성:** `SkydiveSkyGradient.Evaluate`가 돌려주는 `Colors.fog`/`.skyTint`를 Task 3이
같은 이름으로 쓴다. `SkydiveCloudLayers.Altitudes`/`.HalfThickness`/`.BaseFogDensity`/
`.DensityAt`을 Task 2가 정의하고 Task 3·4가 그대로 쓴다. `CreateCloudQuad`의 시그니처가
Task 4의 테스트와 호출부에서 같다.

**빈칸 없음:** 모든 코드 단계에 실제 코드가 있다. "적절히 처리한다" 류의 문장은 없다.
