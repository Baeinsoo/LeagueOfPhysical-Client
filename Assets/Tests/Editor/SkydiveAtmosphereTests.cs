using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class SkydiveAtmosphereTests
{
    private Material originalSkybox;

    [SetUp]
    public void SetUp()
    {
        originalSkybox = RenderSettings.skybox;
    }

    [TearDown]
    public void TearDown()
    {
        RenderSettings.skybox = originalSkybox;
    }

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

    // ── Ruling R3 — 스카이박스는 원본이 아니라 런타임 복사본만 칠한다 ──

    [Test]
    public void 스카이박스는_원본이_아니라_복사본을_칠한다()
    {
        var original = new Material(Shader.Find("Skybox/Procedural"));
        Color originalTint = original.HasProperty("_SkyTint")
            ? original.GetColor("_SkyTint")
            : original.HasProperty("_Tint") ? original.GetColor("_Tint") : Color.white;
        RenderSettings.skybox = original;

        var sut = new SkydiveAtmosphere(new FakeContext { entityId = null }, new EntityRegistry());
        sut.Apply(500f);

        Assert.That(RenderSettings.skybox, Is.Not.SameAs(original), "원본이 아니라 복사본이 꽂혀 있어야 한다");
        Color afterTint = original.HasProperty("_SkyTint")
            ? original.GetColor("_SkyTint")
            : original.HasProperty("_Tint") ? original.GetColor("_Tint") : Color.white;
        Assert.That(afterTint, Is.EqualTo(originalTint), "원본 머티리얼 값은 그대로여야 한다");
    }

    [Test]
    public void 복사본은_한_번만_만든다()
    {
        RenderSettings.skybox = new Material(Shader.Find("Skybox/Procedural"));
        var sut = new SkydiveAtmosphere(new FakeContext { entityId = null }, new EntityRegistry());

        sut.Apply(3000f);
        Material afterFirst = RenderSettings.skybox;

        sut.Apply(1500f);
        sut.Apply(0f);

        Assert.That(RenderSettings.skybox, Is.SameAs(afterFirst), "두 번째부터는 복사가 다시 일어나면 안 된다");
    }

    // ── Finding 1 — 안개는 씬 파일이 아니라 이 드라이버가 켠다 ──

    [Test]
    public void Apply는_안개를_직접_켠다()
    {
        RenderSettings.fog = false;
        var sut = new SkydiveAtmosphere(new FakeContext { entityId = null }, new EntityRegistry());

        sut.Apply(1500f);

        Assert.That(RenderSettings.fog, Is.True, "activeScene 설정에 기대지 않고 직접 켜야 한다");
        Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.ExponentialSquared));
    }

    // ── Finding 2 — 하늘 틴트도 값이 실제로 바뀌는지 읽어서 확인한다 ──

    [Test]
    public void Apply는_고도에_따라_하늘_틴트를_바꾼다()
    {
        RenderSettings.skybox = new Material(Shader.Find("Skybox/Procedural"));
        var sut = new SkydiveAtmosphere(new FakeContext { entityId = null }, new EntityRegistry());

        sut.Apply(3000f);
        float tintAtTop = ((Material)RenderSettings.skybox).GetColor("_SkyTint").r;

        sut.Apply(0f);
        float tintAtBottom = ((Material)RenderSettings.skybox).GetColor("_SkyTint").r;

        Assert.That(tintAtBottom, Is.GreaterThan(tintAtTop), "지면 쪽이 더 따뜻해야(붉은기 상승) 한다");
        Assert.That(tintAtBottom, Is.LessThan(RenderSettings.fogColor.r),
                    "하늘은 안개보다 덜 변한다(0.55배 보간)");
    }

    // ── Finding 3 — Dispose가 원래 값으로 되돌리고 복사본을 정리한다 ──

    [Test]
    public void Dispose는_안개와_스카이박스를_원래대로_되돌린다()
    {
        RenderSettings.fog = false;
        RenderSettings.fogDensity = 0.02f;
        var original = new Material(Shader.Find("Skybox/Procedural"));
        RenderSettings.skybox = original;

        var sut = new SkydiveAtmosphere(new FakeContext { entityId = null }, new EntityRegistry());
        sut.Apply(1500f);

        sut.Dispose();

        Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.02f).Within(1e-6f));
        Assert.That(RenderSettings.skybox, Is.SameAs(original));
    }
}
