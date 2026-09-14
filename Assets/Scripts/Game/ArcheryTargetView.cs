using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 지금 떠 있는 과녁을 그린다. <b>통신으로 받는 것이 아니라</b> 서버와 같은 커널에 같은 씨앗을
    /// 넣어 각자 계산한다 — 그래서 핑과 무관하게 모두가 같은 순간에 같은 과녁을 본다.
    /// 과녁은 엔티티가 아니라서 뷰가 직접 생성 커널을 부른다(<see cref="ArcheryArrowView"/>와 같은 짝).
    /// <para><b>함정은 색으로만 갈린다</b> — 크기가 성한 과녁과 겹치게 데이터를 넣었기 때문에,
    /// 색을 못 보면 구분할 방법이 없다.</para>
    /// </summary>
    public class ArcheryTargetView : ILateTickable, System.IDisposable
    {
        private readonly GameFramework.Runner.IRunner runner;
        private readonly GameFramework.World.IWorld world;
        private readonly ArcheryConfig config;
        private readonly ArcheryConsumed consumed;
        private readonly IMatchSeed matchSeed;

        private readonly List<ArcheryTarget> targets = new List<ArcheryTarget>();
        private readonly Dictionary<(int wave, int slot), GameObject> drawn
            = new Dictionary<(int, int), GameObject>();
        private readonly List<(int, int)> stale = new List<(int, int)>();
        private readonly HashSet<(int, int)> alive = new HashSet<(int, int)>();

        private Material _targetMaterial;
        private Material _trapMaterial;

        public ArcheryTargetView(GameFramework.Runner.IRunner runner,
                                 GameFramework.World.IWorld world,
                                 ArcheryConfig config,
                                 ArcheryConsumed consumed,
                                 IMatchSeed matchSeed)
        {
            this.runner = runner;
            this.world = world;
            this.config = config;
            this.consumed = consumed;
            this.matchSeed = matchSeed;
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

            //  과녁이 떠 있나 없나는 틱 단위 사실이라 소수 틱으로 물을 것이 없다 — 정수 틱으로 묻는다.
            //  (화살의 *자세*는 소수 틱이 필요하지만 과녁은 가만히 있다.)
            long renderTick = (long)System.Math.Floor((runner.tickUpdater.elapsedTime - interval) / interval);
            int wave = ArcheryWaveGenerator.WaveIndexAt(renderTick, world.GameplayStartTick, config);

            targets.Clear();
            if (wave >= 0)
            {
                ArcheryWaveGenerator.Fill(targets, matchSeed.Value, wave, config);
            }

            alive.Clear();
            for (int i = 0; i < targets.Count; i++)
            {
                var key = (targets[i].WaveIndex, targets[i].SlotIndex);
                if (consumed.IsTargetGone(key.Item1, key.Item2))
                {
                    continue;   // 누군가 먹었다
                }
                alive.Add(key);

                if (drawn.TryGetValue(key, out var sphere) == false || sphere == null)
                {
                    sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.Destroy(sphere.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 서버가 한다
                    drawn[key] = sphere;
                }

                var renderer = sphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = targets[i].IsTrap ? TrapMaterial() : TargetMaterial();
                }

                sphere.transform.position = targets[i].Origin;
                //  보이는 크기가 곧 맞는 크기여야 한다 — 판정 반경이 0.25면 지름 0.5짜리 공이다.
                sphere.transform.localScale = Vector3.one * (targets[i].Radius * 2f);
            }

            stale.Clear();
            foreach (var pair in drawn)
            {
                if (alive.Contains(pair.Key) == false)
                {
                    stale.Add(pair.Key);
                }
            }
            for (int i = 0; i < stale.Count; i++)
            {
                Object.Destroy(drawn[stale[i]]);
                drawn.Remove(stale[i]);
            }
        }

        //  과녁마다 material을 새로 만들면 재질 인스턴스가 계속 쌓인다 — 한 장을 돌려 쓴다.
        private Material TargetMaterial()
        {
            if (_targetMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                //  임시 그림이라 실물보다 눈에 띄는 것이 우선이다. 붉은 화살과 갈리게 노랑.
                _targetMaterial = new Material(shader) { color = new Color(1f, 0.85f, 0.1f) };
            }
            return _targetMaterial;
        }

        //  함정은 성한 과녁과 크기가 겹치게 뒀다 — 색이 유일한 단서다. 노랑(성한 것)과
        //  가장 멀고, 붉은 화살과도 갈리는 쪽으로 고른다.
        private Material TrapMaterial()
        {
            if (_trapMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                _trapMaterial = new Material(shader) { color = new Color(0.15f, 0.2f, 0.9f) };
            }
            return _trapMaterial;
        }

        public void Dispose()
        {
            foreach (var pair in drawn)
            {
                Object.Destroy(pair.Value);
            }
            drawn.Clear();

            if (_targetMaterial != null)
            {
                Object.Destroy(_targetMaterial);
                _targetMaterial = null;
            }

            if (_trapMaterial != null)
            {
                Object.Destroy(_trapMaterial);
                _trapMaterial = null;
            }
        }
    }
}
