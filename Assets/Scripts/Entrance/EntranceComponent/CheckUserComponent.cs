using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace LOP
{
    public class CheckUserComponent : IEntranceComponent
    {
        public async Task Execute()
        {
            try
            {
                var getUser = await WebAPI.GetUser(Data.User.user.id);

                switch (getUser.response.code)
                {
                    case ResponseCode.SUCCESS:
                        Data.User.user = MapperConfig.mapper.Map<User>(getUser.response.user);

                        var getUserLocation = await WebAPI.GetUserLocation(getUser.response.user.id);

                        Data.User.userLocation = MapperConfig.mapper.Map<UserLocation>(getUserLocation.response.userLocation);
                        break;

                    case ResponseCode.USER_NOT_EXIST:
                        var createUser = await WebAPI.CreateUser(new CreateUserRequest
                        {
                            username = Data.User.user.username,
                            email = $"{Data.User.user.username} email",
                        });

                        if (createUser.response.code != ResponseCode.SUCCESS)
                        {
                            throw new Exception($"유저 생성에 실패하였습니다. error: {createUser.error}");
                        }

                        Data.User.user = MapperConfig.mapper.Map<User>(createUser.response.user);
                        break;

                    default:
                        throw new Exception($"유저 정보를 가져오는데 실패하였습니다. GetUserResponse code: {getUser.response.code}");
                }
            }
            catch (WebRequestException)
            {

            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }
}
