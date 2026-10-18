using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>시상대 치비 — 자리에 서고, 몸이 오기 전 반응은 기억했다가 붙을 때 건다.</summary>
    public class LobbyPlayerReactionTests
    {
        private const string ControllerPath = "Assets/Characters/Chibi/ChibiArcher.controller";
        private GameObject _host;
        private GameObject _body;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_body != null) Object.DestroyImmediate(_body);
        }

        private LobbyPlayer Make(bool withController)
        {
            _host = new GameObject("host");
            var player = _host.AddComponent<LobbyPlayer>();
            _body = new GameObject("body");
            var animator = _body.AddComponent<Animator>();
            if (withController) animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            return player;
        }

        [Test]
        public void 기본_자리는_로비_옛_자리()
        {
            var player = Make(false);
            player.AttachVisual(_body);
            Assert.AreEqual(LobbyPlayer.StandPosition, _body.transform.position);
            Assert.AreEqual(180f, _body.transform.eulerAngles.y, 0.01f);
            Assert.AreEqual(1f, _body.transform.localScale.x, 1e-4);
        }

        [Test]
        public void Stand한_자리에_선다()
        {
            var player = Make(false);
            player.Stand(new Vector3(2f, 0.9f, 0f), 180f, 0.6f);
            player.AttachVisual(_body);
            Assert.AreEqual(new Vector3(2f, 0.9f, 0f), _body.transform.position);
            Assert.AreEqual(0.6f, _body.transform.localScale.x, 1e-4);
        }

        [Test]
        public void 자리_크기가_뿌리_크기이고_모델_배율은_그대로다()
        {
            //  치비 뿌리는 크기 1, 모델 배율(3)은 자식 Model이 갖는다 — 자리 크기를 뿌리에 그대로 쓴다.
            var player = Make(false);
            var model = new GameObject("Model");
            model.transform.SetParent(_body.transform, false);
            model.transform.localScale = Vector3.one * 3f;
            player.AttachVisual(_body);
            player.Stand(Vector3.zero, 180f, 0.6f);
            Assert.AreEqual(0.6f, _body.transform.localScale.x, 1e-4);
            Assert.AreEqual(3f, model.transform.localScale.x, 1e-4);
        }

        [Test]
        public void 몸이_붙기_전_반응은_붙을_때_건다()
        {
            var player = Make(true);
            Assert.IsFalse(player.HasBody);
            player.React(ResultReaction.Happy);
            Assert.IsFalse(player.ReactionApplied);
            player.AttachVisual(_body);
            Assert.IsTrue(player.HasBody);
            Assert.AreEqual(ResultReaction.Happy, player.LastReaction);
            Assert.IsTrue(player.ReactionApplied);
        }

        [Test]
        public void 기뻐하는_선수는_되풀이한다()
        {
            var player = Make(true);
            player.AttachVisual(_body);
            player.React(ResultReaction.Happy);
            Assert.AreEqual(1, player.ReactionTriggers);
            player.TickCelebration(LobbyPlayer.CelebrateEverySeconds - 0.1f);
            Assert.AreEqual(1, player.ReactionTriggers);
            player.TickCelebration(0.2f);
            Assert.AreEqual(2, player.ReactionTriggers);
        }

        [Test]
        public void 아쉬워하는_선수는_한_번만()
        {
            var player = Make(true);
            player.AttachVisual(_body);
            player.React(ResultReaction.Sad);
            player.TickCelebration(LobbyPlayer.CelebrateEverySeconds * 3f);
            Assert.AreEqual(1, player.ReactionTriggers);
        }

        //  옷 색은 입히기(ChibiOutfit)가 MaterialPropertyBlock에 쓴다 — 회색 처리가 그 블록을 지우면 옷 색이 사라진다(리뷰 Critical).
        private MeshRenderer ToonPart(out Material material)
        {
            var part = new GameObject("part");
            part.transform.SetParent(_body.transform, false);
            var r = part.AddComponent<MeshRenderer>();
            material = new Material(Shader.Find("LOP/Toon"));
            r.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor("_TopColor", Color.red);
            r.SetPropertyBlock(block);
            return r;
        }

        [Test]
        public void 나가지_않은_선수는_옷_색이_남는다()
        {
            var player = Make(false);
            var r = ToonPart(out var m);
            player.AttachVisual(_body);
            player.SetAbsent(false);
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            Assert.AreEqual(Color.red, block.GetColor("_TopColor"));
            Object.DestroyImmediate(m);
        }

        [Test]
        public void 회색은_되돌려도_옷_색이_남는다()
        {
            var player = Make(false);
            var r = ToonPart(out var m);
            player.AttachVisual(_body);
            player.SetAbsent(true);
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            Assert.Less(block.GetColor("_BaseColor").r, m.GetColor("_BaseColor").r + 1e-4f);
            Assert.AreNotEqual(m.GetColor("_BaseColor"), block.GetColor("_BaseColor"));
            player.SetAbsent(false);
            r.GetPropertyBlock(block);
            Assert.AreEqual(m.GetColor("_BaseColor"), block.GetColor("_BaseColor"));
            Assert.AreEqual(Color.red, block.GetColor("_TopColor"));
            Object.DestroyImmediate(m);
        }

        [Test]
        public void 트리거_없는_컨트롤러면_아무_일_없다()
        {
            var player = Make(false);
            player.AttachVisual(_body);
            Assert.DoesNotThrow(() => player.React(ResultReaction.Sad));
            Assert.IsFalse(player.ReactionApplied);
        }
    }
}
