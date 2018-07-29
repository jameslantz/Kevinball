using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.GhostKevinball.Net
{
    [Tracked(false)]
    public class KevinballP2SpawnTrigger : Trigger
    {
        public KevinballP2SpawnTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
        }
    }

}