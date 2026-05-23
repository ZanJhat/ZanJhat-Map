using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using Engine;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public class WorldMapScreen : Screen
    {
        public ComponentMinimap ComponentMinimap
        {
            get => m_componentMinimap;
            set => m_componentMinimap = value;
        }

        private ComponentMinimap m_componentMinimap;

        private SubsystemWorldMap m_subsystemWorldMap => m_componentMinimap?.m_subsystemWorldMap;

        private SubsystemMapMarkers m_subsystemMapMarkers => m_componentMinimap?.m_subsystemMapMarkers;

        public SliderWidget m_zoomSlider;
        public WorldMapWidget m_mapContent;
        public LabelWidget m_infoLabel;
        public ButtonWidget m_locationButton;
        public ButtonWidget m_markerButton;
        public ButtonWidget m_moreButton;

        public string[] Locations = { "Player", "Global Spawn", "Spawn" };
        public string[] Markers = { "Add", "Hide", "Show", "Delete", "Clear All" };
        public string[] MoreOptions = { "Clear explored data" };

        public WorldMapScreen()
        {
            XElement node = ContentManager.Get<XElement>("Screens/WorldMapScreen");
            LoadContents(this, node);

            m_zoomSlider = Children.Find<SliderWidget>("ZoomSlider");
            m_zoomSlider.MinValue = 1f;
            m_zoomSlider.MaxValue = 10f;

            m_mapContent = Children.Find<WorldMapWidget>("MapContent");
            m_infoLabel = Children.Find<LabelWidget>("InfoLabel");
            m_locationButton = Children.Find<ButtonWidget>("LocationButton");
            m_markerButton = Children.Find<ButtonWidget>("MarkerButton");
            m_moreButton = Children.Find<ButtonWidget>("MoreButton");
        }

        public override void Enter(object[] parameters)
        {
            m_componentMinimap = (ComponentMinimap)parameters[0];
        }

        public override void Leave()
        {
            m_componentMinimap = null;
            m_mapContent.ComponentMinimap = null;
        }

        public override void Update()
        {
            Vector3 playerPosition = m_componentMinimap.m_componentPlayer.ComponentBody.Position;

            if (m_zoomSlider.IsSliding)
            {
                m_mapContent.BlockSize = m_zoomSlider.Value;
            }
            else
            {
                m_zoomSlider.Value = m_mapContent.BlockSize;
            }

            m_zoomSlider.Text = $"{m_mapContent.BlockSize:0.#}x";

            if (m_mapContent.ComponentMinimap == null)
            {
                m_mapContent.ComponentMinimap = m_componentMinimap;
            }

            Vector3 crossWorldPosition = m_mapContent.CrossWorldPosition;

            float x = crossWorldPosition.X;
            float y = crossWorldPosition.Y;
            float z = crossWorldPosition.Z;

            m_infoLabel.Text = $"X: {x:0.0} Y: {y:0.0} Z: {z:0.0}";

            if (m_locationButton.IsClicked)
            {
                DialogsManager.ShowDialog(null, new ListSelectionDialog("Locations", Locations, 56f, (object item) => item.ToString(), delegate (object selected)
                {
                    if (selected == null)
                        return;

                    string options = (string)selected;

                    if (options == Locations[0])
                    {
                        m_mapContent.Grab = Vector2.Zero;
                    }
                    else if (options == Locations[1])
                    {
                        Vector3 globalSpawn = m_componentMinimap.m_subsystemPlayers.GlobalSpawnPosition;
                        m_mapContent.Grab = new Vector2(globalSpawn.X - playerPosition.X, playerPosition.Z - globalSpawn.Z);
                    }
                    else if (options == Locations[2])
                    {
                        Vector3 spawn = m_componentMinimap.m_componentPlayer.PlayerData.SpawnPosition;
                        if (spawn != Vector3.Zero)
                        {
                            m_mapContent.Grab = new Vector2(spawn.X - playerPosition.X, playerPosition.Z - spawn.Z);
                        }
                    }
                }));
            }

            if (m_markerButton.IsClicked)
            {
                DialogsManager.ShowDialog(null, new ListSelectionDialog("Markers", Markers, 56f, (object item) => item.ToString(), delegate (object selected)
                {
                    if (selected == null)
                        return;

                    string options = (string)selected;

                    if (options == Markers[0])
                    {
                        DialogsManager.ShowDialog(null, new AddMarkerDialog(m_componentMinimap, m_mapContent.CrossWorldPosition));
                    }
                    else if (options == Markers[1])
                    {
                        List<MapMarker> markers = GetPlayerMarkers().Where(m => !m.IsHidden).ToList();

                        if (markers.Count == 0)
                            return;

                        DialogsManager.ShowDialog(null, new ListSelectionDialog("Hide Marker", markers, 56f, item => ((MapMarker)item).Name, selected =>
                        {
                            if (selected is MapMarker marker)
                            {
                                marker.IsHidden = true;
                            }
                        }));
                    }
                    else if (options == Markers[2])
                    {
                        List<MapMarker> markers = GetPlayerMarkers().Where(m => m.IsHidden).ToList();

                        if (markers.Count == 0)
                            return;

                        DialogsManager.ShowDialog(null, new ListSelectionDialog("Show Marker", markers, 56f, item => ((MapMarker)item).Name, selected =>
                        {
                            if (selected is MapMarker marker)
                            {
                                marker.IsHidden = false;
                            }
                        }));
                    }
                    else if (options == Markers[3])
                    {
                        List<MapMarker> markers = GetPlayerMarkers();

                        if (markers.Count == 0)
                            return;

                        DialogsManager.ShowDialog(null, new ListSelectionDialog("Delete Marker", markers, 56f, item => ((MapMarker)item).Name, selected =>
                        {
                            if (selected is MapMarker marker)
                            {
                                DialogsManager.ShowDialog(null, new MessageDialog("Delete Marker", $"Delete marker \"{marker.Name}\"?", "Yes", "No", button =>
                                {
                                    if (button == MessageDialogButton.Button1)
                                        m_subsystemMapMarkers.RemoveMarker(marker);
                                }));
                            }
                        }));
                    }
                    else if (options == Markers[4])
                    {
                        DialogsManager.ShowDialog(null, new MessageDialog("Clear Markers", "Delete all markers?", "Yes", "No", button =>
                        {
                            if (button == MessageDialogButton.Button1)
                                m_subsystemMapMarkers.ClearMarkers();
                        }));
                    }
                }));
            }

            if (m_moreButton.IsClicked)
            {
                DialogsManager.ShowDialog(null, new ListSelectionDialog("More Options", MoreOptions, 56f, (object item) => item.ToString(), delegate (object selected)
                {
                    if (selected == null)
                        return;

                    string options = (string)selected;

                    if (options == MoreOptions[0])
                    {
                        if (m_subsystemWorldMap != null)
                            m_subsystemWorldMap.CleanupOutOfRangeChunks();
                    }
                }));
            }

            if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back").IsClicked)
            {
                ScreensManager.GoBack();
            }
        }

        public List<MapMarker> GetPlayerMarkers()
        {
            int playerIndex = m_componentMinimap.m_componentPlayer.PlayerData.PlayerIndex;

            return m_subsystemMapMarkers.Markers.Where(m => m.MarkerType == MarkerType.Default && m.PlayerIndex == playerIndex).ToList();
        }
    }
}
