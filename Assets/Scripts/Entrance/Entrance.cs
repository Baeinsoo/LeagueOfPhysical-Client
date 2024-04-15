using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Entrance : MonoBehaviour
{
    private IEntranceComponent[] entranceComponents;

    private async void Start()
    {
        entranceComponents = GetComponents<IEntranceComponent>();

        if (await ExecuteEntranceComponents())
        {
            SceneManager.LoadScene("Lobby");
        }
    }

    private async Task<bool> ExecuteEntranceComponents()
    {
        try
        {
            foreach (var entranceComponent in entranceComponents ?? Enumerable.Empty<IEntranceComponent>())
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
