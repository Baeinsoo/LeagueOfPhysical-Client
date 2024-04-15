using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

public class JoinLobbyComponent : MonoBehaviour, IEntranceComponent
{
    public async Task Execute()
    {
        await UniTask.Delay(TimeSpan.FromSeconds(1));
    }
}
