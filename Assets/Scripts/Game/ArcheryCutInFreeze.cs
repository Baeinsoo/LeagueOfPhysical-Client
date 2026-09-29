using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 컷인 동안 게임 화면을 흑백·고대비로 얼린다 — 전역 볼륨 하나를 코드로 만들고 가중치만 움직인다(게임 시계는 멈추지 않는다).
    /// 카메라 후처리는 슬라이스 2에서 이미 켰다.
    /// </summary>
    public sealed class ArcheryCutInFreeze : IStartable, System.IDisposable
    {
        private GameObject go;
        private Volume volume;
        private VolumeProfile profile;

        public float Weight
        {
            get => volume != null ? volume.weight : 0f;
            set { if (volume != null) { volume.weight = Mathf.Clamp01(value); } }
        }

        public void Start()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(-100f);
            ca.contrast.Override(35f);
            ca.postExposure.Override(-0.4f);
            go = new GameObject("CutInFreeze");
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            volume.weight = 0f;
        }

        public void Dispose()
        {
            if (go != null) { Kill(go); go = null; }
            if (profile != null)
            {
                foreach (var c in profile.components) { Kill(c); }
                Kill(profile);
                profile = null;
            }
            volume = null;
        }

        private static void Kill(Object o)
        {
            if (UnityEngine.Application.isPlaying) { Object.Destroy(o); } else { Object.DestroyImmediate(o); }
        }
    }
}
