namespace Shared;

public class DamagePacket
{
    public byte TargetId { get; set; }
    public int DamageAmount { get; set; }
    public byte AttackerId { get; set; }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((byte)3);
        writer.Write(TargetId);
        writer.Write(AttackerId);
        writer.Write(DamageAmount);
        return ms.ToArray();
    }

    public static DamagePacket FromBytes(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        reader.ReadByte();
        return new DamagePacket {
            TargetId = reader.ReadByte(),
            AttackerId = reader.ReadByte(),
            DamageAmount = reader.ReadInt32()
        };
    }
}