using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public static class MessageFactory
    {
        private static Dictionary<ushort, Func<IMessage>> messageCreators;

        static MessageFactory()
        {
            messageCreators = new Dictionary<ushort, Func<IMessage>>();
        }

        public static void RegisterCreator(ushort messageId, Func<IMessage> creator)
        {
            if (!messageCreators.ContainsKey(messageId))
            {
                messageCreators.Add(messageId, creator);
            }
            else
            {
                Debug.LogWarning($"messageCreators already contains messageId. messageId: {messageId}");
            }
        }

        public static IMessage CreateMessage(ushort messageId)
        {
            if (messageCreators.TryGetValue(messageId, out var creator))
            {
                return creator();
            }
            throw new Exception($"No message registered with messageId: {messageId}");
        }
    }
}
