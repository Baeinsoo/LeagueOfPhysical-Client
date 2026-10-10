using UnityEngine;

namespace LOP
{
    /// <summary>
    /// PolyOne 치비 머리 뼈의 로컬 좌표 관례. 뼈 축이 돌아가 있어 앞 = −Y, 위 = −X다.
    /// 얼굴 판(<see cref="ChibiFace"/>)과 모자(<see cref="ChibiLookApplier"/>)가 같은 값을 써야 서로 어긋나지 않는다.
    /// </summary>
    public static class ChibiHeadFrame
    {
        public const float Radius = 0.175f;
        public static readonly Vector3 Center = new Vector3(-0.12f, 0f, 0f);
        public static readonly Vector3 Facing = new Vector3(0f, -1f, 0f);
        public static readonly Vector3 Up = new Vector3(-1f, 0f, 0f);
    }
}
