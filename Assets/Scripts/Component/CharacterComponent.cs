using GameFramework;
using VContainer;

namespace LOP
{
    public class CharacterComponent : LOPComponent
    {
        [Inject]
        private IMasterDataManager masterDataManager;

        public string characterCode { get; private set; }
        public MasterData.Character masterData { get; private set; }

        public void Initialize(string characterCode)
        {
            this.characterCode = characterCode;
            this.masterData = masterDataManager.GetMasterData<MasterData.Character>(characterCode);
        }
    }
}
