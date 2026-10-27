using System.Globalization;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 개인 최고를 남기는 자리(spec §7). 지금은 기기(<see cref="PlayerPrefsRecordStore"/>) — 퀘스트가 서버 저장을 만들면
    /// 이 등록(Root 스코프) 하나만 서버 구현으로 갈아 끼운다. 시험은 가짜를 쓴다.
    /// </summary>
    public interface IPersonalRecordStore
    {
        bool TryGetBest(string key, out double best);

        void SaveBest(string key, double best);
    }

    /// <summary>
    /// 기기 저장. 값은 double 문자열(<c>"R"</c>, InvariantCulture)로 남긴다 — PlayerPrefs의 float는 52.3을 52.2999992로 바꾼다.
    /// <para>저장 뒤 <c>PlayerPrefs.Save()</c> — 판마다 지표 수만큼뿐이라 싸고, 폰에서 앱이 죽어도 기록이 남는다.</para>
    /// </summary>
    public sealed class PlayerPrefsRecordStore : IPersonalRecordStore
    {
        public bool TryGetBest(string key, out double best)
        {
            best = 0d;
            if (string.IsNullOrEmpty(key) || PlayerPrefs.HasKey(key) == false)
            {
                return false;
            }
            //  다른 형식(int·float)으로 남은 칸이면 GetString이 기본값을 준다 — 없는 것으로 친다.
            return double.TryParse(PlayerPrefs.GetString(key, string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out best);
        }

        public void SaveBest(string key, double best)
        {
            PlayerPrefs.SetString(key, best.ToString("R", CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }
    }
}
