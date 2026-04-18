using Engine;
using System;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public class MinimapSettings
    {
        public bool Enable { get; set; } = true;

        public MinimapUpdateRate UpdateRate { get; set; } = MinimapUpdateRate.Normal;

        public MinimapSizeMode SizeMode { get; set; } = MinimapSizeMode.Auto;

        public MapShadingMode ShadingMode { get; set; } = MapShadingMode.Fast;

        public float DisplayScale { get; set; } = 1f;

        public Anchor Anchor { get; set; } = Anchor.TopRight;

        public float MarginX { get; set; } = 64f;

        public float MarginY { get; set; } = 8f;
    }
}