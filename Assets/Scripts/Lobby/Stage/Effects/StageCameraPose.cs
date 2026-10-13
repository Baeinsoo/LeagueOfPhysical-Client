using UnityEngine;

namespace LOP
{
    /// <summary>로비 카메라 자세. 일반은 4부 그대로, 랭크는 낮고 가깝게 선수를 올려다보는 히어로 샷.</summary>
    public readonly struct StageCameraPose
    {
        public readonly Vector3 Position;
        public readonly Vector3 LookAt;

        public StageCameraPose(Vector3 position, Vector3 lookAt) { Position = position; LookAt = lookAt; }

        public static StageCameraPose For(bool ranked) => ranked
            ? new StageCameraPose(new Vector3(0f, 0.78f, 1.1f), new Vector3(0f, 1.32f, 10f))
            : new StageCameraPose(new Vector3(0f, 1.2f, 0f), new Vector3(0f, 1.0f, 10f));

        public static StageCameraPose Lerp(StageCameraPose a, StageCameraPose b, float t) =>
            new StageCameraPose(Vector3.Lerp(a.Position, b.Position, t), Vector3.Lerp(a.LookAt, b.LookAt, t));

        /// <summary>손에 든 중계 카메라처럼 아주 살짝 떠다님. weight 0이면 0.</summary>
        public static Vector3 Sway(float time, float weight) =>
            new Vector3(Mathf.Sin(time * 0.7f) * 0.025f, Mathf.Sin(time * 1.1f + 1f) * 0.015f, 0f) * weight;
    }
}
