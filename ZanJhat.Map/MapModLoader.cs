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
using Engine.Input;
using System.Globalization;
using Game;
using ZanJhat.Core;
using ZanJhat.Map;

namespace ZanJhat.Map
{
    public class MapModLoader : ModLoader
    {
        public SubsystemMapMarkers m_subsystemMapMarkers;

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("OnProjectLoaded", this);
            ModsManager.RegisterHook("OnCreatureDied", this);
            ModsManager.RegisterHook("OnLoadingFinished", this);
        }

        public override void OnProjectLoaded(Project project)
        {
            m_subsystemMapMarkers = project.FindSubsystem<SubsystemMapMarkers>(true);

            MapManager.OnProjectLoaded();
        }

        public override void OnCreatureDied(ComponentHealth componentHealth, Injury injury, ref int experienceOrbDrop, ref bool calculateInKill)
        {
            if (injury == null)
                return;

            ComponentPlayer targetComponentPlayer = componentHealth?.m_componentPlayer;
            if (targetComponentPlayer != null && MapSettingsManager.MapMarkerSettings.AutoMarkDeathLocation)
            {
                int targetPlayerIndex = targetComponentPlayer.PlayerData.PlayerIndex;
                m_subsystemMapMarkers.Markers.RemoveAll(m => m.MarkerType == MarkerType.Death && m.PlayerIndex == targetPlayerIndex);

                ComponentBody targetComponentBody = targetComponentPlayer.ComponentBody;
                int x = Terrain.ToCell(targetComponentBody.Position.X);
                int y = Terrain.ToCell(targetComponentBody.Position.Y);
                int z = Terrain.ToCell(targetComponentBody.Position.Z);
                m_subsystemMapMarkers.AddMarker("Latest Death", x, y, z, Color.White, false, false, targetComponentPlayer, MarkerType.Death);
            }
        }

        public override void OnLoadingFinished(List<System.Action> actions)
        {
            actions.Add(() =>
            {
                MapSettingsManager.Initialize();
                MapManager.Initialize();
                ScreensManager.AddScreen("WorldMap", new WorldMapScreen());
            });
        }
    }
}
