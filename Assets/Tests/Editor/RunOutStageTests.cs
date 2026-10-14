using System.IO;
using LOP;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>출전 무대 연결 — 선수가 달려 나갔다 제자리로, 연출 중엔 무대 카메라가 손을 뗀다, 로비 UI가 숨는다.</summary>
    public class RunOutStageTests
    {
        private GameObject _root;
        [SetUp] public void SetUp() => _root = new GameObject("runout-stage");
        [TearDown] public void TearDown() { LobbyTunnel.DestroyMaterials(_root); Object.DestroyImmediate(_root); }

        [Test]
        public void 선수는_달려_나갔다가_제자리로_돌아온다()
        {
            var player = _root.AddComponent<LobbyPlayer>();
            var body = new GameObject("body");
            body.transform.SetParent(_root.transform);
            player.AttachVisual(body);
            player.PoseRunOut(9f, 0f, true);
            Assert.AreEqual(9f, body.transform.position.z, 1e-4);
            Assert.AreEqual(0f, body.transform.eulerAngles.y, 1e-3);
            player.ResetRunOut();
            Assert.AreEqual(LobbyPlayer.StandPosition, body.transform.position);
            Assert.AreEqual(180f, body.transform.eulerAngles.y, 1e-3);
        }

        [Test]
        public void 카메라를_넘겨받는_동안_무대는_카메라를_건드리지_않는다()
        {
            var camGo = new GameObject("cam");
            camGo.transform.SetParent(_root.transform);
            var cam = camGo.AddComponent<Camera>();
            var rig = StageRig.Build(_root.transform, new TunnelLayout());
            var effects = _root.AddComponent<StageEffects>();
            effects.Init(rig, null, cam, ranked: false);
            effects.CameraOverride = true;
            cam.transform.position = new Vector3(5f, 5f, 5f);
            effects.Tick(0.1f);
            Assert.AreEqual(new Vector3(5f, 5f, 5f), cam.transform.position);
        }

        [Test]
        public void 출전_중엔_로비_UI가_사라진다()
        {
            StringAssert.Contains(".runout.lobbyhome-root { opacity: 0; }", File.ReadAllText("Assets/UI/LobbyHome/LobbyHomeView.uss"));
        }
    }
}
