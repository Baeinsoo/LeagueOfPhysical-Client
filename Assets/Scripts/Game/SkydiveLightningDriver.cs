using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <see cref="SkydiveLightningView"/>가 몸을 떨게 하는 <b>시각</b>만 맡는다.
    /// 보간기(0)가 몸 자리를 쓴 뒤, 카메라(3000)가 그 자리를 읽기 전에 돌아야 한다 — VContainer LateTick은
    /// MonoBehaviour LateUpdate보다 먼저 돌아 보간기가 떨림을 지워 버린다(<see cref="ArcheryShootOffReactionDriver"/>와 같은 이유).
    /// </summary>
    [DefaultExecutionOrder(2900)]
    public class SkydiveLightningDriver : MonoBehaviour
    {
        public SkydiveLightningView View { get; set; }

        private void LateUpdate()
        {
            View?.ApplyShakes();
        }
    }
}
