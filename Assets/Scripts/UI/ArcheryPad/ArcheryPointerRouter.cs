using System.Collections.Generic;

namespace LOP.UI
{
    /// <summary>손가락 하나가 맡은 일. 왼손은 겨누기만, 오른손은 당기고 쏜다.</summary>
    public enum ArcheryPointerRole { None, Aim, Draw }

    /// <summary>
    /// 활쏘기 화면의 손가락 나누기. 손가락마다 <b>처음 닿은 쪽</b>으로 역할을 정하고, 뗄 때까지
    /// 그 역할을 지킨다 — 겨누던 왼손이 화면 가운데를 넘었다고 활을 들면 안 된다.
    ///
    /// <para>쪽마다 손가락은 하나만 받는다. 같은 쪽에 두 번째 손가락이 닿으면 아무 역할도 안 준다 —
    /// 그 손가락의 뗌이 첫 손가락의 당김을 발사로 바꾸면 안 되기 때문이다.</para>
    /// </summary>
    public sealed class ArcheryPointerRouter
    {
        private readonly Dictionary<int, ArcheryPointerRole> roles = new Dictionary<int, ArcheryPointerRole>();

        /// <summary>화면 왼쪽 절반인가. 정확히 한가운데는 오른쪽(당기는 쪽)으로 친다.</summary>
        public static bool IsLeftHalf(float x, float panelWidth) => x < panelWidth * 0.5f;

        /// <summary>손가락이 닿았다. 맡길 역할을 돌려준다(이미 그 쪽에 손가락이 있으면 None).</summary>
        public ArcheryPointerRole Down(int pointerId, float x, float panelWidth)
        {
            //  화면 폭을 모르면 어느 쪽인지도 모른다 — 짐작으로 당기기를 주면 왼손이 활을 든다.
            if (panelWidth <= 0f || roles.ContainsKey(pointerId))
            {
                return ArcheryPointerRole.None;
            }

            var role = IsLeftHalf(x, panelWidth) ? ArcheryPointerRole.Aim : ArcheryPointerRole.Draw;
            if (roles.ContainsValue(role))
            {
                return ArcheryPointerRole.None;
            }
            roles[pointerId] = role;
            return role;
        }

        /// <summary>그 손가락이 지금 맡은 역할. 모르는 손가락이면 None.</summary>
        public ArcheryPointerRole RoleOf(int pointerId)
            => roles.TryGetValue(pointerId, out var role) ? role : ArcheryPointerRole.None;

        /// <summary>손가락이 떨어졌다(또는 터치를 뺏겼다). 맡았던 역할을 돌려주고 잊는다.</summary>
        public ArcheryPointerRole Up(int pointerId)
        {
            if (roles.TryGetValue(pointerId, out var role) == false)
            {
                return ArcheryPointerRole.None;
            }
            roles.Remove(pointerId);
            return role;
        }
    }
}
