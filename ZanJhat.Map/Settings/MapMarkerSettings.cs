using Engine;
using System;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public class MapMarkerSettings
    {
        public bool AutoMarkDeathLocation { get; set; } = true;

        public bool ShowDeathMarker { get; set; } = true;

        public bool ShowSpawnMarker { get; set; } = false;
    }
}