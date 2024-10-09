using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public static class CustomMirrorMessageSerializer
    {
        public static void WriteCustomMirrorMessage(this NetworkWriter writer, CustomMirrorMessage value)
        {
            writer.WriteUShort(value.payload.messageId);
            writer.WriteBytesAndSize(value.payload.Serialize());
        }

        public static CustomMirrorMessage ReadCustomMirrorMessage(this NetworkReader reader)
        {
            ushort id = reader.ReadUShort();
            var payload = MessageFactory.CreateMessage(id);
            payload.Deserialize(reader.ReadBytesAndSize());

            return new CustomMirrorMessage
            {
                payload = payload,
            };
        }
    }
}
