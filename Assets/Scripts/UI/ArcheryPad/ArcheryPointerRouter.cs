using System.Collections.Generic;

namespace LOP.UI
{
    /// <summary>
    /// 손가락마다 할 일을 정한다. <b>활 버튼에서 시작한 손가락 하나만</b> 활을 들고 쏘고,
    /// 그 밖의 손가락은 끌어서 겨누기만 한다 — 떼도, 터치를 뺏겨도 아무 일이 없다.
    /// 왼쪽 아래 <b>시위 풀기 버튼</b>은 활을 든 동안 누르면 안 쏘고 내려놓는다.
    /// 버튼마다 붙은 <b>손잡이</b>를 잡은 손가락은 버튼 자리만 옮기고 그 밖엔 아무것도 안 한다.
    /// 어디를 눌렀는지는 <see cref="ArcheryButtonLayout.HitTest"/>가 정한다.
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
            Cancel,   // 당기는 중에 시위 풀기 버튼을 눌러 활을 내려놓은 손가락 — 그 뒤로 아무것도 안 한다
            Handle,   // 손잡이를 잡은 손가락 — 버튼 자리만 옮긴다(겨누지도, 들지도, 쏘지도 않는다)
            Inert,    // 아무것도 안 하는 손가락 — 취소당한 활 손가락, 당기는 중에 닿은 손잡이 손가락,
                      // 활을 들어 멈춘 손잡이 손가락, 두 번째 손잡이 손가락.
        }

        /// <summary>손가락을 뗐을 때 일어날 일.</summary>
        public enum Ending
        {
            None,      // 겨누기만 하던 손가락 — 아무 일 없다
            Fire,      // 시위가 걸린 채 뗐다 — 쏜다
            PutDown,   // 시위 풀기 버튼 위에서 뗐거나 취소됐거나 터치를 뺏겼다 — 안 쏘고 조준을 되돌린다
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
        /// <para>시위 풀기 버튼은 활을 든 동안만 보인다. 그래서 그 위라는 것은 <b>활을 든 중일 때만</b>
        /// 뜻이 있다 — 그때는 <b>누르는 순간</b> 내려놓고 <see cref="Role.Cancel"/>을 돌려준다(부른 쪽이
        /// 조준을 되돌린다). 안 든 중이면 버튼이 안 보이니 그 자리는 그냥 화면이다(겨누기).</para>
        ///
        /// <para>손잡이는 <b>안 당길 때만</b> 잡힌다. 당기는 중에 닿은 손잡이 손가락은 아무것도 안 한다 —
        /// 쏘려는 사이에 버튼이 손가락 밑에서 움직이면 안 되고, 시위 풀기 손잡이를 스친 것이 활을 내려서도
        /// 안 된다. 손잡이는 한 번에 하나만 잡힌다.</para>
        /// </summary>
        public Role Down(int pointerId, ArcheryButtonLayout.Hit hit)
        {
            if (roles.TryGetValue(pointerId, out Role existing))
            {
                return existing;   // 같은 누름이 두 번 들어왔다
            }

            Role role;
            switch (hit)
            {
                case ArcheryButtonLayout.Hit.BowHandle:
                case ArcheryButtonLayout.Hit.CancelHandle:
                    role = Drawing || HoldsHandle() ? Role.Inert : Role.Handle;
                    break;
                case ArcheryButtonLayout.Hit.Cancel when Drawing:
                    role = Cancel() == Ending.PutDown ? Role.Cancel : Role.Inert;
                    break;
                case ArcheryButtonLayout.Hit.Bow when Drawing == false:
                    role = Role.Bow;
                    bowPointerId = pointerId;
                    //  활을 드는 순간 옮기던 손잡이는 멈춘다 — 당기는 중엔 버튼이 안 움직인다.
                    StopHandles();
                    break;
                default:
                    role = Role.Look;
                    break;
            }
            roles[pointerId] = role;
            return role;
        }

        private bool HoldsHandle()
        {
            foreach (Role role in roles.Values)
            {
                if (role == Role.Handle) { return true; }
            }
            return false;
        }

        private void StopHandles()
        {
            var handles = new List<int>();
            foreach (var pair in roles)
            {
                if (pair.Value == Role.Handle) { handles.Add(pair.Key); }
            }
            foreach (int id in handles)
            {
                roles[id] = Role.Inert;
            }
        }

        public Role RoleOf(int pointerId) => roles.TryGetValue(pointerId, out Role role) ? role : Role.None;

        /// <summary>이 손가락의 움직임으로 겨누나. 버튼을 쥔 손가락도 끌면 겨눈다.</summary>
        public bool Aims(int pointerId)
        {
            Role role = RoleOf(pointerId);
            return role == Role.Look || role == Role.Bow;
        }

        /// <summary>
        /// 활을 든 중이면 안 쏘고 내려놓는다(시위 풀기 버튼·에디터 Escape). 활 손가락은 남아 있지만
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
    }
}
