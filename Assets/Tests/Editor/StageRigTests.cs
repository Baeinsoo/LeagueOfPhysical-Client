using LOP;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>로비 연출 부품 조립과 지휘 — 개수·콜라이더/그림자 없음·세기 0이면 숨김·쇼는 일반→랭크 때만.</summary>
    public class StageRigTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("test-rig");
        [TearDown] public void TearDown()
        {
            LobbyTunnel.DestroyMaterials(_root);
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void 부품_개수가_맞고_콜라이더와_그림자가_없다()
        {
            var rig = StageRig.Build(_root.transform, new TunnelLayout());
            Assert.AreEqual(46, rig.RunwayCount, "z 1~23 × 양쪽");
            Assert.AreEqual(16, rig.StripCount, "기둥 8쌍");
            Assert.AreEqual(100, rig.ConfettiCount);
            Assert.AreEqual(70, rig.FlagCount, "보 5개 × 14");
            Assert.AreEqual(4, rig.BeamCount);
            Assert.AreEqual(6, rig.FlashCount);
            Assert.AreEqual(0, _root.GetComponentsInChildren<Collider>(true).Length);
            foreach (var r in _root.GetComponentsInChildren<Renderer>(true))
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode, r.name);
        }

        [Test]
        public void 세기가_0인_연출은_숨는다()
        {
            var rig = StageRig.Build(_root.transform, new TunnelLayout());
            rig.Pose(1f, new StageEffectWeights(ranked: false), float.PositiveInfinity);
            Assert.IsFalse(rig.transform.Find("Runway").gameObject.activeSelf);
            Assert.IsTrue(rig.transform.Find("Bunting").gameObject.activeSelf);
            rig.Pose(1f, new StageEffectWeights(ranked: true), float.PositiveInfinity);
            Assert.IsTrue(rig.transform.Find("Runway").gameObject.activeSelf);
            Assert.IsFalse(rig.transform.Find("Bunting").gameObject.activeSelf);
        }

        [Test]
        public void 현수막은_기둥_조명_띠보다_안쪽에_있다()
        {
            //  띠가 현수막 앞을 지나가면 티어 도형·이름을 가로지른다(리뷰 지적).
            var layout = new TunnelLayout();
            var rig = StageRig.Build(_root.transform, layout);
            var signs = StageSigns.Build(_root.transform, layout);
            float stripInner = float.MaxValue;
            foreach (Transform t in rig.transform.Find("PillarStrips"))
                stripInner = Mathf.Min(stripInner, Mathf.Abs(t.localPosition.x) - t.localScale.x / 2f);
            int n = 0;
            foreach (Transform t in signs.transform)
            {
                if (t.name != "Sign_pennant") continue;
                n++;
                Assert.Less(Mathf.Abs(t.localPosition.x), stripInner);
            }
            Assert.AreEqual(4, n);
        }

        [Test]
        public void 조명_쇼가_어두울_땐_빛나는_부품도_어둡다()
        {
            var rig = StageRig.Build(_root.transform, new TunnelLayout());
            var w = new StageEffectWeights(ranked: true);
            rig.Pose(1f, w, float.PositiveInfinity);
            float lit = Brightest(rig, "Runway");
            rig.Pose(1f, w, 0.8f);
            Assert.Less(Brightest(rig, "Runway"), lit * 0.2f, "쇼의 어두운 순간엔 런웨이도 꺼진다");
        }

        private static float Brightest(StageRig rig, string group)
        {
            var mpb = new MaterialPropertyBlock();
            float max = 0f;
            foreach (var r in rig.transform.Find(group).GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(mpb);
                max = Mathf.Max(max, mpb.GetColor("_BaseColor").maxColorComponent);
            }
            return max;
        }

        private StageEffects MakeEffects(bool ranked, out LobbyLighting lighting)
        {
            var rig = StageRig.Build(_root.transform, new TunnelLayout());
            LobbyTunnel.Build(_root.transform, new TunnelLayout(), out var arena);
            lighting = _root.AddComponent<LobbyLighting>();
            lighting.Init(LobbyStage.CreateLights(_root.transform, arena, null), ranked);
            var effects = _root.AddComponent<StageEffects>();
            effects.Init(rig, lighting, null, ranked);
            return effects;
        }

        [Test]
        public void 쇼는_일반에서_랭크로_바뀔_때만_돈다()
        {
            var effects = MakeEffects(ranked: true, out _);
            Assert.IsTrue(float.IsPositiveInfinity(effects.ShowT), "랭크로 시작해도 돌지 않는다");
            effects.SetRanked(true);
            Assert.IsTrue(float.IsPositiveInfinity(effects.ShowT), "같은 값 재설정엔 돌지 않는다");
            effects.SetRanked(false);
            Assert.IsTrue(float.IsPositiveInfinity(effects.ShowT), "랭크→일반엔 돌지 않는다");
            effects.SetRanked(true);
            Assert.AreEqual(0f, effects.ShowT, 1e-4, "일반→랭크엔 처음부터");
            effects.Tick(0.8f);
            effects.SetRanked(false);
            effects.SetRanked(true);
            Assert.AreEqual(0f, effects.ShowT, 1e-4, "다시 오면 다시 처음부터");
        }

        [Test]
        public void 쇼_도중_일반으로_가면_어둡게_하기를_멈춘다()
        {
            var effects = MakeEffects(ranked: false, out var lighting);
            effects.SetRanked(true);
            effects.Tick(0.8f);
            Assert.AreEqual(0.08f, lighting.Dim, 1e-4);
            effects.SetRanked(false);
            effects.Tick(0f);
            Assert.AreEqual(1f, lighting.Dim, 1e-4);
        }
    }
}
