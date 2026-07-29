using System;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 게스트 로그인이 쓰는 기기 신원. 서버는 이 값을 username으로 받아 유저를 찾거나 만든다.
    /// </summary>
    public static class DeviceIdentifier
    {
        private static string cached;

        public static string Current => cached ??= Build();

        private static string Build()
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 한 PC에서 클라를 두 개 띄우면 deviceUniqueIdentifier가 같아서 서버가 둘을 같은 유저로 본다.
            // 그러면 자기 자신과 매칭을 잡으려다 거부되므로 멀티 테스트가 불가능하다.
            // Multiplayer Play Mode가 인스턴스마다 넘겨주는 -name(Player1/Player2/...)으로 갈라준다.
            // MPPM에는 "몇 번 플레이어인가"를 알려주는 API가 없어서, 공식 문서도 이 인자를 읽으라고 안내한다.
            // 구분자는 URL에 그대로 넣어도 안전한 문자여야 한다. username이 조회 API의 경로
            // 세그먼트(/user/username/{username})로 들어가는데, '#'을 쓰면 프래그먼트로 잘려
            // 서버가 접미사 없는 이름을 받는다 — 조회는 늘 실패하고 생성만 반복된다.
            string instanceName = ReadCommandLineValue("-name");
            if (string.IsNullOrEmpty(instanceName) == false && instanceName != "Player1")
            {
                return $"{deviceId}-{instanceName}";
            }
#endif

            return deviceId;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string ReadCommandLineValue(string key)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == key)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
#endif
    }
}
