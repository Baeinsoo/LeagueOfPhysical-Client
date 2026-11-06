using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 머리 위 카드의 기준점(최종 리뷰 m5) — 몸개그 자세(꽈당 구르기·으악 납작)를 벗긴 정수리다.
    /// 자세를 받은 정수리를 따라가면 구르는 동안 카드가 몸 둘레를 돌고 거꾸로 선 순간 바닥까지 내려간다.
    /// 자세를 준 뷰(FxView)와 카드 뷰 중 누가 먼저 돌아도 같은 자리여야 한다(등록 순서에 기대지 않는다).
    /// </summary>
    public class CardRushHeadCardAnchorTests
    {
        private GameObject visual;
        private Transform model;
        private Transform head;

        [SetUp]
        public void SetUp()
        {
            visual = new GameObject("visual");
            model = new GameObject(ChibiRig.ModelChildName).transform;
            model.SetParent(visual.transform, false);
            model.localPosition = new Vector3(0f, 0.1f, 0f);
            model.localRotation = Quaternion.Euler(0f, 180f, 0f);
            head = new GameObject("Head").transform;
            head.SetParent(model, false);
            head.localPosition = new Vector3(0f, 1.0f, 0.05f);
            var hat = new GameObject(ChibiHeadSpots.HatAnchorName).transform;
            hat.SetParent(head, false);
            hat.localPosition = new Vector3(0f, 0.3f, 0f);
            visual.transform.SetPositionAndRotation(new Vector3(5f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f));
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(visual);

        private static readonly Vector3 Forward = Vector3.right;   // 몸이 +x(레인 방향)를 본다

        private Vector3 Center() => CardRushHeadCardView.CardCenter(CardRushHeadCardView.Anchor(visual.transform, model, head), CardRushHeadCardView.MineHeight);

        private void Pose(CardRushGagKind kind, float t01)
            => CardRushRunnerFxView.ApplyPose(visual.transform, model, CardRushRunnerFxView.ModelRest.Of(model), CardRushGag.PoseAt(kind, t01), Forward);

        [TestCase(CardRushGagKind.Tumble, 0.4f)]
        [TestCase(CardRushGagKind.Tumble, 0.55f)]
        [TestCase(CardRushGagKind.Squash, 0.3f)]
        public void 몸개그_중에도_카드는_쉴_때_자리에_떠_있다(CardRushGagKind kind, float t01)
        {
            //  자세 주는 뷰가 먼저 돈다(지금 등록 순서) — 쉴 때 자세를 먼저 잡고 자세를 준다.
            CardRushRunnerFxView.ModelRest.Of(model);
            var rest = Center();
            var restCrown = ChibiHeadSpots.Crown(model, head);

            Pose(kind, t01);
            Assert.Greater((ChibiHeadSpots.Crown(model, head) - restCrown).magnitude, 0.3f, "자세가 정수리를 크게 옮겨야 시험이 뜻이 있다");

            var now = Center();
            Assert.AreEqual(rest.y, now.y, 0.1f, "높이");
            Assert.Less(Vector3.ProjectOnPlane(now - rest, Vector3.up).magnitude, 0.1f, "옆·앞뒤");
        }

        [Test]
        public void 카드_뷰가_먼저_돌아도_같은_자리()
        {
            //  등록 순서가 바뀌어 카드 뷰가 자세보다 먼저 정수리를 본 경우.
            var first = Center();
            Pose(CardRushGagKind.Tumble, 0.4f);
            var during = Center();
            Pose(CardRushGagKind.Tumble, 0.7f);
            var later = Center();
            Assert.Less((during - first).magnitude, 1e-3f);
            Assert.Less((later - first).magnitude, 1e-3f);
        }

        [Test]
        public void 쉴_때는_자세를_받은_정수리와_같다()
        {
            Assert.Less((CardRushHeadCardView.Anchor(visual.transform, model, head) - ChibiHeadSpots.Crown(model, head)).magnitude, 1e-4f);
        }
    }
}
