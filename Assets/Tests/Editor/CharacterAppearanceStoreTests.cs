using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>로비가 보는 "내 캐릭터 외형" — 조회·변경 응답 반영. 실패하면 이전 값을 그대로 둔다(EconomyStore와 같은 규칙).</summary>
    public class CharacterAppearanceStoreTests
    {
        private sealed class FakeUserDataStore : IUserDataStore
        {
            public User user { get; set; }
            public UserProfile userProfile { get; set; }
            public ReadOnlyReactiveProperty<UserLocation> userLocation => null;
            public IReadOnlyDictionary<int, UserRating> userRatingByQueueId => null;
            public void Clear() { }
        }

        private static CharacterAppearanceStore NewStore(Func<string, CancellationToken, UniTask<CharacterAppearanceResponse>> fetch) =>
            new CharacterAppearanceStore(new FakeUserDataStore { user = new User { id = "me" } }, fetch);

        private static CharacterAppearanceResponse Ok(int skin, int hair) => new CharacterAppearanceResponse
        {
            code = ResponseCode.SUCCESS,
            characterAppearance = new CharacterAppearanceDto { skinColorId = skin, hairColorId = hair },
        };

        [Test]
        public void 받기_전엔_0이다()
        {
            Assert.AreEqual(default(CharacterAppearance), NewStore((_, __) => UniTask.FromResult(Ok(1, 1))).Current.CurrentValue);
        }

        [Test]
        public void 조회가_성공하면_반영한다()
        {
            var store = NewStore((_, __) => UniTask.FromResult(Ok(103, 202)));

            store.RefreshAsync(CancellationToken.None).Forget();

            Assert.AreEqual(new CharacterAppearance(103, 202), store.Current.CurrentValue);
        }

        [Test]
        public void 조회가_실패하면_이전_값을_유지한다()
        {
            var response = Ok(103, 202);
            var store = NewStore((_, __) => UniTask.FromResult(response));
            store.RefreshAsync(CancellationToken.None).Forget();

            response = new CharacterAppearanceResponse { code = ResponseCode.USER_NOT_EXIST };
            store.RefreshAsync(CancellationToken.None).Forget();

            Assert.AreEqual(new CharacterAppearance(103, 202), store.Current.CurrentValue);
        }

        [Test]
        public void 변경_응답을_반영하고_거절이면_그대로다()
        {
            var store = NewStore((_, __) => UniTask.FromResult(Ok(101, 201)));

            store.Apply(Ok(104, 203));
            Assert.AreEqual(new CharacterAppearance(104, 203), store.Current.CurrentValue);

            store.Apply(new CharacterAppearanceResponse { code = ResponseCode.INVALID_APPEARANCE_COLOR });
            Assert.AreEqual(new CharacterAppearance(104, 203), store.Current.CurrentValue);
        }

        //  조회가 떠난 뒤 변경이 먼저 반영되면, 늦게 온 조회는 낡은 값이다 — 덮지 않는다.
        [Test]
        public void 변경_뒤에_도착한_옛_조회는_버린다()
        {
            var pending = new UniTaskCompletionSource<CharacterAppearanceResponse>();
            var store = NewStore((_, __) => pending.Task);

            store.RefreshAsync(CancellationToken.None).Forget();
            store.Apply(Ok(105, 204));
            pending.TrySetResult(Ok(101, 201));

            Assert.AreEqual(new CharacterAppearance(105, 204), store.Current.CurrentValue);
        }
    }
}
