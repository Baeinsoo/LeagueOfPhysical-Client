namespace LOP
{
    /// <summary>연출마다 세기 0..1. 모드가 바뀌면 0.4초에 걸쳐 섞고, 섞는 중 다시 바뀌면 지금 세기에서 새 목표로.</summary>
    public sealed class StageEffectWeights
    {
        public const float BlendSeconds = 0.4f;

        private readonly float[] _from = new float[StageEffectSet.All.Length];
        private readonly float[] _to = new float[StageEffectSet.All.Length];
        private readonly float[] _now = new float[StageEffectSet.All.Length];
        private float _t = 1f;

        public StageEffectWeights(bool ranked)
        {
            SetRanked(ranked);
            Snap();
        }

        public void SetRanked(bool ranked)
        {
            foreach (var e in StageEffectSet.All)
            {
                _from[(int)e] = _now[(int)e];
                _to[(int)e] = StageEffectSet.IsOn(e, ranked) ? 1f : 0f;
            }
            _t = 0f;
        }

        public void Step(float dt)
        {
            if (_t >= 1f) return;
            _t = UnityEngine.Mathf.Min(1f, _t + dt / BlendSeconds);
            for (int i = 0; i < _now.Length; i++) _now[i] = UnityEngine.Mathf.Lerp(_from[i], _to[i], _t);
        }

        /// <summary>섞지 않고 목표로 바로 간다(시작할 때·미리보기).</summary>
        public void Snap()
        {
            for (int i = 0; i < _now.Length; i++) _now[i] = _from[i] = _to[i];
            _t = 1f;
        }

        public float Weight(StageEffect e) => _now[(int)e];
    }
}
