using GameFramework;

namespace LOP
{
    public class ItemComponent : LOPComponent
    {
        public string itemCode { get; private set; }
        public MasterData.Item masterData { get; private set; }

        public void Initialize(string itemCode)
        {
            this.itemCode = itemCode;
            this.masterData = SceneLifetimeScope.Resolve<IMasterDataManager>().GetMasterData<MasterData.Item>(itemCode);
        }
    }
}
