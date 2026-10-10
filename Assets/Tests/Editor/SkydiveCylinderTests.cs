using System.Linq;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;
using Y = LOP.EditorTools.SkydiveCylinderLayout;

namespace LOP.Tests
{
    /// <summary>원통 시제품 — 큰 원통을 쭉 내려가며 움직이는 대형 장애물을 돌파하거나 기다린다(사용자 10-07).</summary>
    public class SkydiveCylinderTests
    {
        [Test]
        public void 굽기_검사를_통과한다()
        {
            Assert.IsNull(SkydiveCylinderBuilder.Verify());
        }

        [Test]
        public void 맑게_시작해_어둡게_조이고_출구에서_금빛으로_터진다()
        {
            //  3막(사용자 10-10): 1막 맑은 낮 → 2막 어두움 → 3막 금빛. 1막과 3막은 빛의 종류가 다르다(파랑 vs 금빛).
            var top = SkydiveMoodCurve.Evaluate(Y.Mood, Y.SpawnY);
            var mid = SkydiveMoodCurve.Evaluate(Y.Mood, 800f);
            var dark = SkydiveMoodCurve.Evaluate(Y.Mood, 400f);
            var exit = SkydiveMoodCurve.Evaluate(Y.Mood, Y.ExitY - 20f);

            Assert.Greater(top.Fog.grayscale, 0.5f, "꼭대기는 맑다");
            Assert.Less(top.Fog.grayscale, 0.8f, "그래도 하얗게 날지 않는다 — 첫 원판 격자가 읽혀야");
            Assert.Greater(top.Fog.b, top.Fog.r, "1막은 파란 빛");
            Assert.Greater(exit.Fog.r, exit.Fog.b, "3막은 금빛");

            Assert.Less(mid.Fog.grayscale, top.Fog.grayscale, "내려갈수록 어두워진다");
            Assert.Less(dark.Fog.grayscale, mid.Fog.grayscale, "출구 직전이 가장 어둡다");
            Assert.Less(dark.Fog.grayscale, 0.2f);
            Assert.Less(dark.SunIntensity, top.SunIntensity * 0.5f, "햇빛도 약해진다");

            Assert.Greater(exit.Fog.grayscale, 0.8f, "출구는 하얗게 열린다");
            Assert.Greater(exit.Bloom, dark.Bloom * 3f, "출구에서 빛이 번진다 — 바로 위 어둠과 대비");
            Assert.Greater(exit.Exposure, dark.Exposure);
        }

        [Test]
        public void 별은_출구_아래_공중에서_돈다()
        {
            //  빠져나오자마자 공짜로 닿거나, 내려앉아 걸어서 닿으면 마지막 고민(낚아챌까 쫓을까)이 없다.
            for (int tick = 0; tick < 7200; tick += 10)
            {
                var p = CatchTargetGeometry.PositionAt(new CatchTarget(Y.StarCenter, Y.StarOrbit, Y.StarDegreesPerTick, 0f, Y.StarBob, Y.StarBobPeriod, Y.StarCatchRadius), tick);
                Assert.Less(p.y, Y.ExitY - 150f, "출구 바로 앞이면 애매하다 — 빠져나와 찾고 쫓을 거리가 있어야(사용자 10-07)");
                Assert.Less(p.y, 60f, "더 낮게(사용자 10-08)");
                Assert.Greater(p.y - Y.StarCatchRadius, 8f, "구름 위에 서서 손이 닿으면 안 된다(공중에서 잡아야)");
                Assert.Less(new Vector2(p.x, p.z).magnitude, Y.Radius, "출구 아래(탑 둘레 안)");
            }
        }

        [Test]
        public void 놓치면_태풍_안_출구_바로_위에서_다시_떨어진다()
        {
            //  사용자 10-10 A — 다시 할 때마다 태풍 바닥을 뚫는 순간을 또 겪는다. 마지막 풍차(330)와 출구(300) 사이.
            Assert.AreEqual(new Vector3(0f, 315f, 0f), Y.RetryPoint);
            Assert.Greater(Y.RetryPoint.y, Y.ExitY, "출구 위(태풍 안)");
            Assert.Less(Y.RetryPoint.y, Y.ObstacleYs().Min(), "마지막 장애물 아래");
            Assert.Greater(Y.RetryBelowY, 0f, "구름(윗면 y 0)에 닿는 틱에 걸려야 한다");
            Assert.IsNull(SkydiveCylinderBuilder.CheckRetryPoint(Y.RetryPoint));
        }

