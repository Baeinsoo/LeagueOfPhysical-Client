using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using GameFramework;

namespace LOP
{
    public class JoinLobbyComponent : MonoBehaviour, IEntranceComponent
    {
        public async Task Execute()
        {
            var joinLobby = WebAPI.JoinLobby(Data.User.user.id);

            await joinLobby;

            if (joinLobby.isSuccess == false || joinLobby.response.code != ResponseCode.SUCCESS)
            {
                throw new Exception($"로비 접속에 실패하였습니다. error: {joinLobby.error}");
            }

            SceneManager.LoadScene("Lobby");
        }
    }
}
