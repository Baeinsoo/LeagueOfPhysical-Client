using R3;
using UnityEngine.InputSystem;

namespace LOP.UI
{
    /// <summary>
    /// Flappy Race 입력 화면 ViewModel. 화면을 누르면 날갯짓, 게이지가 가득 차면 대시.
    /// 게이지는 매 틱 변하는 월드 값이라 View가 부르는 <see cref="Refresh"/>에서 읽어
    /// 노출한다(<see cref="SkydivePadViewModel"/>와 같은 짝).
    /// </summary>
    public class FlapPadViewModel
    {
        private readonly PlayerInputManager _playerInputManager;
        private readonly IPlayerContext _playerContext;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly FlappyChaserView _chaserView;

        private readonly ReactiveProperty<float> _dashCharge = new ReactiveProperty<float>(0f);
        private readonly ReactiveProperty<int> _dashStacks = new ReactiveProperty<int>(0);
        private readonly ReactiveProperty<bool> _canDash = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<float> _chaserGap = new ReactiveProperty<float>(0f);

        /// <summary>지금 채우고 있는 칸이 얼마나 찼나(0~1). 버튼이 이만큼 차오른다.
        /// <para>쌓아 둔 칸은 <see cref="DashStacks"/>가 숫자로 따로 보여 준다 — 롤 강타와 같은 모양이다.
        /// 게이지 하나로 0~2를 그리면 "지금 쓸 수 있나"가 눈금 중간에 묻힌다.</para></summary>
        public ReadOnlyReactiveProperty<float> DashCharge => _dashCharge;

        /// <summary>지금 쓸 수 있는 칸 수(0~<see cref="FlappyDash.MaxCharge"/>).</summary>
        public ReadOnlyReactiveProperty<int> DashStacks => _dashStacks;

        /// <summary>지금 대시를 쓸 수 있나. 버튼의 밝기와 반응이 이 값을 따른다.</summary>
        public ReadOnlyReactiveProperty<bool> CanDash => _canDash;

        /// <summary>추격자까지 남은 거리(m). 벽을 그리는 쪽과 <b>같은 값</b>을 읽는다 —
        /// 각자 계산하면 숫자와 화면 속 벽이 어긋난다.</summary>
        public ReadOnlyReactiveProperty<float> ChaserGap => _chaserGap;

        public FlapPadViewModel(PlayerInputManager playerInputManager,
                                IPlayerContext playerContext,
                                GameFramework.World.EntityRegistry entityRegistry,
                                FlappyChaserView chaserView)
        {
            _playerInputManager = playerInputManager;
            _playerContext = playerContext;
            _entityRegistry = entityRegistry;
            _chaserView = chaserView;
        }

        /// <summary>날갯짓. 와이어에는 기존 Jump 입력으로 실린다 — 서버 입력 버퍼는 그대로 쓴다.</summary>
        public void Flap() => _playerInputManager.SetJump(true);

        /// <summary>
        /// 대시. 게이지가 덜 찼으면 아무 일도 하지 않는다 — 눌러도 안 나가는 것을 화면이 먼저
        /// 알려주는 편이, 보내 놓고 서버가 거절해 되돌아오는 것보다 낫다.
        /// (자격의 권위는 여전히 서버다. 여기서는 헛된 입력만 줄인다.)
        /// </summary>
        public void Dash()
        {
            if (_canDash.Value)
            {
                _playerInputManager.SetDash(true);
            }
        }

        /// <summary>내 새의 게이지를 읽어 온다. View가 매 프레임 부른다.</summary>
        public void Refresh()
        {
            var entity = string.IsNullOrEmpty(_playerContext.entityId)
                ? null
                : _entityRegistry.Get(_playerContext.entityId);

            float charge = entity?.Get<FlappyDash>()?.Charge ?? 0f;
            int stacks = (int)charge;
            _dashStacks.Value = stacks;
            //  꽉 찼을 때 나머지를 그리면 게이지가 0으로 돌아가 "다 찼는데 비어 보이는" 그림이 된다.
            _dashCharge.Value = charge >= FlappyDash.MaxCharge ? 1f : charge - stacks;
            _canDash.Value = stacks >= 1;

            _chaserGap.Value = entity == null
                ? 0f
                : (entity.Get<GameFramework.World.Transform>()?.Position.X ?? 0f) - _chaserView.X;
        }

        /// <summary>데스크톱 편의: Space는 날갯짓, Shift/D는 대시. View가 매 프레임 부른다.</summary>
        public void PollKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                Flap();
            }

            if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                Dash();
            }
        }
    }
}
