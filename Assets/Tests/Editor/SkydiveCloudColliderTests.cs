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
