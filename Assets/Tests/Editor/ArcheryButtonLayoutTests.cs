using System.Globalization;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    using Layout = LOP.UI.ArcheryButtonLayout;
    using Which = LOP.UI.ArcheryButtonLayout.Which;
    using Hit = LOP.UI.ArcheryButtonLayout.Hit;
    using Drag = LOP.UI.ArcheryHandleDrag;

    //  활 버튼·시위 풀기 버튼의 자리. 손잡이로 옮기고, 기기마다 PlayerPrefs에 "x,y"로 남긴다.
    //  라우터의 판정과 화면의 그림이 이 한 타입에서 나온다 — 둘이 따로 재면 보이는 자리와 눌리는 자리가 갈라진다.
    public class ArcheryButtonLayoutTests
    {
        static readonly Vector2 Wide = new Vector2(1920f, 1080f);      // 16:9
        static readonly Vector2 Tablet = new Vector2(1440f, 1080f);    // 4:3

        static Rect Full(Vector2 size) => new Rect(0f, 0f, size.x, size.y);

        //  가로 폰의 노치(왼쪽)와 홈 막대(아래), 반대로 쥔 경우(오른쪽 노치)까지.
        static Rect[] SafeAreas(Vector2 size) => new[]
        {
            Full(size),
            new Rect(132f, 0f, size.x - 132f, size.y - 63f),
            new Rect(0f, 0f, size.x - 132f, size.y - 63f),
            new Rect(132f, 0f, size.x - 264f, size.y - 63f),
        };

        //  버튼의 누르는 원과 손잡이의 누르는 원이 모두 안전 영역 안이어야 한다.
        static void AssertInside(Layout layout, Which w, Rect safe, string what)
        {
            const float eps = 1e-2f;
            Vector2 c = layout.Center(w);
            float r = layout.HitRadius(w);
            Assert.GreaterOrEqual(c.x - r, safe.xMin - eps, what + " 왼쪽");
            Assert.LessOrEqual(c.x + r, safe.xMax + eps, what + " 오른쪽");
            Assert.GreaterOrEqual(c.y - r, safe.yMin - eps, what + " 위");
            Assert.LessOrEqual(c.y + r, safe.yMax + eps, what + " 아래");

            Vector2 h = layout.HandleCenter(w);
            float hr = layout.HandleHitRadius;
            Assert.GreaterOrEqual(h.x - hr, safe.xMin - eps, what + " 손잡이 왼쪽");
            Assert.LessOrEqual(h.x + hr, safe.xMax + eps, what + " 손잡이 오른쪽");
            Assert.GreaterOrEqual(h.y - hr, safe.yMin - eps, what + " 손잡이 위");
            Assert.LessOrEqual(h.y + hr, safe.yMax + eps, what + " 손잡이 아래");
        }

        static void AssertApart(Layout layout, string what)
        {
            float d = (layout.Center(Which.Bow) - layout.Center(Which.Cancel)).magnitude;
            Assert.GreaterOrEqual(d + 1e-2f, layout.Footprint(Which.Bow) + layout.Footprint(Which.Cancel), what + " 겹친다");
        }

        //  ── 기본 자리·크기 ───────────────────────────────────────────

        [Test]
        public void 기본_자리는_활_80_62_시위풀기_12_74이고_지름은_세로의_24_18퍼센트다()
        {
            var layout = new Layout(Wide, Full(Wide));

            Assert.AreEqual(1920f * 0.80f, layout.Center(Which.Bow).x, 0.5f);
            Assert.AreEqual(1080f * 0.62f, layout.Center(Which.Bow).y, 0.5f);
            Assert.AreEqual(1920f * 0.12f, layout.Center(Which.Cancel).x, 0.5f);
            Assert.AreEqual(1080f * 0.74f, layout.Center(Which.Cancel).y, 0.5f);
            Assert.AreEqual(1080f * 0.24f, layout.FaceRadius(Which.Bow) * 2f, 0.5f);
            Assert.AreEqual(1080f * 0.18f, layout.FaceRadius(Which.Cancel) * 2f, 0.5f);
        }

        [Test]
        public void 누르는_자리는_보이는_원보다_조금_넉넉하다()
        {
            var layout = new Layout(Wide, Full(Wide));
            foreach (var w in new[] { Which.Bow, Which.Cancel })
            {
                Assert.Greater(layout.HitRadius(w), layout.FaceRadius(w));
                Assert.Less(layout.HitRadius(w), layout.FaceRadius(w) * 1.3f);
            }
        }

        //  손잡이는 세로의 5~6% 원이고 버튼 오른쪽 위 가장자리에 붙어 있다(패널 좌표라 위 = y가 작다).
        [Test]
        public void 손잡이는_버튼_오른쪽_위_가장자리의_작은_원이다()
        {
            var layout = new Layout(Wide, Full(Wide));
            Assert.That(layout.HandleRadius * 2f, Is.InRange(1080f * 0.05f, 1080f * 0.06f));

            foreach (var w in new[] { Which.Bow, Which.Cancel })
            {
                Vector2 off = layout.HandleCenter(w) - layout.Center(w);
                Assert.Greater(off.x, 0f, w + " 손잡이가 오른쪽이 아니다");
                Assert.Less(off.y, 0f, w + " 손잡이가 위가 아니다");
                Assert.AreEqual(layout.FaceRadius(w), off.magnitude, layout.HandleRadius, w + " 손잡이가 가장자리에 없다");
            }
        }

        [Test]
        public void 기본_자리끼리는_16대9와_4대3_모두_안_겹친다()
        {
            foreach (var size in new[] { Wide, Tablet })
            {
                AssertApart(new Layout(size, Full(size)), size.ToString());
            }
        }

        //  ── 판정 우선순위 ───────────────────────────────────────────

        //  손잡이 한가운데는 활 버튼의 누르는 원 안에도 들어간다 — 그래도 손잡이가 이긴다.
        [Test]
        public void 손잡이가_버튼_몸통보다_먼저_잡힌다()
        {
            var layout = new Layout(Wide, Full(Wide));
            Vector2 bowHandle = layout.HandleCenter(Which.Bow);
            Assert.IsTrue(layout.OnBody(Which.Bow, bowHandle), "전제가 틀렸다: 손잡이가 몸통 원 밖이다");

            Assert.AreEqual(Hit.BowHandle, layout.HitTest(bowHandle));
            Assert.AreEqual(Hit.CancelHandle, layout.HitTest(layout.HandleCenter(Which.Cancel)));
            Assert.AreEqual(Hit.Bow, layout.HitTest(layout.Center(Which.Bow)));
            Assert.AreEqual(Hit.Cancel, layout.HitTest(layout.Center(Which.Cancel)));
            Assert.AreEqual(Hit.None, layout.HitTest(new Vector2(960f, 300f)), "화면 한가운데가 버튼으로 잡혔다");
        }

        //  네모 요소의 모서리는 원 밖이다 — 화면처럼 겨눈다.
        [Test]
        public void 버튼_원_밖은_화면이다()
        {
            var layout = new Layout(Wide, Full(Wide));
            Vector2 c = layout.Center(Which.Bow);
            float r = layout.HitRadius(Which.Bow);

            Assert.AreEqual(Hit.None, layout.HitTest(c + new Vector2(-r, r) * 0.9f), "왼쪽 아래 모서리가 버튼으로 잡혔다");
        }

        //  ── 옮기기·가두기 ───────────────────────────────────────────

        [Test]
        public void 옮긴_자리로_간다()
        {
            var layout = new Layout(Wide, Full(Wide));
            var to = new Vector2(1400f, 500f);
            layout.MoveTo(Which.Bow, to);

            Assert.AreEqual(to.x, layout.Center(Which.Bow).x, 1e-3f);
            Assert.AreEqual(to.y, layout.Center(Which.Bow).y, 1e-3f);
            Assert.AreEqual(Hit.Bow, layout.HitTest(to), "판정이 새 자리를 안 따라왔다");
        }

        //  화면 밖으로 끌어도 버튼과 손잡이가 안전 영역 안에 남는다. 16:9·4:3, 노치 양쪽·홈 막대.
        [Test]
        public void 어디로_끌어도_버튼과_손잡이는_안전_영역_안이다()
        {
            var far = new[]
            {
                new Vector2(-5000f, -5000f), new Vector2(9000f, -5000f),
                new Vector2(-5000f, 9000f), new Vector2(9000f, 9000f),
            };
            foreach (var size in new[] { Wide, Tablet })
            {
                foreach (var safe in SafeAreas(size))
                {
                    foreach (var w in new[] { Which.Bow, Which.Cancel })
                    {
                        foreach (var to in far)
                        {
                            var layout = new Layout(size, safe);
                            layout.MoveTo(w, to);
                            string what = $"{size} {safe} {w} → {to}";
                            AssertInside(layout, w, safe, what);
                            AssertApart(layout, what);
                        }
                    }
                }
            }
        }

        //  기본 자리도 노치·홈 막대 밑으로 안 들어간다.
        [Test]
        public void 기본_자리도_안전_영역을_비켜_선다()
        {
            foreach (var size in new[] { Wide, Tablet })
            {
                foreach (var safe in SafeAreas(size))
                {
                    var layout = new Layout(size, safe);
                    AssertInside(layout, Which.Bow, safe, $"{size} {safe} 활");
                    AssertInside(layout, Which.Cancel, safe, $"{size} {safe} 시위 풀기");
                }
            }
        }

        //  다른 버튼 위로 끌면 그 가장자리에서 멈춘다 — 옮기는 쪽만 밀리고, 가만있던 쪽은 그대로다.
        [Test]
        public void 다른_버튼_위로_끌면_옮기는_쪽만_밀려나_안_겹친다()
        {
            foreach (var size in new[] { Wide, Tablet })
            {
                var layout = new Layout(size, Full(size));
                Vector2 bowBefore = layout.Center(Which.Bow);

                layout.MoveTo(Which.Cancel, bowBefore + new Vector2(-10f, 5f));

                AssertApart(layout, size.ToString());
                Assert.AreEqual(bowBefore, layout.Center(Which.Bow), "가만있던 활 버튼이 밀렸다");
            }
        }

        //  구석에 서 있는 버튼 쪽으로 다른 버튼을 끝까지 끌어도 겹치지 않고 화면 안에 선다.
        [Test]
        public void 구석의_버튼_쪽으로_끝까지_끌어도_안_겹친다()
        {
            var layout = new Layout(Wide, Full(Wide));
            layout.MoveTo(Which.Bow, new Vector2(900f, 300f));
            layout.MoveTo(Which.Cancel, new Vector2(9000f, 9000f));

            layout.MoveTo(Which.Bow, new Vector2(9000f, 9000f));

            AssertApart(layout, "구석");
            AssertInside(layout, Which.Bow, Full(Wide), "구석");
            AssertInside(layout, Which.Cancel, Full(Wide), "구석");
        }

        [Test]
        public void 기본값으로_되돌리면_기본_자리로_간다()
        {
            var layout = new Layout(Wide, Full(Wide));
            layout.MoveTo(Which.Bow, new Vector2(1200f, 300f));

            layout.ResetToDefault(Which.Bow);

            Assert.AreEqual(1920f * 0.80f, layout.Center(Which.Bow).x, 0.5f);
            Assert.AreEqual(1080f * 0.62f, layout.Center(Which.Bow).y, 0.5f);
        }

        //  ── 저장 문자열 ─────────────────────────────────────────────

        //  안전 영역 기준 0~1 좌표다. 안전 영역이 바뀌어도(반대로 쥠) 같은 몫의 자리로 간다.
        [Test]
        public void 저장값은_안전_영역_기준_비율이고_되읽으면_같은_자리다()
        {
            var safe = new Rect(132f, 0f, 1920f - 132f, 1080f - 63f);
            var layout = new Layout(Wide, safe);
            layout.MoveTo(Which.Bow, new Vector2(1500f, 400f));
            layout.MoveTo(Which.Cancel, new Vector2(400f, 800f));
            string bow = layout.Serialize(Which.Bow);
            string cancel = layout.Serialize(Which.Cancel);

            var again = new Layout(Wide, safe);
            again.Load(bow, cancel);

            Assert.AreEqual(layout.Center(Which.Bow).x, again.Center(Which.Bow).x, 0.05f);
            Assert.AreEqual(layout.Center(Which.Bow).y, again.Center(Which.Bow).y, 0.05f);
            Assert.AreEqual(layout.Center(Which.Cancel).x, again.Center(Which.Cancel).x, 0.05f);
            Assert.AreEqual(layout.Center(Which.Cancel).y, again.Center(Which.Cancel).y, 0.05f);

            Assert.IsTrue(Layout.TryParse(bow, out Vector2 n));
            Assert.AreEqual((1500f - safe.xMin) / safe.width, n.x, 1e-4f);
            Assert.AreEqual((400f - safe.yMin) / safe.height, n.y, 1e-4f);
        }

        //  소수점이 쉼표인 문화권(독일어)에서 저장해도 "x,y"의 쉼표는 하나뿐이고 다른 문화권에서 그대로 읽힌다.
        [Test]
        public void 문화권이_달라도_같은_문자열이다()
        {
            var saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                string s = Layout.Format(new Vector2(0.8125f, 0.25f));
                Assert.AreEqual("0.8125,0.25", s);
                Assert.IsTrue(Layout.TryParse(s, out Vector2 de));
                Assert.AreEqual(new Vector2(0.8125f, 0.25f), de);

                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
                Assert.IsTrue(Layout.TryParse(s, out Vector2 tr));
                Assert.AreEqual(new Vector2(0.8125f, 0.25f), tr);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void 깨진_저장값은_못_읽는다()
        {
            foreach (var bad in new[] { null, "", "  ", "abc", "0.5", "0.5,", ",0.5", "0.5,0.5,0.5", "0,5,0,6",
                                        "NaN,0.5", "0.5,Infinity", "1e40,0.5", "0.5;0.5" })
            {
                Assert.IsFalse(Layout.TryParse(bad, out _), "읽혔다: " + (bad ?? "null"));
            }
        }

        [Test]
        public void 없거나_깨진_저장값이면_기본_자리다()
        {
            var layout = new Layout(Wide, Full(Wide));
            layout.Load(null, "garbage");

            Assert.AreEqual(1920f * 0.80f, layout.Center(Which.Bow).x, 0.5f);
            Assert.AreEqual(1080f * 0.62f, layout.Center(Which.Bow).y, 0.5f);
            Assert.AreEqual(1920f * 0.12f, layout.Center(Which.Cancel).x, 0.5f);
            Assert.AreEqual(1080f * 0.74f, layout.Center(Which.Cancel).y, 0.5f);
        }

        //  저장된 두 자리가 겹치면(다른 화면 비율에서 저장했다 등) 시위 풀기 버튼을 기본 자리로 돌린다.
        [Test]
        public void 저장된_두_자리가_겹치면_안_겹치게_읽는다()
        {
            var layout = new Layout(Wide, Full(Wide));
            layout.Load("0.5,0.5", "0.51,0.5");

            AssertApart(layout, "겹친 저장값");
            AssertInside(layout, Which.Bow, Full(Wide), "겹친 저장값");
            AssertInside(layout, Which.Cancel, Full(Wide), "겹친 저장값");
        }

        //  화면 밖 좌표가 저장돼 있어도 안전 영역 안으로 가둔다.
        [Test]
        public void 화면_밖_저장값도_안으로_가둔다()
        {
            var safe = new Rect(132f, 0f, 1920f - 264f, 1080f - 63f);
            var layout = new Layout(Wide, safe);
            layout.Load("1.4,-0.3", "-2,3");

            AssertInside(layout, Which.Bow, safe, "활");
            AssertInside(layout, Which.Cancel, safe, "시위 풀기");
        }

        [Test]
        public void 저장_키는_기기_공용_두_개다()
        {
            Assert.AreEqual("lop.archery.layout.bow", Layout.PrefsKey(Which.Bow));
            Assert.AreEqual("lop.archery.layout.cancel", Layout.PrefsKey(Which.Cancel));
        }

        //  ── 손잡이 끌기·길게 누르기 ─────────────────────────────────

        //  손잡이를 잡은 자리와 버튼 한가운데의 차이를 유지한 채 따라온다.
        [Test]
        public void 끌면_버튼이_손가락을_잡은_차이만큼_떨어져_따라온다()
        {
            var drag = new Drag(Which.Bow, fingerAt: new Vector2(1600f, 500f), buttonCenter: new Vector2(1536f, 670f), panelHeight: 1080f);

            Assert.IsTrue(drag.TryMove(new Vector2(1400f, 450f), out Vector2 center));
            Assert.AreEqual(new Vector2(1336f, 620f), center);
            Assert.IsTrue(drag.Changed);
        }

        //  손가락이 살짝 떨린 것은 옮긴 것이 아니다 — 길게 누르기를 깨지 않고 버튼도 안 움직인다.
        [Test]
        public void 살짝_떨린_것은_옮긴_것이_아니다()
        {
            var drag = new Drag(Which.Bow, new Vector2(1600f, 500f), new Vector2(1536f, 670f), 1080f);

            Assert.IsFalse(drag.TryMove(new Vector2(1603f, 498f), out _), "떨림에 버튼이 움직였다");
            Assert.IsFalse(drag.Changed);
        }

        [Test]
        public void 움직이지_않고_1초_누르면_한_번만_기본값으로_되돌린다()
        {
            var drag = new Drag(Which.Cancel, new Vector2(300f, 700f), new Vector2(230f, 800f), 1080f);

            Assert.IsFalse(drag.Tick(0.6f));
            Assert.IsFalse(drag.Tick(0.3f), "1초 전에 되돌렸다");
            Assert.IsTrue(drag.Tick(0.15f), "1초가 지났는데 안 되돌렸다");
            Assert.IsFalse(drag.Tick(1f), "두 번 되돌렸다");
            Assert.IsTrue(drag.Changed);
            //  되돌린 뒤엔 끌어도 안 움직인다 — 손가락 밑에서 버튼이 다시 끌려가면 되돌린 게 무색하다.
            Assert.IsFalse(drag.TryMove(new Vector2(800f, 300f), out _));
        }

        [Test]
        public void 끌기_시작하면_길게_누르기는_없다()
        {
            var drag = new Drag(Which.Bow, new Vector2(1600f, 500f), new Vector2(1536f, 670f), 1080f);
            drag.TryMove(new Vector2(1500f, 500f), out _);

            Assert.IsFalse(drag.Tick(2f), "끌던 중에 기본값으로 돌아갔다");
        }

        //  떨림 범위 안에서 1초 버티면 되돌린다.
        [Test]
        public void 떨림_범위_안이면_길게_누르기가_된다()
        {
            var drag = new Drag(Which.Bow, new Vector2(1600f, 500f), new Vector2(1536f, 670f), 1080f);
            drag.TryMove(new Vector2(1604f, 503f), out _);

            Assert.IsTrue(drag.Tick(1f));
        }
    }
}
