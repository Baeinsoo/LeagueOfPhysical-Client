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
        GameObject volume = SkydiveCourseBuilder.CreateWindVolume(
            null, "wind-test", new Vector3(0f, 1000f, 0f), 25f, 120f, new Vector3(0f, 14f, 0f), null);

        try
        {
            Assert.That(volume.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(volume);
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
}
