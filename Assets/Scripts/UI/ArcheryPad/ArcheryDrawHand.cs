using UnityEngine;

namespace LOP.UI
{
    /// <summary>오른손이 활을 드는 방식. 값은 PlayerPrefs에 그대로 저장되므로 번호를 바꾸지 않는다.</summary>
    public enum ArcheryDrawStyle
    {
        /// <summary>누르면 활이 올라오고, 누른 채 움직이면 곱게 겨눈다.</summary>
        Press = 0,

        /// <summary>(시험용) 눌러서 아래로 끌어야 활이 올라온다. 끄는 손은 겨누지 않는다.</summary>
        Pull = 1,
    }

    /// <summary>
    /// 오른손(당기는 손) 하나의 상태. 화면 좌표만 받아 "지금 당기고 있나"를 정한다.
    ///
    /// <para><b>얼마나 당겨졌는지는 여기서 안 정한다.</b> 어느 방식이든 당기는 동안 보내는 값은 늘 1이고,
    /// 만작까지 걸리는 시간은 시뮬(<see cref="ArcheryAimSystem.FullDrawSeconds"/>)이 틱 수로 정한다 —
    /// 끌기 방식에서 손을 빨리 끈다고 빨리 당겨지면 손 빠른 쪽이 이긴다.</para>
    ///
    /// 좌표는 <b>화면 세로를 1로 본 값</b>이고 y 양수가 위다(<see cref="ArcheryPadViewModel"/>과 같다).
    /// </summary>
    public sealed class ArcheryDrawHand
    {
        /// <summary>끌기: 누른 자리에서 이만큼 아래로 끌면 활이 올라온다(화면 세로 대비, 폰에서 약 4mm).</summary>
        public const float PullStartInHeights = 0.06f;

        /// <summary>
        /// 끌기: 당기는 중에 이 위로 되돌리면 내려놓는다. 시작 문턱보다 조금 위에 둔다 —
        /// 같은 선이면 문턱 근처에서 손이 떨릴 때마다 당김이 끊겨 0.5초를 새로 기다린다.
        /// </summary>
        public const float PullPutDownInHeights = 0.04f;

        /// <summary>
        /// 끌기: 화면에 그리는 시위가 늘어나는 끝. <b>꾸밈일 뿐</b> 당김과 무관하다.
        /// 내려놓기 원(누른 자리 아래 0.235부터)에 안 닿게 짧게 둔다 — 닿아 보이면 "원까지 끌어야
        /// 만작"으로 읽혀 쏘려다 취소한다.
        /// </summary>
        public const float PullGaugeFullInHeights = 0.18f;

        public ArcheryDrawHand(ArcheryDrawStyle style)
        {
            Style = style;
        }

        public ArcheryDrawStyle Style { get; }

        /// <summary>오른손 손가락이 화면에 닿아 있나.</summary>
        public bool Holding { get; private set; }

        /// <summary>지금 "당기고 있다"고 보내야 하나.</summary>
        public bool Drawing { get; private set; }

        /// <summary>입력에 실을 당김 목표. 당기는 동안 늘 1이다 — 끈 거리·속도와 무관하다.</summary>
        public float DrawRatioToSend => Drawing ? 1f : 0f;

        /// <summary>이 손가락의 움직임이 조준도 하나. 누르기 방식에서 당기는 동안만 그렇다.</summary>
        public bool FingerAims => Style == ArcheryDrawStyle.Press && Drawing;

        /// <summary>손가락을 처음 댄 자리.</summary>
        public Vector2 PressInHeights { get; private set; }

        /// <summary>끌기: 화면에 그릴 시위 길이(아래로 끈 거리, 0 ~ <see cref="PullGaugeFullInHeights"/>).</summary>
        public float PullInHeights { get; private set; }

        public void Begin(Vector2 pressInHeights)
        {
            Holding = true;
            PressInHeights = pressInHeights;
            PullInHeights = 0f;
            Drawing = Style == ArcheryDrawStyle.Press;
        }

        public void Move(Vector2 positionInHeights)
        {
            if (Holding == false || Style != ArcheryDrawStyle.Pull)
            {
                return;
            }

            //  세로로 끈 거리만 본다(아래가 양수). 옆으로 미는 건 끌기가 아니다.
            float pulled = PressInHeights.y - positionInHeights.y;
            PullInHeights = Mathf.Clamp(pulled, 0f, PullGaugeFullInHeights);

            if (Drawing == false && pulled >= PullStartInHeights)
            {
                Drawing = true;
            }
            else if (Drawing && pulled < PullPutDownInHeights)
            {
                Drawing = false;
            }
        }

        public void End()
        {
            Holding = false;
            Drawing = false;
            PullInHeights = 0f;
        }

        /// <summary>
        /// 저장된 값에서 방식을 고른다. 토글이 안 보이는 빌드(출시)는 늘 누르기다 —
        /// 고를 길이 없는데 시험용 방식으로 굳어 있으면 되돌릴 수 없다.
        /// </summary>
        public static ArcheryDrawStyle ResolveStyle(int savedValue, bool toggleAvailable)
        {
            if (toggleAvailable && savedValue == (int)ArcheryDrawStyle.Pull)
            {
                return ArcheryDrawStyle.Pull;
            }
            return ArcheryDrawStyle.Press;
        }

        public static string LabelOf(ArcheryDrawStyle style)
            => style == ArcheryDrawStyle.Pull ? "당기기: 끌기" : "당기기: 누르기";

        public static ArcheryDrawStyle Next(ArcheryDrawStyle style)
            => style == ArcheryDrawStyle.Pull ? ArcheryDrawStyle.Press : ArcheryDrawStyle.Pull;
    }
}
