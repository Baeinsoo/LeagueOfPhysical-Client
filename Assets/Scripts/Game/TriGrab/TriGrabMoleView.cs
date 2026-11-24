using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using TG = LOP.TriGrab;

namespace LOP
{
    /// <summary>
    /// 두더지 그림(도형 — 갈색 몸·연갈색 배·분홍 코·흰 앞니·검은 눈과 수염). 구멍에서 <see cref="TG.TriGrabMole.HeightAt"/>만큼 솟는다.
    /// 카드와 같은 시각(원격 몸 시각)으로 그린다 — 두더지가 카드를 튕기는 게 같은 순간으로 보이게.
    /// 맞으면 납작해지며 흔들리고 머리 위에 "★ ★", 서 있을 땐 대사(? ! 안녕…).
    /// </summary>
    public class TriGrabMoleView : IStartable, System.IDisposable
    {
        public const float BodyScale = 1.25f;
        /// <summary>숨었을 때 몸 가운데 높이(판 윗면 기준, 시안 −R × 1.8)와 다 솟았을 때까지 오르는 높이(시안 R × 2.9).</summary>
        public const float SunkOffset = -TG.TriGrabConfig.MoleRadius * 1.8f;
        public const float RiseSpan = TG.TriGrabConfig.MoleRadius * 2.9f;
        /// <summary>바로 위 카메라에서 얼굴이 보이게 앞으로 숙인다(도).</summary>
        public const float TiltDegrees = -40f;

        private static readonly Color Fur = new Color(0.54f, 0.35f, 0.24f);
        private static readonly Color Belly = new Color(0.85f, 0.65f, 0.48f);
        private static readonly Color Nose = new Color(1f, 0.5f, 0.66f);
        private static readonly Color Black = new Color(0.07f, 0.07f, 0.07f);
        private static readonly Color Hole = new Color(0.05f, 0.04f, 0.09f);

        private sealed class MoleArt
        {
            public GameObject Root;
            public Transform Body;
            public CardRushWorldLabel Say;
        }

        private readonly EntityRenderClock renderClock;
        private readonly TriGrabClientState state;

        private TriGrabPool<MoleArt> arts;
        private GameObject root;
        private Material furMaterial, bellyMaterial, noseMaterial, blackMaterial, whiteMaterial, holeMaterial;
        private KnockbackLateUpdateDriver driver;

        public TriGrabMoleView(EntityRenderClock renderClock, TriGrabClientState state)
        {
            this.renderClock = renderClock;
            this.state = state;
        }

        /// <summary>몸 가운데 높이(판 윗면 기준). 땅속이면 음수.</summary>
        public static float BodyY(TG.TriGrabMole mole, double renderTick)
            => SunkOffset + HeightOf(mole, renderTick) * RiseSpan;

        /// <summary>소수 틱의 솟은 정도 — 이웃한 두 틱 사이를 잇는다.</summary>
        public static float HeightOf(TG.TriGrabMole mole, double renderTick)
        {
            long t0 = (long)System.Math.Floor(renderTick);
            return Mathf.Lerp(mole.HeightAt(t0), mole.HeightAt(t0 + 1), (float)(renderTick - t0));
        }

        /// <summary>맞고 눌려 있는 동안(맞은 틱부터 0.45초).</summary>
        public static bool Bonked(TG.TriGrabMole mole, double renderTick)
            => mole.BonkTick >= 0 && renderTick >= mole.BonkTick && renderTick - mole.BonkTick < TG.TriGrabMole.BonkedStandTicks;

        /// <summary>머리 위 글. 많이 솟았을 때만 — 맞았으면 별, 아니면 대사.</summary>
        public static string SpeechOf(TG.TriGrabMole mole, double renderTick)
        {
            if (HeightOf(mole, renderTick) < 0.6f) return null;
            if (mole.BonkTick >= 0 && renderTick >= mole.BonkTick) return "★ ★";
            var lines = TG.TriGrabMoles.SayLines;
            return lines[((mole.SayIndex % lines.Length) + lines.Length) % lines.Length];
        }

        public void Start()
        {
            root = new GameObject("TriGrabMoles");
            furMaterial = CardRushDraw.Opaque("TriGrab_MoleFur", Fur);
            bellyMaterial = CardRushDraw.Opaque("TriGrab_MoleBelly", Belly);
            noseMaterial = CardRushDraw.Opaque("TriGrab_MoleNose", Nose);
            blackMaterial = CardRushDraw.Opaque("TriGrab_MoleBlack", Black);
            whiteMaterial = CardRushDraw.Opaque("TriGrab_MoleWhite", Color.white);
            holeMaterial = CardRushDraw.Opaque("TriGrab_MoleHole", Hole);
            arts = new TriGrabPool<MoleArt>(Make, a => { a.Root.SetActive(false); a.Say?.Hide(); });
            driver = KnockbackLateUpdateDriver.Create("TriGrabMoleDriver", Place);
        }

