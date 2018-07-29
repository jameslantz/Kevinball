using Celeste;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.GhostKevinball.Net
{
    [Tracked(false)]
    public class KevinballP1SpawnTrigger : Trigger
    {
        public KevinballP1SpawnTrigger(EntityData data, Vector2 offset)
            : base(data, offset)
        {
        }
    }

}