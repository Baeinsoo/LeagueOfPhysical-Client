using GameFramework;
using GameFramework.Netcode;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 원격 엔티티 공유 재생 시계(receive-anchored). 최신 받은 스냅 배치 소인에서 적응형 쿠션만큼 뒤를
    /// 가리키며 ClockDilator로 rate 추종(역행 없음). RenderTime은 프레임당 1회만 전진(여러 인터폴레이터 공용).
    /// </summary>
    public class RemoteInterpolationClock
    {
        private readonly double sendInterval;
        private readonly ClockDilator dilator;
        private readonly InterpolationDelayEstimator estimator;

        private long newestTick;
        private bool hasSnapshot;
        private double renderTime;
        private int lastAdvancedFrame = -1;

        public RemoteInterpolationClock(double sendInterval)
        {
            this.sendInterval = sendInterval;
            // errorScale=sendInterval → 오차 1틱이면 최대 rate. snapThreshold=8틱(오래 굶다 재개 시만 점프).
            this.dilator = new ClockDilator(maxRate: 0.05, errorScale: sendInterval, snapThreshold: sendInterval * 8);
            this.estimator = new InterpolationDelayEstimator(
                sendInterval, n: 2, k: 2, minCushion: sendInterval, maxCushion: sendInterval * 5);
        }

        public bool HasSnapshot => hasSnapshot;

        public void RecordArrival(long serverTick, double clientTime)
        {
            estimator.RecordArrival(clientTime);
            if (hasSnapshot == false || serverTick > newestTick)
            {
                newestTick = serverTick;
            }
            if (hasSnapshot == false)
            {
                renderTime = Target();   // 첫 스냅에 시계 시드
                hasSnapshot = true;
            }
        }

        // 목표 = 최신 스냅 소인(서버 타임라인) − 쿠션. 스냅 timestamp = tick*interval과 같은 축.
        private double Target() => newestTick * sendInterval - estimator.Cushion;

        public double RenderTime
        {
            get
            {
                if (hasSnapshot == false)
                {
                    return 0;
                }
                if (Time.frameCount != lastAdvancedFrame)
                {
                    renderTime = dilator.Advance(renderTime, Target(), Time.unscaledDeltaTime);
                    lastAdvancedFrame = Time.frameCount;
                }
                return renderTime;
            }
        }
    }
}
