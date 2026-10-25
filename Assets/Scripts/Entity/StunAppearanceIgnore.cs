using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 이 컴포넌트가 붙은 그림(또는 그 자식)은 <see cref="StunAppearance"/>가 렌더러를 모을 때 건너뛴다.
    /// 내 새 표시(FlappyMyBirdMarker)처럼 그림의 자식으로 붙지만 새 머티리얼과 무관한 연출을 다는 쪽이 쓴다 —
    /// 안 그러면 기절색·무적 깜빡임·칠이 장식에도 덧씌워지고, 장식이 먼저 사라진 뒤 Cleanup이 죽은 렌더러에 쓰려다 죽는다.
    /// </summary>
    public class StunAppearanceIgnore : MonoBehaviour
    {
    }
}
