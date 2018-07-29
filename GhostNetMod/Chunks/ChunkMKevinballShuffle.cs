﻿using FMOD.Studio;
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
    public class ChunkMKevinballShuffle : IChunk
    {

        public const string ChunkID = "nMkSh";

        public bool IsValid => true;
        public bool IsSendable => true;

        public uint NextLevel;

        public void Read(BinaryReader reader)
        {
            NextLevel = reader.ReadUInt32();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(NextLevel);
        }

        public object Clone()
            => new ChunkMKevinballShuffle
            {
                NextLevel = NextLevel
            };

        public static implicit operator ChunkMKevinballShuffle(GhostNetFrame frame)
            => frame.Get<ChunkMKevinballShuffle>();

    }
}
