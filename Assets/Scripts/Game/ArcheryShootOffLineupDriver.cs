using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <see cref="ArcheryShootOffLineupView"/>가 몸통을 옆으로 옮기는 <b>시각</b>만 맡는다.
    /// 보간기(0)가 몸통 자리를 쓴 뒤, 카메라(3000)·이름표(3100)가 그 자리를 읽기 전에 돌아야 한다 —
    /// 그래서 순서를 정할 수 있는 MonoBehaviour로 뺐다(<see cref="PostureTiltView"/>와 같은 수).
    /// </summary>
    [DefaultExecutionOrder(2900)]
    public class ArcheryShootOffLineupDriver : MonoBehaviour
    {
        public ArcheryShootOffLineupView View { get; set; }

        private void LateUpdate()
        {
            View?.Apply();
        }
    }
}
