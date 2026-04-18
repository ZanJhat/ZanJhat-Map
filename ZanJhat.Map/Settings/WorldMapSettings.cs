using Engine;
using System;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public class WorldMapSettings
    {
        //public int Size { get; set; } = 4096;

        public MapShadingMode ShadingMode { get; set; } = MapShadingMode.Fast;

        public bool SaveChunkMap { get; set; } = true;
    }
}