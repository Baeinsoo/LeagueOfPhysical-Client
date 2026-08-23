using GameFramework;
using GameFramework.Http;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class GetUserLocationResponse
    {
        /// <summary>
        /// locationDetail은 위치마다 모양이 다르다(판별 유니온). 태그(`location`)를 보고 알맞은
        /// 타입으로 되살린다 — 안 그러면 베이스 타입이 되고, 소비자의 `is GameRoomLocationDetail`
        /// 같은 검사가 **조용히 실패**해 화면이 아무 반응도 안 한다.
        /// </summary>
        public static GetUserLocationResponse Deserialize(string json)
        {
            //  프로젝트 표준 설정을 쓴다 — 예전엔 본 경로와 실패 경로가 서로 다른 설정으로
            //  파싱해, 실패했을 때만 동작이 미묘하게 달라질 수 있었다.
            var response = HttpJson.DeserializeObject<GetUserLocationResponse>(json);

            //  실패 응답엔 userLocation이 아예 없다 — 정상이다. 아래를 건너뛴다.
            if (response?.userLocation == null)
            {
                return response;
            }

            var detailToken = JObject.Parse(json)["userLocation"]?["locationDetail"];
            if (detailToken == null || detailToken.Type == JTokenType.Null)
            {
                Debug.LogError($"[UserLocation] locationDetail is missing. location: {response.userLocation.location}");
                return response;
            }

            switch (response.userLocation.location)
            {
                case Location.Matchmaking:
                    response.userLocation.locationDetail = Narrow<MatchmakingLocationDetail>(detailToken, response.userLocation.location);
                    break;

                case Location.GameRoom:
                    response.userLocation.locationDetail = Narrow<GameRoomLocationDetail>(detailToken, response.userLocation.location);
                    break;

                case Location.None:
                    response.userLocation.locationDetail = Narrow<NoneLocationDetail>(detailToken, response.userLocation.location);
                    break;

                default:
                    //  서버가 우리가 모르는 위치를 보냈다. 베이스로 두되 조용히 넘어가지 않는다 —
                    //  클·서 버전이 어긋났다는 뜻이고, 그대로 두면 소비자가 아무것도 못 하고 멈춘다.
                    Debug.LogError($"[UserLocation] unknown location: {(int)response.userLocation.location}");
                    break;
            }

            return response;
        }

        //  파싱이 깨지면 예외를 삼키지 않고 남긴다. 예전엔 여기서 통째로 실패해도 베이스 타입으로
        //  조용히 되돌아가, "왜 팝업이 안 뜨지"의 원인이 로그 어디에도 없었다.
        private static LocationDetail Narrow<T>(JToken token, Location location) where T : LocationDetail
        {
            try
            {
                return token.ToObject<T>();
            }
            catch (System.Exception exception)
            {
                Debug.LogError($"[UserLocation] locationDetail does not match its location. location: {location}, error: {exception.Message}");
                return token.ToObject<LocationDetail>();
            }
        }
    }
}
