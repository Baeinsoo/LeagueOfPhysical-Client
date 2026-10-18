using System.Collections.Generic;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 손가락마다 할 일을 정한다. <b>활 버튼에서 시작한 손가락 하나만</b> 활을 들고 쏘고,
    /// 그 밖의 손가락은 끌어서 겨누기만 한다 — 떼도, 터치를 뺏겨도 아무 일이 없다.
    ///
    /// <para>모바일에선 손가락이 둘 이상 닿는다. 왼손으로 화면을 끌며 겨누고 오른손 엄지로 버튼을
    /// 쥐는 것이 이 조작의 기본 자세라, 손가락 id로 역할을 나눠 두지 않으면 겨누던 손을 떼는 순간
    /// 화살이 나간다.</para>
    /// </summary>
    public sealed class ArcheryPointerRouter
    {
        public enum Role { None, Look, Bow }

        /// <summary>손가락을 뗐을 때 일어날 일.</summary>
        public enum Ending
        {
            None,      // 겨누기만 하던 손가락 — 아무 일 없다
            Fire,      // 시위가 걸린 채 뗐다 — 쏜다
            PutDown,   // 내려놓기 원 안에서 뗐거나 터치를 뺏겼다 — 안 쏘고 조준을 되돌린다
            Lowered,   // 시위가 걸리기 전에 뗐다 — 안 쏘고 활만 내린다
        }

        /// <summary>데스크톱에서 스페이스를 "버튼을 누른 손가락"으로 친다. 실제 포인터 id와 겹치지 않는 값이다.</summary>
        public const int KeyboardPointerId = -100;

        private const int NoPointer = int.MinValue;

        private readonly Dictionary<int, Role> roles = new Dictionary<int, Role>();
        private int bowPointerId = NoPointer;

        /// <summary>지금 활을 들고 있나.</summary>
        public bool Drawing => bowPointerId != NoPointer;

        /// <summary>활을 든 것이 스페이스인가. 그땐 손가락 자리가 없어 내려놓기 원을 안 띄운다.</summary>
        public bool DrawingByKeyboard => bowPointerId == KeyboardPointerId;

        /// <summary>
        /// 손가락이 닿았다. 버튼 위이고 아직 아무도 활을 안 들었으면 활, 아니면 겨누기다 —
        /// 두 번째 손가락이 버튼에 닿아도 첫 손가락의 당김을 가로채지 않는다.
        /// </summary>
        public Role Down(int pointerId, bool onBowButton)
        {
            if (roles.TryGetValue(pointerId, out Role existing))
            {
                return existing;   // 같은 누름이 두 번 들어왔다
            }

            Role role = onBowButton && Drawing == false ? Role.Bow : Role.Look;
            roles[pointerId] = role;
            if (role == Role.Bow)
            {
                bowPointerId = pointerId;
            }
            return role;
        }

        public Role RoleOf(int pointerId) => roles.TryGetValue(pointerId, out Role role) ? role : Role.None;

        /// <summary>이 손가락의 움직임으로 겨누나. 버튼을 쥔 손가락도 끌면 겨눈다.</summary>
        public bool Aims(int pointerId) => RoleOf(pointerId) != Role.None;

        /// <summary>손가락을 뗐다. 쏘는 것은 활을 든 손가락뿐이다.</summary>
        public Ending Up(int pointerId, bool drawArmed, bool overCancelTarget)
        {
            if (Forget(pointerId) != Role.Bow)
            {
                return Ending.None;
            }
            if (overCancelTarget)
            {
                return Ending.PutDown;
            }
            return drawArmed ? Ending.Fire : Ending.Lowered;
        }

        /// <summary>터치를 뺏겼다(화면 밖·알림·전화). 쏘겠다는 뜻이 아니므로 활을 들고 있었다면 내려놓는다.</summary>
        public Ending Lost(int pointerId)
        {
            return Forget(pointerId) == Role.Bow ? Ending.PutDown : Ending.None;
        }

        private Role Forget(int pointerId)
        {
            if (roles.TryGetValue(pointerId, out Role role) == false)
            {
                return Role.None;
            }
            roles.Remove(pointerId);
            if (pointerId == bowPointerId)
            {
                bowPointerId = NoPointer;
            }
            return role;
        }

        //  ── 버튼 자리 ───────────────────────────────────────────────
        //  좌표는 모두 패널 좌표(왼쪽 위가 0, 아래로 갈수록 y가 커진다)다.

        /// <summary>보이는 원의 지름 = 화면 세로의 24%. 폰 가로에서 엄지 하나가 넉넉히 덮는다.</summary>
        public const float BowButtonDiameterOfHeight = 0.24f;

        /// <summary>누르는 자리는 보이는 원보다 조금 넓다 — 가장자리를 살짝 빗겨 눌러도 활이 올라온다.</summary>
        public const float BowButtonHitSlack = 1.15f;

        //  웹 시안에서 쓰던 자리. 오른손 엄지가 쉬는 곳이다.
        private const float CenterXOfWidth = 0.86f;
        private const float CenterYOfHeight = 0.74f;

        //  안전 영역 가장자리와 누르는 자리 사이에 남길 틈(패널 픽셀).
        private const float EdgeGap = 8f;

        public static float BowButtonRadius(Vector2 panelSize) => panelSize.y * BowButtonDiameterOfHeight * 0.5f;

        public static float BowButtonHitRadius(Vector2 panelSize) => BowButtonRadius(panelSize) * BowButtonHitSlack;

        /// <summary>
        /// 버튼 한가운데. 기본은 가로 86%·세로 74%이고, 노치·홈 막대(안전 영역 밖)에 누르는 자리가
        /// 걸리면 그만큼 안쪽으로 당긴다. <paramref name="rightInset"/>·<paramref name="bottomInset"/>은
        /// 안전 영역 밖으로 잘린 오른쪽·아래 폭이다.
        /// </summary>
        public static Vector2 BowButtonCenter(Vector2 panelSize, float rightInset, float bottomInset)
        {
            float r = BowButtonHitRadius(panelSize);
            float x = Mathf.Min(panelSize.x * CenterXOfWidth, panelSize.x - rightInset - r - EdgeGap);
            float y = Mathf.Min(panelSize.y * CenterYOfHeight, panelSize.y - bottomInset - r - EdgeGap);
            return new Vector2(x, y);
        }
    }
}
