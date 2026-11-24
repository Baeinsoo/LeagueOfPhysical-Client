using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 회전판 카드 8장. 서버가 보낸 카드 목록·회전판 시계에 렌더 틱을 넣어 서버 판정과 같은 식으로 자리를 낸다(스펙 §6).
    /// 각은 소수 렌더 틱으로 이어 그리고, 글자색은 정수 틱으로 묻는다(색은 틱 단위 사실이다). 빈 슬롯은 숨긴다.
    /// </summary>
    public class TriGrabCarouselView : IStartable, System.IDisposable
    {
        /// <summary>카드 가운데 높이(m). 받침 위로 조금 띄워 몸·갈고리와 겹쳐 보이지 않게.</summary>
        public const float CardHeight = 0.9f;
        public const float PopSeconds = 0.25f;
        /// <summary>카드 판 배율. 1이면 1.6 × 1.1 m — 폰에서 글자가 작아 시안처럼 판정 원 지름(2.3 m) 가까이 키운다.</summary>
        public const float CardScale = 1.35f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly TriGrabClientState state;

        private readonly TriGrabCardFace[] faces = new TriGrabCardFace[TriGrabConfig.Slots];
        private readonly int[] shownId = new int[TriGrabConfig.Slots];
        private readonly float[] bornAt = new float[TriGrabConfig.Slots];
        private GameObject root;
        private KnockbackLateUpdateDriver driver;

        public TriGrabCarouselView(GameFramework.Runner.IRunner runner, TriGrabClientState state)
        {
            this.runner = runner;
            this.state = state;
            for (int i = 0; i < shownId.Length; i++) shownId[i] = -1;
        }

        /// <summary>그 렌더 틱(소수)의 슬롯 카드 가운데 자리.</summary>
        public static Vector3 CardWorldPosition(int slot, in TriGrabRoundClock clock, double renderTick)
        {
            var p = TriGrabCarousel.SlotPosition(slot, TriGrabClientState.BaseAt(clock, renderTick));
            return new Vector3(p.x, CardHeight, p.y);
        }

        /// <summary>그 렌더 틱의 글자색 — 정수 틱(내림)으로 묻는다.</summary>
        public static int InkAt(in TriGrabCard card, double renderTick) => card.InkAt((long)System.Math.Floor(renderTick));

        /// <summary>글자를 보이나. 준비 중엔 흰 판만 — 주문을 먼저 읽게(시안과 같다).</summary>
        public static bool FaceUp(TriGrabPhase phase) => phase != TriGrabPhase.Prep && phase != TriGrabPhase.Waiting;

        public static double RenderTickOf(GameFramework.Runner.IRunner runner)
        {
            var t = runner?.tickUpdater;
            if (t == null || t.interval <= 0d) return double.NaN;
            return (t.elapsedTime - t.interval) / t.interval;
        }

        public void Start()
        {
            root = new GameObject("TriGrabCarousel");
            driver = KnockbackLateUpdateDriver.Create("TriGrabCarouselDriver", Place);
        }

        private void Place()
        {
            double renderTick = RenderTickOf(runner);
            var camera = Camera.main;
            if (double.IsNaN(renderTick) || camera == null || root == null) return;

            bool any = state.Received;
            var phase = state.PhaseAt((long)System.Math.Floor(renderTick));
            bool faceUp = FaceUp(phase);
            var facing = camera.transform.rotation;
            for (int i = 0; i < faces.Length; i++)
            {
                var card = any ? state.CardAt(i) : null;
                if (card == null)
                {
                    faces[i]?.Hide();
                    shownId[i] = -1;
                    continue;
                }
                if (faces[i] == null)
                {
                    faces[i] = TriGrabCardFace.Create("Card" + i, root.transform);
                    if (faces[i] == null) continue;
                }
                var c = card.Value;
                if (c.Id != shownId[i])
                {
                    //  처음 받은 판의 카드는 번쩍이지 않는다 — 빈자리를 채운 새 카드만.
                    bornAt[i] = shownId[i] == -1 && phase != TriGrabPhase.Live ? -100f : Time.unscaledTime;
                    shownId[i] = c.Id;
                }
                float pop = 1f - (Time.unscaledTime - bornAt[i]) / PopSeconds;
                faces[i].Show(c, InkAt(c, renderTick), faceUp, CardWorldPosition(i, state.Clock, renderTick), facing,
                              CardScale * (1f + Mathf.Clamp01(pop) * 0.15f), TriGrabHookResult.None, pop);
            }
        }

        public void Dispose()
        {
            if (driver != null) Object.Destroy(driver.gameObject);
            if (root != null) Object.Destroy(root);
        }
    }
}
