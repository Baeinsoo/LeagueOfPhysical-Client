using GameFramework;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class GetUserLocationResponse
    {
        public static GetUserLocationResponse Deserialize(string json)
        {
            try
            {
                var getUserLocationResponse = JsonConvert.DeserializeObject<GetUserLocationResponse>(json);

                var jObject = JObject.Parse(json);
                var locationDetail = jObject["userLocation"]["locationDetail"];

                switch (getUserLocationResponse.userLocation.location)
                {
                    case Location.WaitingRoom:
                        getUserLocationResponse.userLocation.locationDetail = locationDetail.ToObject<WaitingRoomLocationDetail>();
                        break;

                    case Location.GameRoom:
                        getUserLocationResponse.userLocation.locationDetail = locationDetail.ToObject<GameRoomLocationDetail>();
                        break;
                }

                return getUserLocationResponse;
            }
            catch
            {
                return WebRequestJson.DeserializeObject<GetUserLocationResponse>(json);
            }
        }
    }
}
