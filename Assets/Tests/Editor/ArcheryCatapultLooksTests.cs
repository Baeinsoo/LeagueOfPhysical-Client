using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryCatapultLooksTests
    {
        static readonly (string code, float radius)[] Festival =
        {
            ("WaterBalloon", 1.0f), ("Watermelon", 0.75f), ("Plate", 0.85f), ("Rocket", 0.45f), ("GoldenBell", 0.8f),
            ("Pinata", 0.95f), ("BirdNest", 0.75f), ("CheerFlag", 0.8f), ("KidBalloon", 0.8f),
        };

        [Test]
        public void 축제_표적_아홉은_각자_회색_도형이_있고_옛_여섯은_없다()
        {
            foreach (var (code, _) in Festival) { Assert.IsTrue(ArcheryCatapultLooks.IsFestival(code), code); }
            CollectionAssert.AreEqual(Festival.Select(f => f.code).ToArray(), ArcheryCatapultLooks.Codes);
            foreach (var old in new[] { "Shield", "Helmet", "Apple", "Grail", "KingFlag", "FlowerBasket", "RangeFace" })
            {
                Assert.IsFalse(ArcheryCatapultLooks.IsFestival(old), old);
            }
        }

        [Test]
        public void 흔들림은_종과_피냐타만_그림으로_한다()
        {
            Assert.AreEqual(0f, ArcheryCatapultLooks.SwayDegrees("WaterBalloon", 0.7f, 0.3f));
            Assert.AreEqual(0f, ArcheryCatapultLooks.SwayDegrees("Plate", 0.7f, 0.3f));
            float bell = 0f, pinata = 0f;
            for (int i = 0; i < 200; i++)
            {
                bell = Mathf.Max(bell, Mathf.Abs(ArcheryCatapultLooks.SwayDegrees("GoldenBell", i * 0.02f, 0.1f)));
                pinata = Mathf.Max(pinata, Mathf.Abs(ArcheryCatapultLooks.SwayDegrees("Pinata", i * 0.02f, 0.1f)));
            }
            Assert.AreEqual(20f, bell, 0.5f);
            Assert.AreEqual(14f, pinata, 0.5f);
        }

        private static ArcheryTarget Catapult(float spinHz, float phase)
            => new ArcheryTarget(
                waveIndex: 0, slotIndex: 0, origin: new Vector3(0f, 3f, 40f), riseSpeed: 0f, spawnTick: 100L,
                radius: 0.85f, points: 2, isTrap: false, shape: ArcheryTargetShape.Sphere, bands: null,
                facing: new Vector3(0.6f, 0f, -0.8f), lifetimeSeconds: 2.6f, ownerUserId: string.Empty,
                lateralSpan: 0f, lateralPeriod: 0f, spinHz: spinHz, motionPhase: phase);

        [Test]
        public void 접시_그림이_보이는_납작함은_그_시각의_판정_반지름과_같다()
        {
            //  화면이 다른 각으로 돌리면 "누워 보이는데 맞는다/정면인데 빗나간다"가 된다.
            var t = Catapult(spinHz: 1.1f, phase: 0.2f);
            for (double tick = 100; tick < 230; tick += 3.7)
            {
                Vector3 front = ArcheryCatapultLooks.RotationAt(t, "Plate", tick, 0.02f) * Vector3.forward;
                float seen = Mathf.Max(ArcheryTargetMotion.MinSpinRadiusRatio, Mathf.Abs(Vector3.Dot(front, t.Facing.normalized)));
                Assert.AreEqual(ArcheryTargetMotion.RadiusAt(t, tick, 0.02f) / t.Radius, seen, 1e-4f, $"tick {tick}");
            }
        }

        [Test]
        public void 종의_흔들림은_앞면을_돌리지_않고_굴리기만_한다()
        {
            //  흔들림은 그림만이다 — 앞면이 사수를 벗어나면 안 되고, 위쪽만 SwayDegrees만큼 기운다.
            var t = Catapult(spinHz: 0f, phase: 0.1f);
            for (double tick = 100; tick < 200; tick += 7)
            {
                Quaternion q = ArcheryCatapultLooks.RotationAt(t, "GoldenBell", tick, 0.02f);
                Assert.AreEqual(1f, Vector3.Dot(q * Vector3.forward, t.Facing.normalized), 1e-4f, $"tick {tick}");
                float sway = ArcheryCatapultLooks.SwayDegrees("GoldenBell", (float)((tick - t.SpawnTick) * 0.02f), t.MotionPhase);
                Assert.AreEqual(Mathf.Abs(sway), Vector3.Angle(q * Vector3.up, Vector3.up), 1e-2f, $"tick {tick}");
            }
        }

        [Test]
        public void 보이는_크기가_맞는_크기를_넘지_않는다()
        {
            //  활쏘기 원칙: 보이는 크기가 곧 맞는 크기 — 그림이 판정 구 밖으로 5% 넘게 나가면 "맞았는데 안 맞음"이 된다.
            foreach (var (code, radius) in Festival)
            {
                var go = ArcheryCatapultLooks.BuildPlaceholder(code, radius);
                try
                {
                    var bounds = new Bounds(go.transform.position, Vector3.zero);
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) { bounds.Encapsulate(r.bounds); }
                    Assert.LessOrEqual(bounds.extents.magnitude, radius * 1.05f * 1.733f, code);   // 상자 대각 ≤ 구를 감싼 상자
                    Assert.LessOrEqual(Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z), radius * 1.05f, code);
                    Assert.Greater(bounds.extents.magnitude, radius * 0.5f, $"{code}: 너무 작다");
                    Assert.IsEmpty(go.GetComponentsInChildren<Collider>(), $"{code}: 그림일 뿐이다 — 판정은 서버가 한다");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void 쏘면_안_되는_것은_빨간_금지_표시를_단다()
        {
            foreach (var code in new[] { "BirdNest", "CheerFlag", "KidBalloon" })
            {
                var go = ArcheryCatapultLooks.BuildPlaceholder(code, 0.8f);
                try { Assert.IsNotNull(go.transform.Find("forbid"), code); }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void 회색_도형을_지우면_따로_만든_재질도_지운다()
        {
            //  적중·무리마다 회색 도형이 생겼다 지워진다 — 재질을 남기면 판이 길어질수록 쌓인다.
            var go = ArcheryCatapultLooks.BuildPlaceholder("Watermelon", 0.75f);
            var materials = new System.Collections.Generic.List<Material>();
            foreach (var r in go.GetComponentsInChildren<Renderer>()) { materials.Add(r.sharedMaterial); }
            Assert.IsNotEmpty(materials);
            ArcheryCatapultLooks.DestroyVisual(go);
            Assert.IsTrue(go == null, "그림이 지워져야 한다");
            foreach (var m in materials) { Assert.IsTrue(m == null, "재질이 남았다"); }
        }

        [Test]
        public void 키트_그림을_지워도_에셋_재질은_남긴다()
        {
            //  축제 키트 재질은 에셋이다 — 지우면 다음 표적부터 분홍이 된다.
            var shared = LOPToonMaterials.Create(Color.white);
            try
            {
                var go = new GameObject("catapult-Plate");
                go.AddComponent<MeshRenderer>().sharedMaterial = shared;
                ArcheryCatapultLooks.DestroyVisual(go);
                Assert.IsTrue(go == null);
                Assert.IsTrue(shared != null, "남의 재질을 지웠다");
            }
            finally { Object.DestroyImmediate(shared); }
        }
    }
}
