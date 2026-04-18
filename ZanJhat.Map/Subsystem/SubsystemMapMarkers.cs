using Engine;
using GameEntitySystem;
using System.Collections.Generic;
using TemplatesDatabase;
using Engine.Graphics;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public enum MarkerType
    {
        Default = 0,
        Death = 1
    }

    public class MapMarker
    {
        public string Name;
        public int X;
        public int Y;
        public int Z;
        public Color Color;
        public bool ClampToEdge;
        public bool IsHidden;
        public double CreationTime;
        public int PlayerIndex;
        public MarkerType MarkerType;

        public MapMarker(string name, int x, int y, int z, Color color, bool clampToEdge, bool isHidden, double creationTime, int playerIndex, MarkerType markerType)
        {
            Name = name;
            X = x;
            Y = y;
            Z = z;
            Color = color;
            ClampToEdge = clampToEdge;
            IsHidden = isHidden;
            CreationTime = creationTime;
            PlayerIndex = playerIndex;
            MarkerType = markerType;
        }
    }

    public class SubsystemMapMarkers : Subsystem, IUpdateable, IDrawable
    {
        public SubsystemPlayers m_subsystemPlayers;
        public SubsystemGameInfo m_subsystemGameInfo;

        public PrimitivesRenderer3D m_primitivesRenderer3D;

        public List<MapMarker> Markers = new List<MapMarker>();

        public int[] DrawOrders => [1101];

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt)
        {
            double time = m_subsystemGameInfo.TotalElapsedGameTime;

            Markers.RemoveAll(marker => marker.MarkerType == MarkerType.Death && marker.CreationTime + 1200 <= time);
        }

        public void Draw(Camera camera, int drawOrder)
        {
            ComponentPlayer componentPlayer = GetComponentPlayerFromCamera(m_subsystemPlayers, camera);
            if (componentPlayer == null)
                return;

            if (MapSettingsManager.MapMarkerSettings.ShowSpawnMarker)
                DrawIcon(camera, componentPlayer.PlayerData.SpawnPosition, "Spawn", "Textures/Gui/UpdateChecking", Color.White, 0);

            Vector3 camPos = camera.ViewPosition;
            Vector3 right = camera.ViewRight;
            Vector3 up = camera.ViewUp;
            Vector3 viewDir = camera.ViewDirection;

            int baseLayer = 1;
            int i = 0;

            foreach (MapMarker marker in Markers)
            {
                if (marker.IsHidden)
                    continue;

                int layer = baseLayer + i * 3;

                FlatBatch3D flatBatch = m_primitivesRenderer3D.FlatBatch(layer, DepthStencilState.None, null, null);

                FontBatch3D fontBatch = m_primitivesRenderer3D.FontBatch(
                    LabelWidget.BitmapFont,
                    layer,
                    DepthStencilState.None,
                    null,
                    null,
                    null
                );

                int playerIndex = componentPlayer.PlayerData.PlayerIndex;

                int ownerIndex = marker.PlayerIndex;

                if (ownerIndex != -1 && ownerIndex != playerIndex)
                    continue;

                Vector3 pos = new Vector3(marker.X + 0.5f, marker.Y + 0.5f, marker.Z + 0.5f);

                float distance = Vector3.Distance(camPos, pos);

                float size = MathUtils.Max(0.6f, distance * 0.075f);

                Vector3 p1 = pos + right * size / 2 + up * size / 2;
                Vector3 p2 = pos - right * size / 2 + up * size / 2;
                Vector3 p3 = pos - right * size / 2 - up * size / 2;
                Vector3 p4 = pos + right * size / 2 - up * size / 2;

                if (marker.MarkerType == MarkerType.Default)
                {
                    flatBatch.QueueQuad(
                        p1, p2, p3, p4,
                        Color.White
                    );

                    float innerSize = size * 0.9f;

                    flatBatch.QueueQuad(
                        pos + right * innerSize / 2 + up * innerSize / 2,
                        pos - right * innerSize / 2 + up * innerSize / 2,
                        pos - right * innerSize / 2 - up * innerSize / 2,
                        pos + right * innerSize / 2 - up * innerSize / 2,
                        marker.Color
                    );

                    string firstLetter = !string.IsNullOrEmpty(marker.Name) ? marker.Name[0].ToString() : "?";

                    fontBatch.QueueText(
                        firstLetter,
                        pos,
                        right * 0.035f * size,
                        -up * 0.035f * size,
                        Color.White,
                        TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter
                    );
                }
                else if (marker.MarkerType == MarkerType.Death)
                {
                    if (!MapSettingsManager.MapMarkerSettings.ShowDeathMarker)
                        continue;

                    TexturedBatch3D deathMarkerBatch = m_primitivesRenderer3D.TexturedBatch(
                        GetMarkerTexture(MarkerType.Death),
                        false,
                        layer,
                        DepthStencilState.None,
                        null,
                        null,
                        SamplerState.PointClamp
                    );

                    deathMarkerBatch.QueueQuad(
                        p1, p2, p3, p4,
                        Vector2.Zero,
                        Vector2.UnitX,
                        Vector2.One,
                        Vector2.UnitY,
                        Color.White
                    );
                }

                Vector3 toMarker = Vector3.Normalize(pos - camPos);

                float dot = Vector3.Dot(viewDir, toMarker);

                if (dot > 0.99f)
                {

                    string label = $"{marker.Name} ({MathUtils.Round(distance)} m)";

                    float scale = 0.02f * size;

                    Vector3 textPos = pos - up * size;

                    Vector2 textSize = LabelWidget.BitmapFont.MeasureText(label, new Vector2((right * scale).Length(), (-up * scale).Length()), Vector2.Zero);

                    Vector3 bgRight = right * textSize.X * 0.6f;
                    Vector3 bgUp = up * textSize.Y * 0.6f;

                    flatBatch.QueueQuad(
                        textPos + bgRight + bgUp,
                        textPos - bgRight + bgUp,
                        textPos - bgRight - bgUp,
                        textPos + bgRight - bgUp,
                        new Color(0, 0, 0, 128)
                    );

                    fontBatch.QueueText(
                        label,
                        textPos,
                        right * scale,
                        -up * scale,
                        Color.White,
                        TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter
                    );
                }

                i++;
            }

            m_primitivesRenderer3D.Flush(camera.ViewProjectionMatrix);
        }

        public void DrawIcon(Camera camera, Vector3 pos, string name, string iconPath, Color color, int layer)
        {
            TexturedBatch3D texturedBatch = m_primitivesRenderer3D.TexturedBatch(
                ContentManager.Get<Texture2D>(iconPath),
                false,
                layer,
                DepthStencilState.None,
                null,
                null,
                SamplerState.PointClamp
            );

            FlatBatch3D flatBatch = m_primitivesRenderer3D.FlatBatch(layer, DepthStencilState.None, null, null);

            FontBatch3D fontBatch = m_primitivesRenderer3D.FontBatch(
                LabelWidget.BitmapFont,
                layer,
                DepthStencilState.None,
                null,
                null,
                null
            );

            pos.Y += 0.5f;

            Vector3 camPos = camera.ViewPosition;
            float distance = Vector3.Distance(camPos, pos);

            Vector3 right = camera.ViewRight;
            Vector3 up = camera.ViewUp;

            float size = MathUtils.Max(0.6f, distance * 0.075f);

            Vector3 p1 = pos + right * size / 2 + up * size / 2;
            Vector3 p2 = pos - right * size / 2 + up * size / 2;
            Vector3 p3 = pos - right * size / 2 - up * size / 2;
            Vector3 p4 = pos + right * size / 2 - up * size / 2;

            texturedBatch.QueueQuad(
                p1, p2, p3, p4,
                Vector2.Zero,
                Vector2.UnitX,
                Vector2.One,
                Vector2.UnitY,
                color
            );

            Vector3 viewDir = camera.ViewDirection;
            Vector3 toIcon = Vector3.Normalize(pos - camPos);

            float dot = Vector3.Dot(viewDir, toIcon);

            if (dot > 0.99f && !string.IsNullOrEmpty(name))
            {
                string label = $"{name} ({MathUtils.Round(distance)} m)";
                float scale = 0.02f * size;
                Vector3 textPos = pos - up * size;

                Vector2 textSize = LabelWidget.BitmapFont.MeasureText(label, new Vector2((right * scale).Length(), (-up * scale).Length()), Vector2.Zero);

                Vector3 bgRight = right * textSize.X * 0.6f;
                Vector3 bgUp = up * textSize.Y * 0.6f;

                flatBatch.QueueQuad(
                    textPos + bgRight + bgUp,
                    textPos - bgRight + bgUp,
                    textPos - bgRight - bgUp,
                    textPos + bgRight - bgUp,
                    new Color(0, 0, 0, 128)
                );

                fontBatch.QueueText(
                    label,
                    textPos,
                    right * scale,
                    -up * scale,
                    Color.White,
                    TextAnchor.HorizontalCenter | TextAnchor.VerticalCenter
                );
            }
        }

        public static ComponentPlayer GetComponentPlayerFromCamera(SubsystemPlayers subsystemPlayers, Camera camera)
        {
            if (subsystemPlayers == null || camera == null)
                return null;

            foreach (ComponentPlayer componentPlayer in subsystemPlayers.ComponentPlayers)
            {
                if (componentPlayer.GameWidget.ActiveCamera == camera)
                {
                    return componentPlayer;
                }
            }

            return null;
        }

        public Texture2D GetMarkerTexture(MarkerType type)
        {
            Texture2D texture = null;

            if (type == MarkerType.Death)
                texture = ContentManager.Get<Texture2D>("Textures/Map/DeathMarker");

            return texture;
        }

        public void AddMarker(string name, int x, int y, int z, Color color, bool clampToEdge, bool isHidden, ComponentPlayer componentPlayer, MarkerType markerType = MarkerType.Default)
        {
            if (componentPlayer == null)
            {
                Log.Warning($"[{GetType().Name}] AddMarker '{name}' called with null ComponentPlayer.");
            }

            int playerIndex = componentPlayer?.PlayerData?.PlayerIndex ?? -1;
            AddMarker(name, x, y, z, color, clampToEdge, isHidden, playerIndex, markerType);
        }

        public void AddMarker(string name, int x, int y, int z, Color color, bool clampToEdge, bool isHidden, int playerIndex, MarkerType markerType)
        {
            double currentTime = m_subsystemGameInfo.TotalElapsedGameTime;
            AddMarker(name, x, y, z, color, clampToEdge, isHidden, currentTime, playerIndex, markerType);
        }

        public void AddMarker(string name, int x, int y, int z, Color color, bool clampToEdge, bool isHidden, double creationTime, int playerIndex, MarkerType markerType)
        {
            Markers.Add(new MapMarker(name, x, y, z, color, clampToEdge, isHidden, creationTime, playerIndex, markerType));
        }

        public void RemoveMarker(MapMarker marker)
        {
            Markers.Remove(marker);
        }

        public void ClearMarkers()
        {
            Markers.Clear();
        }

        public MapMarker FindMarker(string name)
        {
            foreach (MapMarker marker in Markers)
            {
                if (marker.Name == name)
                    return marker;
            }

            return null;
        }

        public bool HideMarker(string name)
        {
            MapMarker marker = FindMarker(name);
            if (marker == null)
                return false;

            marker.IsHidden = true;
            return true;
        }

        public bool ShowMarker(string name)
        {
            MapMarker marker = FindMarker(name);
            if (marker == null)
                return false;

            marker.IsHidden = false;
            return true;
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);

            m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
            m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            m_primitivesRenderer3D = Project.FindSubsystem<SubsystemModelsRenderer>(true).PrimitivesRenderer;

            Markers.Clear();

            ValuesDictionary markersDict = valuesDictionary.GetValue<ValuesDictionary>("Markers", new ValuesDictionary());

            foreach (ValuesDictionary v in markersDict.Values)
            {
                string name = v.GetValue<string>("Name");
                int x = v.GetValue<int>("X");
                int y = v.GetValue<int>("Y");
                int z = v.GetValue<int>("Z");

                byte r = v.GetValue<byte>("R");
                byte g = v.GetValue<byte>("G");
                byte b = v.GetValue<byte>("B");

                bool clampToEdge = v.GetValue<bool>("ClampToEdge", false);
                bool isHidden = v.GetValue<bool>("IsHidden", false);
                double creationTime = v.GetValue<double>("CreationTime", 0);
                int playerIndex = v.GetValue<int>("PlayerIndex", -1);
                MarkerType markerType = (MarkerType)v.GetValue<int>("MarkerType", (int)MarkerType.Default);

                AddMarker(name, x, y, z, new Color(r, g, b), clampToEdge, isHidden, creationTime, playerIndex, markerType);
            }
        }

        public override void Save(ValuesDictionary valuesDictionary)
        {
            ValuesDictionary markersDict = new ValuesDictionary();
            int index = 0;

            foreach (MapMarker marker in Markers)
            {
                ValuesDictionary v = new ValuesDictionary();

                v.SetValue("Name", marker.Name);
                v.SetValue("X", marker.X);
                v.SetValue("Y", marker.Y);
                v.SetValue("Z", marker.Z);

                v.SetValue("R", marker.Color.R);
                v.SetValue("G", marker.Color.G);
                v.SetValue("B", marker.Color.B);

                v.SetValue("ClampToEdge", marker.ClampToEdge);
                v.SetValue("IsHidden", marker.IsHidden);
                v.SetValue("CreationTime", marker.CreationTime);
                v.SetValue("PlayerIndex", marker.PlayerIndex);
                v.SetValue("MarkerType", (int)marker.MarkerType);

                markersDict.SetValue("Marker" + index++, v);
            }

            valuesDictionary.SetValue("Markers", markersDict);
        }
    }
}
