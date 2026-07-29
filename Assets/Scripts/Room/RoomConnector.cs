using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;
using System;

namespace LOP
{
    public class RoomConnector
    {
        //  게임서버 파드가 뜨는 데 걸리는 시간을 기다려야 한다(스케줄링 + 이미지 pull + 유니티 부팅).
        //  서버의 하트비트 임계값(room-server HEARTBEAT_THRESHOLD)과 같은 60초로 맞춘다 —
        //  한쪽만 짧으면 그쪽이 먼저 포기해서 다른 쪽을 늘린 의미가 없다.
        private const int DEFAULT_RETRY_COUNT = 60;
        private const int RETRY_INTERVAL_MILLISECONDS = 1000;

        private IRoomDataStore roomDataStore;

        public RoomConnector(IRoomDataStore roomDataStore)
        {
            this.roomDataStore = roomDataStore;
        }

        //  방 배정 확인(성공 시 RoomJoinableResponse 구독이 RoomDataStore.room을 채움).
        //  씬 로드는 하지 않는다 — 호출자가 성공을 받아 AppStateMachine에 MatchFound를 발행하고,
        //  실제 Room 씬 로드는 AppStateMachine의 InMatch 진입이 담당한다.
        public async Task<bool> TryToEnterRoomById(string roomId, int retryCount = DEFAULT_RETRY_COUNT)
        {
            for (int attempt = 0; attempt < retryCount; attempt++)
            {
                try
                {
                    var checkRoomJoinable = await WebAPI.CheckRoomJoinable(roomId);

                    if (checkRoomJoinable.response.code == ResponseCode.SUCCESS)
                    {
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
