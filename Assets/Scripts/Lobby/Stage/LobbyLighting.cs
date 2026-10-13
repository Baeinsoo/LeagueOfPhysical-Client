using UnityEngine;

namespace LOP
{
    /// <summary>터널의 빛 묶음 — 방향광·끝 빛 점광·발밑 스포트라이트·끝 빛 판.</summary>
    public sealed class StageLights
    {
        public Light Sun;
        public Light EndPoint;
        public Light Spot;
        /// <summary>출구 너머 경기장 배경 판.</summary>
        public Renderer EndGlow;
        /// <summary>발밑 스포트라이트가 바닥에 만드는 밝은 원. LOP/Toon은 방향광만 받아 실제 스포트라이트가 보이지 않는다.</summary>
        public Renderer FootPool;
    }

    /// <summary>일반/랭크 조명을 0.4초에 걸쳐 섞어 바꾼다. 섞는 중에 다시 바뀌면 지금 보이는 값에서 새 목표로.</summary>
    public sealed class LobbyLighting : MonoBehaviour
    {
        public const float BlendSeconds = 0.4f;

        private StageLights _lights;
        private StageLighting _from;
        private StageLighting _to;
        private float _t = 1f;
        private float _dim = 1f;

        private static readonly Color FloorColor = new Color32(0x26, 0x24, 0x58, 0xFF);
        private static readonly Color PoolColor = new Color32(0xFF, 0xF8, 0xE8, 0xFF);

        public StageLighting Current { get; private set; }

        /// <summary>조명 쇼가 쓰는 어둡게 배율(1 = 그대로).</summary>
        public float Dim => _dim;

        public void SetDim(float dim)
        {
            if (Mathf.Approximately(dim, _dim)) return;
            _dim = dim;
            if (_lights != null) Apply(Current, _lights, _dim);
        }

        public void Init(StageLights lights, bool ranked)
        {
            _lights = lights;
            _from = _to = Current = StageLighting.For(ranked);
            _t = 1f;
            Apply(Current, _lights, _dim);
        }

        public void SetRanked(bool ranked)
        {
            _from = Current;
            _to = StageLighting.For(ranked);
            _t = 0f;
        }

        public void Step(float dt)
        {
            if (_lights == null || _t >= 1f) return;
            _t = Mathf.Min(1f, _t + dt / BlendSeconds);
            Current = StageLighting.Lerp(_from, _to, _t);
            Apply(Current, _lights, _dim);
        }

        private void Update() => Step(Time.deltaTime);

        public static void Apply(StageLighting v, StageLights lights, float dim = 1f)
        {
            if (lights.Sun != null)
            {
                lights.Sun.intensity = v.SunIntensity * dim;
                lights.Sun.color = v.SunColor;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = v.Ambient * (0.35f + 0.65f * dim);
            if (lights.EndPoint != null)
            {
                lights.EndPoint.color = v.EndPointColor;
                lights.EndPoint.intensity = v.EndPointIntensity;
                lights.EndPoint.range = v.EndPointRange;
            }
            if (lights.Spot != null)
            {
                lights.Spot.enabled = v.Spotlight;
                lights.Spot.intensity = v.SpotIntensity;
                lights.Spot.spotAngle = v.SpotAngle;
            }
            if (lights.FootPool != null)
            {
                lights.FootPool.enabled = v.Spotlight;
                //  꺼질 때·켜질 때 세기를 따라 바닥색에서 크림색으로 차오른다.
                var pool = Color.Lerp(FloorColor, PoolColor, Mathf.Clamp01(v.SpotIntensity / StageLighting.For(true).SpotIntensity)) * dim;   // 조명 쇼의 어두운 순간엔 발밑 원도 꺼진다
                pool.a = 1f;
                var m = lights.FootPool.sharedMaterial;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", pool);
                if (m.HasProperty("_ShadowColor")) m.SetColor("_ShadowColor", pool);
            }
            if (lights.EndGlow != null)
            {
                var m = lights.EndGlow.sharedMaterial;
                var glow = v.EndGlow * (0.3f + 0.7f * dim);
                glow.a = 1f;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", glow);
                if (m.HasProperty("_ShadowColor")) m.SetColor("_ShadowColor", glow);
            }
        }
    }
}
