using Google.Protobuf;
using LOP;

public sealed partial class StatAllocationToS : GameFramework.IMessage
{
    public ushort messageId => MessageIds.StatAllocationToS;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
