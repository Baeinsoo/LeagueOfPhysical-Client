using System;

namespace LOP
{
    /// <summary>큐 하나에서의 유저 실력·전적. 레이팅 엔진의 내부 추정치(mu/sigma)는 서버 밖으로 나오지 않는다.</summary>
    public class UserRating
    {
        public string userId;
        public int queueId;
        //  매칭이 읽는 정수 실력값.
        public int mmr;
        public int gamesPlayed;
        public int firstPlaces;
        //  등수의 총합. 판수로 나누면 평균 등수가 된다.
        public int placementSum;
    }
}
