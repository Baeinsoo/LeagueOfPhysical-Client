using System.Collections.Generic;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 손가락마다 할 일을 정한다. <b>활 버튼에서 시작한 손가락 하나만</b> 활을 들고 쏘고,
    /// 그 밖의 손가락은 끌어서 겨누기만 한다 — 떼도, 터치를 뺏겨도 아무 일이 없다.
    /// 왼쪽 아래 <b>취소 버튼</b>은 활을 든 동안 누르면 안 쏘고 내려놓는다.
    ///
    /// <para>모바일에선 손가락이 둘 이상 닿는다. 왼손으로 화면을 끌며 겨누고 오른손 엄지로 버튼을
    /// 쥐는 것이 이 조작의 기본 자세라, 손가락 id로 역할을 나눠 두지 않으면 겨누던 손을 떼는 순간
    /// 화살이 나간다.</para>
    /// </summary>
    public sealed class ArcheryPointerRouter
    {
        public enum Role
        {
            None,
            Look,     // 끌어서 겨누기만 한다
            Bow,      // 활을 든 손가락 — 끌면 겨누고, 떼면 쏜다
            Cancel,   // 당기는 중에 취소 버튼을 눌러 활을 내려놓은 손가락 — 그 뒤로 아무것도 안 한다
            Inert,    // 아무것도 안 하는 손가락 — 취소당한 활 손가락. 취소 버튼은 안 당길 땐 안 보이므로
                      // 그 위에서 시작한 손가락은 더는 여기로 오지 않는다(화면 손가락으로 간다).
        }

        /// <summary>손가락을 뗐을 때 일어날 일.</summary>
        public enum Ending
        {
            None,      // 겨누기만 하던 손가락 — 아무 일 없다
            Fire,      // 시위가 걸린 채 뗐다 — 쏜다
            PutDown,   // 취소 버튼 위에서 뗐거나 취소됐거나 터치를 뺏겼다 — 안 쏘고 조준을 되돌린다
            Lowered,   // 시위가 걸리기 전에 뗐다 — 안 쏘고 활만 내린다
        }

        /// <summary>데스크톱에서 스페이스를 "버튼을 누른 손가락"으로 친다. 실제 포인터 id와 겹치지 않는 값이다.</summary>
        public const int KeyboardPointerId = -100;

        private const int NoPointer = int.MinValue;

        private readonly Dictionary<int, Role> roles = new Dictionary<int, Role>();
        private int bowPointerId = NoPointer;

        /// <summary>지금 활을 들고 있나.</summary>
        public bool Drawing => bowPointerId != NoPointer;

        /// <summary>
        /// 손가락이 닿았다. 버튼 위이고 아직 아무도 활을 안 들었으면 활, 아니면 겨누기다 —
        /// 두 번째 손가락이 버튼에 닿아도 첫 손가락의 당김을 가로채지 않는다.
        ///
        /// <para>취소 버튼은 활을 든 동안만 보인다. 그래서 취소 버튼 위라는 것은
        /// <b>활을 든 중일 때만</b> 뜻이 있다 — 그때는 <b>누르는 순간</b> 내려놓고
        /// <see cref="Role.Cancel"/>을 돌려준다(부른 쪽이 조준을 되돌린다). 안 든 중이면
        /// 버튼이 안 보이니 그 자리는 그냥 화면이다 — <paramref name="onCancelButton"/>을
        /// 무시하고 보통 손가락(겨누기)으로 받는다.</para>
        /// </summary>
        public Role Down(int pointerId, bool onBowButton, bool onCancelButton = false)
        {
            if (roles.TryGetValue(pointerId, out Role existing))
            {
                return existing;   // 같은 누름이 두 번 들어왔다
            }

            if (onCancelButton && Drawing)
            {
                Role pressed = Cancel() == Ending.PutDown ? Role.Cancel : Role.Inert;
                roles[pointerId] = pressed;
                return pressed;
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
        public bool Aims(int pointerId)
        {
            Role role = RoleOf(pointerId);
            return role == Role.Look || role == Role.Bow;
        }

        /// <summary>
        /// 활을 든 중이면 안 쏘고 내려놓는다(취소 버튼·에디터 Escape). 활 손가락은 남아 있지만
        /// <see cref="Role.Inert"/>가 돼 그 뒤로 떼도 안 쏘고 겨누지도 않는다.
        /// </summary>
        public Ending Cancel()
        {
            if (Drawing == false)
            {
                return Ending.None;
            }
            if (roles.ContainsKey(bowPointerId))
            {
                roles[bowPointerId] = Role.Inert;
            }
            bowPointerId = NoPointer;
            return Ending.PutDown;
        }

        /// <summary>손가락을 뗐다. 쏘는 것은 활을 든 손가락뿐이다.</summary>
        public Ending Up(int pointerId, bool drawArmed, bool overCancelButton)
        {
            if (Forget(pointerId) != Role.Bow)
            {
                return Ending.None;
            }
            if (overCancelButton)
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

        //  오른손 엄지가 쉬는 곳. 10-18 폰 판 "버튼을 쥔 채 끌 자리가 없다" — 86%·74%(구석)에서
        //  왼쪽 위로 당겨 사방으로 끌 틈을 남긴다. 같은 날 "너무 안쪽" — 가로만 78% → 80%.
        private const float CenterXOfWidth = 0.80f;
        private const float CenterYOfHeight = 0.62f;

        //  안전 영역 가장자리와 누르는 자리 사이에 남길 틈(패널 픽셀).
        private const float EdgeGap = 8f;

        public static float BowButtonRadius(Vector2 panelSize) => panelSize.y * BowButtonDiameterOfHeight * 0.5f;

        public static float BowButtonHitRadius(Vector2 panelSize) => BowButtonRadius(panelSize) * BowButtonHitSlack;

        /// <summary>
        /// 버튼 한가운데. 기본은 가로 78%·세로 62%이고, 노치·홈 막대(안전 영역 밖)에 누르는 자리가
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
    
        //  ── 취소 버튼 자리 ──────────────────────────────────────────
        //  왼손 엄지가 닿는 왼쪽 아래에 늘 서 있다. 활을 든 손가락을 끌어와도 되고, 다른 손가락으로 톡 쳐도 된다.

        /// <summary>보이는 원의 지름 = 화면 세로의 18%. 활 버튼보다 작다 — 덜 쓰는 버튼이다.</summary>
        public const float CancelButtonDiameterOfHeight = 0.18f;

        private const float CancelCenterXOfWidth = 0.12f;
        private const float CancelCenterYOfHeight = 0.74f;

        public static float CancelButtonRadius(Vector2 panelSize) => panelSize.y * CancelButtonDiameterOfHeight * 0.5f;

        /// <summary>누르는 자리는 활 버튼처럼 보이는 원보다 조금 넓다.</summary>
        public static float CancelButtonHitRadius(Vector2 panelSize) => CancelButtonRadius(panelSize) * BowButtonHitSlack;

        /// <summary>
        /// 취소 버튼 한가운데. 기본은 가로 12%·세로 74%이고, 안전 영역 밖(왼쪽 노치·홈 막대)에
        /// 누르는 자리가 걸리면 그만큼 안쪽으로 민다.
        /// </summary>
        public static Vector2 CancelButtonCenter(Vector2 panelSize, float leftInset, float bottomInset)
        {
            float r = CancelButtonHitRadius(panelSize);
            float x = Mathf.Max(panelSize.x * CancelCenterXOfWidth, leftInset + r + EdgeGap);
            float y = Mathf.Min(panelSize.y * CancelCenterYOfHeight, panelSize.y - bottomInset - r - EdgeGap);
            return new Vector2(x, y);
        }
    }
}
