using Microsoft.Xna.Framework;

namespace Client;

public class Bullet
{
    public byte PlayerId;
    public Vector2 Position;
    public float Rotation;
    public float Speed = 20f;
    public float Lifetime = 2.0f;
}
