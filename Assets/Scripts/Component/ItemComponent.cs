using VContainer;

namespace LOP
{
    public class ItemComponent : LOPComponent
    {
        [Inject]
        private LOP.MasterData.LOPMasterData md;

        public string itemCode { get; private set; }
        public MasterData.Item masterData { get; private set; }

        public void Initialize(string itemCode)
        {
            this.itemCode = itemCode;
            this.masterData = md.Tables.TbItem.Get(itemCode);
        }
    }
}
