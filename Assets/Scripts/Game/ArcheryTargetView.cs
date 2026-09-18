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
        private readonly ArcheryCourse course;
        private readonly ArcheryConsumed consumed;
        private readonly IUserDataStore userDataStore;

        private readonly List<ArcheryTarget> targets = new List<ArcheryTarget>();
        private readonly Dictionary<(int wave, int slot), GameObject> drawn
            = new Dictionary<(int, int), GameObject>();
        private readonly List<(int, int)> stale = new List<(int, int)>();
        private readonly HashSet<(int, int)> alive = new HashSet<(int, int)>();

        private Material _targetMaterial;
        private Material _trapMaterial;

        //  색깔당 한 장. 흐린 판(남의 것)은 같은 색을 어둡게 만든 별도 한 장이다 —
        //  불투명 재질에 알파만 낮추면 아무 변화가 없어서(투명 모드가 아니다) 명도로 가른다.
        private readonly Dictionary<(int band, bool dimmed), Material> _bandMaterials
            = new Dictionary<(int, bool), Material>();

        //  양궁 과녁면의 색차례(가운데 금색 → 빨강 → 파랑 → 검정 → 흰색). 띠가 더 많으면 돌려 쓴다.
        private static readonly Color[] BandColors =
        {
            new Color(1f, 0.85f, 0.1f), new Color(0.9f, 0.15f, 0.15f),
            new Color(0.15f, 0.35f, 0.9f), new Color(0.1f, 0.1f, 0.1f), Color.white,
        };

        public ArcheryTargetView(GameFramework.Runner.IRunner runner,
                                 GameFramework.World.IWorld world,
                                 ArcheryCourse course,
                                 ArcheryConsumed consumed,
                                 IUserDataStore userDataStore)
        {
            this.runner = runner;
            this.world = world;
            this.course = course;
            this.consumed = consumed;
            this.userDataStore = userDataStore;
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

            //  과녁이 이제 솟았다 떨어지므로 소수 틱이 필요하다 — 정수 틱으로 물으면 20ms 계단으로
            //  튄다. (예전에는 "과녁은 가만히 있다"가 근거였는데 그 전제가 깨졌다.)
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;

            //  어느 단계인지는 틱 단위 사실이라 여기는 정수로 묻는다.
            int step = course.IndexAt((long)System.Math.Floor(renderTick), world.GameplayStartTick);

            targets.Clear();
            if (step >= 0 && (course.StepCount == 0 || step < course.StepCount))
            {
                course.Fill(targets, step, world.GameplayStartTick);
            }

            alive.Clear();
            for (int i = 0; i < targets.Count; i++)
            {
                var key = (targets[i].WaveIndex, targets[i].SlotIndex);
                if (consumed.IsTargetGone(key.Item1, key.Item2))
                {
                    continue;   // 누군가 먹었다
                }
                if (ArcheryTargetMotion.IsAlive(targets[i], renderTick, (float)interval) == false)
                {
                    continue;   // 아직 안 솟았거나 이미 떨어졌다
                }
                alive.Add(key);

                //  공만 있던 시절엔 이 변수가 늘 구(sphere)였다 — 이제 판도 그리므로 모양을
                //  가리지 않는 이름을 쓴다(sphere라는 이름의 변수가 판을 들고 있으면 헷갈린다).
                if (drawn.TryGetValue(key, out var visual) == false || visual == null)
                {
                    if (targets[i].Shape == ArcheryTargetShape.Face)
                    {
                        visual = BuildFace(targets[i]);
                    }
                    else
                    {
                        visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        Object.Destroy(visual.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 서버가 한다
                    }
                    drawn[key] = visual;
                }

                if (targets[i].Shape == ArcheryTargetShape.Face)
                {
                    //  띠 색은 여기서 매 프레임 정한다(내 것/남의 것이 바뀔 수 있어서) — 캐시라 싸다.
                    SetBandMaterials(visual, targets[i]);
                }
                else
                {
                    var renderer = visual.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = targets[i].IsTrap ? TrapMaterial() : TargetMaterial();
                    }
                }

                visual.transform.position = ArcheryTargetMotion.PositionAt(targets[i], renderTick, (float)interval);

                if (targets[i].Shape == ArcheryTargetShape.Face)
                {
                    //  실린더의 축(y)을 과녁이 보는 쪽에 맞춘다 — 그래야 원판의 앞면이 사수를 향한다.
                    //  판정이 쓰는 Facing을 그대로 쓴다(그림용 각도를 따로 두면 어긋난다).
                    visual.transform.rotation = Quaternion.FromToRotation(Vector3.up, targets[i].Facing);
                }
                else
                {
                    //  공은 예전 그대로 — 보이는 크기가 곧 맞는 크기다.
                    visual.transform.localScale = Vector3.one * (targets[i].Radius * 2f);
                }
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

        //  판은 띠마다 원판을 하나씩 겹쳐 그린다. 가장 바깥이 뒤, 가운데가 앞이다.
        //  유니티 실린더는 y축으로 2만큼 길고 반지름이 0.5라, 지름 배율은 (반경 x 2)이고
        //  y 배율이 곧 두께의 절반이다.
        private GameObject BuildFace(in ArcheryTarget target)
        {
            var root = new GameObject("archery-face");
            var bands = target.Bands;
            int count = bands == null || bands.Count == 0 ? 1 : bands.Count;

            for (int i = count - 1; i >= 0; i--)
            {
                float ratio = bands == null || bands.Count == 0 ? 1f : bands[i].OuterRatio;
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(disc.GetComponent<Collider>());   // 그림일 뿐이다 — 판정은 서버가 한다
                disc.transform.SetParent(root.transform, worldPositionStays: false);

                //  안쪽 띠일수록 사수 쪽으로 살짝 당겨 앞에 오게 한다(z-파이팅 방지).
                disc.transform.localPosition = new Vector3(0f, 0.01f * (count - i), 0f);
                float diameter = target.Radius * 2f * ratio;
                disc.transform.localScale = new Vector3(diameter, 0.02f, diameter);

                //  띠 색은 아래 SetBandMaterials가 매 프레임 정한다(내 것/남의 것이 바뀔 수 있어서).
                disc.name = "band" + i;
            }

            return root;
        }

        //  남의 레인도 보이지만 쏠 이유가 없다는 것이 한눈에 읽혀야 한다.
        //  주인이 없으면(원형 맵) 전부 내 것처럼 밝게 — 예전 그대로다.
        private void SetBandMaterials(GameObject face, in ArcheryTarget target)
        {
            bool mine = string.IsNullOrEmpty(target.OwnerUserId)
                     || target.OwnerUserId == userDataStore.user.id;

            for (int i = 0; i < face.transform.childCount; i++)
            {
                var renderer = face.transform.GetChild(i).GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }
                //  자식 이름이 "band{i}"라 그 번호가 곧 띠 번호다(BuildFace가 그렇게 붙인다).
                int band = int.Parse(renderer.name.Substring("band".Length));
                renderer.sharedMaterial = BandMaterial(band, dimmed: mine == false);
            }
        }

        private Material BandMaterial(int bandIndex, bool dimmed)
        {
            var key = (bandIndex % BandColors.Length, dimmed);
            if (_bandMaterials.TryGetValue(key, out var material) == false || material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                Color color = BandColors[key.Item1];
                material = new Material(shader) { color = dimmed ? color * 0.35f : color };
                _bandMaterials[key] = material;
            }
            return material;
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

            foreach (var pair in _bandMaterials)
            {
                Object.Destroy(pair.Value);
            }
            _bandMaterials.Clear();
        }
    }
}
