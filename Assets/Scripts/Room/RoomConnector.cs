using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameFramework;
using System;

namespace LOP
{
    public class RoomConnector
    {
        private const int DEFAULT_RETRY_COUNT = 10;
        private const int RETRY_INTERVAL_MILLISECONDS = 1000;

        public async Task<bool> TryToEnterRoomById(string roomId, int retryCount = DEFAULT_RETRY_COUNT)
        {
            for (int attempt = 0; attempt < retryCount; attempt++)
            {
                try
                {
                    var checkRoomJoinable = await WebAPI.CheckRoomJoinable(roomId);

                    if (checkRoomJoinable.response.code == ResponseCode.SUCCESS)
                    {
                        SceneManager.LoadScene("Room");
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error checking room joinability (Attempt {attempt + 1}/{retryCount}): {e.Message}");
                }

                if (attempt < retryCount - 1)
                {
                    await Task.Delay(RETRY_INTERVAL_MILLISECONDS);
                }
            }

            Debug.LogError($"Failed to join room after {retryCount} attempts");
            return false;
        }
    }
}
