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
        public void 통로선이_없으면_아무것도_안_한다()
        {
            composed = new GameObject("ComposedMap");

            Assert.DoesNotThrow(() => FlappyClassicCourseBuilder.RemoveCorridorLine(composed));
            Assert.IsNull(composed.GetComponent<FlappyCorridorLine>());
        }
    }
}
