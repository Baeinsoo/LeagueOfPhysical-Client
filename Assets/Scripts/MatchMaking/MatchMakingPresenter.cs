using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using GameFramework;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class MatchMakingPresenter : MonoBehaviour
    {
        [SerializeField]
        private MatchStateMachine matchStateMachine;

        [SerializeField]
        private Button playButton;

        [Inject]
        private IDataContextManager dataManager;

        private void Start()
        {
            playButton.OnClickAsObservable().Subscribe(OnPlayButtonClick).AddTo(this);

            matchStateMachine.onStateChange += OnStateChange;
        }

        private void OnDestroy()
        {
            matchStateMachine.onStateChange -= OnStateChange;
        }

        private void OnPlayButtonClick(Unit value)
        {
            dataManager.Get<MatchMakingDataContext>().matchType = MatchType.Friendly;
            dataManager.Get<MatchMakingDataContext>().subGameId = "FlapWang";
            dataManager.Get<MatchMakingDataContext>().mapId = "FlapWangMap";

            matchStateMachine.ProcessInput(MatchStateInput.RequestMatchmaking);
        }

        private void OnStateChange(IState previous, IState current)
        {
            switch (previous)
            {
                case InWaitingRoom:
                    MatchingWaitingUI.Hide();
                    break;

                case InGameRoom:
                    GameLoadingUI.Hide();
                    break;
            }

            switch (current)
            {
                case InWaitingRoom:
                    MatchingWaitingUI.Show(() =>
                    {
                        matchStateMachine.ProcessInput(MatchStateInput.CancelMatchmaking);
                    });
                    break;

                case InGameRoom:
                    GameLoadingUI.Show();
                    break;
            }
        }
    }
}
