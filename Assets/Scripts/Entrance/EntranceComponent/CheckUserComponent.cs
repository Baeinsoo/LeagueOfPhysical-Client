using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    public class CheckUserComponent : IEntranceComponent
    {
        public async Task Execute()
        {
            var getUser = WebAPI.GetUser(Data.User.user.id);
            await getUser;

            if (getUser.isSuccess == false)
            {
                throw new Exception($"유저 정보를 가져오는데 실패하였습니다. error: {getUser.error}");
            }

            switch (getUser.response.code)
            {
                case ResponseCode.SUCCESS:

                    Data.User.user = MapperConfig.mapper.Map<User>(getUser.response.user);

                    var verifyUserLocation = WebAPI.VerifyUserLocation(getUser.response.user.id);
                    await verifyUserLocation;

                    if (verifyUserLocation.isSuccess == false || verifyUserLocation.response.code != ResponseCode.SUCCESS)
                    {
                        throw new Exception($"유저 위치 정보를 가져오는데 실패하였습니다. error: {verifyUserLocation.error}");
                    }

                    Data.User.user = MapperConfig.mapper.Map<User>(verifyUserLocation.response.user);
                    break;

                case ResponseCode.USER_NOT_EXIST:

                    var createUser = WebAPI.CreateUser(new CreateUserRequest
                    {
                        id = Data.User.user.id,
                        nickname = $"{Data.User.user.id} nickname",
                    });

                    await createUser;

                    if (createUser.isSuccess == false || createUser.response.code != ResponseCode.SUCCESS)
                    {
                        throw new Exception($"유저 생성에 실패하였습니다. error: {createUser.error}");
                    }

                    Data.User.user = MapperConfig.mapper.Map<User>(createUser.response.user);
                    break;

                default:
                    throw new Exception($"유저 정보를 가져오는데 실패하였습니다. getUser.response.code: {getUser.response.code}");
            }
        }
    }
}
