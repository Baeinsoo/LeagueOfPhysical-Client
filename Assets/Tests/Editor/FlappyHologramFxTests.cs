using System.Collections.Generic;
using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappyHologramFxTests
{
    //  이 테스트가 만든 GameObject/Material — 플레이 중이 아니라 DestroyImmediate로만 지울 수 있다.
    private readonly List<Object> spawned = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (var obj in spawned)
        {
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }
        spawned.Clear();
    }

    private FlappyHologramMarker CreateMarker(Vector3 center, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = center;
        go.transform.localScale = size;
        var marker = go.AddComponent<FlappyHologramMarker>();
        spawned.Add(go);
        return marker;
    }

    //  새 한 마리를 담은 EntityRegistry + 위치를 바꿀 수 있는 Transform을 함께 돌려준다
    //  (FlappyAtmosphereTests.Make와 같은 모양).
    private static (EntityRegistry registry, GameFramework.World.Transform transform) MakeBird(Vector3 startPosition)
    {
        var registry = new EntityRegistry();
        var entity = new Entity("bird");
        var transform = new GameFramework.World.Transform
        {
            Position = new System.Numerics.Vector3(startPosition.x, startPosition.y, startPosition.z),
        };
        entity.Add(transform);
        entity.Add(new EntityKind(EntityType.Character));
        registry.Add(entity);
        return (registry, transform);
    }

    private static void MoveTo(GameFramework.World.Transform transform, Vector3 position)
    {
        transform.Position = new System.Numerics.Vector3(position.x, position.y, position.z);
    }

    //  ── 마커 검색 상한 ──────────────────────────────────────

    [Test]
    public void 마커가_없으면_5초_동안만_재시도하고_그_뒤엔_찾지_않는다()
    {
        var sut = new FlappyHologramFx(new EntityRegistry());
        int searchCount = 0;
        sut.markerSearch = () => { searchCount++; return System.Array.Empty<FlappyHologramMarker>(); };

        sut.Tick(0.016f);   //  스코프 시작(첫 틱) — 곧바로 한 번 찾아본다
        Assert.AreEqual(1, searchCount, "첫 틱에 바로 한 번 찾아야 한다");

        for (int i = 0; i < 11; i++)
        {
            sut.Tick(0.5f);   //  0.016 + 0.5×11 = 5.516초 — 5초 창을 넘어서까지 채운다
        }
        int countWithinWindow = searchCount;
        Assert.Greater(countWithinWindow, 1, "5초 창 안에서는 1초 간격으로 여러 번 재시도해야 한다");

        for (int i = 0; i < 10; i++)
        {
            sut.Tick(1f);   //  창을 완전히 넘겨서도 계속 흘려본다
        }
        Assert.AreEqual(countWithinWindow, searchCount, "5초가 지나면 그 뒤로는 다시 찾지 않는다");
    }

    [Test]
    public void 창_안에서_나타난_마커를_찾아_실제로_쓴다()
    {
        var (registry, transform) = MakeBird(new Vector3(1000f, 0f, 0f));   //  일단 관문 밖에서 시작
        var marker = CreateMarker(new Vector3(10f, 0f, 0f), Vector3.one * 2f);

        var sut = new FlappyHologramFx(registry);
        int searchCount = 0;
        sut.markerSearch = () =>
        {
            searchCount++;
            //  세 번째 재시도에야 나타난다 — "창 안에서 나타나면 찾아낸다"를 재시도 도중 발견으로 검증.
            return searchCount >= 3 ? new[] { marker } : System.Array.Empty<FlappyHologramMarker>();
        };

        sut.Tick(0.016f);   //  1차 — 없음
        sut.Tick(1f);       //  2차 — 없음
        sut.Tick(1f);       //  3차 — 찾음
        Assert.AreEqual(3, searchCount);

        for (int i = 0; i < 10; i++)
        {
            sut.Tick(1f);   //  이미 찾았으니 5초를 더 넘겨도 다시 찾지 않는다
        }
        Assert.AreEqual(3, searchCount, "찾은 뒤에는 재시도하지 않는다");

        //  찾은 마커가 장식으로만 남지 않고 실제로 동작하는지 — 새가 들어오면 발동해야 한다.
        MoveTo(transform, new Vector3(10f, 0f, 0f));
        sut.Tick(0.016f);
        Assert.AreEqual(1, sut.TriggerCount, "창 안에서 찾은 마커도 실제로 발동해야 한다");

        sut.Dispose();
    }

    //  ── 들어오는 순간에만 발동(rising edge) ──────────────────────

    [Test]
    public void 들어오는_순간에만_발동하고_나갔다_돌아오면_다시_발동한다()
    {
        var outside = new Vector3(1000f, 0f, 0f);
        var inside = new Vector3(10f, 0f, 0f);
        var (registry, transform) = MakeBird(outside);
        var marker = CreateMarker(inside, Vector3.one * 2f);

        var sut = new FlappyHologramFx(registry);
        sut.markerSearch = () => new[] { marker };

        sut.Tick(0.016f);
        Assert.AreEqual(0, sut.TriggerCount, "밖에 있으면 발동하지 않는다");

        MoveTo(transform, inside);
        sut.Tick(0.016f);
        Assert.AreEqual(1, sut.TriggerCount, "들어오는 순간 발동한다");

        sut.Tick(0.016f);
        sut.Tick(0.016f);
        Assert.AreEqual(1, sut.TriggerCount, "안에 머무는 동안은 다시 발동하지 않는다");

        MoveTo(transform, outside);
        sut.Tick(0.016f);
        Assert.AreEqual(1, sut.TriggerCount, "나가는 것 자체로는 발동하지 않는다");

        MoveTo(transform, inside);
        sut.Tick(0.016f);
        Assert.AreEqual(2, sut.TriggerCount, "다시 들어오면 또 발동한다");

        sut.Dispose();
    }

    //  ── Dispose가 원본 재질을 되돌리는지 ──────────────────────

    [Test]
    public void Dispose가_원본_재질을_되돌린_뒤_인스턴스를_지운다()
    {
        var original = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        spawned.Add(original);
        var marker = CreateMarker(new Vector3(10f, 0f, 0f), Vector3.one * 2f);
        var renderer = marker.GetComponent<Renderer>();
        renderer.sharedMaterial = original;

        var sut = new FlappyHologramFx(new EntityRegistry());
        sut.markerSearch = () => new[] { marker };
        sut.Tick(0.016f);   //  마커를 찾아 상태를 만든다(재질 인스턴스화 포함)

        Assert.AreNotSame(original, renderer.sharedMaterial, "찾자마자 개별 인스턴스로 바뀌어 있어야 한다");

        sut.Dispose();

        Assert.AreSame(original, renderer.sharedMaterial, "Dispose 후에는 원본 재질로 되돌아가 있어야 한다");
    }
}
