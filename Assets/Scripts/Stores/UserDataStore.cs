using System;
using MessagePipe;

namespace LOP
{
    public class UserDataStore : IUserDataStore, IDisposable
    {
        public User user { get; set; } = new User();
        public UserProfile userProfile { get; set; } = new UserProfile();
        public UserLocation userLocation { get; set; } = new UserLocation();
        public UserStats normalUserStats { get; set; }
        public UserStats rankedUserStats { get; set; }

        private readonly IDisposable subscriptions;

        public UserDataStore(
            ISubscriber<CreateUserResponse> createUserSubscriber,
            ISubscriber<GetUserLocationResponse> getUserLocationSubscriber,
            ISubscriber<GetUserResponse> getUserSubscriber,
            ISubscriber<GetUserStatsResponse> getUserStatsSubscriber,
            ISubscriber<UpdateUserProfileResponse> updateUserProfileSubscriber)
        {
            var bag = DisposableBag.CreateBuilder();
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
        }

        private void HandleCreateUser(CreateUserResponse response)
        {
            user = MapperConfig.mapper.Map<User>(response.user);
        }

        private void HandleGetUserLocation(GetUserLocationResponse response)
        {
            userLocation = MapperConfig.mapper.Map<UserLocation>(response.userLocation);
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

            if (userStats.gameMode == GameMode.Normal)
            {
                normalUserStats = userStats;
            }
            else if (userStats.gameMode == GameMode.Ranked)
            {
                rankedUserStats = userStats;
            }
        }

        private void HandleUpdateUserProfile(UpdateUserProfileResponse response)
        {
            userProfile = MapperConfig.mapper.Map<UserProfile>(response.userProfile);
        }

        public void Clear()
        {
            user = new User();
            userProfile = new UserProfile();
            userLocation = new UserLocation();
            normalUserStats = null;
            rankedUserStats = null;
        }
    }
}
