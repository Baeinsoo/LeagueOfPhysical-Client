using System;
using System.Collections.Generic;
using GameFramework;
using MessagePipe;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 말 활쏘기 전투 표시의 상태: 맞힘 ✕(0.2초), 맞은 방향 호(1초), 킬 피드(4줄, 줄마다 4초).
    /// 시간 흐름은 <see cref="Tick"/>이 만든다 — 화면(View)이 매 프레임 불러 준다.
    /// </summary>
    public class MountedCombatHudViewModel : IDisposable
    {
        public const float HitMarkerSeconds = 0.2f;
        public const float DamageFromSeconds = 1f;
        public const float KillLineSeconds = 4f;
        public const int MaxKillLines = 4;

        private readonly MountedPredictedHits predictedHits;
        private readonly MountedArcheryClientState state;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private readonly CompositeDisposable disposables = new CompositeDisposable();

        private readonly ReactiveProperty<bool> hitMarker = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<float?> damageFromBearing = new ReactiveProperty<float?>(null);
        private readonly ReactiveProperty<IReadOnlyList<string>> killFeed =
            new ReactiveProperty<IReadOnlyList<string>>(Array.Empty<string>());

        private readonly List<(string text, float expireAt)> lines = new List<(string, float)>();
        private float hitMarkerUntil;
        private float damageFromUntil;

        /// <summary>시험이 시간을 직접 몰기 위한 자리. 컨테이너가 만드는 생성자에는 넣지 않는다(선택 인자는 해석이 실패한다).</summary>
        public Func<float> Clock { get; set; } = () => Time.unscaledTime;

        public ReadOnlyReactiveProperty<bool> HitMarker => hitMarker;

        /// <summary>내가 맞았을 때 쏜 사람이 있는 방위(도, 월드 +z가 0, +x가 90). 없으면 null.</summary>
        public ReadOnlyReactiveProperty<float?> DamageFromBearing => damageFromBearing;

        public ReadOnlyReactiveProperty<IReadOnlyList<string>> KillFeed => killFeed;

        private readonly ReactiveProperty<string> zoneText = new ReactiveProperty<string>("");
        private readonly ReactiveProperty<string> aliveText = new ReactiveProperty<string>("");

        public ReadOnlyReactiveProperty<string> ZoneText => zoneText;
        public ReadOnlyReactiveProperty<string> AliveText => aliveText;

        private readonly ReactiveProperty<bool> outsideZone = new ReactiveProperty<bool>(false);

        /// <summary>내 몸이 지금 원 밖이고 그 자리 구역 피해가 0보다 크면 true. 화면 가장자리 경고를 켠다.</summary>
        public ReadOnlyReactiveProperty<bool> OutsideZone => outsideZone;

        private readonly ReactiveProperty<float> hpRatio = new ReactiveProperty<float>(0f);
        private readonly ReactiveProperty<string> hpText = new ReactiveProperty<string>("");
        private readonly ReactiveProperty<bool> hpVisible = new ReactiveProperty<bool>(false);

        /// <summary>내 체력 비율(0~1). 값은 서버 스냅샷이 내 몸의 Health에 써 준 것을 읽기만 한다.</summary>
        public ReadOnlyReactiveProperty<float> HpRatio => hpRatio;
        public ReadOnlyReactiveProperty<string> HpText => hpText;

        /// <summary>내 몸이 없으면(탈락·관전) 체력 바를 숨긴다.</summary>
        public ReadOnlyReactiveProperty<bool> HpVisible => hpVisible;

        public struct Minimap
        {
            public Vector2 CurrentCenter;
            public float CurrentRadius;
            public bool HasNext;
            public Vector2 NextCenter;
            public float NextRadius;
            public float InitialRadius;
            public bool HasMe;
            public Vector2 Me;
        }

        /// <summary>
        /// 원 문구. 정수 틱 하나로 정한다. 다음 단계 공개는 앞 단계 축소가 끝나는 순간이라
        /// 단계 사이에 빈 틈은 없고, 마지막 단계는 줄어드는 동안 경고, 다 줄면 "구역 피해 증가 중"이다.
        /// </summary>
        public static string ZoneTextFor(IReadOnlyList<ZoneStage> stages, long tick, float interval,
                                         long firstRevealTick, int lastStageIndex)
        {
            ZoneStage? latest = null;
            for (int i = stages.Count - 1; i >= 0; i--)
            {
                if (stages[i].revealTick <= tick) { latest = stages[i]; break; }
            }
            if (latest == null)
            {
                if (firstRevealTick == long.MaxValue) return "";
                return $"구역 공개까지 {SecondsLeft(firstRevealTick - tick, interval)}초";
            }
            var s = latest.Value;
            if (s.index >= lastStageIndex && lastStageIndex > 0)
            {
                // 마지막 원이 다 줄고 나면 가만히 있어도 피해가 커져 몇 초 만에 쓰러진다 — 줄어드는 동안 미리 알린다.
                return tick >= s.shrinkEndTick ? "구역 피해 증가 중" : "마지막 원 · 닫히면 피해 증가";
            }
            if (tick < s.shrinkStartTick) return $"구역 축소까지 {SecondsLeft(s.shrinkStartTick - tick, interval)}초";
            if (tick < s.shrinkEndTick) return "구역 축소 중";
            return "구역 축소 중";   // 다음 단계 메시지가 오기 전 한두 틱 — 문구가 깜박이지 않게 그대로 둔다
        }

        private static int SecondsLeft(long ticks, float interval)
            => Mathf.Max(0, Mathf.CeilToInt(ticks * interval - 1e-4f));

        public MountedCombatHudViewModel(MountedPredictedHits predictedHits, MountedArcheryClientState state,
                                         IPlayerContext playerContext, ISubscriber<WorldEventBatchToC> batchSubscriber,
                                         GameFramework.World.EntityRegistry entityRegistry)
        {
            this.predictedHits = predictedHits;
            this.state = state;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;


            if (predictedHits != null)
            {
                predictedHits.Hit += OnPredictedHit;
            }
            if (state != null)
            {
                state.Eliminated += OnEliminated;
            }
            batchSubscriber?.Subscribe(OnBatch).AddTo(disposables);
        }

        /// <summary>화면 위쪽이 0도인 호의 각도: 쏜 방위에서 카메라가 보는 방위를 뺀다.</summary>
        public static float ScreenAngle(float bearingDeg, float cameraYawDeg)
            => Mathf.Repeat(bearingDeg - cameraYawDeg, 360f);

        public static float BearingDegrees(Vector3 from, Vector3 to)
            => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        public void OnPredictedHit()
        {
            hitMarkerUntil = Clock() + HitMarkerSeconds;
            hitMarker.Value = true;
        }

        public void OnDamaged(float bearingDeg)
        {
            damageFromUntil = Clock() + DamageFromSeconds;
            damageFromBearing.Value = bearingDeg;
        }

        public void OnEliminated(MountedArcheryClientState.Elimination e)
        {
            string text = string.IsNullOrEmpty(e.KillerId)
                ? $"{e.VictimName} 구역 밖에서 쓰러짐"
                : $"{e.KillerName} → {e.VictimName}";
            lines.Add((text, Clock() + KillLineSeconds));
            while (lines.Count > MaxKillLines)
            {
                lines.RemoveAt(0);
            }
            PublishFeed();
        }

        /// <summary>미니맵에 그릴 값. 위치는 월드 (x, z)다.</summary>
        public Minimap BuildMinimap()
        {
            double tick = state.RenderTick;
            var cur = state.CircleAtRender(tick);
            var map = new Minimap
            {
                CurrentCenter = cur.center,
                CurrentRadius = cur.radius,
                InitialRadius = state.InitialRadius,
            };
            if (state.TryGetNextCircle((long)System.Math.Floor(tick), out var next))
            {
                map.HasNext = true;
                map.NextCenter = next.center;
                map.NextRadius = next.radius;
            }
            if (TryGetMyPosition(out var me))
            {
                map.HasMe = true;
                map.Me = me;
            }
            return map;
        }

        private bool TryGetMyPosition(out Vector2 position)
        {
            position = default;
            string myId = playerContext?.entityId;
            if (myId == null) return false;
            var t = entityRegistry?.Get(myId)?.Get<GameFramework.World.Transform>();
            if (t == null) return false;
            position = new Vector2(t.Position.X, t.Position.Z);
            return true;
        }

        private void RefreshZone()
        {
            long tick = (long)System.Math.Floor(state.RenderTick);
            zoneText.Value = ZoneTextFor(state.ZoneStages, tick, state.TickInterval,
                                         state.FirstRevealTick, state.LastStageIndex);
            aliveText.Value = state.PlayerCount == 0 ? "" : $"생존 {state.AliveCount}";

            // 내 몸이 이미 쓰러졌으면 경고할 이유가 없다.
            bool warn = false;
            string myId = playerContext?.entityId;
            if (myId != null && state.IsEliminated(myId) == false && TryGetMyPosition(out var me))
            {
                var circle = state.CircleAtRender(state.RenderTick);
                float dps = MountedArcheryZone.DamagePerSecondAt(state.ZoneStages, tick, state.OvertimeDpsGrowth, state.TickInterval);
                warn = dps > 0f && MountedArcheryZone.IsOutside(me, circle);
            }
            outsideZone.Value = warn;
        }

        private void RefreshHp()
        {
            string myId = playerContext?.entityId;
            var health = myId == null ? null : entityRegistry?.Get(myId)?.Get<GameFramework.World.Health>();
            if (health == null)
            {
                hpVisible.Value = false;
                return;
            }
            hpVisible.Value = true;
            hpRatio.Value = health.Max > 0 ? Mathf.Clamp01((float)health.Current / health.Max) : 0f;
            hpText.Value = $"HP {health.Current}";
        }

        /// <summary>시간이 지난 표시를 거둔다.</summary>
        public void Tick()
        {
            RefreshHp();
            if (state != null)
            {
                RefreshZone();
            }
            float now = Clock();
            if (hitMarker.Value && now >= hitMarkerUntil)
            {
                hitMarker.Value = false;
            }
            if (damageFromBearing.Value.HasValue && now >= damageFromUntil)
            {
                damageFromBearing.Value = null;
            }
            int before = lines.Count;
            lines.RemoveAll(l => now >= l.expireAt);
            if (lines.Count != before)
            {
                PublishFeed();
            }
        }

        private void PublishFeed()
        {
            var copy = new string[lines.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = lines[i].text;
            }
            killFeed.Value = copy;
        }

        private void OnBatch(WorldEventBatchToC msg)
        {
            string myId = playerContext?.entityId;
            if (myId == null)
            {
                return;
            }
            foreach (var rec in msg.Events)
            {
                if (rec.EventCase != WorldEventToC.EventOneofCase.Damage)
                {
                    continue;
                }
                if (WorldEventWire.FromWire(rec) is GameFramework.World.DamageDealtEvent damage
                    && damage.targetId == myId && string.IsNullOrEmpty(damage.attackerId) == false)
                {
                    var me = entityRegistry?.Get(myId)?.Get<GameFramework.World.Transform>();
                    var attacker = entityRegistry?.Get(damage.attackerId)?.Get<GameFramework.World.Transform>();
                    if (me != null && attacker != null)
                    {
                        OnDamaged(BearingDegrees(
                            new Vector3(me.Position.X, 0f, me.Position.Z),
                            new Vector3(attacker.Position.X, 0f, attacker.Position.Z)));
                    }
                }
            }
        }

        public void Dispose()
        {
            if (predictedHits != null)
            {
                predictedHits.Hit -= OnPredictedHit;
            }
            if (state != null)
            {
                state.Eliminated -= OnEliminated;
            }
            disposables.Dispose();
            hitMarker.Dispose();
            damageFromBearing.Dispose();
            killFeed.Dispose();
            zoneText.Dispose();
            aliveText.Dispose();
            outsideZone.Dispose();
            hpRatio.Dispose();
            hpText.Dispose();
            hpVisible.Dispose();
        }
    }
}
