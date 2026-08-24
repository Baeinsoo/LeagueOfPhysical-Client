using UnityEngine;

namespace LOP
{
    /// <summary>가속 없는 외삽(등속 직선). 외삽 대상이 아예 없는 게임(FlapWang)이 등록한다 — <see cref="NoServerCorrection"/> 대칭.</summary>
    public class ZeroExtrapolationAcceleration : IExtrapolationAcceleration
    {
        public Vector3 Acceleration => Vector3.zero;
    }
}
