using Microsoft.Xna.Framework;

namespace Client;

public class KillMessage
{
    public string Text;
    public float Timer = GameConstants.KillFeedDisplayTime;
    public Color Color = Color.White;
}
