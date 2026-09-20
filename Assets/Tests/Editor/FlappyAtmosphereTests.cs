using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappyAtmosphereTests
{
    private bool fog;
    private Color fogColor;
    private float density;
    private Material skybox;

    [SetUp]
    public void SetUp()
    {
        fog = RenderSettings.fog;
        fogColor = RenderSettings.fogColor;
        density = RenderSettings.fogDensity;
        skybox = RenderSettings.skybox;
    }

    [TearDown]
    public void TearDown()
    {
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = density;
        RenderSettings.skybox = skybox;
    }

    private sealed class FakeContext : IPlayerContext
    {
        public GameFramework.ISession session { get; set; }
        public string entityId { get; set; }
        public LOPActor actor { get; set; }
    }

    private static FlappyAtmosphere Make(float x)
    {
        var registry = new EntityRegistry();
        var e = new Entity("me");
        e.Add(new GameFramework.World.Transform
        {
            Position = new System.Numerics.Vector3(x, 0f, 0f)
        });
        registry.Add(e);
        return new FlappyAtmosphere(new FakeContext { entityId = "me" }, registry);
    }

    [Test]
    public void 안개를_켜고_지수제곱으로_둔다()
    {
        //  씬에 저장해 봐야 소용없다 — 맵이 additive라 활성 씬의 설정이 이긴다.
        RenderSettings.fog = false;

        Make(0f).Apply(0f);

        Assert.IsTrue(RenderSettings.fog);
        Assert.AreEqual(FogMode.ExponentialSquared, RenderSettings.fogMode);
    }

    [Test]
    public void 진행률이_안개_밀도를_정한다()
    {
        var sut = Make(0f);

        sut.Apply(0f);
        float atStart = RenderSettings.fogDensity;
        sut.Apply(1f);

        Assert.Greater(RenderSettings.fogDensity, atStart);
    }

    [Test]
    public void 참가_전에는_손대지_않는다()
    {
        //  entityId가 비어 있는 동안 칠하면 로비 화면 색이 바뀐다.
        var sut = new FlappyAtmosphere(new FakeContext { entityId = null }, new EntityRegistry());
        RenderSettings.fogDensity = 0.5f;

        sut.Tick();

        Assert.AreEqual(0.5f, RenderSettings.fogDensity, 1e-5f);
    }

    [Test]
    public void 끝나면_시작값으로_되돌린다()
    {
        //  안개는 전역이라 안 되돌리면 다음 게임모드로 샌다.
        RenderSettings.fog = false;
        RenderSettings.fogDensity = 0.5f;
        var sut = Make(0f);

        sut.Apply(1f);
        sut.Dispose();

        Assert.IsFalse(RenderSettings.fog);
        Assert.AreEqual(0.5f, RenderSettings.fogDensity, 1e-5f);
    }

    [Test]
    public void 스카이박스_원본을_칠하지_않는다()
    {
        //  원본은 서브모듈 파일이다 — 그대로 칠하면 플레이할 때마다 .mat이 더러워진다.
        var source = new Material(Shader.Find("Skybox/Procedural"));
        RenderSettings.skybox = source;
        var sut = Make(0f);

        sut.Apply(1f);

        Assert.AreNotSame(source, RenderSettings.skybox);
        sut.Dispose();
        Object.DestroyImmediate(source);
    }
}
