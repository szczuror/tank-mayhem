using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Client;

public class Animation
{
    public List<Rectangle> Frames { get; set; }
    public TimeSpan Delay { get; set; }

    public Animation(int frameWidth, int frameHeight, int frameCount, int msDelay)
    {
        Frames = new List<Rectangle>();
        for (int i = 0; i < frameCount; i++)
        {
            Frames.Add(new Rectangle(i * frameWidth, 0, frameWidth, frameHeight));
        }
        Delay = TimeSpan.FromMilliseconds(msDelay);
    }
}
