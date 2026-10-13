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

        [Inject]
        private EntranceProgress entranceProgress;

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
                var components = entranceComponents.ToList();
                for (int i = 0; i < components.Count; i++)
                {
                    entranceProgress.Begin(i, components.Count, components[i].StepLabel);
                    await components[i].Execute();
                }

                entranceProgress.Complete();
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
