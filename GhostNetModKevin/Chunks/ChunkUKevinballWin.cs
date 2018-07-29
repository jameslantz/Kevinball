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
    public class ChunkUKevinballWin : IChunk
    {

        public const string ChunkID = "nUkW";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint Winner;
        public uint Loser;
        public uint WinType; 

        public void Read(BinaryReader reader)
        {
            Winner = reader.ReadUInt32();
            Loser = reader.ReadUInt32();
            WinType = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Winner);
            writer.Write(Loser);
            writer.Write(WinType);
        }

        public object Clone()
            => new ChunkUKevinballWin
            {
                Winner = Winner, 
                Loser = Loser,
                WinType = WinType
            };

        public static implicit operator ChunkUKevinballWin(GhostNetFrame frame)
            => frame.Get<ChunkUKevinballWin>();

    }
}

