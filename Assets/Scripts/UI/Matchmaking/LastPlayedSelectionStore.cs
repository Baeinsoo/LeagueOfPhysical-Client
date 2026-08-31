using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 마지막으로 <b>플레이한</b> 게임·맵을 이 기기에 기억해 둔다. 로비에 들어올 때마다 처음 항목으로
    /// 돌아가면, 늘 같은 걸 하는 사람이 매번 두 번씩 골라야 한다.
    ///
    /// <para><b>순번이 아니라 id를 저장한다.</b> 목록에서 몇 번째였는지를 저장하면 마스터데이터에
    /// 게임이나 맵이 추가·삭제되는 순간 엉뚱한 것이 선택된다 — 그것도 조용히. id는 그 변화에 안 흔들리고,
    /// 없어졌으면 못 찾았다는 게 드러나 첫 항목으로 떨어뜨릴 수 있다.</para>
    ///
    /// <para>기기 로컬 캐시라 <see cref="PlayerPrefs"/>를 쓴다(이 프로젝트가 인증 자격증명에 쓰는 것과
    /// 같은 자리). 서버로 보내는 값이 아니고, 지워져도 첫 항목으로 시작할 뿐이라 잃어도 되는 값이다.</para>
    /// </summary>
    public class LastPlayedSelectionStore
    {
        private const string GameModeKey = "LOP.LastPlayed.GameModeId";
        private const string MapKey = "LOP.LastPlayed.MapId";

        /// <summary>저장된 적이 없음을 뜻하는 값. 마스터데이터 id는 1부터라 0과 겹치지 않는다.</summary>
        private const int None = 0;

        public void Save(int gameModeId, int mapId)
        {
            PlayerPrefs.SetInt(GameModeKey, gameModeId);
            PlayerPrefs.SetInt(MapKey, mapId);

            //  즉시 디스크에 쓴다. 안 쓰면 앱이 정상 종료될 때만 저장되는데, 플레이 버튼을 누른
            //  직후는 곧바로 게임 씬으로 넘어가는 지점이라 그 뒤에 무슨 일이 있을지 보장이 없다.
            PlayerPrefs.Save();
        }

        /// <summary>저장된 것이 있으면 true. 없으면 out 값은 의미 없다.</summary>
        public bool TryLoad(out int gameModeId, out int mapId)
        {
            gameModeId = PlayerPrefs.GetInt(GameModeKey, None);
            mapId = PlayerPrefs.GetInt(MapKey, None);
            return gameModeId != None && mapId != None;
        }
    }
}
