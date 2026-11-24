using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;
using TG = LOP.TriGrab;

namespace LOP
{
    /// <summary>
    /// 원판 위에 뜨는 글. 준비 동안 가운데 큰 주문("글씨 색이 / 빨강 / 인 카드를 잡아라" — 목표 글은 미끼 색),
    /// 그리고 서버가 알린 연출 글: 정답 "+1" 초록, 오답 "땡! −1" 빨강, 사거리 끝 "빗나감"(내 것만), 두더지 대사.
    /// </summary>
    public class TriGrabOverlayView : IStartable, System.IDisposable
    {
        public const float FxSeconds = 1f;
        public const float FxRise = 0.8f;
        public const float FxHeight = 2.4f;

        private sealed class Floating
        {
            public CardRushWorldLabel Label;
            public string Text;
            public Color Color;
            public Vector3 At;
            public float Born;
        }

        private readonly EntityRenderClock renderClock;
        private readonly TriGrabClientState state;
        private readonly IPlayerContext playerContext;

        private readonly List<Floating> floating = new List<Floating>();
        private readonly Stack<CardRushWorldLabel> spareLabels = new Stack<CardRushWorldLabel>();
        private GameObject root;
        private GameObject backdrop;
        private Material backdropMaterial;
        private CardRushWorldLabel kindLabel, targetLabel, suffixLabel;
        private KnockbackLateUpdateDriver driver;

        public TriGrabOverlayView(EntityRenderClock renderClock, TriGrabClientState state, IPlayerContext playerContext)
        {
            this.renderClock = renderClock;
            this.state = state;
            this.playerContext = playerContext;
        }

        /// <summary>주문의 첫 줄(무엇을 보는가).</summary>
        public static string RuleKind(TG.TriGrabRule rule)
            => rule.Type == TG.TriGrabRuleType.Ink ? "글씨 색이"
             : rule.Type == TG.TriGrabRuleType.Word ? "쓰인 말이"
             : "글자와 글씨 색이";

        /// <summary>목표 글. 색 규칙이면 색 이름, 같은 카드 규칙이면 "같은".</summary>
        public static string RuleTarget(TG.TriGrabRule rule)
            => rule.Type == TG.TriGrabRuleType.Match ? "같은" : TriGrabPalette.ColorName(rule.Value);

        public static string RuleSuffix(TG.TriGrabRule rule) => rule.Type == TG.TriGrabRuleType.Match ? "카드를 잡아라" : "인 카드를 잡아라";

        /// <summary>목표 글 색 — 색 이름은 미끼 색으로 칠한다(속임). 같은 카드 규칙은 흰색.</summary>
        public static Color RuleTargetColor(TG.TriGrabRule rule)
            => rule.Type == TG.TriGrabRuleType.Match ? Color.white : TriGrabPalette.Ink(rule.Lure);

        /// <summary>연출 글과 색. 띄우지 않을 연출(남의 빗나감)이면 null.</summary>
        public static string FxText(in TriGrabFxEvent e, string myId, out Color color)
        {
            switch (e.Kind)
            {
                case TG.TriGrabFxKind.Correct:
                    color = TriGrabPalette.Correct;
                    return "+1";
                case TG.TriGrabFxKind.Wrong:
                    color = TriGrabPalette.Wrong;
                    return "땡! −1";
                case TG.TriGrabFxKind.Range:
                    color = TriGrabPalette.Miss;
                    return e.Owner == myId ? "빗나감" : null;
                default:
                    color = TriGrabPalette.MoleLine;
                    var lines = TG.TriGrabMoles.BonkLines;
                    return lines[((e.Line % lines.Length) + lines.Length) % lines.Length];
            }
        }

        public void Start()
        {
            root = new GameObject("TriGrabOverlay");
            backdropMaterial = CardRushDraw.Transparent("TriGrab_PrepBackdrop", new Color(0.06f, 0.05f, 0.09f, 0.6f));
            backdrop = CardRushDraw.Primitive(PrimitiveType.Cylinder, "PrepBackdrop", root.transform, backdropMaterial);
            float r = TG.TriGrabConfig.DiscRadius * 0.9f;
            backdrop.transform.localScale = new Vector3(r * 2f, 0.005f, r * 2f);
            backdrop.transform.localPosition = new Vector3(0f, TriGrabCardsView.PlateTop + 0.4f, 0f);
            backdrop.SetActive(false);
            kindLabel = CardRushWorldLabel.Create("RuleKind", root.transform);
            targetLabel = CardRushWorldLabel.Create("RuleTarget", root.transform);
            suffixLabel = CardRushWorldLabel.Create("RuleSuffix", root.transform);
            driver = KnockbackLateUpdateDriver.Create("TriGrabOverlayDriver", Place);
        }

        private void Place()
        {
            var camera = Camera.main;
            if (root == null || camera == null) return;
            double tick = TriGrabRenderTicks.Remote(renderClock);
            bool prep = !double.IsNaN(tick) && state.Received && state.Round >= 1
                        && state.PhaseAt((long)System.Math.Floor(tick)) == TG.TriGrabPhase.Prep;
            PlacePrep(prep, camera);
            SpawnFx();
            PlaceFx(camera);
        }

        private void PlacePrep(bool prep, Camera camera)
        {
            CardRushDraw.SetActive(backdrop, prep);
            if (!prep)
            {
                kindLabel?.Hide();
                targetLabel?.Hide();
                suffixLabel?.Hide();
                return;
            }
            var rule = state.Rule;
            float y = TriGrabCardsView.PlateTop + 0.5f;
            kindLabel?.Show(RuleKind(rule), new Vector3(0f, y, 2f), Color.white, 1f, 0.9f, camera);
            targetLabel?.Show(RuleTarget(rule), new Vector3(0f, y, 0f), RuleTargetColor(rule), 1f, 1.8f, camera);
            suffixLabel?.Show(RuleSuffix(rule), new Vector3(0f, y, -2f), new Color(1f, 1f, 1f, 0.8f), 0.8f, 0.65f, camera);
        }

        private void SpawnFx()
        {
            string me = playerContext.entityId;
            while (state.TryDequeueFx(out var e))
            {
                string text = FxText(e, me, out var color);
                if (text == null) continue;
                var label = spareLabels.Count > 0 ? spareLabels.Pop() : CardRushWorldLabel.Create("Fx", root.transform);
                if (label == null) continue;
                floating.Add(new Floating
                {
                    Label = label, Text = text, Color = color, Born = Time.unscaledTime,
                    At = new Vector3(e.Pos.x, FxHeight, e.Pos.y),
                });
            }
        }

        private void PlaceFx(Camera camera)
        {
            for (int i = floating.Count - 1; i >= 0; i--)
            {
                var f = floating[i];
                float k = (Time.unscaledTime - f.Born) / FxSeconds;
                if (k >= 1f)
                {
                    f.Label.Hide();
                    spareLabels.Push(f.Label);
                    floating.RemoveAt(i);
                    continue;
                }
                //  바로 위 카메라라 위로 뜨는 대신 화면 위쪽(+z)으로 떠오른다.
                f.Label.Show(f.Text, f.At + new Vector3(0f, 0f, k * FxRise), f.Color, 1f - k, 0.8f, camera);
            }
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root);
            if (backdropMaterial != null) Object.Destroy(backdropMaterial);
        }
    }
}
