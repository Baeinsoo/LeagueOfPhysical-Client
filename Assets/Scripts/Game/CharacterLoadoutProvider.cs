using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Luban <c>TbCharacterLoadout</c>을 캐릭터별 장착 목록으로 바꾸는 side-local 어댑터.
    /// 표는 int id로 키잉돼 있어 캐릭터 코드로는 못 찾으므로, 생성 시 한 번 색인해 둔다.
    /// </summary>
    public class CharacterLoadoutProvider
    {
        private readonly Dictionary<string, List<(int slot, int abilityId)>> _byCharacter
            = new Dictionary<string, List<(int slot, int abilityId)>>();

        public CharacterLoadoutProvider(LOP.MasterData.LOPMasterData md)
        {
            foreach (var row in md.Tables.TbCharacterLoadout.DataList)
            {
                if (_byCharacter.TryGetValue(row.CharacterCode, out var list) == false)
                {
                    list = new List<(int, int)>();
                    _byCharacter[row.CharacterCode] = list;
                }
                list.Add((row.Slot, row.AbilityId));
            }
        }

        /// <summary>해당 캐릭터의 장착 목록. 없으면 빈 목록.</summary>
        public IReadOnlyList<(int slot, int abilityId)> Get(string characterCode)
        {
            return _byCharacter.TryGetValue(characterCode, out var list)
                ? list
                : System.Array.Empty<(int, int)>();
        }
    }
}
