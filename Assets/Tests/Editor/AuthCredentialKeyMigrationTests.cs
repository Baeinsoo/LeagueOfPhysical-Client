using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// 계정 저장 칸을 환경별로 나누면서 붙인 일회성 이사 규칙을 고정한다. 여기가 틀리면 개발자가
    /// 쓰던 계정이 조용히 사라지거나(안 옮김) 환경마다 같은 계정이 복제된다(여러 번 옮김).
    /// </summary>
    public class AuthCredentialKeyMigrationTests
    {
        //  실제 PlayerPrefs를 쓰므로 다른 키와 겹치지 않는 이름으로 두고 끝나면 지운다.
        private const string LegacyKey = "테스트.LOP.Auth.default.Credential";
        private const string CurrentKey = "테스트.LOP.Auth.dev.default.Credential";
        private const string OtherKey = "테스트.LOP.Auth.local-k8s.default.Credential";

        [SetUp]
        [TearDown]
        public void 칸을_비운다()
        {
            PlayerPrefs.DeleteKey(LegacyKey);
            PlayerPrefs.DeleteKey(CurrentKey);
            PlayerPrefs.DeleteKey(OtherKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void 옛_칸의_계정을_지금_환경_칸으로_옮긴다()
        {
            PlayerPrefs.SetString(LegacyKey, "{\"Secret\":\"옛계정\"}");

            bool moved = AuthCredentialKeyMigration.MigrateIfNeeded(LegacyKey, CurrentKey);

            Assert.IsTrue(moved);
            Assert.AreEqual("{\"Secret\":\"옛계정\"}", PlayerPrefs.GetString(CurrentKey, string.Empty));
        }

        [Test]
        public void 옮긴_뒤_옛_칸은_비운다()
        {
            //  남겨 두면 다음에 다른 환경으로 켰을 때 그 환경으로 또 이사해 계정이 복제된다.
            PlayerPrefs.SetString(LegacyKey, "{\"Secret\":\"옛계정\"}");

            AuthCredentialKeyMigration.MigrateIfNeeded(LegacyKey, CurrentKey);

            Assert.IsEmpty(PlayerPrefs.GetString(LegacyKey, string.Empty));
        }

        [Test]
        public void 한_번_옮긴_뒤_다른_환경으로_켜도_또_옮기지_않는다()
        {
            PlayerPrefs.SetString(LegacyKey, "{\"Secret\":\"옛계정\"}");
            AuthCredentialKeyMigration.MigrateIfNeeded(LegacyKey, CurrentKey);

            bool movedAgain = AuthCredentialKeyMigration.MigrateIfNeeded(LegacyKey, OtherKey);

            Assert.IsFalse(movedAgain);
            Assert.IsEmpty(PlayerPrefs.GetString(OtherKey, string.Empty),
                "다른 환경 칸까지 같은 계정으로 채우면 두 환경이 한 계정을 나눠 쓰게 된다");
        }

        [Test]
        public void 지금_칸에_이미_계정이_있으면_덮지_않는다()
        {
            PlayerPrefs.SetString(LegacyKey, "{\"Secret\":\"옛계정\"}");
            PlayerPrefs.SetString(CurrentKey, "{\"Secret\":\"지금계정\"}");

            bool moved = AuthCredentialKeyMigration.MigrateIfNeeded(LegacyKey, CurrentKey);

            Assert.IsFalse(moved);
            Assert.AreEqual("{\"Secret\":\"지금계정\"}", PlayerPrefs.GetString(CurrentKey, string.Empty));
            Assert.IsEmpty(PlayerPrefs.GetString(LegacyKey, string.Empty), "쓸 일이 없어진 옛 칸은 치운다");
        }

        [Test]
        public void 옛_칸이_비어_있으면_아무_일도_하지_않는다()
        {
            bool moved = AuthCredentialKeyMigration.MigrateIfNeeded(LegacyKey, CurrentKey);

            Assert.IsFalse(moved);
            Assert.IsEmpty(PlayerPrefs.GetString(CurrentKey, string.Empty));
        }
    }
}
