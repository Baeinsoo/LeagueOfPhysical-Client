using System.Collections.Generic;
using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 광산 굽기는 통로 중심선(<see cref="FlappyCorridorLine"/>)을 ComposedMap <b>자체</b>에 붙인다 — 자식만 지우는
    /// 전통 굽기에서 살아남는다. 남으면 전통 코스 카메라가 광산 높이를 따라가므로(10-08 최종 리뷰 I2) 전통 굽기가 뗀다.
    /// 굽기 전체는 씬·대화상자를 건드려 여기서 돌리지 않고, 굽기가 부르는 그 한 단계를 본다.
    /// </summary>
    public class FlappyClassicCourseBuilderTests
    {
        private GameObject composed;

        [TearDown]
        public void TearDown()
        {
            if (composed != null) { Object.DestroyImmediate(composed); }
        }

        [Test]
        public void 스폰_자리는_찾은_순서가_아니라_Order_순서로_위에서부터()
        {
            //  10-12: FindObjectsByType(None)이 굽기마다 다른 순서로 돌려줘 Order 1~4의 높이가 굽기마다 섞였다(씬: 위에서 2·4·3·1).
            composed = new GameObject("SpawnHolder");
            var spawns = new List<LOP.SpawnPoint>();
            foreach (int order in new[] { 2, 4, 1, 3 })
            {
                var go = new GameObject($"PlayerSpawn_{order}");
                go.transform.SetParent(composed.transform);
                var sp = go.AddComponent<LOP.SpawnPoint>();
                sp.Order = order;
                spawns.Add(sp);
            }

            FlappyClassicCourseBuilder.PlaceSpawns(spawns, -7f, 7f, 3.75f, 0f);

            float[] ys = spawns.OrderBy(sp => sp.Order).Select(sp => sp.transform.position.y).ToArray();
            for (int i = 1; i < ys.Length; i++)
            {
                Assert.Greater(ys[i - 1], ys[i], $"Order {i}이 Order {i + 1}보다 위가 아니다: {string.Join(", ", ys)}");
            }
        }

        [Test]
        public void 광산이_남긴_통로선을_떼고_되돌리기로_돌아온다()
        {
            composed = new GameObject("ComposedMap");
            composed.AddComponent<FlappyCorridorLine>().Points = new[] { new Vector2(0f, 1f), new Vector2(10f, 2f) };

            Undo.IncrementCurrentGroup();
            FlappyClassicCourseBuilder.RemoveCorridorLine(composed);

            Assert.IsNull(composed.GetComponent<FlappyCorridorLine>(), "전통 굽기 뒤에도 광산 통로선이 남았다");

            Undo.PerformUndo();
            Assert.IsNotNull(composed.GetComponent<FlappyCorridorLine>(), "Undo로 떼지 않아 되돌릴 수 없다");
        }

        [Test]
        public void 결승선_마커는_렌더러를_켠_채_재질만_비워_숨기고_형상은_그대로()
        {
            //  10-12 fix1: 광산 굽기가 마커 기둥을 숨긴다. FinishLine.Shape는 renderer.bounds로 골인을 판정하므로(서버 헤드리스) 렌더러를 끄지 않는다.
            composed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            composed.transform.position = new Vector3(386.15f, -49f, 0f);
            composed.transform.localScale = new Vector3(1.2f, 18f, 2.5f);
            var r = composed.GetComponent<Renderer>();
            Material shown = r.sharedMaterial;
            Assert.IsNotNull(shown);
            Bounds before = r.bounds;
            r.enabled = false;   // 앞선 fix1 굽기가 꺼 둔 씬

            Assert.IsTrue(FlappyClassicCourseBuilder.SetMarkerDrawn(r, false, shown, "test"));
            Assert.IsTrue(r.enabled, "렌더러를 꺼 두면 서버가 결승선 형상을 못 믿는다");
            Assert.AreEqual(0, r.sharedMaterials.Length, "그림이 남았다");
            Assert.AreEqual(before.center, r.bounds.center);
            Assert.AreEqual(before.size, r.bounds.size);
            Assert.IsFalse(FlappyClassicCourseBuilder.SetMarkerDrawn(r, false, shown, "test"), "이미 숨겼는데 또 바꿨다");

            Assert.IsTrue(FlappyClassicCourseBuilder.SetMarkerDrawn(r, true, shown, "test"));
            Assert.IsTrue(r.enabled);
            CollectionAssert.AreEqual(new[] { shown }, r.sharedMaterials, "전통 굽기가 기둥 재질을 되살리지 않았다");
            Assert.AreEqual(before.center, r.bounds.center);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(FlappyClassicCourseBuilder.FinishMarkerMaterialPath), "기둥 재질 경로가 틀렸다");
        }

        [Test]
        public void 통로선이_없으면_아무것도_안_한다()
        {
            composed = new GameObject("ComposedMap");

            Assert.DoesNotThrow(() => FlappyClassicCourseBuilder.RemoveCorridorLine(composed));
            Assert.IsNull(composed.GetComponent<FlappyCorridorLine>());
        }
    }
}
