using VContainer;

namespace LOP
{
    public class CharacterComponent : LOPComponent
    {
        [Inject]
        private LOP.MasterData.LOPMasterData md;

        public string characterCode { get; private set; }
        public MasterData.Character masterData { get; private set; }

        public void Initialize(string characterCode)
        {
            this.characterCode = characterCode;
            this.masterData = md.Tables.TbCharacter.Get(characterCode);
        }
    }
}
