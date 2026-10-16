using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    using Pad = LOP.UI.ArcheryPadViewModel;

    //  왼손 겨누기의 감도는 지금 화각을 따른다 — 넓게 볼 땐 빠르게(판 건너편 표적으로 휙),
    //  당겨서 좁아지면 곱게. 기준은 "화면에서 끈 만큼 그림이 따라온다"(화면 높이만큼 끌면 세로 화각만큼).
    public class ArcheryPadAimSensitivityTests
    {
        const float Aspect = 16f / 9f;
        const float Wide = 60f;
        const float Zoomed = 22f;

        [Test]
        public void 화면_높이만큼_끌면_세로_화각만큼_돈다()
        {
            Vector2 degrees = Pad.DragToDegrees(new Vector2(0f, 1f), Wide, Aspect);
            Assert.AreEqual(Wide, degrees.y, 1e-3f);
        }

        [Test]
        public void 당기면_같은_손짓이_덜_돈다()
        {
            Vector2 drag = new Vector2(0.1f, 0.1f);
            Vector2 wide = Pad.DragToDegrees(drag, Wide, Aspect);
            Vector2 zoomed = Pad.DragToDegrees(drag, Zoomed, Aspect);

            Assert.Less(Mathf.Abs(zoomed.x), Mathf.Abs(wide.x));
            Assert.Less(Mathf.Abs(zoomed.y), Mathf.Abs(wide.y));
            Assert.AreEqual(Zoomed / Wide, zoomed.y / wide.y, 1e-3f, "세로 감도가 화각에 비례하지 않는다");
        }

        //  가로는 가로 화각을 쓴다 — 세로 화각을 그대로 쓰면 가로 화면에서 좌우가 그림보다 느리게 돈다.
        [Test]
        public void 가로는_가로_화각을_쓴다()
        {
            Vector2 degrees = Pad.DragToDegrees(new Vector2(1f, 0f), Wide, Aspect);
            float horizontal = 2f * Mathf.Atan(Mathf.Tan(Wide * 0.5f * Mathf.Deg2Rad) * Aspect) * Mathf.Rad2Deg;
            Assert.AreEqual(horizontal, degrees.x, 1e-3f);
            Assert.Greater(degrees.x, Wide);
        }

        [Test]
        public void 방향을_그대로_따른다()
        {
            Vector2 degrees = Pad.DragToDegrees(new Vector2(-0.2f, 0.1f), Wide, Aspect);
            Assert.Less(degrees.x, 0f, "왼쪽으로 끌었는데 오른쪽으로 돈다");
            Assert.Greater(degrees.y, 0f, "위로 끌었는데 아래로 돈다");
        }

        //  넓게 볼 때 왼쪽 절반을 한 번 쓸면 투석기 판의 끝(±16m @ 30m ≈ 28°)까지 닿아야 한다 —
        //  이 조작을 만든 이유다.
        [Test]
        public void 넓게_볼_때_왼쪽_절반_한_번에_판_끝까지_닿는다()
        {
            Vector2 degrees = Pad.DragToDegrees(new Vector2(0.5f, 0f), Wide, Aspect);
            float edge = Mathf.Atan2(16f, 30f) * Mathf.Rad2Deg;
            Assert.Greater(degrees.x, edge);
        }
    }
}
