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
    public class ChunkUSpikeTrigger : IChunk
    {

        public const string ChunkID = "nUsT";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint TIdx;
        public uint With;
        public uint SInfo; 

        public void Read(BinaryReader reader)
        {
            TIdx = reader.ReadUInt32();
            With = reader.ReadUInt32();
            SInfo = reader.ReadUInt32(); 
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(TIdx);
            writer.Write(With);
            writer.Write(SInfo);
        }

        public object Clone()
            => new ChunkUSpikeTrigger
            {
                TIdx = TIdx,
                With = With,
                SInfo = SInfo
            };

        public static implicit operator ChunkUSpikeTrigger(GhostNetFrame frame)
            => frame.Get<ChunkUSpikeTrigger>();

    }
}
