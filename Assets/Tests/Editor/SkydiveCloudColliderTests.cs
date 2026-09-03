using LOP.EditorTools;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SkydiveCloudColliderTests
{
    private const string SkydiveMapPath = "Assets/Art/Scenes/SkydiveMap.unity";

    // Finding 4 — 위 테스트는 팩토리 메서드만 본다. 실제 사고("플레이어가 구름 위에 착지")는
    // 구운 씬 파일에서 일어나므로, 그 파일 자체를 훑어야 낡은 베이크나 손으로 붙인 콜라이더를
    // 잡을 수 있다.
    [Test]
    public void 구운_맵의_구름에는_콜라이더가_없다()
    {
        // additive로 열어야 현재 열려 있는 씬(사용자 작업물일 수 있다)을 건드리지 않는다.
        // 그 위에 unsaved 변경이 있는 채로 Single로 열면 저장 여부를 묻는 모달이 떠 에디터가
        // 멈춘다.
        Scene scene = EditorSceneManager.OpenScene(SkydiveMapPath, OpenSceneMode.Additive);
        try
        {
            int checkedCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Cloud_") == false)
                    {
                        continue;
                    }
                    checkedCount++;
                    Assert.That(t.GetComponents<Collider>(), Is.Empty,
                                $"{t.name}에 콜라이더가 남아 있다 — 캐릭터가 구름 위에 착지한다");
                }
            }

            Assert.That(checkedCount, Is.GreaterThan(0), "Cloud_로 시작하는 오브젝트를 하나도 못 찾았다 — 검사가 헛돈 것 아닌지 확인");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, removeScene: true);   // 저장하지 않고 닫는다
        }
    }

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
            // Quad 프리미티브는 로컬 Z축이 판의 법선이다(Y축은 판 평면 안에 있다) — 판이
            // 수평이면 법선(forward)이 수직이 된다. up이 아니라 forward를 봐야 한다.
            Vector3 forward = quad.transform.forward;
            Assert.That(Mathf.Abs(forward.y), Is.GreaterThan(0.99f), "판이 수평이어야 층으로 보인다");
        }
        finally
        {
            Object.DestroyImmediate(quad);
        }
    }
}
