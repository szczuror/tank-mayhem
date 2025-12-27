namespace Shared;

public class TankState
{
    public const int MaxHealth = 100;
    public byte Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float HullRotation { get; set; }
    public float TurretRotation { get; set; }
    public int Health { get; set; } = MaxHealth;
    public int Kills { get; set; } = 0;
    public string Name { get; set; } = "Player";
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public float TargetHullRotation { get; set; }
    public float TargetTurretRotation { get; set; }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write((byte)1);
        writer.Write(Id);
        writer.Write(Name);
        writer.Write(X);
        writer.Write(Y);
        writer.Write(HullRotation);
        writer.Write(TurretRotation);
        writer.Write(Health);
        writer.Write(Kills);
        return ms.ToArray();
    }

    public static TankState FromBytes(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);
        reader.ReadByte();
        return new TankState {
            Id = reader.ReadByte(),
            Name = reader.ReadString(),
            X = reader.ReadSingle(),
            Y = reader.ReadSingle(),
            HullRotation = reader.ReadSingle(),
            TurretRotation = reader.ReadSingle(),
            Health = reader.ReadInt32(),
            Kills = reader.ReadInt32(),
        };
    }
}