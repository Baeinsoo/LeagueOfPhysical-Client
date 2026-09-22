using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 화면 스틱을 민 방향을 <b>월드 이동 방향</b>으로 바꾼다 — 스틱의 위는 "화면에서 먼 쪽",
    /// 즉 카메라가 보는 앞이다.
    ///
    /// <para>부호를 한 번만 헷갈려도 스틱을 오른쪽으로 밀었는데 왼쪽으로 걸어간다. 눈으로는
    /// "조작이 이상하다"까지만 보이고 어디가 뒤집혔는지는 안 보이므로, 계산만 떼어내 못 박는다.</para>
    /// </summary>
    public static class MoveStickDirection
    {
        /// <param name="stick">스틱 입력. x=오른쪽, y=위(=카메라 앞).</param>
        /// <param name="cameraYawDegrees">카메라의 수평 회전각.</param>
        /// <returns>수평 이동 방향(y는 늘 0). 스틱이 가운데면 0 — 그대로 밀어야 캐릭이 선다.</returns>
        public static Vector3 ToWorld(Vector2 stick, float cameraYawDegrees)
        {
            return Quaternion.Euler(0f, cameraYawDegrees, 0f) * new Vector3(stick.x, 0f, stick.y);
        }
    }
}
