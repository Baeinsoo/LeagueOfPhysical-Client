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
        private readonly IUserDataStore userDataStore;

        public CheckUserComponent(IUserDataStore userDataStore)
        {
            this.userDataStore = userDataStore;
        }

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

                // 큐 목록을 TbQueue에서 읽는 것은 로비 선택 UI 슬라이스 몫이다 —
                // 마스터데이터가 이 컴포넌트보다 뒤에 로드돼서 지금은 값을 안다고 칠 수 없다.
                await WebAPI.GetUserStats(userDataStore.user.id, 1);   // TbQueue: Casual
                await WebAPI.GetUserStats(userDataStore.user.id, 2);   // TbQueue: Ranked
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
