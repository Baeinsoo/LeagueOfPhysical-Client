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
            var getUser = WebAPI.GetUser(Data.User.user.id);
            await getUser;

            if (getUser.isSuccess == false || getUser.response.code != ResponseCode.SUCCESS)
            {
                throw new Exception($"유저 정보를 가져오는데 실패하였습니다. error: {getUser.error}");
            }

            Data.User.user = MapperConfig.mapper.Map<User>(getUser.response.user);

            switch (getUser.response.user.location)
            {
                case Location.InGameRoom:
                    var roomId = (getUser.response.user.locationDetail as GameRoomLocationDetail).gameRoomId;
                    var getRoom = WebAPI.GetRoom(roomId);

                    await getRoom;

                    if (getRoom.isSuccess == false || getRoom.response.code != ResponseCode.SUCCESS)
                    {
                        throw new Exception($"룸 정보를 가져오는데 실패하였습니다. error: {getRoom.error}");
                    }

                    if (getRoom.response.room.status == RoomStatus.Ready || getRoom.response.room.status == RoomStatus.Playing)
                    {
                        //RoomConnector.Instance.TryToEnterRoomById(roomId);
                        Debug.LogWarning($"RoomConnector is not implemented yet.");
                    }
                    break;

                case Location.InWaitingRoom:
                default:
                    SceneManager.LoadScene("Lobby");
                    break;
            }
        }
    }
}
