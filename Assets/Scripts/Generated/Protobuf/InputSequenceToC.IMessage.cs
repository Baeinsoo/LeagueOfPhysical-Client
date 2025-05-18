using Google.Protobuf;
using LOP;

public sealed partial class InputSequenceToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.InputSequenceToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
