using Engine;
using Engine.Graphics;
using Engine.Media;
using Engine.Serialization;
using GameEntitySystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using TemplatesDatabase;
using System.IO;
using System.Text;
using XmlUtilities;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public static class MapSettingsManager
    {
        public static MinimapSettings MinimapSettings;
        public static WorldMapSettings WorldMapSettings;
        public static MapMarkerSettings MapMarkerSettings;

        public static void Initialize()
        {
            RegisterModSettings();

            MinimapSettings = CoreSettingsManager.Get<MinimapSettings>();
            WorldMapSettings = CoreSettingsManager.Get<WorldMapSettings>();
            MapMarkerSettings = CoreSettingsManager.Get<MapMarkerSettings>();

            RegisterSettingsScreen();
        }

        public static void RegisterModSettings()
        {
            CoreSettingsManager.Register(new MinimapSettings());
            CoreSettingsManager.Register(new WorldMapSettings());
            CoreSettingsManager.Register(new MapMarkerSettings());
        }

        public static void RegisterSettingsScreen()
        {
            // Minimap
            SettingsScreenRegistry.Register(builder =>
            {
                builder.AddHeader("Minimap");

                builder.AddToggle("Enable",
                    () => MinimapSettings.Enable,
                    v => MinimapSettings.Enable = v);

                builder.AddEnum(
                    "Update Rate",
                    () => MinimapSettings.UpdateRate,
                    v => MinimapSettings.UpdateRate = v,
                    v => v.ToString().Replace("VeryLow", "Very Low")
                );

                builder.AddEnum(
                     "Size",
                     () => MinimapSettings.SizeMode,
                     v => MinimapSettings.SizeMode = v,
                     v =>
                     {
                         int i = (int)v;

                         if (i == 0) return "Auto";

                         return i.ToString();
                     }
                 );

                builder.AddEnum(
                    "Shading",
                    () => MinimapSettings.ShadingMode,
                    v => MinimapSettings.ShadingMode = v,
                    v => v.ToString()
                    );

                builder.AddSlider("Display Scale",
                    () => MinimapSettings.DisplayScale,
                    v => MinimapSettings.DisplayScale = v,
                    0.5f, 2f);

                builder.AddEnum(
                     "Anchor",
                     () => MinimapSettings.Anchor,
                     v => MinimapSettings.Anchor = v,
                     v =>
                     {
                         int i = (int)v;

                         if (i == 0) return "Top Left";

                         if (i == 1) return "Top Right";

                         if (i == 2) return "Bottom Left";

                         if (i == 3) return "Bottom Right";

                         return v.ToString();
                     }
                 );

                builder.AddSlider("Margin X",
                   () => MinimapSettings.MarginX,
                   v => MinimapSettings.MarginX = v,
                   0f, 256f, 1f);

                builder.AddSlider("Margin Y",
                   () => MinimapSettings.MarginY,
                   v => MinimapSettings.MarginY = v,
                   0f, 128f, 1f);
            });

            // World Map
            SettingsScreenRegistry.Register(builder =>
            {
                builder.AddHeader("World Map");

                builder.AddEnum(
                    "Shading",
                    () => WorldMapSettings.ShadingMode,
                    v => WorldMapSettings.ShadingMode = v,
                    v => v.ToString()
                    );

                builder.AddToggle("Save Chunk Map",
                    () => WorldMapSettings.SaveChunkMap,
                    v => WorldMapSettings.SaveChunkMap = v);
            });

            // Map Marker
            SettingsScreenRegistry.Register(builder =>
            {
                builder.AddHeader("Map Marker");

                builder.AddToggle("Save Chunk Map",
                    () => MapMarkerSettings.AutoMarkDeathLocation,
                    v => MapMarkerSettings.AutoMarkDeathLocation = v);

                builder.AddToggle("Show Death Marker",
                    () => MapMarkerSettings.ShowDeathMarker,
                    v => MapMarkerSettings.ShowDeathMarker = v);

                builder.AddToggle("Show Spawn Marker",
                    () => MapMarkerSettings.ShowSpawnMarker,
                    v => MapMarkerSettings.ShowSpawnMarker = v);
            });
        }
    }
}
