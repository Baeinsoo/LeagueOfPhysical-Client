using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;
using GameFramework;
using LOP.UI;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class MatchMakingPresenter : MonoBehaviour
    {
        [SerializeField]
        private Button playButton;

        [Inject]
        private IMatchMakingDataStore matchMakingDataStore;

        [Inject]
        private MatchStateMachine matchStateMachine;

        [Inject]
        private IWindowManager windowManager;

        private MatchingWaitingView matchingWaitingView;

        private void Start()
        {
            matchStateMachine.onStateChange += OnStateChange;
            matchStateMachine.Start();

            playButton.OnClickAsObservable().Subscribe(OnPlayButtonClick).AddTo(this);
        }

        private void OnDestroy()
        {
            if (matchStateMachine != null)
            {
                matchStateMachine.onStateChange -= OnStateChange;
                matchStateMachine.Stop();
            }
        }

        private void OnPlayButtonClick(Unit value)
        {
            matchMakingDataStore.matchType = GameMode.Normal;
            matchMakingDataStore.subGameId = "FlapWang";
            matchMakingDataStore.mapId = "FlapWangMap";

            matchStateMachine.Fire(MatchEvent.PlayClicked);
        }

        private void OnStateChange(IState<MatchEvent> previous, IState<MatchEvent> current)
        {
            switch (previous)
            {
                case InWaitingRoom:
                    if (matchingWaitingView != null)
                    {
                        windowManager.Close(matchingWaitingView);
                        matchingWaitingView = null;
                    }
                    break;
            }

            switch (current)
            {
                case InWaitingRoom:
                    matchingWaitingView = windowManager.Open<MatchingWaitingView>();
                    matchingWaitingView.SetCancelCallback(() =>
                    {
                        matchStateMachine.Fire(MatchEvent.CancelClicked);
                    });
                    break;
            }
        }
    }
}
