using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 로비 연출 3D 부품을 한 번에 세우고, Pose(시간, 세기, 쇼 시각)로 모양·밝기를 정한다.
    /// 같은 시간을 넣으면 같은 그림이 나와서 미리보기도 이 함수를 쓴다.
    /// </summary>
    public sealed class StageRig : MonoBehaviour
    {
        private static readonly string[] FlagColors = { "#FFD23F", "#E8322F", "#28B9E8", "#9BE15D", "#F25FB0", "#FF8A00" };
        private static readonly Color RunwayOff = new Color32(0x2A, 0x28, 0x60, 0xFF);
        private static readonly Color StageWhite = new Color32(0xEA, 0xF1, 0xFF, 0xFF);
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ShadowColor = Shader.PropertyToID("_ShadowColor");

        private TunnelLayout _layout;
        private GameObject _bunting, _confetti, _runway, _search, _flashes, _strips;
        private readonly List<Renderer> _runwayTiles = new List<Renderer>();
        private readonly List<Renderer> _stripBars = new List<Renderer>();
        private readonly List<(Transform t, float speed, float spin, float phase, float x0)> _paper = new List<(Transform, float, float, float, float)>();
        private readonly List<(Transform pivot, Renderer r, float phase, float dir)> _beams = new List<(Transform, Renderer, float, float)>();
        private readonly List<Renderer> _flashDots = new List<Renderer>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private MaterialPropertyBlock _mpb;

        public int RunwayCount => _runwayTiles.Count;
        public int StripCount => _stripBars.Count;
        public int ConfettiCount => _paper.Count;
        public int FlagCount { get; private set; }
        public int BeamCount => _beams.Count;
        public int FlashCount => _flashDots.Count;
        public StageSigns Signs { get; set; }
        public CrowdAmbience Crowd { get; set; }

        public static StageRig Build(Transform parent, TunnelLayout layout)
        {
            var go = new GameObject("StageRig");
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<StageRig>();
            rig._layout = layout;
            rig._mpb = new MaterialPropertyBlock();
            rig.BuildBunting();
            rig.BuildConfetti();
            rig.BuildRunway();
            rig.BuildStrips();
            rig.BuildSearchlights();
            rig.BuildFlashes();
            return rig;
        }

        private GameObject Group(string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(transform, false);
            return g;
        }

        private static GameObject Piece(PrimitiveType type, string name, Transform parent, Material m, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private void BuildBunting()
        {
            _bunting = Group("Bunting");
            var mesh = GlowMeshes.Triangle(0.26f, 0.3f);
            _meshes.Add(mesh);
            var mats = new Material[FlagColors.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = LobbyTunnel.MakeGlowMaterial(FlagColors[i]);
            float half = _layout.Width / 2f;
            for (int z = 3; z <= 15; z += 3)
            {
                const int n = 14;
                for (int i = 0; i < n; i++)
                {
                    float t = (i + 0.5f) / n;
                    float x = -half + 0.3f + t * (_layout.Width - 0.6f);
                    float sag = Mathf.Sin(t * Mathf.PI) * 0.55f;
                    var f = new GameObject($"Flag_{z}_{i}");
                    f.transform.SetParent(_bunting.transform, false);
                    f.transform.localPosition = new Vector3(x, _layout.BeamY - 0.2f - sag, z - 0.3f);
                    f.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = f.AddComponent<MeshRenderer>();
                    r.sharedMaterial = mats[(i + z) % mats.Length];
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    FlagCount++;
                }
            }
        }

        private void BuildConfetti()
        {
            _confetti = Group("Confetti");
            var mats = new Material[FlagColors.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = LobbyTunnel.MakeGlowMaterial(FlagColors[i]);
            var rnd = new System.Random(7);   // 같은 씨앗 — 미리보기·시험이 같은 그림
            for (int i = 0; i < 100; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * (_layout.Width - 0.6f);
                var p = Piece(PrimitiveType.Cube, $"Paper_{i}", _confetti.transform, mats[i % mats.Length],
                    new Vector3(x, 0f, 1.5f + (float)rnd.NextDouble() * 16f), new Vector3(0.07f, 0.11f, 0.004f));
                _paper.Add((p.transform, 0.25f + (float)rnd.NextDouble() * 0.35f, (float)rnd.NextDouble() * 3f, (float)rnd.NextDouble() * _layout.Height, x));
            }
        }

        private void BuildRunway()
        {
            _runway = Group("Runway");
            var m = LobbyTunnel.MakeGlowMaterial("#EAF1FF");
            for (int z = 1; z < (int)_layout.Length; z++)
                foreach (float x in new[] { -1.6f, 1.6f })
                    _runwayTiles.Add(Piece(PrimitiveType.Cube, $"Run_{z}_{x}", _runway.transform, m, new Vector3(x, 0.015f, z), new Vector3(0.22f, 0.03f, 0.42f)).GetComponent<Renderer>());
        }

        private void BuildStrips()
        {
            _strips = Group("PillarStrips");
            var m = LobbyTunnel.MakeGlowMaterial("#EAF1FF");
            float px = _layout.Width / 2f - _layout.PillarSize / 2f - 0.27f;
            foreach (float z in _layout.PillarZs())
                foreach (float s in new[] { -1f, 1f })
                    _stripBars.Add(Piece(PrimitiveType.Cube, $"Strip_{z}_{s}", _strips.transform, m, new Vector3(s * px, _layout.Height / 2f, z), new Vector3(0.05f, _layout.Height * 0.82f, 0.14f)).GetComponent<Renderer>());
        }

        private void BuildSearchlights()
        {
            _search = Group("Searchlights");
            var mesh = GlowMeshes.Cone(0.15f, 2.4f, 26f, 24);
            _meshes.Add(mesh);
            float[] xs = { -7f, -3f, 3f, 7f };
            for (int i = 0; i < xs.Length; i++)
            {
                var pivot = new GameObject($"Beam_{i}").transform;
                pivot.SetParent(_search.transform, false);
                pivot.localPosition = new Vector3(xs[i], -1f, 36f);
                var cone = new GameObject("Cone");
                cone.transform.SetParent(pivot, false);
                cone.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = cone.AddComponent<MeshRenderer>();
                var mat = LobbyTunnel.MakeStageGlowMaterial(new Color(0.92f, 0.95f, 1f, 0.2f));
                if (mat.HasProperty("_FadeAlongV")) mat.SetFloat("_FadeAlongV", 0.8f);
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _beams.Add((pivot, r, i * 1.7f, i % 2 == 0 ? -1f : 1f));
            }
        }

        private void BuildFlashes()
        {
            _flashes = Group("Flashes");
            for (int i = 0; i < 6; i++)
            {
                var m = LobbyTunnel.MakeStageGlowMaterial(new Color(1f, 1f, 1f, 0f));
                var q = Piece(PrimitiveType.Quad, $"Flash_{i}", _flashes.transform, m, Vector3.zero, Vector3.one * 0.44f);
                _flashDots.Add(q.GetComponent<Renderer>());
            }
        }

        public void Pose(float time, StageEffectWeights w, float showT)
        {
            float bunting = w.Weight(StageEffect.Bunting), confetti = w.Weight(StageEffect.Confetti);
            float runway = w.Weight(StageEffect.Runway), search = w.Weight(StageEffect.Searchlights);
            float flash = w.Weight(StageEffect.Flashes), show = w.Weight(StageEffect.LightShow);
            //  쇼의 어두운 순간엔 빛나는 부품도 함께 꺼진다 — 기둥 띠만 차례로 켜져야 쇼가 읽힌다.
            float dim = LightShowCurve.Dim(showT);

            _bunting.SetActive(bunting > 0.5f);
            _confetti.SetActive(confetti > 0.01f);
            if (confetti > 0.01f)
            {
                foreach (var p in _paper)
                {
                    float y = _layout.Height - 0.3f - Mathf.Repeat(p.phase + time * p.speed, _layout.Height - 0.3f);
                    p.t.localPosition = new Vector3(p.x0 + Mathf.Sin(time * 1.5f + p.phase) * 0.15f, y, p.t.localPosition.z);
                    p.t.localRotation = Quaternion.Euler(time * p.spin * 57f, time * 75f, 0f);
                }
            }

            _runway.SetActive(runway > 0.01f);
            if (runway > 0.01f)
                foreach (var r in _runwayTiles)
                    SetGlow(r, Color.Lerp(RunwayOff, StageWhite, RunwayPulse.Brightness(r.transform.localPosition.z, time) * runway) * dim);

            _strips.SetActive(show > 0.01f);
            if (show > 0.01f)
            {
                float last = _layout.PillarZs()[_layout.PillarZs().Count - 1];
                foreach (var r in _stripBars)
                    SetGlow(r, StageWhite * (LightShowCurve.Strip(r.transform.localPosition.z, last, _layout.PillarSpacing, showT) * show));
            }

            _search.SetActive(search > 0.01f);
            if (search > 0.01f)
                foreach (var b in _beams)
                {
                    b.pivot.localRotation = Quaternion.Euler(-14f + Mathf.Sin(time * 0.4f + b.phase) * 7f, 0f, Mathf.Sin(time * 0.6f * b.dir + b.phase) * 31.5f);
                    SetStageGlow(b.r, new Color(0.92f, 0.95f, 1f, 0.2f * search * dim));
                }

            _flashes.SetActive(flash > 0.5f);
            if (flash > 0.5f)
                for (int i = 0; i < _flashDots.Count; i++)
                {
                    //  각 플래시가 자기 주기(1.2~31초)마다 0.12초 번쩍. 자리는 주기 번호로 정해 같은 시간엔 같은 그림.
                    float period = 1.2f + Hash(i, 0) * 30f;
                    int cycle = Mathf.FloorToInt((time + Hash(i, 1) * period) / period);
                    float local = time + Hash(i, 1) * period - cycle * period;
                    float side = Hash(i, cycle) < 0.5f ? -1f : 1f;
                    var tr = _flashDots[i].transform;
                    tr.localPosition = new Vector3(side * (_layout.Width / 2f - 0.02f), 1.2f + Hash(i, cycle + 7) * 1.6f, 6f + Hash(i, cycle + 13) * 14f);
                    tr.localRotation = Quaternion.Euler(0f, side * -90f, 0f);
                    SetStageGlow(_flashDots[i], new Color(1f, 1f, 1f, Mathf.Max(0f, 1f - local / 0.12f) * dim));
                }
        }

        private static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private void SetGlow(Renderer r, Color c)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, c);
            _mpb.SetColor(ShadowColor, c);
            r.SetPropertyBlock(_mpb);
        }

        private void SetStageGlow(Renderer r, Color c)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColor, c);
            r.SetPropertyBlock(_mpb);
        }

        private void OnDestroy()
        {
            foreach (var m in _meshes)
            {
                if (m == null) continue;
                if (UnityEngine.Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            }
        }
    }
}
