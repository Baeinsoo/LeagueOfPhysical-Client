using UnityEngine.InputSystem;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// PC Space 잡기(최종 리뷰 I5). VContainer <see cref="ITickable"/>은 Update 단계 맨 앞(ScriptRunBehaviourUpdate 앞)에서 돌고,
    /// 틱 코루틴(<c>TickUpdaterBase</c>, yield null)은 그 뒤 ScriptRunDelayedDynamicFrameRate에서 돈다. 그래서 여기서 켠 잡기는
    /// 이번 프레임의 첫 틱 — 지난 프레임 카드가 보여 준 <see cref="PlayerInputManager.NextInputTick"/> — 에 실린다.
    /// 예전처럼 조작판 패널 스케줄러(PreLateUpdate.UIElementsUpdatePanels, 틱 뒤)에서 읽으면 한 틱 늦게 붙어
    /// 색 경계에서 "빨강 보고 눌렀는데 노랑 판정"이 난다.
    /// </summary>
    public sealed class CardRushKeyboardGrab : ITickable
    {
        private readonly PlayerInputManager input;
        private readonly System.Func<bool> pressedThisFrame;

        [VContainer.Inject]
        public CardRushKeyboardGrab(PlayerInputManager input) : this(input, SpacePressedThisFrame) { }

        /// <summary>시험용 — 키보드 대신 눌림을 넣는다.</summary>
        public CardRushKeyboardGrab(PlayerInputManager input, System.Func<bool> pressedThisFrame)
        {
            this.input = input;
            this.pressedThisFrame = pressedThisFrame;
        }

        public void Tick()
        {
            //  뒷면·출발 전·완주 뒤 탭도 그대로 보낸다 — 무시는 시뮬이 한다(조작판 탭과 같다).
            if (pressedThisFrame())
            {
                input.SetGrab();
            }
        }

        private static bool SpacePressedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        }
    }
}
