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
        private Button playButton;

        [Inject]
        private IMatchMakingDataStore matchMakingDataStore;

        [Inject]
        private MatchStateMachine matchStateMachine;

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
                    MatchingWaitingUI.Hide();
                    break;
            }

            switch (current)
            {
                case InWaitingRoom:
                    MatchingWaitingUI.Show(() =>
                    {
                        matchStateMachine.Fire(MatchEvent.CancelClicked);
                    });
                    break;
            }
        }
    }
}
