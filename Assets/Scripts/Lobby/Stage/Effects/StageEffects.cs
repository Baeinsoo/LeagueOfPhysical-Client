using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 로비 연출 지휘. 연출 세기를 섞고(0.4초), 일반→랭크 순간에만 조명 쇼를 돌리며, 카메라 자세를 섞는다.
    /// 매 프레임 StageRig·간판·함성·조명(어둡게)·카메라에 값을 넘긴다.
    /// </summary>
    public sealed class StageEffects : MonoBehaviour
    {
        public const float CameraBlendSeconds = 0.6f;

        private StageRig _rig;
        private LobbyLighting _lighting;
        private Camera _camera;
        private bool _ranked;
        private float _time;
        private float _camT = 1f;
        private StageCameraPose _camFrom, _camTo, _camNow;

        public StageEffectWeights Weights { get; private set; }
        public float ShowT { get; private set; } = float.PositiveInfinity;

        /// <summary>출전 연출이 카메라를 넘겨받는 동안 true — 무대 카메라 자세를 쓰지 않는다.</summary>
        public bool CameraOverride { get; set; }

        public void Init(StageRig rig, LobbyLighting lighting, Camera cam, bool ranked)
        {
            _rig = rig;
            _lighting = lighting;
            _camera = cam;
            _ranked = ranked;
            Weights = new StageEffectWeights(ranked);
            _camFrom = _camTo = _camNow = StageCameraPose.For(ranked);
            Tick(0f);
        }

        public void SetRanked(bool ranked)
        {
            if (ranked == _ranked) return;
            _ranked = ranked;
            Weights.SetRanked(ranked);
            ShowT = ranked ? 0f : float.PositiveInfinity;
            _camFrom = _camNow;
            _camTo = StageCameraPose.For(ranked);
            _camT = 0f;
            if (ranked && _rig != null && _rig.Crowd != null) _rig.Crowd.Swell();
        }

        public void Tick(float dt)
        {
            _time += dt;
            Weights.Step(dt);
            if (!float.IsPositiveInfinity(ShowT))
            {
                ShowT += dt;
                if (ShowT >= LightShowCurve.Duration) ShowT = float.PositiveInfinity;
            }
            if (_lighting != null) _lighting.SetDim(_ranked ? LightShowCurve.Dim(ShowT) : 1f);
            if (_rig != null)
            {
                _rig.Pose(_time, Weights, ShowT);
                if (_rig.Signs != null) _rig.Signs.SetWeight(Weights.Weight(StageEffect.Signs) * LightShowCurve.Dim(ShowT));
                if (_rig.Crowd != null) _rig.Crowd.Tick(dt, Weights.Weight(StageEffect.Crowd));
            }
            if (_camT < 1f) _camT = Mathf.Min(1f, _camT + dt / CameraBlendSeconds);
            _camNow = StageCameraPose.Lerp(_camFrom, _camTo, Mathf.SmoothStep(0f, 1f, _camT));
            if (_camera != null && !CameraOverride)
            {
                _camera.transform.position = _camNow.Position + StageCameraPose.Sway(_time, Weights.Weight(StageEffect.HeroCamera));
                _camera.transform.LookAt(_camNow.LookAt);
            }
        }

        private void LateUpdate() => Tick(Time.deltaTime);
    }
}
