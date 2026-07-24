using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class EntranceScene : MonoBehaviour
    {
        [Inject]
        private IEnumerable<IEntranceComponent> entranceComponents;

        [Inject]
        private AppStateMachine appStateMachine;

        private async void Start()
        {
            if (await ExecuteEntranceComponents())
            {
                appStateMachine.Fire(AppEvent.BootCompleted);
            }
        }

        private async Task<bool> ExecuteEntranceComponents()
        {
            try
            {
                foreach (var entranceComponent in entranceComponents.OrEmpty())
                {
                    await entranceComponent.Execute();
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
                return false;
            }
        }
    }
}
