namespace Shared;

public class BulletPacket
{
    public byte PlayerId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((byte)2);
        writer.Write(PlayerId);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(Rotation);
        return ms.ToArray();
    }

    public static BulletPacket FromBytes(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        reader.ReadByte();
        return new BulletPacket {
            PlayerId = reader.ReadByte(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            Rotation = reader.ReadSingle()
        };
    }
}