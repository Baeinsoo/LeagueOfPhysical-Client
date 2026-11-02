using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KnockbackPadViewModelTests
    {
        [Test]
        public void 키보드_대각선은_길이_1()
        {
            var v = LOP.UI.KnockbackPadViewModel.Combine(true, false, false, true);
            Assert.AreEqual(1f, v.magnitude, 1e-4f);
            Assert.AreEqual(0.707f, v.x, 1e-3f);
            Assert.AreEqual(0.707f, v.y, 1e-3f);
        }

        [Test]
        public void 아무것도_안_누르면_0()
        {
            Assert.AreEqual(Vector2.zero, LOP.UI.KnockbackPadViewModel.Combine(false, false, false, false));
        }

        static readonly System.Collections.Generic.List<Vector2> None = new System.Collections.Generic.List<Vector2>();

        [Test]
        public void 자동_조준은_가장_가까운_적()
        {
            var enemies = new System.Collections.Generic.List<Vector2> { new Vector2(0f, 8f), new Vector2(3f, 0f) };
            float? yaw = LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, enemies, 15f);
            Assert.IsTrue(yaw.HasValue);
            Assert.AreEqual(90f, yaw.Value, 1e-3f);   // +x가 90도 — 가까운 쪽(3, 0)
        }

        //  팀전은 몸싸움으로 아군이 붙어 다녀 가장 가까운 몸이 아군인 일이 흔하다 — 서버는 아군을 통과시킨다.
        [Test]
        public void 자동_조준은_가까운_아군보다_먼_적을_고른다()
        {
            var bodies = new System.Collections.Generic.List<Vector2> { new Vector2(3f, 0f), new Vector2(0f, 8f) };
            var teams = new System.Collections.Generic.List<int> { 1, 2 };
            float? yaw = LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, bodies, teams, 1, 15f);
            Assert.IsTrue(yaw.HasValue);
            Assert.AreEqual(0f, yaw.Value, 1e-3f);   // 먼 적(0, 8) 쪽
        }

        [Test]
        public void 자동_조준_개인전은_가장_가까운_몸_그대로()
        {
            var bodies = new System.Collections.Generic.List<Vector2> { new Vector2(3f, 0f), new Vector2(0f, 8f) };
            var teams = new System.Collections.Generic.List<int> { 0, 0 };
            Assert.AreEqual(90f, LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, bodies, teams, 0, 15f).Value, 1e-3f);
        }

        [Test]
        public void 자동_조준은_아군만_있으면_쏘지_않는다()
        {
            var bodies = new System.Collections.Generic.List<Vector2> { new Vector2(3f, 0f) };
            var teams = new System.Collections.Generic.List<int> { 2 };
            Assert.IsNull(LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, bodies, teams, 2, 15f));
        }

        [Test]
        public void 자동_조준_0도는_z_앞쪽()
        {
            var enemies = new System.Collections.Generic.List<Vector2> { new Vector2(0f, 5f) };
            Assert.AreEqual(0f, LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, enemies, 15f).Value, 1e-3f);
        }

        [Test]
        public void 사거리_밖이면_없음()
        {
            var enemies = new System.Collections.Generic.List<Vector2> { new Vector2(0f, 20f) };
            Assert.IsNull(LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, enemies, 15f));
            Assert.IsNull(LOP.UI.KnockbackPadViewModel.AutoAimYaw(Vector2.zero, None, 15f));
        }

        [Test]
        public void 짧게_누르면_톡()
        {
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.TapOrDrag.Tap, LOP.UI.KnockbackPadViewModel.Classify(0.1f, 0.1f));
        }

        [Test]
        public void 끌면_조준()
        {
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.TapOrDrag.Drag, LOP.UI.KnockbackPadViewModel.Classify(0.1f, 0.5f));
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.TapOrDrag.Drag, LOP.UI.KnockbackPadViewModel.Classify(1.0f, 0.3f));
        }

        [Test]
        public void 오래_눌러도_안_끌면_아무것도_아님()
        {
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.TapOrDrag.None, LOP.UI.KnockbackPadViewModel.Classify(1.0f, 0.1f));
        }

        [Test]
        public void 위로_끌면_화면_위쪽_월드방향()
        {
            var dir = LOP.UI.KnockbackPadViewModel.AimDirection(new Vector2(0f, 1f), 0f);
            Assert.AreEqual(0f, dir.x, 1e-4f);
            Assert.AreEqual(1f, dir.y, 1e-4f);
        }

        [Test]
        public void 오른쪽으로_끌면_월드_양의_x()
        {
            var dir = LOP.UI.KnockbackPadViewModel.AimDirection(new Vector2(1f, 0f), 0f);
            Assert.AreEqual(1f, dir.x, 1e-4f);
            Assert.AreEqual(0f, dir.y, 1e-4f);
        }

        [Test]
        public void 카메라가_90도_돌면_위로_끌기가_월드_x쪽()
        {
            var dir = LOP.UI.KnockbackPadViewModel.AimDirection(new Vector2(0f, 1f), 90f);
            Assert.AreEqual(1f, dir.x, 1e-4f);
            Assert.AreEqual(0f, dir.y, 1e-4f);
        }

        // 발사가 쓰는 yaw와 조준선이 쓰는 yaw는 같은 함수의 결과여야 한다 — 선이 가리킨 곳과 탄이 간 곳이 갈리면 안 된다.
        [Test]
        public void 발사_yaw와_조준선_yaw는_같은_함수()
        {
            var drag = new Vector2(0.6f, 0.8f);
            float? yaw = LOP.UI.KnockbackPadViewModel.AimYaw(drag, 30f);
            var dir = LOP.UI.KnockbackPadViewModel.AimDirection(drag, 30f);
            Assert.IsTrue(yaw.HasValue);
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.YawOf(dir), yaw.Value, 1e-4f);
            Assert.IsFalse(LOP.UI.KnockbackPadViewModel.AimYaw(Vector2.zero, 30f).HasValue);
        }

        // 표시 상태와 놓을 때의 결과가 모든 경계에서 일치해야 한다.
        [TestCase(0.2f, 0.1f, 0.1f, 1, 3)]
        [TestCase(0.3f, 0.1f, 0.1f, 0, 0)]
        [TestCase(0.2f, 0.35f, 0.1f, 3, 1)]
        [TestCase(0.3f, 0.35f, 0.1f, 3, 1)]
        [TestCase(0.2f, 0.35f, 0.35f, 2, 2)]
        [TestCase(0.3f, 0.35f, 0.35f, 2, 2)]
        [TestCase(0.2f, 0.29f, 0.29f, 1, 3)]
        [TestCase(0.2f, 0.3f, 0.3f, 2, 2)]
        [TestCase(0.2f, 0.9f, 0.29f, 3, 1)]
        public void 조준표시는_놓을_때의_결과와_일치(float held, float max, float cur, int release, int aim)
        {
            Assert.AreEqual(release, (int)LOP.UI.KnockbackPadViewModel.OutcomeOnRelease(held, max, cur));
            Assert.AreEqual(aim, (int)LOP.UI.KnockbackPadViewModel.AimIndicatorState(held, max, cur, true));
        }

        [Test]
        public void 조준표시_대상이_없으면_고리_없음()
        {
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.AimKind.None, LOP.UI.KnockbackPadViewModel.AimIndicatorState(0.1f, 0f, 0f, false));
            // 양성 대조: 같은 입력에 대상이 있으면 고리.
            Assert.AreEqual(LOP.UI.KnockbackPadViewModel.AimKind.AutoTarget, LOP.UI.KnockbackPadViewModel.AimIndicatorState(0.1f, 0f, 0f, true));
        }

        // 발사 경로와 조준선 경로가 같은 방향 함수(AimYaw)를 거치는지 소스로 지킨다 — 한쪽이 자기 식을 쓰기 시작하면 빨강.
        [Test]
        public void 발사와_조준선은_둘_다_AimYaw를_거친다()
        {
            var path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts/UI/KnockbackPad/KnockbackPadViewModel.cs");
            var src = System.IO.File.ReadAllText(path);
            foreach (var method in new[] { "public void FireTowardScreenDirection(", "public void UpdateAim(" })
            {
                int start = src.IndexOf(method, System.StringComparison.Ordinal);
                Assert.GreaterOrEqual(start, 0, method);
                //  메서드 본문 = 다음 public 선언 앞까지(줄끝이 CRLF여도 같다).
                int end = src.IndexOf("public ", start + method.Length, System.StringComparison.Ordinal);
                if (end < 0) end = src.Length;
                StringAssert.Contains("AimYaw(", src.Substring(start, end - start), method);
            }
        }

        [Test]
        public void 자동_조준_대상_위치는_가장_가까운_적_오프셋()
        {
            var bodies = new System.Collections.Generic.List<Vector2> { new Vector2(0f, 8f), new Vector2(3f, 0f) };
            var offset = LOP.UI.KnockbackPadViewModel.AutoAimOffset(Vector2.zero, bodies, null, 0, 15f);
            Assert.IsTrue(offset.HasValue);
            Assert.AreEqual(new Vector2(3f, 0f), offset.Value);
        }

        // 진동은 "취소 구간에 막 들어간 순간"에만. 들어가 있는 동안 계속 울리면 안 된다.
        [TestCase(2, 3, true)]   // Fire -> Cancel
        [TestCase(3, 3, false)]  // Cancel -> Cancel
        [TestCase(3, 2, false)]  // Cancel -> Fire (나올 때는 안 울림)
        [TestCase(1, 3, false)]  // Auto -> Cancel
        [TestCase(0, 3, false)]  // Nothing -> Cancel
        [TestCase(2, 2, false)]  // Fire -> Fire
        public void 취소_구간에_들어갈_때만_진동한다(int prev, int now, bool expected)
        {
            Assert.AreEqual(expected, LOP.UI.KnockbackPadViewModel.ShouldPulseOnCancelEntry(
                (LOP.UI.KnockbackPadViewModel.ReleaseOutcome)prev, (LOP.UI.KnockbackPadViewModel.ReleaseOutcome)now));
        }

        // 마우스는 PC 클릭 발사 경로가 따로 처리하므로 발사 영역은 터치·펜만 받는다 — 에디터에선 마우스도 손가락처럼.
        [TestCase("mouse", false, false)]
        [TestCase("mouse", true, true)]
        [TestCase("touch", false, true)]
        [TestCase("pen", false, true)]
        public void 발사_영역은_빌드_PC에선_마우스를_받지_않는다(string pointerType, bool mouseActsAsTouch, bool expected)
        {
            Assert.AreEqual(expected, LOP.UI.KnockbackPadViewModel.AcceptsFirePointer(pointerType, mouseActsAsTouch));
        }
    }
}
