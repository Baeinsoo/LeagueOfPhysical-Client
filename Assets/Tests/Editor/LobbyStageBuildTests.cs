using LOP;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>로비 3D 무대 조립 — 터널 부품 수·콜라이더 없음, 조명 적용·섞기, 카메라, 룩 입히는 순서.</summary>
    public class LobbyStageBuildTests
    {
        private GameObject _root;

        [SetUp] public void SetUp() => _root = new GameObject("test-stage");
        [TearDown] public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void 터널은_부품_30개에_콜라이더가_없다()
        {
            var layout = new TunnelLayout();
            var tunnel = LobbyTunnel.Build(_root.transform, layout, out var glow);
            //  바닥1·벽2·천장1·기둥16·보8·바닥 줄1·끝 빛1
            Assert.AreEqual(30, tunnel.transform.childCount);
            Assert.AreEqual(0, tunnel.GetComponentsInChildren<Collider>().Length);
            Assert.AreEqual(layout.EndGlowZ, glow.transform.position.z, 1e-3);
        }

        [Test]
        public void 조명_값이_빛에_들어간다()
        {
            LobbyTunnel.Build(_root.transform, new TunnelLayout(), out var glow);
            var lights = LobbyStage.CreateLights(_root.transform, glow, null);
            LobbyLighting.Apply(StageLighting.For(true), lights);
            Assert.AreEqual(0.35f, lights.Sun.intensity, 1e-4);
            Assert.IsTrue(lights.Spot.enabled);
            LobbyLighting.Apply(StageLighting.For(false), lights);
            Assert.IsFalse(lights.Spot.enabled);
        }

        [Test]
        public void 섞는_중에_바뀌면_지금_값에서_다시_섞는다()
        {
            LobbyTunnel.Build(_root.transform, new TunnelLayout(), out var glow);
            var lighting = _root.AddComponent<LobbyLighting>();
            lighting.Init(LobbyStage.CreateLights(_root.transform, glow, null), ranked: false);
            lighting.SetRanked(true);
            lighting.Step(LobbyLighting.BlendSeconds / 2f);
            float mid = lighting.Current.SunIntensity;
            lighting.SetRanked(false);
            lighting.Step(0f);
            Assert.AreEqual(mid, lighting.Current.SunIntensity, 1e-4, "바뀐 순간 튀지 않는다");
            lighting.Step(LobbyLighting.BlendSeconds);
            Assert.AreEqual(1.0f, lighting.Current.SunIntensity, 1e-4);
        }

        [Test]
        public void 카메라는_고정_화각과_위치()
        {
            var cam = _root.AddComponent<Camera>();
            LobbyStage.ConfigureCamera(cam);
            Assert.AreEqual(40f, cam.fieldOfView, 1e-3);
            Assert.AreEqual(new Vector3(0f, 1.2f, 0f), cam.transform.position);
            Assert.AreEqual(CameraClearFlags.SolidColor, cam.clearFlags);
        }

        [Test]
        public void 몸이_나중에_붙어도_먼저_온_룩을_입는다()
        {
            var player = _root.AddComponent<LobbyPlayer>();
            var look = new PlayerLook(new System.Collections.Generic.Dictionary<string, string>(), "a", 3);
            player.SetLook(look);
            Assert.IsNull(player.AppliedLook, "몸이 없으면 아직 못 입는다");
            var body = new GameObject("body");
            body.transform.SetParent(_root.transform);
            player.AttachVisual(body);
            Assert.AreSame(look, player.AppliedLook);
            Assert.AreEqual(LobbyPlayer.StandPosition, body.transform.position);
        }
    }
}
