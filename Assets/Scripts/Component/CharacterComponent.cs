using GameFramework;

namespace LOP
{
    public class CharacterComponent : LOPComponent
    {
        public string characterCode { get; private set; }
        public MasterData.Character masterData { get; private set; }

        public void Initialize(string characterCode)
        {
            this.characterCode = characterCode;
            this.masterData = SceneLifetimeScope.Resolve<IMasterDataManager>().GetMasterData<MasterData.Character>(characterCode);
        }
    }
}
