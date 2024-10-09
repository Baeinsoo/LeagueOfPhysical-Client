using Google.Protobuf;
using LOP;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class GameInfoRequest : GameFramework.IMessage
{
    public ushort messageId => MessageIds.GameInfoRequest;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
