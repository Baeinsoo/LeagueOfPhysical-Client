using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameFramework;

namespace LOP
{
    public class CheckLocationComponent : IEntranceComponent
    {
        public async Task Execute()
        {
            var getUserLocation = await WebAPI.GetUserLocation(Data.User.user.id);

            if (getUserLocation.response.code != ResponseCode.SUCCESS)
            {
                throw new Exception($"유저 위치 정보를 가져오는데 실패하였습니다. GetUserLocation code: {getUserLocation.response.code}");
            }

            Data.User.userLocation = MapperConfig.mapper.Map<UserLocation>(getUserLocation.response.userLocation);

            switch (getUserLocation.response.userLocation.location)
            {
                case Location.GameRoom:
                    var roomId = (Data.User.userLocation.locationDetail as GameRoomLocationDetail).gameRoomId;
                    await new RoomConnector().TryToEnterRoomById(roomId);
                    break;

                case Location.WaitingRoom:
                default:
                    SceneManager.LoadScene("Lobby");
                    break;
            }
        }
    }
}