        private void Place()
        {
            double tick = TriGrabRenderTicks.Remote(renderClock);
            var camera = Camera.main;
            if (double.IsNaN(tick) || root == null) return;
            bool live = state.PhaseAt((long)System.Math.Floor(tick)) == TG.TriGrabPhase.Live;
            float top = TriGrabCardsView.PlateTop;

            foreach (var mole in state.Moles)
            {
                if (!live || tick < mole.SpawnTick || mole.GoneAt((long)System.Math.Floor(tick))) continue;
                var art = arts.Get(mole.Id, out _);
                if (!art.Root.activeSelf) art.Root.SetActive(true);
                art.Root.transform.position = new Vector3(mole.Pos.X, top, mole.Pos.Y);

                float h = HeightOf(mole, tick);
                bool bonk = Bonked(mole, tick);
                float age = (float)(tick - mole.SpawnTick) * TG.TriGrabConfig.TickSeconds;
                art.Body.gameObject.SetActive(h > 0f);
                art.Body.localPosition = new Vector3(0f, BodyY(mole, tick), 0f);
                art.Body.localScale = BodyScale * (bonk
                    ? new Vector3(1.3f, 0.55f, 1.3f)
                    : new Vector3(1f, 1f + Mathf.Sin(age * 18f) * 0.05f, 1f));
                float wobble = bonk ? Mathf.Sin(age * 40f) * 17f : Mathf.Sin(age * 6f + mole.Id * 9f) * 7f;
                art.Body.localRotation = Quaternion.Euler(TiltDegrees, 0f, wobble);

                string say = SpeechOf(mole, tick);
                if (art.Say != null && camera != null && say != null)
                {
                    art.Say.Show(say, art.Root.transform.position + new Vector3(0f, 1.2f, 0.75f),
                                 bonk || mole.BonkTick >= 0 ? TriGrabPalette.Star : Color.white, h, 0.55f, camera);
                }
                else
                {
                    art.Say?.Hide();
                }
            }
            arts.EndFrame();
        }

        private MoleArt Make()
        {
            float r = TG.TriGrabConfig.MoleRadius;
            var go = new GameObject("Mole");
            go.transform.SetParent(root.transform, false);
            var hole = CardRushDraw.Primitive(PrimitiveType.Cylinder, "Hole", go.transform, holeMaterial);
            hole.transform.localScale = new Vector3((r + 0.06f) * 2f, 0.005f, (r + 0.06f) * 2f);
            hole.transform.localPosition = new Vector3(0f, 0.006f, 0f);

            var body = CardRushDraw.Node("Body", go.transform).transform;
            var fur = CardRushDraw.Primitive(PrimitiveType.Sphere, "Fur", body, furMaterial);
            fur.transform.localScale = new Vector3(r * 2f, r * 2f * 1.35f, r * 2f);
            var belly = CardRushDraw.Primitive(PrimitiveType.Sphere, "Belly", body, bellyMaterial);
            belly.transform.localScale = Vector3.one * r * 1.4f;
            belly.transform.localPosition = new Vector3(0f, -0.06f, r * 0.42f);
            for (int s = -1; s <= 1; s += 2)
            {
                var eye = CardRushDraw.Primitive(PrimitiveType.Sphere, "Eye", body, blackMaterial);
                eye.transform.localScale = Vector3.one * 0.084f;
                eye.transform.localPosition = new Vector3(s * 0.11f, 0.16f, r * 0.85f);
                var whisker = CardRushDraw.Box("Whisker", body, new Vector3(s * 0.19f, 0.035f, r * 0.82f),
                                               new Vector3(0.16f, 0.012f, 0.012f), blackMaterial);
                whisker.transform.localRotation = Quaternion.Euler(0f, 0f, s * 8.6f);
            }
            var nose = CardRushDraw.Primitive(PrimitiveType.Sphere, "Nose", body, noseMaterial);
            nose.transform.localScale = Vector3.one * 0.14f;
            nose.transform.localPosition = new Vector3(0f, 0.06f, r * 1.02f);
            CardRushDraw.Box("Tooth", body, new Vector3(0f, -0.025f, r * 0.98f), new Vector3(0.09f, 0.08f, 0.025f), whiteMaterial);

            return new MoleArt { Root = go, Body = body, Say = CardRushWorldLabel.Create("MoleSay", root.transform) };
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root);
            foreach (var m in new[] { furMaterial, bellyMaterial, noseMaterial, blackMaterial, whiteMaterial, holeMaterial })
            {
                if (m != null) Object.Destroy(m);
            }
        }
    }
}
