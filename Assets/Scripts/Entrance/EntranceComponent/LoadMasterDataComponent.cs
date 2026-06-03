using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using VContainer;

namespace LOP
{
    public class LoadMasterDataComponent : IEntranceComponent
    {
        [Inject]
        private LOP.MasterData.LOPMasterData masterData;

        public async Task Execute()
        {
            await masterData.LoadAsync();
        }
    }
}
