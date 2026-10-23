using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 말 활쏘기의 화살 그림. 시뮬과 같은 식에 같은 시각을 넣어 놓고, 땅·바위에 닿으면 거기 꽂아 둔다.
    /// 사람을 맞히는 연출은 MountedPredictedHits가 한다(Character 층은 레이캐스트에서 뺀다).
    /// </summary>
    public class MountedArrowView : ILateTickable, System.IDisposable
    {
        // 꽂힌 자리에 이만큼 남는다(초). 월드의 발사 목록(3초)보다 오래 — 어디에 떨어졌나를 볼 시간.
        private const float LandedSeconds = 6f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly MountedArcheryWorld world;
        private readonly MountedPredictedHits predictedHits;

        private readonly int worldLayerMask = LayerMask.GetMask("Default");

        // 목록의 자리(index)가 아니라 쏜 사람+쏜 틱으로 알아본다 — 앞 화살이 빠지면 자리가 당겨진다.
        private readonly Dictionary<(string shooterId, long fireTick), GameObject> flying =
            new Dictionary<(string, long), GameObject>();
        private readonly Dictionary<(string shooterId, long fireTick), (GameObject arrow, float removeAt)> landed =
            new Dictionary<(string, long), (GameObject, float)>();
        private readonly HashSet<(string, long)> alive = new HashSet<(string, long)>();
        private readonly List<(string, long)> scratch = new List<(string, long)>();

        private Mesh arrowMesh;
        private Material[] arrowMaterials;

        public MountedArrowView(GameFramework.Runner.IRunner runner, MountedArcheryWorld world,
                                MountedPredictedHits predictedHits)
        {
            this.runner = runner;
            this.world = world;
            this.predictedHits = predictedHits;
        }

        public void LateTick()
        {
            if (runner?.tickUpdater == null)
            {
                return;   // 씬 진입 초기거나 언로드 도중
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return;
            }
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            RemoveExpiredLanded();

            var shots = world.Shots;
            alive.Clear();
            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                var key = (shot.ShooterId, shot.FireTick);
                if (predictedHits.IsSpent(shot.ShooterId, shot.FireTick))
                {
                    continue;   // 맞은 자리에서 사라진다 — alive에 안 넣으면 아래에서 그림이 지워진다
                }
                if (landed.ContainsKey(key))
                {
                    continue;   // 이미 꽂혔다 — 그 그림은 landed가 들고 있다
                }
                alive.Add(key);

                float seconds = Mathf.Max(0f, (float)((renderTick - shot.FireTick) * interval));
                if (flying.TryGetValue(key, out var arrow) == false || arrow == null)
                {
                    arrow = CreateArrow();
                    flying[key] = arrow;
                }

                Vector3 position = ArcheryTrajectory.PositionAt(shot, seconds);
                Vector3 velocity = ArcheryTrajectory.VelocityAt(shot, seconds);
                Vector3 heading = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : Vector3.down;

                // 지난 프레임에서 지금까지 지나온 구간이 땅·바위를 통과했으면 거기서 멈춘다.
                float previousSeconds = Mathf.Max(0f, seconds - Time.deltaTime);
                if (previousSeconds < seconds
                    && Physics.Linecast(ArcheryTrajectory.PositionAt(shot, previousSeconds), position,
                                        out RaycastHit hit, worldLayerMask, QueryTriggerInteraction.Ignore))
                {
                    // 촉이 표면에 닿고 몸통은 밖에 남게 살짝 앞으로 밀어 넣는다.
                    arrow.transform.position = hit.point + heading * 0.1f;
                    arrow.transform.rotation = Quaternion.LookRotation(heading);
                    flying.Remove(key);
                    alive.Remove(key);
                    landed[key] = (arrow, Time.time + LandedSeconds);
                    continue;
                }

                arrow.transform.position = position;
                arrow.transform.rotation = Quaternion.LookRotation(heading);
            }

            // 목록에서 빠진 발(수명 끝·되감기로 취소)은 지운다.
            scratch.Clear();
            foreach (var pair in flying)
            {
                if (alive.Contains(pair.Key) == false)
                {
                    scratch.Add(pair.Key);
                }
            }
            foreach (var key in scratch)
            {
                if (flying[key] != null)
                {
                    Object.Destroy(flying[key]);
                }
                flying.Remove(key);
            }
        }

        private GameObject CreateArrow()
        {
            if (arrowMesh == null)
            {
                arrowMesh = ArcheryArrowMesh.Build();
            }
            if (arrowMaterials == null)
            {
                arrowMaterials = ArcheryArrowMesh.BuildMaterials();
            }

            // 그림일 뿐이다 — 콜라이더가 없다.
            var arrow = new GameObject("MountedArrow");
            arrow.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
            var renderer = arrow.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = arrowMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return arrow;
        }

        private void RemoveExpiredLanded()
        {
            if (landed.Count == 0)
            {
                return;
            }

            scratch.Clear();
            foreach (var pair in landed)
            {
                if (Time.time >= pair.Value.removeAt)
                {
                    scratch.Add(pair.Key);
                }
            }
            foreach (var key in scratch)
            {
                if (landed[key].arrow != null)
                {
                    Object.Destroy(landed[key].arrow);
                }
                landed.Remove(key);
            }
        }

        public void Dispose()
        {
            foreach (var pair in flying)
            {
                if (pair.Value != null)
                {
                    Object.Destroy(pair.Value);
                }
            }
            flying.Clear();

            foreach (var pair in landed)
            {
                if (pair.Value.arrow != null)
                {
                    Object.Destroy(pair.Value.arrow);
                }
            }
            landed.Clear();

            // 재질과 메시는 우리가 만든 것이라 아무도 대신 지워 주지 않는다.
            if (arrowMaterials != null)
            {
                foreach (var material in arrowMaterials)
                {
                    Object.Destroy(material);
                }
                arrowMaterials = null;
            }
            if (arrowMesh != null)
            {
                Object.Destroy(arrowMesh);
                arrowMesh = null;
            }
        }
    }
}
