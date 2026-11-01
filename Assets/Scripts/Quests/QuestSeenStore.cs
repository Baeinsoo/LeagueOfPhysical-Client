using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 퀘스트 판에서 이미 본 "끝난 칸" id — 로비 배지(새로 끝난 퀘스트 수)가 쓴다. 화면만의 상태라 서버에 두지 않는다
    /// (spec 2026-10-30 §5). 시험은 가짜를 꽂는다.
    /// </summary>
    public interface IQuestSeenStore
    {
        bool IsSeen(string questId);

        void MarkSeen(IEnumerable<string> questIds);
    }

    /// <summary>기기 저장(PlayerPrefs). 칸 id는 기간마다 새로 생기니 오래된 것부터 밀어내 최근 64개만 둔다.</summary>
    public class PlayerPrefsQuestSeenStore : IQuestSeenStore
    {
        private const string Key = "LOP.Quests.Seen";
        private const int Max = 64;

        private readonly List<string> _ids = new List<string>();

        public PlayerPrefsQuestSeenStore()
        {
            string stored = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(stored)) _ids.AddRange(stored.Split(','));
        }

        public bool IsSeen(string questId) => _ids.Contains(questId);

        public void MarkSeen(IEnumerable<string> questIds)
        {
            bool changed = false;
            foreach (var id in questIds)
            {
                if (string.IsNullOrEmpty(id) || _ids.Contains(id)) continue;
                _ids.Add(id);
                changed = true;
            }
            if (!changed) return;
            if (_ids.Count > Max) _ids.RemoveRange(0, _ids.Count - Max);
            PlayerPrefs.SetString(Key, string.Join(",", _ids));
            PlayerPrefs.Save();
        }
    }
}
