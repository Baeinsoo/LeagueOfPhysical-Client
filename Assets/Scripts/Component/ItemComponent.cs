using GameFramework;
using VContainer;

namespace LOP
{
    public class ItemComponent : LOPComponent
    {
        [Inject]
        private IMasterDataManager masterDataManager;

        public string itemCode { get; private set; }
        public MasterData.Item masterData { get; private set; }

        public void Initialize(string itemCode)
        {
            this.itemCode = itemCode;
            this.masterData = masterDataManager.GetMasterData<MasterData.Item>(itemCode);
        }
    }
}
