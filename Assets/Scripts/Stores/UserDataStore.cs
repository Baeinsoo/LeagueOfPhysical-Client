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
        // 큐가 enum이 아니라 데이터(TbQueue 행)라서 전적도 큐 id로 담는다.
        private readonly Dictionary<int, UserStats> statsByQueueId = new();
        public IReadOnlyDictionary<int, UserStats> userStatsByQueueId => statsByQueueId;

        private readonly IDisposable subscriptions;

        public UserDataStore(
            ISubscriber<CreateUserResponse> createUserSubscriber,
            ISubscriber<GetUserLocationResponse> getUserLocationSubscriber,
            ISubscriber<GetUserResponse> getUserSubscriber,
            ISubscriber<GetUserStatsResponse> getUserStatsSubscriber,
            ISubscriber<UpdateUserProfileResponse> updateUserProfileSubscriber)
        {
            var bag = MessagePipe.DisposableBag.CreateBuilder();
            createUserSubscriber.Subscribe(HandleCreateUser).AddTo(bag);
            getUserLocationSubscriber.Subscribe(HandleGetUserLocation).AddTo(bag);
            getUserSubscriber.Subscribe(HandleGetUser).AddTo(bag);
            getUserStatsSubscriber.Subscribe(HandleGetUserStats).AddTo(bag);
            updateUserProfileSubscriber.Subscribe(HandleUpdateUserProfile).AddTo(bag);
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

        private void HandleGetUserStats(GetUserStatsResponse response)
        {
            UserStats userStats = MapperConfig.mapper.Map<UserStats>(response.userStats);

            statsByQueueId[userStats.queueId] = userStats;
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
            statsByQueueId.Clear();
        }
    }
}
