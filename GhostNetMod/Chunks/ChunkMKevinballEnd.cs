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
    /// Make an emote spawn above the player.
    /// </summary>
    public class ChunkMKevinballEnd : IChunk
    {

        public const string ChunkID = "nMkE";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint Winner;
        public uint Wintype;
        public uint NextLevel; 

        public void Read(BinaryReader reader)
        {
            Winner = reader.ReadUInt32();
            Wintype = reader.ReadUInt32();
            NextLevel = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Winner);
            writer.Write(Wintype);
            writer.Write(NextLevel);
        }

        public object Clone()
            => new ChunkMKevinballEnd
            {
                Winner = Winner,
                Wintype = Wintype,
                NextLevel = NextLevel
            };

        public static implicit operator ChunkMKevinballEnd(GhostNetFrame frame)
            => frame.Get<ChunkMKevinballEnd>();

    }
}
