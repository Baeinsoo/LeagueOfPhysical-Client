using GameFramework;
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
            writer.WriteBytesAndSize(value.payload.CompressionSerialize());
        }

        public static CustomMirrorMessage ReadCustomMirrorMessage(this NetworkReader reader)
        {
            byte[] data = reader.ReadBytesAndSize();
            var payload = GetMirrorMessage(data);

            return new CustomMirrorMessage
            {
                payload = payload,
            };
        }

        public static IMessage GetMirrorMessage(byte[] data)
        {
            return data.CompressionDeserialize() as IMessage;
        }
    }
}
