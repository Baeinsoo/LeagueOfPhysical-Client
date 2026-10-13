using System;
using GameFramework.Http;

namespace LOP
{
    /// <summary>
    /// 서버에 아예 닿지 못한 실패인가 — 응답 코드가 없는 <see cref="HttpRequestException"/>(연결 실패·시간 초과)이 그 신호다.
    /// 401·500처럼 코드가 있으면 서버가 답한 것이라 "로그인 실패"·"서버 오류"로 다룬다.
    /// 단 502·503·504는 입구(ingress·프록시)가 "뒤쪽 서버가 없다/아직 준비 안 됐다"고 대신 답한 것이라 닿지 못한 것과 같다.
    /// </summary>
    public static class ConnectionFailure
    {
        public static bool Is(Exception exception)
        {
            for (var e = exception; e != null; e = e.InnerException)
            {
                if (e is HttpRequestException http && (http.StatusCode == null || IsGatewayDown(http.StatusCode.Value)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>입구가 대신 답한 "뒤쪽 서버 없음" — 502 Bad Gateway · 503 Service Unavailable · 504 Gateway Timeout.</summary>
        public static bool IsGatewayDown(long statusCode) => statusCode == 502 || statusCode == 503 || statusCode == 504;
    }
}
