using GameFramework.Http;
using NUnit.Framework;

namespace LOP.Tests
{
    //  1a 백엔드(GetEconomyResponseDto/PurchaseResponseDto/SetLoadoutResponseDto)가 보내는 JSON을
    //  클라가 실제로 쓰는 역직렬화기(HttpJson, Newtonsoft)로 그대로 읽는지 본다.
    public class EconomyDtoTests
    {
        [Test]
        public void 이코노미_조회_응답을_읽는다()
        {
            const string json = "{\"code\":200,\"wallets\":[{\"currencyId\":1,\"balance\":0}],"
                + "\"progress\":{\"level\":1,\"xp\":0,\"xpIntoLevel\":0,\"xpToNext\":500},"
                + "\"loadout\":[{\"slotId\":1,\"userCosmeticId\":\"u1\",\"cosmeticId\":101},"
                + "{\"slotId\":2,\"userCosmeticId\":\"u2\",\"cosmeticId\":201},"
                + "{\"slotId\":3,\"userCosmeticId\":\"u3\",\"cosmeticId\":301},"
                + "{\"slotId\":4,\"userCosmeticId\":\"u4\",\"cosmeticId\":401},"
                + "{\"slotId\":5,\"userCosmeticId\":\"u5\",\"cosmeticId\":501},"
                + "{\"slotId\":6,\"userCosmeticId\":\"u6\",\"cosmeticId\":601},"
                + "{\"slotId\":7,\"userCosmeticId\":\"u7\",\"cosmeticId\":701}],"
                + "\"owned\":[{\"id\":\"u1\",\"cosmeticId\":101,\"source\":\"DEFAULT\",\"acquiredAt\":\"2026-10-10T00:00:00.000Z\"}]}";

            var r = HttpJson.DeserializeObject<GetEconomyResponse>(json);

            Assert.AreEqual(0, r.wallets[0].balance);
            Assert.AreEqual(500, r.progress.xpToNext);
            Assert.AreEqual(7, r.loadout.Length);
            Assert.AreEqual(101, r.loadout[0].cosmeticId);
            Assert.AreEqual(1, r.loadout[0].slotId);
            Assert.AreEqual("DEFAULT", r.owned[0].source);
        }

        [Test]
        public void 구매_성공_응답을_읽는다()
        {
            const string json = "{\"code\":200,"
                + "\"owned\":{\"id\":\"oc1\",\"cosmeticId\":102,\"source\":\"PURCHASE\",\"acquiredAt\":\"2026-10-10T00:00:00.000Z\"},"
                + "\"wallets\":[{\"currencyId\":1,\"balance\":700}],"
                + "\"loadout\":[{\"slotId\":1,\"userCosmeticId\":\"oc1\",\"cosmeticId\":102}]}";

            var r = HttpJson.DeserializeObject<PurchaseCosmeticResponse>(json);

            Assert.AreEqual(ResponseCode.SUCCESS, r.code);
            Assert.AreEqual(102, r.owned.cosmeticId);
            Assert.AreEqual(700, r.wallets[0].balance);
            Assert.AreEqual(102, r.loadout[0].cosmeticId);
        }

        [Test]
        public void 구매_거절_응답은_코드만_온다()
        {
            //  잔액 부족 — 거절 응답은 owned/wallets/loadout 필드 자체가 없다(economy.service.ts).
            const string json = "{\"code\":80000}";

            var r = HttpJson.DeserializeObject<PurchaseCosmeticResponse>(json);

            Assert.AreEqual(80000, r.code);
            Assert.IsNull(r.owned);
            Assert.IsNull(r.wallets);
            Assert.IsNull(r.loadout);
        }

        [Test]
        public void 로드아웃_설정_응답을_읽는다()
        {
            const string json = "{\"code\":200,\"loadout\":[{\"slotId\":1,\"userCosmeticId\":\"oc1\",\"cosmeticId\":102}]}";

            var r = HttpJson.DeserializeObject<SetLoadoutResponse>(json);

            Assert.AreEqual(1, r.loadout.Length);
            Assert.AreEqual("oc1", r.loadout[0].userCosmeticId);
        }

        [Test]
        public void 보상이_있는_참가자를_읽는다()
        {
            const string json = "{\"userId\":\"U1\",\"displayName\":\"a\",\"placement\":1,\"mmrBefore\":1000,\"mmrAfter\":1100,\"stats\":{},"
                + "\"reward\":{\"coins\":50,\"xp\":120,\"firstWin\":true,\"levelBefore\":1,\"levelAfter\":2,\"xpAfter\":120}}";

            var p = HttpJson.DeserializeObject<MatchHistoryParticipantDto>(json);

            Assert.IsNotNull(p.reward);
            Assert.AreEqual(50, p.reward.coins);
            Assert.AreEqual(120, p.reward.xp);
            Assert.IsTrue(p.reward.firstWin);
            Assert.AreEqual(1, p.reward.levelBefore);
            Assert.AreEqual(2, p.reward.levelAfter);
            Assert.AreEqual(120, p.reward.xpAfter);
        }

        [Test]
        public void 보상이_없는_참가자는_reward가_null이다()
        {
            //  나감·너무 짧음·동결·지급 꺼짐이면 서버가 reward 필드 자체를 안 싣는다.
            const string json = "{\"userId\":\"U2\",\"displayName\":\"b\",\"placement\":2,\"mmrBefore\":1000,\"mmrAfter\":900,\"stats\":{}}";

            var p = HttpJson.DeserializeObject<MatchHistoryParticipantDto>(json);

            Assert.IsNull(p.reward);
        }

        [Test]
        public void 이코노미_응답_코드가_백엔드_값과_같다()
        {
            //  responseCode.interface.ts의 Economy region과 1:1 — 값이 어긋나면 클라가 다른 사유로 오해한다.
            Assert.AreEqual(80000, ResponseCode.INSUFFICIENT_FUNDS);
            Assert.AreEqual(80001, ResponseCode.COSMETIC_ALREADY_OWNED);
            Assert.AreEqual(80002, ResponseCode.COSMETIC_NOT_PURCHASABLE);
            Assert.AreEqual(80003, ResponseCode.PRICE_MISMATCH);
            Assert.AreEqual(80004, ResponseCode.COSMETIC_NOT_OWNED);
            Assert.AreEqual(80005, ResponseCode.ACCOUNT_FROZEN);
            Assert.AreEqual(80006, ResponseCode.ECONOMY_DISABLED);
            Assert.AreEqual(80007, ResponseCode.IDEMPOTENCY_CONFLICT);
            Assert.AreEqual(80008, ResponseCode.COSMETIC_NOT_EXIST);
            Assert.AreEqual(80009, ResponseCode.SLOT_MISMATCH);
            Assert.AreEqual(80010, ResponseCode.WALLET_FULL);
            Assert.AreEqual(80011, ResponseCode.ALREADY_REVERSED);
        }
    }
}
