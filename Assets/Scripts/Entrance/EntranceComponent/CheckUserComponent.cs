using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CheckUserComponent : IEntranceComponent
    {
        [Inject]
        private IUserDataStore userDataStore;

        public async Task Execute()
        {
            try
            {
                var getUser = await WebAPI.GetUserByUsername(userDataStore.user.username);

                switch (getUser.response.code)
                {
                    case ResponseCode.SUCCESS:
                        var getUserLocation = await WebAPI.GetUserLocation(userDataStore.user.id);
                        break;

                    case ResponseCode.USER_NOT_EXIST:
                        var createUser = await WebAPI.CreateUser(new CreateUserRequest
                        {
                            username = userDataStore.user.username,
                            email = userDataStore.user.email,
                        });

                        if (createUser.response.code != ResponseCode.SUCCESS)
                        {
                            throw new Exception($"유저 생성에 실패하였습니다. error: {createUser.error}");
                        }
                        break;

                    default:
                        throw new Exception($"유저 정보를 가져오는데 실패하였습니다. GetUserResponse code: {getUser.response.code}");
                }

                var getNormalUserStats = await WebAPI.GetUserStats(userDataStore.user.id, GameMode.Normal);
                var getRankedUserStats = await WebAPI.GetUserStats(userDataStore.user.id, GameMode.Ranked);
            }
            catch (WebRequestException e)
            {
                Debug.LogError(e);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}
