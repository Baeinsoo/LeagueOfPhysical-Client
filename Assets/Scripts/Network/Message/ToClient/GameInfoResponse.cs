using Google.Protobuf;
using LOP;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class GameInfoResponse : GameFramework.IMessage
{
    public ushort messageId => MessageIds.GameInfoResponse;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
