using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameFramework;
using VContainer;

namespace LOP
{
    public class LoadMasterDataComponent : IEntranceComponent
    {
        [Inject]
        private IMasterDataManager masterDataManager;

        public async Task Execute()
        {
            await masterDataManager.LoadMasterData();
        }
    }
}
