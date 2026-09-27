using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <see cref="ArcheryShootOffCameraRig"/>가 도는 <b>시각</b>만 맡는다. 리액션(2900)이 내 몸을 튕긴 뒤,
    /// 카메라(3000)가 그 몸을 따라 그리기 전에 돌아야 튕긴 만큼을 같은 프레임에 빼 줄 수 있다.
    /// </summary>
    [DefaultExecutionOrder(2950)]
    public class ArcheryShootOffCameraRigDriver : MonoBehaviour
    {
        public ArcheryShootOffCameraRig Rig { get; set; }

        private void LateUpdate()
        {
            Rig?.Apply();
        }
    }
}
