using System;
using Microsoft.Xna.Framework;

namespace Client;

public class Explosion 
{
    public Vector2 Position;
    public int CurrentFrame = 0;
    public TimeSpan Elapsed = TimeSpan.Zero;
    public bool Finished = false;

    public void Update(GameTime gameTime, Animation animation)
    {
        Elapsed += gameTime.ElapsedGameTime;
        if (Elapsed >= animation.Delay)
        {
            Elapsed -= animation.Delay;
            CurrentFrame++;
            if (CurrentFrame >= animation.Frames.Count)
            {
                Finished = true;
            }
        }
    }
}
