using System.Collections.Generic;
using LOP.EditorTools;
using LOP.MapTools;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 광산 굽기가 구간마다 표식(<see cref="FlappyCourseSection"/>)을 굽는다(spec §5). 굽기 전체는 씬·대화상자를 건드려
    /// 여기서 돌리지 않고, 굽기가 부르는 그 한 단계를 본다(<see cref="FlappyClassicCourseBuilderTests"/>와 같은 요령).
    /// </summary>
    public class FlappyMineSectionBakeTests
    {
        //  MasterData FlappyConfig 원조 물리(MineCourseRuleTests와 같은 값).
        private static readonly MinePhysics P = new MinePhysics(4.5f, 10.125f, 33.75f, 11.25f, 0.02f);

        private GameObject composed;

        [TearDown]
        public void TearDown()
        {
            if (composed != null) { UnityEngine.Object.DestroyImmediate(composed); }
        }

        private static List<MineSection> Three() => new List<MineSection>
        {
            new MineSection("낙석", 93.05f, 1),
            new MineSection("진자 광차", 176.95f, 2),
            new MineSection("고수 갈림길 ⬆굴", 429.1f, 3),
        };

        [Test]
        public void 구간마다_표식_하나를_ComposedMap_자식으로_구간_시작_x에()
        {
            composed = new GameObject("ComposedMap");

            int baked = FlappyMineCourseBuilder.BakeSections(composed.transform, Three());

            var markers = composed.GetComponentsInChildren<FlappyCourseSection>();
            Assert.AreEqual(3, baked);
            Assert.AreEqual(3, markers.Length);
            Assert.AreEqual(176.95f, markers[1].X, 1e-3f);
            Assert.AreEqual(2, markers[1].Act);
            Assert.AreEqual("진자 광차", markers[1].Title);
            //  자식이라야 다음 굽기(광산·전통 둘 다 ComposedMap 자식을 전부 지운다)가 치운다 — 전통 맵에 광산 배너가 남지 않는다.
            Assert.AreSame(composed.transform, markers[1].transform.parent);
            StringAssert.StartsWith(FlappyMineCourseBuilder.SectionMarkerPrefix, markers[1].gameObject.name);
        }

        [Test]
        public void 마지막_구간만_Final()
        {
            composed = new GameObject("ComposedMap");

            FlappyMineCourseBuilder.BakeSections(composed.transform, Three());

            var markers = composed.GetComponentsInChildren<FlappyCourseSection>();
            Assert.IsFalse(markers[0].Final);
            Assert.IsFalse(markers[1].Final);
            Assert.IsTrue(markers[2].Final, "LAST STRETCH!는 마지막 표식에서만");
            Assert.AreEqual("고수 갈림길", markers[2].Title);
        }

        [Test]
        public void 갈림길은_꼬리표를_떼고_모두_고수_갈림길()
        {
            //  사용자 결정(10-25): 위 굴·아래 굴을 가르지 않는다. ⬆·⬇는 화면 글꼴에도 없다.
            Assert.AreEqual("고수 갈림길", FlappyMineCourseBuilder.BannerTitle("고수 갈림길 ⬇굴"));
            Assert.AreEqual("고수 갈림길", FlappyMineCourseBuilder.BannerTitle("고수 갈림길 ⬆굴"));
            Assert.AreEqual("낙석", FlappyMineCourseBuilder.BannerTitle("낙석"));
        }

        [Test]
        public void 광산_구간_이름은_모두_화면_글꼴에_있는_글자다()
        {
            //  블랙한산스·주아에 ⬆(U+2B06)·⬇(U+2B07)·−(U+2212)·가운뎃점(U+00B7)이 없고 대체 글꼴도 비어 있다 — 그대로 구우면 배너에 빈 네모가 뜬다.
            //  그래서 한글 음절·공백·ASCII만 허락한다.
            foreach (MineSection s in MineCourseRule.Layout(P).Sections)
            {
                foreach (char ch in FlappyMineCourseBuilder.BannerTitle(s.Name))
                {
                    bool ok = ch == ' ' || (ch >= '가' && ch <= '힣') || (ch >= '!' && ch <= '~');
                    Assert.IsTrue(ok, $"{s.Name}의 '{ch}'(U+{(int)ch:X4})는 화면 글꼴에 없다");
                }
            }
        }

        [Test]
        public void 전통_굽기_뒤엔_구간_표식이_0개()
        {
            //  Pre-flight 점검 대상: 전통 굽기(FlappyClassicCourseBuilder.Build)는 ComposedMap 자식을 지우기 전에
            //  RescueMarkers로 일부 마커(FinishLine·SpawnPoint)만 구출해 "---Course---" 밑으로 옮긴다.
            //  FlappyCourseSection이 그 구출 대상 목록에 없음을 실제 코드(RescueMarkers)로 확인한다 —
            //  포함돼 있었다면 전통 맵에 광산 구간 배너가 남는다(Review Focus #6).
            composed = new GameObject("ComposedMap");
            FlappyMineCourseBuilder.BakeSections(composed.transform, Three());
            Assert.AreEqual(3, composed.GetComponentsInChildren<FlappyCourseSection>().Length);

            //  전통 굽기가 실제로 부르는 순서 그대로: 먼저 구출, 그 뒤 자식을 전부 지운다.
            FlappyClassicCourseBuilder.RescueMarkers(composed.transform);
            for (int i = composed.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(composed.transform.GetChild(i).gameObject);
            }

            Assert.AreEqual(0, composed.GetComponentsInChildren<FlappyCourseSection>().Length,
                "구간 표식이 전통 굽기에서 구출돼 살아남았다 — 전통 맵에 광산 배너가 남는다");
        }
    }
}
