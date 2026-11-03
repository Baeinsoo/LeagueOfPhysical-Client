using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 카드 잡기 입력 — 화면 탭(터치·마우스)과 PC Space를 <b>틱 앞</b>에서 읽는다(최종 리뷰 I5, 터치 확장).
    /// VContainer <see cref="ITickable"/>은 Update 단계 맨 앞(ScriptRunBehaviourUpdate 앞)에서 돌고, 틱 코루틴(<c>TickUpdaterBase</c>,
    /// yield null)은 그 뒤 ScriptRunDelayedDynamicFrameRate에서 돈다. 그래서 여기서 켠 잡기는 이번 프레임의 첫 틱 —
    /// 지난 프레임 카드가 보여 준 <see cref="PlayerInputManager.NextInputTick"/> — 에 실린다.
    /// UI Toolkit의 PointerDown·패널 스케줄러는 PreLateUpdate.UIElementsUpdatePanels(틱 뒤)라 거기서 잡으면 한 틱 늦는다.
    /// <para>잡기를 켜는 곳은 여기 하나뿐이다 — 조작판은 PointerDown을 안 듣는다(한 탭이 두 번 세지지 않게).
    /// 탭이 조작판 면 위인지는 <see cref="CardRushTapTarget"/>(열린 조작판이 채운다)에 묻는다 — 위에 겹친 버튼(나가기 등)
    /// 위 탭은 잡기가 아니다. 조작판이 닫혀 있으면 탭은 무시한다(Space는 늘 보낸다, 무시는 시뮬이 한다).</para>
    /// </summary>
    public sealed class CardRushTapGrab : ITickable
    {
        private readonly PlayerInputManager input;
        private readonly CardRushTapTarget target;
        private readonly System.Func<bool> keyPressedThisFrame;
        private readonly System.Action<List<Vector2>> collectPressesThisFrame;
        private readonly List<Vector2> presses = new List<Vector2>();

        [VContainer.Inject]
        public CardRushTapGrab(PlayerInputManager input, CardRushTapTarget target)
            : this(input, target, SpacePressedThisFrame, CollectPointerPresses) { }

        /// <summary>시험용 — 장치 대신 이번 프레임 눌림을 넣는다(화면 좌표, 왼쪽 아래 원점).</summary>
        public CardRushTapGrab(PlayerInputManager input, CardRushTapTarget target,
                               System.Func<bool> keyPressedThisFrame, System.Action<List<Vector2>> collectPressesThisFrame)
        {
            this.input = input;
            this.target = target;
            this.keyPressedThisFrame = keyPressedThisFrame;
            this.collectPressesThisFrame = collectPressesThisFrame;
        }

        public void Tick()
        {
            bool grab = keyPressedThisFrame();

            presses.Clear();
            collectPressesThisFrame(presses);
            foreach (var p in presses)
            {
                //  여러 손가락이 한 프레임에 닿아도 잡기는 한 틱에 한 번이다(입력은 틱당 bool 하나).
                if (target.IsOnPad(p))
                {
                    grab = true;
                }
            }

            if (grab)
            {
                input.SetGrab();
            }
        }

        private static bool SpacePressedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        }

        //  손가락마다 닿은 그 프레임 한 번만 — 누르고 있는 동안은 다시 안 센다.
        private static void CollectPointerPresses(List<Vector2> into)
        {
            var touch = Touchscreen.current;
            if (touch != null)
            {
                foreach (var t in touch.touches)
                {
                    if (t.press.wasPressedThisFrame)
                    {
                        into.Add(t.position.ReadValue());
                    }
                }
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                into.Add(mouse.position.ReadValue());
            }
        }
    }

    /// <summary>
    /// 지금 탭이 조작판 면 위인가 — 열린 조작판 뷰가 <see cref="HitTest"/>를 채우고 닫힐 때 비운다.
    /// 비어 있으면(조작판이 없다: 출발 전 몸이 아직 없거나 완주 뒤) 아무 탭도 잡기가 아니다.
    /// </summary>
    public sealed class CardRushTapTarget
    {
        /// <summary>화면 좌표(Input System, 왼쪽 아래 원점) → 조작판 면 위인가.</summary>
        public System.Func<Vector2, bool> HitTest { get; set; }

        public bool IsOnPad(Vector2 screenPosition) => HitTest != null && HitTest(screenPosition);
    }
}
