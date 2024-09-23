using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using GameFramework;

namespace LOP
{
    public class JoinLobbyComponent : IEntranceComponent
    {
        public async Task Execute()
        {
            try
            {
                var joinLobby = await WebAPI.JoinLobby(Data.User.user.id);

                if (joinLobby.response.code != ResponseCode.SUCCESS)
                {
                    throw new Exception($"로비 접속에 실패하였습니다. JoinLobbyResponse code: {joinLobby.response.code}");
                }

                SceneManager.LoadScene("Lobby");
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
