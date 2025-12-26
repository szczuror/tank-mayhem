using Microsoft.Xna.Framework;

namespace Client;

public class Bullet
{
    public byte PlayerId;
    public Vector2 Position;
    public float Rotation;
    public float Speed = GameConstants.BulletSpeed;
    public float Lifetime = GameConstants.BulletLifetime;
}
