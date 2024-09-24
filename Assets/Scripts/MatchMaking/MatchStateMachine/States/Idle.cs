using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class Idle : MonoState
    {
        public override IState GetNext<I>(I input)
        {
            if (input is not MatchStateInput matchStateInput)
            {
                throw new ArgumentException($"Invalid input type. Expected MatchStateInput, got {typeof(I).Name}");
            }

            switch (matchStateInput)
            {
                case MatchStateInput.RequestMatchmaking:
                    return gameObject.GetOrAddComponent<RequestMatchmaking>();
            }

            throw new ArgumentException($"Invalid transition: {GetType().Name} with {matchStateInput}");
        }
    }
}
