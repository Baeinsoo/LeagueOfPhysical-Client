using LOP;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class MessageInitializer
    {
        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            MessageFactory.RegisterCreator(MessageIds.GameInfoRequest, () => new GameInfoRequest());
            MessageFactory.RegisterCreator(MessageIds.GameInfoResponse, () => new GameInfoResponse());
        }
    }
}
