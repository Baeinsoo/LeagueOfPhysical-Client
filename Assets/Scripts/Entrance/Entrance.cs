using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace LOP
{
    public class Entrance : MonoBehaviour
    {
        [Inject]
        private IEnumerable<IEntranceComponent> entranceComponents;

        private async void Start()
        {
            if (await ExecuteEntranceComponents())
            {
                SceneManager.LoadScene("Lobby");
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
