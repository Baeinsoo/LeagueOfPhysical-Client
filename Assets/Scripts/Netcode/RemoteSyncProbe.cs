using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// [진단용 임시 — 관찰이 끝나면 지운다] 남의 새를 지연 스냅샷 보간으로 그릴 때 화면이 실제로
    /// 매끄러운지 창 단위로 요약한다.
    ///
    /// "떨린다/가끔 확 움직인다"는 체감은 프레임 하나하나를 찍어서는 판단할 수 없다(초당 수십 줄).
    /// 그래서 2초를 모아 세 가지를 본다:
    /// <list type="bullet">
    /// <item><b>hold</b> — 감쌀 스냅 쌍이 없어 최신 스냅에 얼어 있던 프레임 비율. 이게 곧 "떨림"이다.</item>
    /// <item><b>속도</b> — 프레임 간 이동거리/시간. 정상은 전진 11에 세로가 얹힌 값이고,
    ///   그보다 훨씬 큰 값이 "확 움직인" 순간이다.</item>
    /// <item><b>여유</b> — 재생 시계가 최신 스냅보다 얼마나 뒤에 있나. 0에 붙으면 park 상태라
    ///   스냅이 조금만 늦어도 곧바로 hold로 떨어진다.</item>
    /// </list>
    ///
    /// 호출부에 <c>#if</c>를 뿌리지 않으려고 <see cref="System.Diagnostics.ConditionalAttribute"/>를
    /// 쓴다 — 에디터가 아니면 호출 자체가 컴파일에서 사라진다.
    /// </summary>
    public static class RemoteSyncProbe
    {
        private const float WindowSeconds = 2f;
        //  이보다 적게 움직인 프레임은 "안 움직였다"로 본다. 정상 프레임은 11m/s × dt라 자릿수가 다르다.
        private const float StillEpsilon = 1e-4f;

        private class Track
        {
            public Vector3 LastPosition;
            public bool HasLast;
            public double LastArrival;
            public int StillRun;        // 지금 몇 프레임째 안 움직이고 있나
        }

        private static readonly Dictionary<string, Track> tracks = new Dictionary<string, Track>();
        private static readonly List<float> speeds = new List<float>();
        private static readonly List<float> arrivalGaps = new List<float>();
        private static readonly List<float> behinds = new List<float>();
        //  [외삽 모드] 새 스냅이 와서 궤적을 갈아탈 때 화면이 건너뛰어야 하는 거리.
        //  블렌드가 이걸 0.1초에 걸쳐 숨긴다 — 크면 아무리 녹여도 "확 움직인다"로 보인다.
        private static readonly List<float> correctionGaps = new List<float>();
        //  [예측 모드] 남의 새를 받은 입력으로 굴린 결과가 서버와 얼마나 어긋났나.
        //  이게 곧 화면이 튀는 크기다 — 입력이 제대로 오면 작아야 한다.
        private static readonly List<float> remoteErrors = new List<float>();
        private static int remoteInputsReceived, remoteInputsAhead;

        private static int frames, holds, stills;
        //  [판별] 안 움직인 프레임이 "길게 이어지나 vs 한두 프레임씩 끼나".
        //  길면(0.8초≈48프레임) 스턴이라 진짜로 멈춘 것이고, 짧으면 보간이 계단처럼 끊기는 것이다.
        private static readonly List<float> stillRuns = new List<float>();
        //  [판별] 안 움직일 때 서버가 준 속도. 0이면 원본이 멈춘 것, 0이 아닌데 화면이 안 움직이면 우리 탓이다.
        private static readonly List<float> stillSourceSpeeds = new List<float>();
        private static float windowStart = -1f;

        /// <summary>서버 스냅 도착. 도착 간격이 들쭉날쭉하면 그게 곧 hold의 원인이다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Arrived(string entityId, double clientTime)
        {
            var track = Get(entityId);
            if (track.LastArrival > 0)
            {
                arrivalGaps.Add((float)(clientTime - track.LastArrival));
            }
            track.LastArrival = clientTime;
        }

        /// <summary>[예측 모드] 남의 새 예측이 서버 스냅과 얼마나 어긋났나(=화면이 튈 크기).</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void RemoteError(float error)
        {
            remoteErrors.Add(error);
            MaybeReport();
        }

        /// <summary>
        /// [예측 모드] 남의 입력이 도착했다. <paramref name="ticksAhead"/>는 내 현재 틱보다
        /// 얼마나 과거인가 — 클수록 "이미 지나간 틱"이라 되감기 재생으로만 반영된다.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void RemoteInput(long ticksBehind)
        {
            remoteInputsReceived++;
            if (ticksBehind <= 0)
            {
                remoteInputsAhead++;   // 아직 안 지난 틱 = 예측에 제때 쓸 수 있다
            }
            MaybeReport();
        }

        /// <summary>[외삽 모드] 새 스냅으로 갈아탈 때 화면이 건너뛰어야 할 거리. 블렌드가 숨길 양이다.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Corrected(string entityId, float gap)
        {
            correctionGaps.Add(gap);
            MaybeReport();
        }

        /// <param name="bracketed">감쌀 스냅 쌍을 찾아 보간했나. false면 최신 스냅에 hold한 것이다.</param>
        /// <param name="behind">재생 시계가 최신 스냅보다 얼마나 뒤인가(초). 0 이하면 park.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Rendered(string entityId, Vector3 position, Vector3 sourceVelocity,
                                    bool bracketed, double behind)
        {
            if (windowStart < 0f)
            {
                windowStart = Time.unscaledTime;
            }

            var track = Get(entityId);
            frames++;
            if (bracketed == false)
            {
                holds++;
            }
            behinds.Add((float)behind);

            if (track.HasLast && Time.unscaledDeltaTime > 0f)
            {
                float moved = (position - track.LastPosition).magnitude;
                if (moved < StillEpsilon)
                {
                    stills++;
                    track.StillRun++;
                    stillSourceSpeeds.Add(sourceVelocity.magnitude);
                }
                else
                {
                    if (track.StillRun > 0)
                    {
                        stillRuns.Add(track.StillRun);
                        track.StillRun = 0;
                    }
                }
                speeds.Add(moved / Time.unscaledDeltaTime);
            }
            track.LastPosition = position;
            track.HasLast = true;

            MaybeReport();
        }

        //  창이 찼으면 찍는다. 렌더 훅뿐 아니라 모든 입구에서 부른다 — 모드에 따라 도는 훅이
        //  달라서(예측이면 보간기·외삽기가 아예 안 돈다), 한 곳에만 걸면 조용히 아무것도 안 찍힌다.
        private static void MaybeReport()
        {
            if (windowStart < 0f)
            {
                windowStart = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - windowStart >= WindowSeconds)
            {
                Report();
            }
        }

        private static Track Get(string entityId)
        {
            if (tracks.TryGetValue(entityId, out var track) == false)
            {
                track = new Track();
                tracks[entityId] = track;
            }
            return track;
        }

        private static void Report()
        {
            if (remoteErrors.Count > 0 || remoteInputsReceived > 0)
            {
                Debug.Log(string.Format(
                    "[RemotePredict] 남의새 오차 중앙 {0:F2}m p95 {1:F2}m 최대 {2:F2}m ({3}회)"
                    + " | 입력수신 {4}개 중 제때 도착 {5}개"
                    + " | 화면튐 {6}회 중앙 {7:F2}m p95 {8:F2}m 최대 {9:F2}m",
                    Percentile(remoteErrors, 0.5f), Percentile(remoteErrors, 0.95f),
                    Percentile(remoteErrors, 1f), remoteErrors.Count,
                    remoteInputsReceived, remoteInputsAhead,
                    correctionGaps.Count, Percentile(correctionGaps, 0.5f),
                    Percentile(correctionGaps, 0.95f), Percentile(correctionGaps, 1f)));
                correctionGaps.Clear();
                remoteErrors.Clear();
                remoteInputsReceived = 0; remoteInputsAhead = 0;
                windowStart = Time.unscaledTime;
            }
            if (frames > 0)
            {
                Debug.Log(string.Format(
                    "[RemoteSync] 프레임 {0} · hold {1:P0} · 정지 {2:P0} | 속도 중앙 {3:F1} p95 {4:F1} 최대 {5:F1} m/s"
                    + " | 시각오프셋 중앙 {6:F0}ms (음수=미래를 그림) | 스냅간격 중앙 {8:F0}ms 최대 {9:F0}ms ({10}개)"
                    + " | 정지구간 {11}개 중앙 {12:F0}f 최대 {13:F0}f · 그때 원본속도 중앙 {14:F1} 최대 {15:F1}"
                    + " | 궤적전환 {16}회 건너뛸거리 중앙 {17:F2}m p95 {18:F2}m 최대 {19:F2}m",
                    frames, holds / (float)frames, stills / (float)frames,
                    Percentile(speeds, 0.5f), Percentile(speeds, 0.95f), Percentile(speeds, 1f),
                    Percentile(behinds, 0.5f) * 1000f, Percentile(behinds, 0f) * 1000f,
                    Percentile(arrivalGaps, 0.5f) * 1000f, Percentile(arrivalGaps, 1f) * 1000f,
                    arrivalGaps.Count,
                    stillRuns.Count, Percentile(stillRuns, 0.5f), Percentile(stillRuns, 1f),
                    Percentile(stillSourceSpeeds, 0.5f), Percentile(stillSourceSpeeds, 1f),
                    correctionGaps.Count, Percentile(correctionGaps, 0.5f),
                    Percentile(correctionGaps, 0.95f), Percentile(correctionGaps, 1f)));
            }
            frames = 0; holds = 0; stills = 0;
            speeds.Clear(); behinds.Clear(); arrivalGaps.Clear();
            stillRuns.Clear(); stillSourceSpeeds.Clear(); correctionGaps.Clear();
            windowStart = Time.unscaledTime;
        }

        private static float Percentile(List<float> values, float q)
        {
            if (values.Count == 0)
            {
                return 0f;
            }
            values.Sort();
            int index = Mathf.Clamp(Mathf.RoundToInt(q * (values.Count - 1)), 0, values.Count - 1);
            return values[index];
        }
    }
}
