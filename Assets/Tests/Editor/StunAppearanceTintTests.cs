using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 평소 모습의 칠을 <see cref="StunAppearance"/>가 같이 쥔다. 지금은 아무도 안 부르지만(사용자 결정 10-25 —
    /// 게임이 선수에게 색을 배정하지 않는다) 장차 코스튬 색을 입힐 자리라 메서드와 시험은 남긴다. 머티리얼을
    /// 갈아 끼우는 주인이 둘이면 기절이 풀릴 때 "원본"으로 되돌리며 칠을 지운다 — 주인을 하나로 둔 것을 고정한다.
    /// </summary>
    public class StunAppearanceTintTests
    {
        private static readonly Color Tint = new Color(0.54f, 0.65f, 0.72f);
        private static readonly Color StunColor = new Color(0.6f, 0.6f, 0.7f);

        private GameObject actorGo;
        private GameObject extraVisual;
        private LOPEntityView entityView;
        private StunAppearance appearance;
        private Material body, beak;
        private MeshRenderer bodyRenderer, beakRenderer;

        [SetUp]
        public void SetUp()
        {
            actorGo = new GameObject("Actor_a");
            entityView = actorGo.AddComponent<LOPEntityView>();
            var actor = actorGo.AddComponent<LOPActor>();
            actor.SetEntityId("a");
            actor.SetView(entityView);
            appearance = actorGo.AddComponent<StunAppearance>();
            appearance.SetEntity(actor);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            body = new Material(shader) { name = "BodyMaterial", color = Color.yellow };
            beak = new Material(shader) { name = "BeakMaterial", color = new Color(1f, 0.56f, 0.23f) };
            var visual = MakeVisual(out bodyRenderer, out beakRenderer);
            visual.transform.SetParent(actorGo.transform, false);
            SetVisual(visual);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(actorGo);
            if (extraVisual != null) { Object.DestroyImmediate(extraVisual); }
            Object.DestroyImmediate(body);
            Object.DestroyImmediate(beak);
        }

        private GameObject MakeVisual(out MeshRenderer bodyR, out MeshRenderer beakR)
        {
            var visual = new GameObject("Visual");
            var b = new GameObject("Body"); b.transform.SetParent(visual.transform, false);
            bodyR = b.AddComponent<MeshRenderer>(); bodyR.sharedMaterial = body;
            var k = new GameObject("Beak"); k.transform.SetParent(visual.transform, false);
            beakR = k.AddComponent<MeshRenderer>(); beakR.sharedMaterial = beak;
            return visual;
        }

        //  로드가 끝나 그림이 붙은 것처럼 — LOPEntityView의 private 필드에 꽂는다(MountedHorseViewTests와 같다).
        private void SetVisual(GameObject visual)
        {
            typeof(LOPEntityView).GetField("_visualGameObject", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(entityView, visual);
        }

        private static bool IsBody(Material m) => m.name.Contains("Body");

        private static void AreClose(Color expected, Color actual, string message = null)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f, message);
            Assert.AreEqual(expected.g, actual.g, 1e-3f, message);
            Assert.AreEqual(expected.b, actual.b, 1e-3f, message);
        }

        [Test]
        public void 칠하면_몸만_바뀌고_부리는_그대로()
        {
            appearance.SetBaseTint(Tint, IsBody);

            AreClose(Tint, bodyRenderer.sharedMaterial.color);
            Assert.AreSame(beak, beakRenderer.sharedMaterial, "부리는 복제도 안 한다");
            AreClose(Color.yellow, body.color, "원본 에셋은 안 건드린다");
        }

        [Test]
        public void 기절이_풀리면_칠한_색으로_돌아온다()
        {
            appearance.SetBaseTint(Tint, IsBody);

            appearance.SetState(StunVisual.Stunned);
            AreClose(StunColor, bodyRenderer.sharedMaterial.color, "기절 중엔 기절색이 이긴다");

            appearance.SetState(StunVisual.None);
            AreClose(Tint, bodyRenderer.sharedMaterial.color);
        }

        [Test]
        public void 그림이_붙기_전에_칠해도_붙는_순간_칠해진다()
        {
            SetVisual(null);
            appearance.SetBaseTint(Tint, IsBody);

            extraVisual = MakeVisual(out var lateBody, out _);
            SetVisual(extraVisual);
            appearance.SetState(StunVisual.None);   // 보간기가 매 프레임 부르는 것과 같다

            AreClose(Tint, lateBody.sharedMaterial.color);
        }

        [Test]
        public void 모델이_바뀌어도_칠이_다시_붙는다()
        {
            appearance.SetBaseTint(Tint, IsBody);
            appearance.SetState(StunVisual.None);

            extraVisual = MakeVisual(out var newBody, out _);
            SetVisual(extraVisual);
            appearance.SetState(StunVisual.None);

            AreClose(Tint, newBody.sharedMaterial.color);
        }

        [Test]
        public void 정리하면_원본으로_돌아간다()
        {
            appearance.SetBaseTint(Tint, IsBody);

            appearance.Cleanup();

            Assert.AreSame(body, bodyRenderer.sharedMaterial);
        }

        [Test]
        public void 칠이_없으면_지금과_같다()
        {
            appearance.SetState(StunVisual.Stunned);
            appearance.SetState(StunVisual.None);

            Assert.AreSame(body, bodyRenderer.sharedMaterial, "칠을 안 받은 새(내 새)는 복제 없이 원본");
        }

        //  내 새 표시(FlappyMyBirdMarker)는 그림의 자식이라 GetComponentsInChildren에 같이 걸린다.
        //  StunAppearanceIgnore를 단 장식은 기절색·칠이 안 묻어야 하고, 표시가 먼저 사라져도(Destroy) 정리가 안 죽어야 한다.
        [Test]
        public void 표시_장식은_기절색을_안_받고_지워져도_정리가_안_죽는다()
        {
            var markerRoot = new GameObject("Marker");
            markerRoot.transform.SetParent(actorGo.transform.Find("Visual"), false);
            markerRoot.AddComponent<StunAppearanceIgnore>();
            var markerGo = new GameObject("MarkerSprite");
            markerGo.transform.SetParent(markerRoot.transform, false);
            var markerRenderer = markerGo.AddComponent<SpriteRenderer>();
            var markerOriginalMaterial = markerRenderer.sharedMaterial;

            appearance.SetState(StunVisual.Stunned);

            Assert.AreSame(markerOriginalMaterial, markerRenderer.sharedMaterial, "표시는 StunAppearance가 건드리지 않는다");

            Object.DestroyImmediate(markerGo);
            Assert.DoesNotThrow(() => appearance.Cleanup(), "표시가 먼저 사라져도 정리는 죽지 않는다");

            Object.DestroyImmediate(markerRoot);
        }
    }
}
