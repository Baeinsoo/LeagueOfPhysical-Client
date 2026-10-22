using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// <see cref="ArcheryPadView"/>가 보는 조작판의 모양. 활쏘기와 말 활쏘기가 같은 화면을 쓰도록
    /// 화면은 이 인터페이스만 안다.
    /// </summary>
    public interface IArcheryPadViewModel
    {
        ArcheryAimAccel.Strength AimAccel { get; }
        string AimAccelLabel { get; }
        void CycleAimAccel();
        void PollKeyboard();
        bool OverCancelButton { get; }
        float DrawRatio { get; }
        bool DrawArmed { get; }
        ArcheryPointerRouter.Role PointerDown(int pointerId, ArcheryButtonLayout.Hit hit, float screenWidthInHeights);
        bool MovesButton(int pointerId);
        void PointerMove(int pointerId, Vector2 deltaFraction, bool overCancelButton);
        void PointerUp(int pointerId);
        void PointerLost(int pointerId);
        void SetMove(Vector2 stick);
        void ClearMove();
        void FeedMove();
        void AdvanceFingerClock(float unscaledDeltaSeconds);
        void Tick(float deltaSeconds);
        bool Holding { get; }
        bool CancelActive { get; }

        Camera Camera { get; }
        bool ShowMoveStick { get; }
        float ShakeLevel { get; }
        int Score { get; }
        int ArrowsLeft { get; }
        System.Collections.Generic.IReadOnlyList<ArcheryImpactLog.Shot> Impacts { get; }
        int ImpactWave { get; }
        string PointsPrefix { get; }
        string PopupText(int points);
        bool ShowImpactPlot { get; }
        bool ShowHitPopups { get; }
        System.Collections.Generic.IReadOnlyList<ArcheryRingBand> Bands { get; }
        /// <summary>점수·남은 화살 글자를 보이나.</summary>
        bool ShowScoreboard { get; }
    }
}
