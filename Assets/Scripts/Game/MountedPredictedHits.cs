using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 내가 쏜 화살이 <b>화면에 그려진</b> 남의 말을 지났는지 미리 재서 불꽃·✕·진동을 바로 낸다.
    /// 진짜 맞음(목숨·탈락)은 서버가 정한다 — 여기는 연출뿐이라 한 번 맞았다고 표시하면 되돌리지 않는다.
    /// 판정 식은 서버와 같은 <see cref="MountedArcheryHitTest"/>다.
    /// </summary>
    public class MountedPredictedHits : ILateTickable, IDisposable
    {
        /// <summary>그려진 몸 하나의 자리(뿌리 월드 위치)와 말 머리 방향(도).</summary>
        public readonly struct Target
        {
            public readonly string Id;
            public readonly Vector3 Position;
            public readonly float HeadingDeg;

            public Target(string id, Vector3 position, float headingDeg)
            {
                Id = id;
                Position = position;
                HeadingDeg = headingDeg;
            }
        }

        private readonly GameFramework.Runner.IRunner runner;
        private readonly MountedArcheryWorld world;
        private readonly ActorRegistry actorRegistry;
        private readonly IPlayerContext playerContext;
        private readonly IHaptics haptics;
        private readonly MountedArcheryClientState state;

        private readonly HashSet<(string, long)> spent = new HashSet<(string, long)>();
        private readonly Dictionary<(string, long), Vector3> lastPosition = new Dictionary<(string, long), Vector3>();
        private readonly HashSet<(string, long)> present = new HashSet<(string, long)>();
        private readonly List<(string, long)> scratch = new List<(string, long)>();
        private readonly List<Target> targets = new List<Target>();

        private ParticleSystem burst;

        /// <summary>내 화살이 남을 처음 맞힌 순간마다 한 번.</summary>
        public event Action Hit;

        public MountedPredictedHits(GameFramework.Runner.IRunner runner, MountedArcheryWorld world,
                                    ActorRegistry actorRegistry, IPlayerContext playerContext, IHaptics haptics,
                                    MountedArcheryClientState state)
        {
            this.runner = runner;
            this.world = world;
            this.actorRegistry = actorRegistry;
            this.playerContext = playerContext;
            this.haptics = haptics;
            this.state = state;
        }

        public bool IsSpent(string shooterId, long fireTick) => spent.Contains((shooterId, fireTick));

        /// <summary>
        /// 화살 한 발을 이번 프레임 구간으로 잰다. 구간은 지난 프레임 자리 → 지금 자리다.
        /// 맞으면 접점을 <paramref name="point"/>에 주고 참(그 발당 한 번만).
        /// </summary>
        public bool Evaluate(in ArcheryShot shot, string myId, float seconds, float deltaSeconds,
                             IReadOnlyList<Target> bodies, out Vector3 point)
        {
            point = default;
            // 남의 화살은 예측하지 않는다 — 서버 확정을 기다린다.
            if (shot.ShooterId != myId)
            {
                return false;
            }

            var key = (shot.ShooterId, shot.FireTick);
            Vector3 current = ArcheryTrajectory.PositionAt(shot, seconds);
            if (lastPosition.TryGetValue(key, out Vector3 previous) == false)
            {
                previous = ArcheryTrajectory.PositionAt(shot, Mathf.Max(0f, seconds - deltaSeconds));
            }
            lastPosition[key] = current;

            if (spent.Contains(key))
            {
                return false;
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                if (body.Id == myId)
                {
                    continue;
                }
                if (MountedArcheryHitTest.SegmentHits(previous, current, body.Position, body.HeadingDeg, out float along))
                {
                    spent.Add(key);
                    point = Vector3.Lerp(previous, current, along);
                    return true;
                }
            }
            return false;
        }

        public void LateTick()
        {
            if (runner?.tickUpdater == null || playerContext?.entityId == null)
            {
                return;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            string myId = playerContext.entityId;

            CollectTargets(myId);

            var shots = world.Shots;
            present.Clear();
            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                if (shot.ShooterId != myId)
                {
                    continue;
                }
                present.Add((shot.ShooterId, shot.FireTick));

                float seconds = Mathf.Max(0f, (float)((renderTick - shot.FireTick) * interval));
                if (Evaluate(shot, myId, seconds, Time.deltaTime, targets, out Vector3 point))
                {
                    OnHit(point);
                }
            }

            Prune();
        }

        private void CollectTargets(string myId)
        {
            targets.Clear();
            foreach (var actor in actorRegistry.All)
            {
                if (actor == null || actor.entityId == myId || state.IsEliminated(actor.entityId))
                {
                    continue;
                }
                var visual = actor.visualGameObject;
                if (visual == null)
                {
                    continue;
                }
                var t = visual.transform;
                targets.Add(new Target(actor.entityId, t.position, t.eulerAngles.y));
            }
        }

        // 목록에서 빠진 발의 기록은 버린다.
        private void Prune()
        {
            scratch.Clear();
            foreach (var key in lastPosition.Keys)
            {
                if (present.Contains(key) == false)
                {
                    scratch.Add(key);
                }
            }
            foreach (var key in scratch)
            {
                lastPosition.Remove(key);
                spent.Remove(key);
            }
        }

        private void OnHit(Vector3 point)
        {
            Burst(point);
            haptics?.Play(HapticPulse.Light);
            Hit?.Invoke();
        }

        private void Burst(Vector3 point)
        {
            if (burst == null)
            {
                var go = new GameObject("MountedHitBurst");
                burst = go.AddComponent<ParticleSystem>();
                var main = burst.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 0.3f;
                main.startLifetime = 0.35f;
                main.startSpeed = 4f;
                main.startSize = 0.18f;
                main.startColor = new Color(1f, 0.75f, 0.2f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = burst.emission;
                emission.rateOverTime = 0f;
                var shape = burst.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.05f;
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            burst.transform.position = point;
            burst.Emit(14);
        }

        public void Dispose()
        {
            if (burst != null)
            {
                var renderer = burst.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    UnityEngine.Object.Destroy(renderer.sharedMaterial);
                }
                UnityEngine.Object.Destroy(burst.gameObject);
                burst = null;
            }
        }
    }
}
