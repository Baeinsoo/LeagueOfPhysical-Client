using GameFramework;
using System.Threading.Tasks;

namespace LOP
{
    /// <summary>로그인으로 확보한 userId로 유저 부속 데이터를 읽어 온다. 계정 생성은 /auth 몫이다.</summary>
    public class LoadUserComponent : IEntranceComponent
    {
        private readonly IUserDataStore userDataStore;

        public LoadUserComponent(IUserDataStore userDataStore)
        {
            this.userDataStore = userDataStore;
        }

        public async Task Execute()
        {
            string userId = userDataStore.user.id;

            var getUser = await WebAPI.GetUser(userId);
            if (getUser.response.code != ResponseCode.SUCCESS)
            {
                throw new System.Exception($"유저 정보를 가져오는데 실패했습니다. code: {getUser.response.code}");
            }

            await WebAPI.GetUserLocation(userId);

            //  큐 목록을 TbQueue에서 읽는 것은 로비 선택 UI 슬라이스 몫이다 —
            //  마스터데이터가 이 컴포넌트보다 뒤에 로드돼서 지금은 값을 안다고 칠 수 없다.
            await WebAPI.GetUserStats(userId, 1);   // TbQueue: Casual
            await WebAPI.GetUserStats(userId, 2);   // TbQueue: Ranked
        }
    }
}
