using Google.Protobuf;
using LOP;

public sealed partial class EntityDespawnToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.EntityDespawnToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
