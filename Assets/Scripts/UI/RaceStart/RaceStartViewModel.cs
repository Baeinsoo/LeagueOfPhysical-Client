using System;

namespace LOP.UI
{
    /// <summary>
    /// 대기 인원과 카운트다운을 화면 값으로 바꾼다. 플래피(<see cref="IRaceStartLook.Animated"/>)는 spec §3대로 팝 크기·GO 사라짐·박자 소리,
    /// 기마 궁술은 <see cref="CurrentText"/>(예전 계산) 그대로. View가 매 프레임 <see cref="Refresh"/>를 부른다(변경 알림이 없는 샘플링 값).
    /// </summary>
    public class RaceStartViewModel
    {
        //  숫자를 띄우는 구간. 출발까지 이보다 더 남았으면 아직 "대기 중"으로 보여준다 —
        //  서버가 전원 준비 뒤 한 박자 쉬고 출발선을 긋기 때문에(늦게 들어온 사람이 상황을 볼 시간),
        //  그 여유까지 숫자로 세면 5부터 세는 꼴이 된다.
        private const double CountdownSeconds = 3.0;

        private readonly MatchStartState state;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly IRaceStartLook look;
        private readonly CountdownCue cue = new CountdownCue();

        public RaceStartViewModel(MatchStartState state, GameFramework.Runner.IRunner runner, IRaceStartLook look)
        {
            this.state = state;
            this.runner = runner;
            this.look = look;
        }

        /// <summary>지금 띄울 문구. 빈 문자열이면 아무것도 안 띄운다.</summary>
        public string Text { get; private set; } = string.Empty;

        /// <summary>팝 크기(1.5 → 1). 예전 모양이면 늘 1.</summary>
        public float TextScale { get; private set; } = 1f;

        public float Alpha { get; private set; } = 1f;

        /// <summary>플래피 숫자·GO 글씨(큰 팝 글씨)인가. 대기 문구와 예전 모양은 거짓.</summary>
        public bool IsPop { get; private set; }

        /// <summary>플래피 GO!(초록)인가.</summary>
        public bool IsGo { get; private set; }

        public void Refresh()
        {
            if (look.Animated == false)
            {
                //  예전 모양 그대로(기마 궁술) — 문구 계산도 예전 그대로 한 곳(CurrentText).
                Text = CurrentText();
                TextScale = 1f;
                Alpha = 1f;
                IsPop = false;
                IsGo = false;
                return;
            }

            var ticker = runner.tickUpdater;
            bool running = runner.gameState == GameFramework.Runner.RunnerState.Playing;
            var frame = RaceCountdown.At(state.StartTick.CurrentValue, ticker?.interval ?? 0d, ticker?.elapsedTime ?? 0d, running);

            switch (cue.Observe(frame))
            {
                case CountdownSound.Beat: look.Beat(); break;
                case CountdownSound.Go: look.Go(); break;
            }

            IsPop = frame.Kind == CountdownKind.Number || frame.Kind == CountdownKind.Go;
            IsGo = frame.Kind == CountdownKind.Go;
            switch (frame.Kind)
            {
                case CountdownKind.Waiting:
                    Text = WaitingText();
                    TextScale = 1f;
                    Alpha = 1f;
                    break;
                case CountdownKind.Number:
                    Text = frame.Number.ToString();
                    TextScale = RaceCountdown.Scale(frame.SecondsIn);
                    Alpha = 1f;
                    break;
                case CountdownKind.Go:
                    Text = "GO!";
                    TextScale = RaceCountdown.Scale(frame.SecondsIn);
                    Alpha = RaceCountdown.GoAlpha(frame.SecondsIn);
                    break;
                default:
                    //  출발 뒤 0.8초가 지나면 비운다(창을 닫지 않고 View가 숨긴다) — 계속 띄우면 레이스 내내 화면을 덮는다.
                    Text = string.Empty;
                    TextScale = 1f;
                    Alpha = 0f;
                    break;
            }
        }

        /// <summary>예전 문구 계산(기마 궁술이 쓴다). 틱으로 세고 GO는 1초. 빈 문자열이면 아무것도 안 띄운다.</summary>
        public string CurrentText()
        {
            long startTick = state.StartTick.CurrentValue;

            //  runner.Run()이 불리기 전에는 tick도 interval도 0이다. 그 상태로 아래 산술에 들어가면
            //  남은 초가 0으로 계산돼 화면에 "0"이 뜬다 — 이 가드가 막는 건 그거 하나다.
            //  인원 표시는 서버가 보내준 값이라 tick을 안 쓰므로 이 구간에도 그대로 보여준다
            //  (입장 직후 시계 정착에 최대 7초가 걸리는데, 그동안 빈 화면이면 늦게 들어온 사람은
            //  맥락 없이 갑자기 "3"을 맞는다). 로딩 화면과 겹치는 구간은 밴드로 해결한다 — RaceStartView 주석 참고.
            if (startTick == long.MaxValue || runner.gameState != GameFramework.Runner.RunnerState.Playing)
            {
                return WaitingText();
            }

            long remainingTicks = startTick - runner.tickUpdater.tick;
            if (remainingTicks <= 0)
            {
                //  출발 후 1초만 "GO!"를 띄우고 빈 문자열을 반환한다(창을 닫는 게 아니라 View가 숨긴다) —
                //  계속 띄우면 레이스 내내 화면을 덮는다.
                double elapsedSinceStart = -remainingTicks * runner.tickUpdater.interval;
                return elapsedSinceStart < 1.0 ? "GO!" : string.Empty;
            }

            double remainingSeconds = remainingTicks * runner.tickUpdater.interval;
            if (remainingSeconds > CountdownSeconds)
            {
                return WaitingText();
            }

            //  올림이라야 "3, 2, 1"이 각각 1초씩 보인다. 내림이면 3이 한 순간만 스친다.
            return ((int)Math.Ceiling(remainingSeconds)).ToString();
        }

        private string WaitingText()
            => $"{state.ReadyCount.CurrentValue} / {state.TotalCount.CurrentValue} 대기 중";
    }
}
