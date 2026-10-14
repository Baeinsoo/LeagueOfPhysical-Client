using UnityEngine;

namespace LOP
{
    /// <summary>출전 카메라 — 지금 자세(일반이든 랭크 히어로든)에서 0.3초에 걸쳐 선수 어깨 뒤로 들어가 함께 달린다.</summary>
    public static class RunOutCamera
    {
        public static StageCameraPose Behind(float playerZ) =>
            new StageCameraPose(new Vector3(0.5f, 1.9f, playerZ - 3.6f), new Vector3(0f, 1.0f, playerZ + 12f));

        public static StageCameraPose At(StageCameraPose start, float playerZ, float t) =>
            StageCameraPose.Lerp(start, Behind(playerZ), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / RunOutTimeline.TurnEnd)));
    }
}
