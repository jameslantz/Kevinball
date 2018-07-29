using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Celeste.Mod.GhostKevinball.Net
{
    [Chunk(ChunkID)]
    /// <summary>
    /// Update chunk sent on (best case) each "collision" frame.
    /// A player always receives this with a HHead.PlayerID of the "colliding" player.
    /// </summary>
    public class ChunkUCrushHit : IChunk
    {

        public const string ChunkID = "nUcH";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint With;
        public uint Dir; 

        public void Read(BinaryReader reader)
        {
            With = reader.ReadUInt32();
            Dir = reader.ReadUInt32(); 
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(With);
            writer.Write(Dir); 
        }

        public object Clone()
            => new ChunkUCrushHit
            {
                With = With,
                Dir = Dir
            };

        public static implicit operator ChunkUCrushHit(GhostNetFrame frame)
            => frame.Get<ChunkUCrushHit>();

    }
}
