using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 로비 함성. 반복 재생 AudioSource 하나에 합성 소리를 건다. 녹음 파일로 바꿀 땐 SetClip만 바꾸면 된다.
    /// LobbyStage 자식이라 로비를 떠나면 함께 사라진다.
    /// </summary>
    public sealed class CrowdAmbience : MonoBehaviour
    {
        private const int SampleRate = 22050;

        private AudioSource _source;
        private float _swell;

        public float Volume => _source != null ? _source.volume : 0f;

        public static CrowdAmbience Build(Transform parent)
        {
            var go = new GameObject("CrowdAmbience");
            go.transform.SetParent(parent, false);
            var crowd = go.AddComponent<CrowdAmbience>();
            crowd._source = go.AddComponent<AudioSource>();
            crowd._source.loop = true;
            crowd._source.playOnAwake = false;
            crowd._source.spatialBlend = 0f;
            crowd._source.volume = 0f;
            var data = CrowdNoise.Generate(SampleRate, 4f, 11);
            var clip = AudioClip.Create("CrowdAmbience", data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            crowd.SetClip(clip);
            return crowd;
        }

        public void SetClip(AudioClip clip)
        {
            _source.clip = clip;
            if (UnityEngine.Application.isPlaying) _source.Play();
        }

        public void Swell() => _swell = 1f;

        public void Tick(float dt, float weight)
        {
            if (_source == null) return;
            _swell = Mathf.Max(0f, _swell - dt * CrowdMix.SwellDecayPerSecond);
            float target = CrowdMix.Target(weight, _swell);
            float k = dt <= 0f ? 0f : 1f - Mathf.Exp(-dt / CrowdMix.FollowSeconds);
            _source.volume += (target - _source.volume) * k;
        }

        private void OnDestroy()
        {
            if (_source != null && _source.clip != null)
            {
                var clip = _source.clip;
                if (UnityEngine.Application.isPlaying) Destroy(clip); else DestroyImmediate(clip);
            }
        }
    }
}