        [TestCase(0f, 270f, 0f, TestName = "출구_아래(옛_자리)는_거부")]
        [TestCase(0f, 302f, 0f, TestName = "출구에_딱_붙으면_거부")]
        [TestCase(0f, 328f, 0f, TestName = "풍차_날개와_겹치면_거부")]
        [TestCase(0f, 324f, 0f, TestName = "풍차_날개_바로_밑이면_거부")]
        [TestCase(55f, 315f, 0f, TestName = "벽에_붙으면_거부")]
        public void 다시_떨어지는_자리_검사는_어긋난_자리를_막는다(float x, float y, float z)
        {
            Assert.IsNotNull(SkydiveCylinderBuilder.CheckRetryPoint(new Vector3(x, y, z)));
        }

        [Test]
        public void 별은_보이는_빛_안이면_잡힌다()
        {
            //  10-08 실측: 빛(반지름 13m) 속 8.4m까지 들어갔는데 판정(6m)이 작아 안 잡혔다. 빛 크기는 판정에서 만든다(빌더).
            //  10-08 사용자: "그냥 터치해도 바로 잡히게" — 판정이 보이는 빛보다 넉넉해야 스치기만 해도 잡힌다.
            Assert.GreaterOrEqual(Y.StarCatchRadius, Y.StarHaloDiameter * 0.5f + 3f);
            //  쫓아가 닿을 수 있게 — 활강(초속 14m)보다 확실히 느리게.
            float metersPerSecond = 2f * Mathf.PI * Y.StarOrbit * Y.StarDegreesPerTick / 360f * 50f;
            Assert.LessOrEqual(metersPerSecond, 8f);
        }

        [Test]
        public void 사람을_태울지는_표에서_정하고_원판_풍차만_태운다()
        {
            //  사용자 10-10: 장애물마다 고른다. 맵은 표에서 굽는 산출물이라 인스펙터에서 바꾸면 다시 구울 때 사라진다 — 표가 진실이다.
            Assert.IsTrue(Y.Discs.All(d => d.Rideable), "원판은 태운다");
            Assert.IsTrue(Y.Windmills.All(m => m.Rideable), "풍차는 태운다");
            Assert.IsTrue(Y.Irises.All(i => i.Rideable == false), "조리개는 관문 — 안 태운다");
            Assert.IsFalse(Y.Door.Rideable, "문은 관문 — 안 태운다");
        }

