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
    public class ChunkMKevinballStart : IChunk
    {

        public const string ChunkID = "nMkS";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint Player1;
        public uint Player2;

        public void Read(BinaryReader reader)
        {
            Player1 = reader.ReadUInt32();
            Player2 = reader.ReadUInt32(); 
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Player1);
            writer.Write(Player2);
        }

        public object Clone()
            => new ChunkMKevinballStart
            {
                Player1 = Player1,
                Player2 = Player2,
            };

        public static implicit operator ChunkMKevinballStart(GhostNetFrame frame)
            => frame.Get<ChunkMKevinballStart>();

    }
}
