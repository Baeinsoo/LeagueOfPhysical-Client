using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 투석기 표적이 맞으면 그 자리에서 깨지는 연출을 띄운다. 적중 사건(누가·어느 표적·언제)을 듣고, 표적 그림은
    /// <see cref="ArcheryTargetView"/>가 이미 지웠으므로 여기서 조각을 새로 만든다. 1.2초 뒤 지운다.
    /// 쏘면 안 되는 것은 깨지지 않고 통째로 빨갛게 깜빡이며 떨어진다.
    /// </summary>
    public sealed class ArcheryCatapultBreakView : IStartable, ILateTickable, System.IDisposable
    {
        private readonly ArcheryCourse course;
        private readonly ArcheryWorld world;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryFestivalKit kit;
        private readonly LOP.MasterData.LOPMasterData md;
        private readonly ISubscriber<WorldEventBatchToC> batchSubscriber;
        private readonly List<ArcheryTarget> scratch = new List<ArcheryTarget>();
        private readonly List<Burst> bursts = new List<Burst>();
        private System.IDisposable subscription;

        //  키트에 빨간 재질이 없을 때(슬라이스 1 모양) 쓸 한 장. 이 뷰가 만들었으니 이 뷰가 지운다.
        private Material ownRed;

        private sealed class Burst
        {
            public ArcheryBreakKind Kind;
            public Vector3 At;
            public Vector3 ToShooter;
            public float StartedAt;
            public readonly List<GameObject> Pieces = new List<GameObject>();

            //  깜빡임용 — 렌더러마다 원래 재질과 같은 칸 수의 빨간 재질.
            public readonly List<(Renderer renderer, Material[] original, Material[] red)> Blink
                = new List<(Renderer, Material[], Material[])>();
            public bool? Red;   // 마지막으로 꽂은 쪽(처음엔 없음) — 바뀔 때만 재질을 갈아 끼운다
        }

        public ArcheryCatapultBreakView(ArcheryCourse course, ArcheryWorld world, GameFramework.Runner.IRunner runner,
                                        ArcheryFestivalKit kit, LOP.MasterData.LOPMasterData md,
                                        ISubscriber<WorldEventBatchToC> batchSubscriber)
        {
            this.course = course; this.world = world; this.runner = runner; this.kit = kit; this.md = md;
            this.batchSubscriber = batchSubscriber;
        }

        public void Start()
        {
            if (course.IsCatapult)
            {
                subscription = batchSubscriber.Subscribe(OnBatch);
            }
        }

        private void OnBatch(WorldEventBatchToC msg)
        {
            if (runner?.tickUpdater == null)
            {
                return;
            }
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase != WorldEventToC.EventOneofCase.ArcheryHit) { continue; }
                var hit = (ArcheryTargetHitEvent)WorldEventWire.FromWire(rec);
                if (ArcheryCatapultHits.TryLocate(course, world.GameplayStartTick, (float)runner.tickUpdater.interval,
                                                  hit, scratch, out var target, out var at) == false)
                {
                    continue;
                }
                string code = md.Tables.TbArcheryTarget.GetOrDefault(target.KindId)?.Code;
                Spawn(target, code, at);
            }
        }

        private void Spawn(in ArcheryTarget target, string code, Vector3 at)
        {
            var burst = new Burst { Kind = ArcheryCatapultBreakFx.KindOf(code), At = at, ToShooter = target.Facing, StartedAt = Time.time };
            int count = ArcheryCatapultBreakFx.PieceCount(burst.Kind);
            for (int i = 0; i < count; i++)
            {
                burst.Pieces.Add(PieceVisual(burst.Kind, code, i, target.Radius));
            }
            if (burst.Kind == ArcheryBreakKind.Shame)
            {
                var red = RedMaterial();
                foreach (var piece in burst.Pieces)
                {
                    foreach (var r in piece.GetComponentsInChildren<Renderer>(true))
                    {
                        var original = r.sharedMaterials;
                        var reds = new Material[original.Length];
                        for (int m = 0; m < reds.Length; m++) { reds[m] = red; }
                        burst.Blink.Add((r, original, reds));
                    }
                }
            }
            bursts.Add(burst);
            Place(burst, 0f);
        }

        //  키트가 비었으면(슬라이스 1 모양) 회색 도형으로 조각을 만든다 — 연출이 키트에 막히지 않게.
        private GameObject PieceVisual(ArcheryBreakKind kind, string code, int piece, float radius)
        {
            GameObject go;
            if (kind == ArcheryBreakKind.Coins && kit.coin != null)
            {
                go = kit.Make("coin", kit.coin, new[] { kit.coinMaterial });
            }
            else if (kind == ArcheryBreakKind.Split && code == "Shield" && kit.shieldHalf != null)
            {
                go = kit.Make("shield-half", kit.shieldHalf, kit.shieldHalfMaterials);
            }
            else if (kind == ArcheryBreakKind.Split && code == "Apple" && kit.appleHalf != null)
            {
                go = kit.Make("apple-half", kit.appleHalf, kit.appleHalfMaterials);
            }
            else if (kit.TryBuild(code, out var whole))
            {
                go = whole;
            }
            else
            {
                go = ArcheryCatapultLooks.BuildPlaceholder(code, kind == ArcheryBreakKind.Coins ? 0.08f : radius * 0.7f);
            }
            if (kind == ArcheryBreakKind.Split && piece == 0)
            {
                go.transform.localScale = new Vector3(-1f, 1f, 1f);   // 왼쪽 반 = 오른쪽 반의 거울
            }
            return go;
        }

        private Material RedMaterial()
        {
            if (kit.forbid != null)
            {
                return kit.forbid;
            }
            if (ownRed == null)
            {
                ownRed = LOPToonMaterials.Create(new Color(0.9f, 0.1f, 0.12f));
            }
            return ownRed;
        }

        public void LateTick()
        {
            float now = Time.time;
            for (int b = bursts.Count - 1; b >= 0; b--)
            {
                float t = now - bursts[b].StartedAt;
                if (t > ArcheryCatapultBreakFx.Seconds)
                {
                    Clear(bursts[b]);
                    bursts.RemoveAt(b);
                    continue;
                }
                Place(bursts[b], t);
            }
        }

        private static void Place(Burst burst, float t)
        {
            var face = Quaternion.LookRotation(burst.ToShooter == Vector3.zero ? Vector3.back : burst.ToShooter);
            for (int i = 0; i < burst.Pieces.Count; i++)
            {
                var p = burst.Pieces[i];
                if (p == null) { continue; }
                p.transform.position = burst.At + ArcheryCatapultBreakFx.PieceOffset(burst.Kind, i, t, burst.ToShooter);
                p.transform.rotation = face * ArcheryCatapultBreakFx.PieceSpin(burst.Kind, i, t);
            }
            if (burst.Blink.Count > 0)
            {
                SetRed(burst, ArcheryCatapultBreakFx.ShameIsRed(t));
            }
        }

        private static void SetRed(Burst burst, bool red)
        {
            if (burst.Red == red)
            {
                return;
            }
            burst.Red = red;
            foreach (var (renderer, original, reds) in burst.Blink)
            {
                if (renderer != null) { renderer.sharedMaterials = red ? reds : original; }
            }
        }

        private static void Clear(Burst burst)
        {
            //  원래 재질로 돌려놓고 지운다 — 회색 도형은 지울 때 렌더러의 재질을 같이 지우는데,
            //  빨간 재질이 꽂힌 채면 키트 에셋(또는 다른 조각이 쓰는 한 장)을 지우게 된다.
            if (burst.Blink.Count > 0)
            {
                foreach (var (renderer, original, _) in burst.Blink)
                {
                    if (renderer != null) { renderer.sharedMaterials = original; }
                }
            }
            foreach (var p in burst.Pieces) { ArcheryCatapultLooks.DestroyVisual(p); }
        }

        public void Dispose()
        {
            subscription?.Dispose();
            foreach (var b in bursts) { Clear(b); }
            bursts.Clear();
            if (ownRed != null)
            {
                Object.Destroy(ownRed);
                ownRed = null;
            }
        }
    }
}
