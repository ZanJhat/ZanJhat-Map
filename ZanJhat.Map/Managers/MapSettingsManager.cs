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
using System.Text.RegularExpressions;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public static class MapSettingsManager
    {
        public static GlobalMapSettings GlobalMapSettings;
        public static MapMarkerSettings MapMarkerSettings;

        public static void Initialize()
        {
            RegisterModSettings();
            ResolveSettings();
            RegisterSettingsScreen();
        }

        public static void RegisterModSettings()
        {
            CoreSettingsManager.Register(new GlobalMapSettings());
            CoreSettingsManager.Register(new MapMarkerSettings());
        }

        public static void ResolveSettings()
        {
            GlobalMapSettings = CoreSettingsManager.Get<GlobalMapSettings>();
            MapMarkerSettings = CoreSettingsManager.Get<MapMarkerSettings>();

        }

        public static void RegisterSettingsScreen()
        {
            // Global Map
            SettingsScreenRegistry.Register("Global Map", builder =>
            {
                builder.AddToggle("Enable",
                    () => GlobalMapSettings.Enable,
                    v => GlobalMapSettings.Enable = v);

                builder.AddEnum(
                    "Update Rate",
                    () => GlobalMapSettings.UpdateRate,
                    v => GlobalMapSettings.UpdateRate = v,
                    v => Regex.Replace(v.ToString(), "([a-z])([A-Z])", "$1 $2"));

                builder.AddEnum(
                     "Size",
                     () => GlobalMapSettings.SizeMode,
                     v => GlobalMapSettings.SizeMode = v,
                     v =>
                     {
                         int i = (int)v;

                         if (i == 0)
                             return "Auto";

                         return i.ToString();
                     }
                 );

                builder.AddEnum(
                    "Shading",
                    () => GlobalMapSettings.ShadingMode,
                    v => GlobalMapSettings.ShadingMode = v,
                    v => Regex.Replace(v.ToString(), "([a-z])([A-Z])", "$1 $2"));

                builder.AddSlider("Display Scale",
                    () => GlobalMapSettings.DisplayScale,
                    v => GlobalMapSettings.DisplayScale = v,
                    0.5f, 2f);

                builder.AddEnum(
                     "Anchor",
                     () => GlobalMapSettings.Anchor,
                     v => GlobalMapSettings.Anchor = v,
                     v => Regex.Replace(v.ToString(), "([a-z])([A-Z])", "$1 $2"));

                builder.AddSlider("Margin X",
                   () => GlobalMapSettings.MarginX,
                   v => GlobalMapSettings.MarginX = v,
                   0f, 256f, 1f);

                builder.AddSlider("Margin Y",
                   () => GlobalMapSettings.MarginY,
                   v => GlobalMapSettings.MarginY = v,
                   0f, 128f, 1f);

                builder.AddToggle("Save Chunk Map",
                    () => GlobalMapSettings.SaveChunkMap,
                    v => GlobalMapSettings.SaveChunkMap = v);
            });

            // Map Marker
            SettingsScreenRegistry.Register("Map Marker", builder =>
            {
                builder.AddToggle("Auto Mark Death",
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