        [Test]
        public void 문을_만들_때_표의_Rideable_값을_그대로_쓴다()
        {
            //  리뷰 PR3 재리뷰 #1: 표의 값이 지금은 모두 기본값과 같아, 굽기가 값을 안 넘겨도 아무도 모른다 — 기본값과 반대인 값으로 잰다.
            var parent = new GameObject("DoorParent");
            try
            {
                var spec = new SkydiveMapKit.DoorSpec("RideableDoor", Vector3.zero, 4f, 4f, 0f, 40, 10, 5, 0, rideable: true);
                var go = SkydiveMapKit.CreateDoorVolume(parent.transform, spec, null);
                Assert.IsTrue(go.GetComponent<LOP.DoorVolume>().Rideable);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void 장애물은_세_종류가_다_있다()
        {
            Assert.GreaterOrEqual(Y.Discs.Length, 1, "도는 원판");
            Assert.GreaterOrEqual(Y.Irises.Length, 1, "조리개");
            Assert.GreaterOrEqual(Y.Windmills.Length, 1, "풍차 날개");
        }

        [Test]
        public void 세이브는_벽_선반_몇_개뿐이고_자동_체크포인트는_출발_하나다()
        {
            Assert.LessOrEqual(Y.Ledges.Length, 3, "적게 — 전략적으로 쓰게");
            CollectionAssert.AreEquivalent(new[] { Y.SpawnY }, Y.RespawnPoints.Keys.ToArray());
            foreach (var l in Y.Ledges)
            {
                float r = new Vector2(l.PadCenter.x, l.PadCenter.z).magnitude;
                Assert.Greater(r, Y.Radius - l.Depth, $"{l.Label}은 벽에 붙은 좁은 턱이어야");
            }
        }

        [Test]
        public void 장애물_윗면에는_판과_같이_도는_격자가_있다()
        {
            //  단색 큰 판은 가까워져도 거리가 안 읽힌다(사용자 10-07) — 칸이 커지는 게 보여야 한다.
            //  도는 판이라 월드 좌표 격자면 판만 돌고 격자는 멈춰 있다 → 메시 UV(미터) 기준.
            foreach (var name in new[] { "HazardDisc", "HazardIris", "HazardMill" })
            {
                var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Materials/Pyramid/Pyramid{name}.mat");
                Assert.IsNotNull(m, name);
                Assert.Greater(m.GetFloat("_TopGrid"), 0f, $"{name} 격자");
                Assert.AreEqual(1f, m.GetFloat("_TopGridSpace"), $"{name} 격자는 메시 기준");
            }
        }

        [Test]
        public void 날개_메시의_UV는_미터라_격자가_날개를_따라간다()
        {
            var mesh = SkydiveCylinderBuilder.BarMesh("CylTestBar", new Vector3(10f, 2f, 4f), save: false);
            var uv = mesh.uv;
            var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
            {
                Assert.AreEqual(v[i].x, uv[i].x, 0.001f);
                Assert.AreEqual(v[i].z, uv[i].y, 0.001f);
            }
            Assert.AreEqual(10f, mesh.bounds.size.x, 0.001f);
        }

        [Test]
        public void 부채꼴_메시는_반지름과_두께가_맞다()
        {
            var mesh = SkydiveCylinderBuilder.Sector("CylTestSector", 10f, 20f, 0f, 90f, Vector3.zero, save: false);
            var b = mesh.bounds;
            Assert.AreEqual(Y.Thickness, b.size.y, 0.01f);
            Assert.AreEqual(20f, b.max.x, 0.05f);
            Assert.AreEqual(20f, b.max.z, 0.05f);
            Assert.AreEqual(0f, b.min.x, 0.05f, "90°까지면 x는 0 아래로 안 간다");
            //  면은 위를 본다 — 위에서 내려다본 삼각형이 앞면이어야 보인다
            var tris = mesh.triangles;
            var v = mesh.vertices;
            Vector3 normal = Vector3.Cross(v[tris[1]] - v[tris[0]], v[tris[2]] - v[tris[0]]);
            Assert.Greater(normal.y, 0f);
        }

        [Test]
        public void 벽은_출발점보다_한참_높아_위로_하늘이_동그랗게_뚫린다()
        {
            //  spec §2 — 위를 보면 높은 구름 벽 사이로 하늘이 뚫린 경기장 모양. 지금(+60)은 벽 끝이 바로 보여 탑처럼 읽힌다.
            Assert.GreaterOrEqual(Y.WallAboveSpawn, 300f);
            var (top, _) = SkydiveCylinderBuilder.WallSpan();   // 굽는 쪽이 실제로 쓰는 값 — 상수만 키우고 빌더가 +60에 머물면 여기서 잡힌다
            Assert.GreaterOrEqual(top, Y.SpawnY + 300f);
        }

        [Test]
        public void 구름_벽_재질은_둘레를_따라_흐르고_원통_반지름을_안다()
        {
            var m = SkydiveCylinderBuilder.StormWallMaterial();
            Assert.AreEqual("LOP/StormWall", m.shader.name);
            Assert.AreEqual(Y.Radius, m.GetFloat("_Radius"), 0.001f, "둘레 칸 수를 반지름으로 정한다 — 틀리면 무늬가 늘어나거나 이음매가 생긴다");
            Assert.AreNotEqual(0f, m.GetFloat("_FlowSpeed"), "흘러야 태풍이다");
        }
    }
}
