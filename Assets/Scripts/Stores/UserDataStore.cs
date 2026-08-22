using System;
using System.Collections.Generic;
using MessagePipe;
using R3;

namespace LOP
{
    public class UserDataStore : IUserDataStore, IDisposable
    {
        public User user { get; set; } = new User();
        public UserProfile userProfile { get; set; } = new UserProfile();
        private readonly ReactiveProperty<UserLocation> _userLocation = new(new UserLocation());
        public ReadOnlyReactiveProperty<UserLocation> userLocation => _userLocation;
        // 큐가 enum이 아니라 데이터(TbQueue 행)라서 레이팅도 큐 id로 담는다.
        private readonly Dictionary<int, UserRating> ratingByQueueId = new();
        public IReadOnlyDictionary<int, UserRating> userRatingByQueueId => ratingByQueueId;

        private readonly IDisposable subscriptions;

        public UserDataStore(
            ISubscriber<CreateUserResponse> createUserSubscriber,
            ISubscriber<GetUserLocationResponse> getUserLocationSubscriber,
            ISubscriber<GetUserResponse> getUserSubscriber,
            ISubscriber<GetUserRatingResponse> getUserRatingSubscriber,
            ISubscriber<UpdateUserProfileResponse> updateUserProfileSubscriber,
            ISubscriber<ChangeDisplayNameResponse> changeDisplayNameSubscriber)
        {
            var bag = MessagePipe.DisposableBag.CreateBuilder();
            createUserSubscriber.Subscribe(HandleCreateUser).AddTo(bag);
            getUserLocationSubscriber.Subscribe(HandleGetUserLocation).AddTo(bag);
            getUserSubscriber.Subscribe(HandleGetUser).AddTo(bag);
            getUserRatingSubscriber.Subscribe(HandleGetUserRating).AddTo(bag);
            updateUserProfileSubscriber.Subscribe(HandleUpdateUserProfile).AddTo(bag);
            changeDisplayNameSubscriber.Subscribe(HandleChangeDisplayName).AddTo(bag);
            subscriptions = bag.Build();
        }

        public void Dispose()
        {
            subscriptions.Dispose();
            _userLocation.Dispose();
        }

        private void HandleCreateUser(CreateUserResponse response)
        {
            user = MapperConfig.mapper.Map<User>(response.user);
        }

        private void HandleGetUserLocation(GetUserLocationResponse response)
        {
            UserLocation mapped = response.userLocation == null
                ? null
                : MapperConfig.mapper.Map<UserLocation>(response.userLocation);

            //  위치 없는 응답은 무시한다 — null을 발행하면 구독자가 역참조하다 터진다.
            if (mapped == null)
            {
                UnityEngine.Debug.LogWarning("[Location] 응답에 유저 위치가 없어 무시한다.");
                return;
            }

            _userLocation.Value = mapped;
        }

        private void HandleGetUser(GetUserResponse response)
        {
            if (response.user == null)
            {
                return;
            }

            user = MapperConfig.mapper.Map<User>(response.user);
        }

        private void HandleChangeDisplayName(ChangeDisplayNameResponse response)
        {
            //  형식 위반으로 거절되면 user가 안 실려 온다. 그때 매핑하면 유저가 통째로 비워진다.
            if (response.user == null)
            {
                return;
            }

            user = MapperConfig.mapper.Map<User>(response.user);
        }

        private void HandleGetUserRating(GetUserRatingResponse response)
        {
            //  레이팅 행이 없으면 응답 본문에 userRating이 안 실려 온다. 그대로 매핑하면 null이 되고
            //  바로 아래 큐 id를 읽다 터진다 — 위치 응답과 같은 방식으로 무시한다.
            if (response.userRating == null)
            {
                UnityEngine.Debug.LogWarning($"[Rating] 응답에 레이팅이 없어 무시한다. code: {response.code}");
                return;
            }

            UserRating userRating = MapperConfig.mapper.Map<UserRating>(response.userRating);

            ratingByQueueId[userRating.queueId] = userRating;
        }

        private void HandleUpdateUserProfile(UpdateUserProfileResponse response)
        {
            userProfile = MapperConfig.mapper.Map<UserProfile>(response.userProfile);
        }

        public void Clear()
        {
            user = new User();
            userProfile = new UserProfile();
            _userLocation.Value = new UserLocation();
            ratingByQueueId.Clear();
        }
    }
}
