using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 계정 저장 칸을 환경별로 나누기 전에 쓰던 <b>옛 칸</b>의 값을 지금 환경 칸으로 한 번 옮긴다.
    ///
    /// <para>옛 칸에는 환경이 없었다(<c>LOP.Auth.default.Credential</c>). 그래서 dev로 만든 계정과
    /// local로 만든 계정이 한 칸을 나눠 썼고, 환경을 바꿔 접속하면 서버가 "그런 계정 없다"(401)고
    /// 하는 바람에 클라가 그 칸을 지우고 새 계정을 만들었다 — 앞 환경 계정이 그때 사라졌다.</para>
    ///
    /// <para>칸을 나누는 것만으로는 <b>지금 쓰던 계정</b>이 새 칸에 없어 똑같이 새로 만들어진다.
    /// 그래서 한 번만 이사시킨다. 옛 값이 어느 환경 것인지는 알 길이 없으므로 <b>지금 환경 것으로
    /// 본다</b> — 마지막으로 쓰던 환경이 지금 환경일 가능성이 가장 높다.</para>
    /// </summary>
    public static class AuthCredentialKeyMigration
    {
        /// <summary>
        /// 옛 칸에 값이 있고 새 칸이 비었으면 옮긴다. 옮긴 뒤 옛 칸은 지운다 —
        /// 남겨 두면 다음에 다른 환경으로 켰을 때 그 환경으로 또 이사해 계정이 복제된다.
        /// </summary>
        /// <returns>실제로 옮겼으면 true.</returns>
        public static bool MigrateIfNeeded(string legacyKey, string currentKey)
        {
            if (string.IsNullOrEmpty(legacyKey) || string.IsNullOrEmpty(currentKey) || legacyKey == currentKey)
            {
                return false;
            }

            string legacy = PlayerPrefs.GetString(legacyKey, string.Empty);
            if (string.IsNullOrEmpty(legacy))
            {
                return false;
            }

            //  새 칸에 이미 값이 있으면 그쪽이 최신이다. 옛 값으로 덮으면 계정이 뒤바뀐다.
            if (string.IsNullOrEmpty(PlayerPrefs.GetString(currentKey, string.Empty)) == false)
            {
                PlayerPrefs.DeleteKey(legacyKey);
                PlayerPrefs.Save();
                return false;
            }

            PlayerPrefs.SetString(currentKey, legacy);
            PlayerPrefs.DeleteKey(legacyKey);
            PlayerPrefs.Save();
            return true;
        }
    }
}
