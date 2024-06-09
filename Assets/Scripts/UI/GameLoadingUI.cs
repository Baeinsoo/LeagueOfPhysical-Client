using GameFramework;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LOP
{
    public class GameLoadingUI : MonoSingleton<GameLoadingUI>
    {
        [SerializeField] private TextMeshProUGUI message;

        public static void Show()
        {
            instance.gameObject.SetActive(true);
        }

        public static void Hide()
        {
            instance.gameObject.SetActive(false);
        }

        public static GameLoadingUI CreateInstance()
        {
            return PrefabReferences.instance.Instantiate<GameLoadingUI>(CanvasManager.instance.loadingCanvas.transform);
        }
    }
}
