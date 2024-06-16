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
            if (!input.TryParse(out MatchStateInput matchStateInput))
            {
                throw new ArgumentException($"Invalid input. input: {input}");
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
