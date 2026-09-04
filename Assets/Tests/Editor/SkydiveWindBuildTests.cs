using System.Collections.Generic;
using LOP;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SkydiveWindBuildTests
{
    private const string SkydiveMapPath = "Assets/Art/Scenes/SkydiveMap.unity";

    // 콜라이더가 붙으면 키네마틱 이동이 벽으로 인식해 바람 위에 착지한다 — 구름에서 겪은 함정.
    [Test]
    public void 바람_시각물에는_콜라이더가_없다()
    {
        WindVisualAssets assets = SkydiveWindAssets.CreateInMemory();
        GameObject volume = SkydiveCourseBuilder.CreateWindVolume(
            null, "wind-test", new Vector3(0f, 1000f, 0f), 25f, 120f, new Vector3(0f, 14f, 0f), assets);

        try
        {
            Assert.That(volume.GetComponentsInChildren<MeshRenderer>(true), Is.Not.Empty,
                        "시각물이 아예 안 생겼으면 이 검사는 아무것도 검증하지 않는다");
            Assert.That(volume.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(volume);
            DestroyAssets(assets);
        }
    }

    // 나중에 범위 표시를 옵션으로 끌 수 있어야 한다. 끄고 나면 화살표와 흐름만 남아야 한다.
    [Test]
    public void 범위_표시만_따로_끌_수_있다()
    {
        WindVisualAssets assets = SkydiveWindAssets.CreateInMemory();
        GameObject volume = SkydiveCourseBuilder.CreateWindVolume(
            null, "wind-test", Vector3.zero, 150f, 400f, new Vector3(0f, 0f, -20f), assets);

        try
        {
            var visualizer = volume.GetComponent<WindVolumeVisualizer>();
            Assert.IsNotNull(visualizer);
            Assert.IsNotNull(visualizer.ArrowsRoot, "화살표 부모를 안 이어 두면 흐름이 안 돈다");
            Assert.IsNotNull(visualizer.BoundsRoot);
            Assert.IsTrue(visualizer.ShowBounds);

            int arrowCount = visualizer.ArrowsRoot.childCount;

            visualizer.ShowBounds = false;

            Assert.IsFalse(visualizer.BoundsRoot.activeSelf);
            Assert.AreEqual(arrowCount, visualizer.ArrowsRoot.childCount,
                            "범위를 껐는데 화살표까지 사라지면 분리가 안 된 것이다");
            Assert.IsTrue(visualizer.ArrowsRoot.gameObject.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(volume);
            DestroyAssets(assets);
        }
    }

    // 길이가 세기를 나타내지 않으면 14 m/s와 20 m/s가 똑같이 생긴다 — 고치려던 문제 그 자체.
    [Test]
    public void 화살표_길이는_바람_세기다()
    {
        WindVisualAssets assets = SkydiveWindAssets.CreateInMemory();
        GameObject slow = SkydiveCourseBuilder.CreateWindVolume(
            null, "slow", Vector3.zero, 25f, 120f, new Vector3(0f, 10f, 0f), assets);
        GameObject fast = SkydiveCourseBuilder.CreateWindVolume(
            null, "fast", Vector3.zero, 25f, 120f, new Vector3(0f, 20f, 0f), assets);

        try
        {
            Assert.AreEqual(10f, ArrowOf(slow).localScale.z, 0.001f);
            Assert.AreEqual(20f, ArrowOf(fast).localScale.z, 0.001f);
        }
        finally
        {
            Object.DestroyImmediate(slow);
            Object.DestroyImmediate(fast);
            DestroyAssets(assets);
        }
    }

    [Test]
    public void 세기_단계가_다르면_색도_다르다()
    {
        WindVisualAssets assets = SkydiveWindAssets.CreateInMemory();

        try
        {
            Assert.AreEqual(0, SkydiveWindAssets.StrengthBand(10f));
            Assert.AreEqual(1, SkydiveWindAssets.StrengthBand(14f));
            Assert.AreEqual(2, SkydiveWindAssets.StrengthBand(20f));

            Assert.AreNotEqual(assets.ArrowFor(10f).GetColor("_BaseColor"),
                               assets.ArrowFor(20f).GetColor("_BaseColor"));
        }
        finally
        {
            DestroyAssets(assets);
        }
    }

    private static Transform ArrowOf(GameObject volume)
        => volume.GetComponent<WindVolumeVisualizer>().ArrowsRoot.GetChild(0);

    private static void DestroyAssets(WindVisualAssets assets)
    {
        Object.DestroyImmediate(assets.Arrow);
        Object.DestroyImmediate(assets.Shell);
        for (int i = 0; i < assets.ArrowMaterials.Length; i++)
        {
            Object.DestroyImmediate(assets.ArrowMaterials[i]);
            Object.DestroyImmediate(assets.ShellMaterials[i]);
        }
    }

    [Test]
    public void 구운_볼륨에_마커가_붙는다()
    {
        GameObject volume = SkydiveCourseBuilder.CreateWindVolume(
            null, "wind-test", new Vector3(30f, 1900f, 30f), 25f, 120f, new Vector3(0f, 14f, 0f), null);

        try
        {
            var marker = volume.GetComponent<WindVolume>();
            Assert.IsNotNull(marker, "마커가 없으면 맵을 읽어도 바람이 안 생긴다");
            Assert.AreEqual(25f, marker.Radius);
            Assert.AreEqual(120f, marker.Height);
            Assert.AreEqual(14f, marker.Wind.y);
        }
        finally
        {
            Object.DestroyImmediate(volume);
        }
    }

    [Test]
    public void 구운_맵의_바람에는_콜라이더가_없다()
    {
        Scene scene = EditorSceneManager.OpenScene(SkydiveMapPath, OpenSceneMode.Additive);
        try
        {
            // 이름이 아니라 마커 컴포넌트로 센다 — 막대도 "Wind_"로 시작해서 이름으로 세면
            // 볼륨 8개가 아니라 막대까지 120개가 잡힌다.
            int volumeCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (WindVolume marker in root.GetComponentsInChildren<WindVolume>(true))
                {
                    volumeCount++;
                    Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty,
                                $"{marker.name}에 콜라이더가 남아 있다 — 캐릭터가 바람 위에 착지한다");
                }
            }

            Assert.That(volumeCount, Is.EqualTo(SkydiveCourseBuilder.Winds.Length),
                        "구운 맵의 바람 개수가 표와 다르다 — 다시 구워야 한다");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    // 역풍은 밀린 거리와 필요 이동이 더해져 구간을 막을 수 있다. 표가 그렇게 되어 있으면
    // 굽기 전에 여기서 걸린다.
    [Test]
    public void 모든_구간을_적어도_한_자세는_지날_수_있다()
    {
        string failure = SkydiveCourseBuilder.FindImpassableSection();

        Assert.IsNull(failure, failure);
    }

    // 볼륨은 씬에서 디자이너가 손으로 만진다 — 표(SkydiveCourseBuilder.Winds)만 검사하면 씬을
    // 고친 것은 아무 검사도 안 거치고 통과한다. 구운 맵을 그대로 읽어서 검사한다.
    [Test]
    public void 구운_맵의_바람으로도_모든_구간을_지날_수_있다()
    {
        Scene scene = EditorSceneManager.OpenScene(SkydiveMapPath, OpenSceneMode.Additive);
        try
        {
            var winds = new List<SkydiveCourseBuilder.WindSpec>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (WindVolume marker in root.GetComponentsInChildren<WindVolume>(true))
                {
                    winds.Add(new SkydiveCourseBuilder.WindSpec(
                        marker.name, marker.transform.position, marker.Radius, marker.Height, marker.Wind));
                }
            }

            string failure = SkydiveCourseBuilder.FindImpassableSection(winds);

            Assert.IsNull(failure, failure);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, removeScene: true);
        }
    }

    // 위 두 테스트는 통과하는 배치만 본다 — 진짜로 막는 배치를 던져도 걸리는지는 아무것도
    // 검증하지 않았다. 구간 전체를 덮는 강한 역풍 하나로 확인한다.
    [Test]
    public void 구간_전체를_덮는_강한_역풍은_걸린다()
    {
        var winds = new[]
        {
            new SkydiveCourseBuilder.WindSpec(
                "Test_Block", new Vector3(0f, 2400f, 0f), 150f, 400f, new Vector3(-40f, 0f, 0f)),
        };

        string failure = SkydiveCourseBuilder.FindImpassableSection(winds);

        Assert.IsNotNull(failure);
        StringAssert.Contains("2600", failure);
        StringAssert.Contains("2200", failure);
    }
}
