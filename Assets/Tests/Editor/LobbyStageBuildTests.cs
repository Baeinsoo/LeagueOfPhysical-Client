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
        public void 터널은_부품_31개에_출구가_열려_있고_콜라이더가_없다()
        {
            var layout = new TunnelLayout();
            var tunnel = LobbyTunnel.Build(_root.transform, layout, out var arena);
            //  바닥1·벽2·천장1·기둥16·보8·바닥 줄1·경기장 배경1·경기장 바닥1
            Assert.AreEqual(31, tunnel.transform.childCount);
            Assert.IsNull(tunnel.transform.Find("EndGlow"), "출구를 막던 끝 빛 판은 없다");
            Assert.AreEqual(0, tunnel.GetComponentsInChildren<Collider>().Length);
            foreach (var r in tunnel.GetComponentsInChildren<Renderer>())
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode, r.name);
            Assert.AreEqual(layout.ArenaZ, arena.transform.position.z, 1e-3);
            Assert.AreEqual("ArenaBackdrop", arena.name);
        }

        [Test]
        public void 조명_값이_빛에_들어간다()
        {
            LobbyTunnel.Build(_root.transform, new TunnelLayout(), out var glow);
            var lights = LobbyStage.CreateLights(_root.transform, glow, null);
            LobbyLighting.Apply(StageLighting.For(true), lights);
            Assert.AreEqual(0.6f, lights.Sun.intensity, 1e-4);
            Assert.IsTrue(lights.Spot.enabled);
            //  툰 셰이더는 방향광만 받는다 — 발밑 조명은 바닥의 밝은 원으로 보인다.
            Assert.IsTrue(lights.FootPool.enabled);
            LobbyLighting.Apply(StageLighting.For(false), lights);
            Assert.IsFalse(lights.Spot.enabled);
            Assert.IsFalse(lights.FootPool.enabled);
            LobbyLighting.Apply(StageLighting.For(false), lights, dim: 0.5f);
            Assert.AreEqual(0.5f, lights.Sun.intensity, 1e-4, "어둡게 하기는 해 세기에 곱한다");
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

        [Test]
        public void 같은_룩이_다시_오면_다시_입히지_않는다()
        {
            //  상점을 열면 진행도·장착이 한 프레임에 연달아 바뀐다. 같은 룩을 두 번 입히면 플레이 중엔
            //  옛 모자가 프레임 끝까지 남아 있어 새 모자가 하나씩 쌓인다(리뷰 지적).
            var player = _root.AddComponent<LobbyPlayer>();
            var body = new GameObject("body");
            body.transform.SetParent(_root.transform);
            player.AttachVisual(body);
            var first = new PlayerLook(new System.Collections.Generic.Dictionary<string, string> { ["hat"] = "hat_cube_red" }, "a", 3);
            player.SetLook(first);
            player.SetLook(new PlayerLook(new System.Collections.Generic.Dictionary<string, string> { ["hat"] = "hat_cube_red" }, "a", 4));
            Assert.AreSame(first, player.AppliedLook, "슬롯이 같으면 다시 입히지 않는다");
            var changed = new PlayerLook(new System.Collections.Generic.Dictionary<string, string>(), "a", 4);
            player.SetLook(changed);
            Assert.AreSame(changed, player.AppliedLook, "슬롯이 바뀌면 다시 입힌다");
        }
    }
}
