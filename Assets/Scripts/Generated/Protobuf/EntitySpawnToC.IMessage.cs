using Google.Protobuf;
using LOP;

public sealed partial class EntitySpawnToC : GameFramework.IMessage
{
    public ushort messageId => MessageIds.EntitySpawnToC;

    public byte[] Serialize()
    {
        return this.ToByteArray();
    }

    public void Deserialize(byte[] data)
    {
        this.MergeFrom(data);
    }
}
