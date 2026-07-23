using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LOP
{
    public class LoadMasterDataComponent : IEntranceComponent
    {
        private readonly LOP.MasterData.LOPMasterData masterData;

        public LoadMasterDataComponent(LOP.MasterData.LOPMasterData masterData)
        {
            this.masterData = masterData;
        }

        public async Task Execute()
        {
            await masterData.LoadAsync();
        }
    }
}
