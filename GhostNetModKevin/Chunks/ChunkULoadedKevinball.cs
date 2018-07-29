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
    public class ChunkULoadedKevinball : IChunk
    {

        public const string ChunkID = "nUlK";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint With;

        public void Read(BinaryReader reader)
        {
            With = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(With);
        }

        public object Clone()
            => new ChunkULoadedKevinball
            {
                With = With
            };

        public static implicit operator ChunkULoadedKevinball(GhostNetFrame frame)
            => frame.Get<ChunkULoadedKevinball>();

    }
}

