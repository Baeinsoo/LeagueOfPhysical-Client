using GameFramework;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LOP
{
    public class LOPMasterDataManager : IMasterDataManager
    {
        private Dictionary<Type, Dictionary<string, IMasterData>> masterDataMap = new Dictionary<Type, Dictionary<string, IMasterData>>();

        public async Task LoadMasterData()
        {
            var characterCollection = await MasterDataLoader.LoadFromCSV<MasterData.Character>("MasterData/Character.csv");
            RegisterMasterData(characterCollection);

            var skinCollection = await MasterDataLoader.LoadFromCSV<MasterData.Skin>("MasterData/Skin.csv");
            RegisterMasterData(skinCollection);

            var actionCollection = await MasterDataLoader.LoadFromCSV<MasterData.Action>("MasterData/Action.csv");
            RegisterMasterData(actionCollection);

            var itemCollection = await MasterDataLoader.LoadFromCSV<MasterData.Item>("MasterData/Item.csv");
            RegisterMasterData(itemCollection);
        }

        public void RegisterMasterData<T>(IEnumerable<T> collection) where T : IMasterData
        {
            if (collection == null)
            {
                Debug.LogError($"Trying to register null MasterData: {typeof(T).Name}");
                return;
            }

            masterDataMap[typeof(T)] = collection.ToDictionary(data => data.Code, data => (IMasterData)data);
        }

        public T GetMasterData<T>(string code) where T : IMasterData    
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (masterDataMap.TryGetValue(typeof(T), out var dataDict) == false)
            {
                throw new KeyNotFoundException($"Master data type {typeof(T).Name} not found");
            }

            if (dataDict.TryGetValue(code, out var masterData) == false)
            {
                throw new KeyNotFoundException($"Code {code} not found in {typeof(T).Name}");
            }

            return (T)masterData;
        }

        public IEnumerable<T> GetMasterData<T>() where T : IMasterData
        {
            if (masterDataMap.TryGetValue(typeof(T), out var dataDict) == false)
            {
                Debug.LogWarning($"MasterData collection not found: {typeof(T).Name}");
                return Array.Empty<T>();
            }

            return dataDict.Values.Cast<T>();
        }

        public bool TryGetMasterData<T>(string code, out T masterData) where T : IMasterData
        {
            masterData = default;

            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            if (masterDataMap.TryGetValue(typeof(T), out var dataDict) == false)
            {
                return false;
            }

            if (!dataDict.TryGetValue(code, out var data))
            {
                return false;
            }

            masterData = (T)data;
            return true;
        }
    }
}
