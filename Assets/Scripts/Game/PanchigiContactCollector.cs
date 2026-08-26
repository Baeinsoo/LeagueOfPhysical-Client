using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 한 번의 치기가 모으는 접촉점. 손가락이 전부 떨어지면 완성된다.
    ///
    /// 상한(<c>contactMax</c>)은 <b>동시에 눌린 손가락 수가 아니라 한 번의 치기가 모으는 총 개수</b>다 —
    /// 손가락을 떼도 그 접촉점은 보관되므로 자리가 나지 않고, 앞서 넘쳐서 무시된 손가락이
    /// 승격되지도 않는다. 승격시키면 조준 중에 없던 선이 갑자기 생기고 "먼저 온 순서"가 흔들린다.
    /// </summary>
    public class PanchigiContactCollector
    {
        /// <summary>확정된 접촉점 하나.</summary>
        public readonly struct Contact
        {
            public readonly Vector3 StrikePoint;
            public readonly Vector3 DragDelta;
            public readonly float HoldTime;

            public Contact(Vector3 strikePoint, Vector3 dragDelta, float holdTime)
            {
                StrikePoint = strikePoint;
                DragDelta = dragDelta;
                HoldTime = holdTime;
            }
        }

        /// <summary>아직 눌려 있는 손가락 하나 — 조준선을 그리는 데 쓴다.</summary>
        public readonly struct Aim
        {
            public readonly int TouchId;
            public readonly Vector3 Start;
            public readonly Vector3 Current;
            public readonly float PressTime;

            public Aim(int touchId, Vector3 start, Vector3 current, float pressTime)
            {
                TouchId = touchId;
                Start = start;
                Current = current;
                PressTime = pressTime;
            }
        }

        private readonly int contactMax;

        //  List로 두는 건 순서가 곧 규칙이기 때문이다 — 먼저 닿은 순서로 접수한다.
        //  손가락은 많아야 상한(현재 4)이라 선형 탐색으로 충분하다.
        private readonly List<Aim> pressed = new();
        private readonly List<Contact> done = new();

        public PanchigiContactCollector(int contactMax)
        {
            this.contactMax = contactMax;
        }

        public IReadOnlyList<Contact> Contacts => done;
        public IReadOnlyList<Aim> Pressed => pressed;

        /// <summary>손가락이 전부 떨어졌고 모인 접촉점이 있다 = 한 번의 치기가 끝났다.</summary>
        public bool IsComplete => pressed.Count == 0 && done.Count > 0;

        /// <summary>손가락이 판에 닿았다. 상한을 넘었으면 접수하지 않고 false를 준다.</summary>
        public bool Begin(int touchId, Vector3 boardPoint, float now)
        {
            if (IndexOf(touchId) >= 0)
            {
                return false;   // 이미 추적 중인 손가락
            }
            if (done.Count + pressed.Count >= contactMax)
            {
                return false;   // 이번 치기가 모을 수 있는 만큼 다 찼다
            }

            pressed.Add(new Aim(touchId, boardPoint, boardPoint, now));
            return true;
        }

        /// <summary>손가락이 움직였다. 추적 중이 아니면 아무 일도 하지 않는다.</summary>
        public void Update(int touchId, Vector3 boardPoint)
        {
            int i = IndexOf(touchId);
            if (i < 0)
            {
                return;
            }
            Aim a = pressed[i];
            pressed[i] = new Aim(a.TouchId, a.Start, boardPoint, a.PressTime);
        }

        /// <summary>손가락이 떨어졌다. 그 손가락의 결과를 확정해 보관한다.</summary>
        public void End(int touchId, Vector3 boardPoint, float now, float holdTimeMax, float strikePowerMax)
        {
            int i = IndexOf(touchId);
            if (i < 0)
            {
                return;
            }
            Aim a = pressed[i];
            pressed.RemoveAt(i);

            //  누른 시간에 상한이 없으면 오래 누를수록 힘이 무한히 커진다(원본의 문제).
            float holdTime = Mathf.Min(now - a.PressTime, holdTimeMax);

            Vector3 drag = boardPoint - a.Start;
            drag.y = 0f;
            //  세기 상한도 여기서 자른다 — 서버는 넘으면 클램프가 아니라 거절한다.
            drag = Vector3.ClampMagnitude(drag, strikePowerMax);

            done.Add(new Contact(boardPoint, drag, holdTime));
        }

        public void Clear()
        {
            pressed.Clear();
            done.Clear();
        }

        private int IndexOf(int touchId)
        {
            for (int i = 0; i < pressed.Count; i++)
            {
                if (pressed[i].TouchId == touchId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
