using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;
using GameFramework;
using VContainer;

namespace LOP
{
    public class JoinLobbyComponent : IEntranceComponent
    {
        [Inject]
        private IUserDataStore userDataStore;

        public async Task Execute()
        {
            try
            {
                var joinLobby = await WebAPI.JoinLobby(userDataStore.user.id);

                if (joinLobby.response.code != ResponseCode.SUCCESS)
                {
                    throw new Exception($"로비 접속에 실패하였습니다. JoinLobbyResponse code: {joinLobby.response.code}");
                }
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
