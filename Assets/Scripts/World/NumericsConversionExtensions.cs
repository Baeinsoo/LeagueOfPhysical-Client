using UnityEngine;

namespace LOP
{
    /// <summary>
    /// UnityEngine → System.Numerics 변환. 코어(noEngineReferences) 경계 어댑터 전용 —
    /// 코어는 System.Numerics만, 클라(엔진)는 UnityEngine만 보고 이 변환이 둘을 잇는다.
    /// (World→Unity 역변환은 pull 소비가 생기는 후속 슬라이스에서 추가.)
    /// </summary>
    public static class NumericsConversionExtensions
    {
        public static System.Numerics.Vector3 ToNumerics(this Vector3 v)
            => new System.Numerics.Vector3(v.x, v.y, v.z);

        public static System.Numerics.Quaternion ToNumerics(this Quaternion q)
            => new System.Numerics.Quaternion(q.x, q.y, q.z, q.w);
    }
}
