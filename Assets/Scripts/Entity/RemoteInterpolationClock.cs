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
        private readonly IGameDataStore gameDataStore;

        private double sendInterval;
        private ClockDilator dilator;
        private InterpolationDelayEstimator estimator;
        private bool initialized;

        private long newestTick;
        private bool hasSnapshot;
        private double renderTime;
        private int lastAdvancedFrame = -1;

        public RemoteInterpolationClock(IGameDataStore gameDataStore)
        {
            this.gameDataStore = gameDataStore;
        }

        // gameInfo는 스코프 빌드 시점(엔트리포인트가 이 clock을 즉시 주입)엔 아직 null일 수 있다 →
        // 첫 스냅 도착(게임 실행 중, gameInfo 준비됨) 시점에 sendInterval을 읽어 초기화한다.
        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }
            sendInterval = gameDataStore.gameInfo.Interval;
            // errorScale=sendInterval → 오차 1틱이면 최대 rate. snapThreshold=8틱(오래 굶다 재개 시만 점프).
            dilator = new ClockDilator(maxRate: 0.05, errorScale: sendInterval, snapThreshold: sendInterval * 8);
            estimator = new InterpolationDelayEstimator(
                sendInterval, n: 2, k: 2, minCushion: sendInterval, maxCushion: sendInterval * 5);
            initialized = true;
        }

        public bool HasSnapshot => hasSnapshot;

        public void RecordArrival(long serverTick, double clientTime)
        {
            EnsureInitialized();
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
                    double advanced = dilator.Advance(renderTime, Target(), Time.unscaledDeltaTime);
                    // 최신 받은 스냅 너머로는 안 감(외삽 금지) — 굶으면 여기 park, 새 스냅 오면 재개.
                    // 이 상한이 target 동결(손실) 시 current 폭주 → 역방향 snap 사이클도 막는다(역행 금지 불변식).
                    double newestTime = newestTick * sendInterval;
                    renderTime = advanced < newestTime ? advanced : newestTime;
                    lastAdvancedFrame = Time.frameCount;
                }
                return renderTime;
            }
        }
    }
}
