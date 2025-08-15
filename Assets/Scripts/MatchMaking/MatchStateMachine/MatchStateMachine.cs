using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public class MatchStateMachine : MonoStateMachine
    {
        public override IState initState => gameObject.GetOrAddComponentWithInject<CheckMatchState>();

        private void Awake()
        {
            StartStateMachine();
        }
    }
}
