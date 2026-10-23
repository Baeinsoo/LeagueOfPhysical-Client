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

        /// <summary>시간이 지난 표시를 거둔다.</summary>
        public void Tick()
        {
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
        }
    }
}
