using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using GameFramework;
using System;

namespace LOP
{
    public class MatchingWaitingUI : MonoSingleton<MatchingWaitingUI>
    {
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private Button cancelButton;

        private event Action onCancelClick;

        private void Start()
        {
            cancelButton.onClick.AsObservable().Subscribe(_ =>
            {
                onCancelClick?.Invoke();
            })
            .AddTo(this)
            ;
        }

        public static void Show(Action onCancelClick = null)
        {
            instance.gameObject.SetActive(true);

            instance.onCancelClick += onCancelClick;
        }

        public static void Hide()
        {
            instance.gameObject.SetActive(false);

            instance.onCancelClick = null;
        }

        public static MatchingWaitingUI CreateInstance()
        {
            return PrefabReferences.instance.Instantiate<MatchingWaitingUI>(CanvasManager.instance.loadingCanvas.transform);
        }
    }
}
