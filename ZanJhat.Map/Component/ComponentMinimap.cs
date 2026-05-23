using Engine;
using Engine.Graphics;
using Engine.Media;
using Engine.Input;
using GameEntitySystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TemplatesDatabase;
using System.Xml.Linq;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public struct BlockPixelData
    {
        public string BlockName;
        public Color Color;
        public bool NeedChangeWithEnvironment;
    }

    public enum MinimapUpdateRate
    {
        VeryLow,
        Low,
        Normal,
        High,
        Ultra
    }

    public enum MinimapSizeMode
    {
        Auto = 0,
        Size32 = 32,
        Size64 = 64,
        Size96 = 96,
        Size128 = 128,
        Size192 = 192,
        Size256 = 256
    }

    public class ComponentMinimap : Component, IUpdateable
    {
        public ComponentPlayer m_componentPlayer;
        public SubsystemTerrain m_subsystemTerrain;
        public SubsystemSky m_subsystemSky;
        public SubsystemPlayers m_subsystemPlayers;
        public SubsystemGameInfo m_subsystemGameInfo;
        public SubsystemWorldMap m_subsystemWorldMap;
        public SubsystemMapMarkers m_subsystemMapMarkers;

        private double m_lastUpdateTime;
        private double m_updateInterval = 0.5;
        private Point2 m_lastBlockPosition;

        private Texture2D m_mapTexture;
        private RenderTarget2D m_mapRenderTarget;

        public Texture2D MapTexture => m_mapTexture;

        private const float FrameImageSize = 73f;
        private const float BorderThickness = 4f;
        private const float MapSize = 128f;

        private MinimapUpdateRate m_lastUpdateRate;

        private PrimitivesRenderer2D m_primitivesRenderer2D = new PrimitivesRenderer2D();
        public GlobalMapSettings m_globalMapSettings;

        public CanvasWidget m_controlsContainer;
        public AutoSizeCanvasWidget m_minimapRoot;

        private CanvasWidget m_minimapViewport;
        private MinimapWidget m_minimapContents;
        private BevelledButtonWidget m_minimapInputOverlay;
        private List<LabelWidget> m_minimapLabels = new List<LabelWidget>();

        private static readonly Dictionary<MinimapUpdateRate, float> RateToFps = new()
        {
            { MinimapUpdateRate.VeryLow, 0.5f },
            { MinimapUpdateRate.Low, 1f },
            { MinimapUpdateRate.Normal, 2f },
            { MinimapUpdateRate.High, 4f },
            { MinimapUpdateRate.Ultra, 10f }
        };

        public Vector3 MapCenterPosition { get; set; }

        public bool m_isOpenWorldMapPressed;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt)
        {
            UpdateModInput();

            if (m_globalMapSettings.UpdateRate != m_lastUpdateRate)
            {
                m_lastUpdateRate = m_globalMapSettings.UpdateRate;
                SetUpdateRate(m_lastUpdateRate);
            }

            double currentTime = Time.RealTime;

            bool canUpdate = currentTime - m_lastUpdateTime >= m_updateInterval;

            if (m_globalMapSettings.Enable && canUpdate && (MapTexture == null || IsPlayerPositionChanged() || Time.PeriodicEvent(5.0, 0.0)))
            {
                m_lastUpdateTime = currentTime;
                UpdateMapTexture();
            }

            UpdateMinimapWidget();
        }

        public void UpdateModInput()
        {
            m_isOpenWorldMapPressed = false;

            // 1. Kiểm tra null
            if (!Window.IsActive || m_componentPlayer?.ComponentInput == null || m_componentPlayer?.PlayerData == null)
                return;

            bool canHandleInput = m_componentPlayer.ComponentInput.AllowHandleInput && !DialogsManager.HasDialogs(m_componentPlayer.GuiWidget);

            if (!canHandleInput)
                return;

            // 2. Kiểm tra bàn phím  
            Key worldMapKey = Key.Null;

            if (ModSettingsManager.ModKeyboardMapSettings.TryGetValue("ZanJhat.Map", out ValuesDictionary modKeys))
                worldMapKey = modKeys.GetValue<Key>("Open World Map", Key.Null);

            if (worldMapKey != Key.Null && Keyboard.IsKeyDownOnce(worldMapKey))
                m_isOpenWorldMapPressed = true;

            // 3. Kiểm tra GamePad  
            int gamePadIndex = GetGamePadIndex(m_componentPlayer.PlayerData.InputDevice);

            if (gamePadIndex >= 0 && GamePad.IsConnected(gamePadIndex))
            {
                if (ModSettingsManager.ModGamepadMapSettings.TryGetValue("ZanJhat.Map", out ValuesDictionary modGamepad))
                {
                    ValuesDictionary worldMapGamepad = modGamepad.GetValue<ValuesDictionary>("Open World Map", null);

                    if (worldMapGamepad != null)
                    {
                        GamePadButton mod = worldMapGamepad.GetValue<GamePadButton>("ModifierKey", GamePadButton.Null);
                        GamePadButton act = worldMapGamepad.GetValue<GamePadButton>("ActionKey", GamePadButton.Null);

                        bool validButton = mod != GamePadButton.Null && act != GamePadButton.Null;

                        if (validButton && GamePad.IsButtonDown(gamePadIndex, mod) && GamePad.IsButtonDownOnce(gamePadIndex, act))
                            m_isOpenWorldMapPressed = true;
                    }
                }
            }
        }

        public bool IsOpenWorldMapPressed() => m_isOpenWorldMapPressed;

        public static int GetGamePadIndex(WidgetInputDevice device)
        {
            switch (device)
            {
                case WidgetInputDevice.GamePad1: return 0;
                case WidgetInputDevice.GamePad2: return 1;
                case WidgetInputDevice.GamePad3: return 2;
                case WidgetInputDevice.GamePad4: return 3;
                default: return -1;
            }
        }

        public bool IsPlayerPositionChanged()
        {
            Vector3 pos = m_componentPlayer.ComponentBody.Position;

            int blockX = Terrain.ToCell(pos.X);
            int blockZ = Terrain.ToCell(pos.Z);

            Point2 currentBlock = new Point2(blockX, blockZ);

            if (currentBlock != m_lastBlockPosition)
            {
                m_lastBlockPosition = currentBlock;
                return true;
            }

            return false;
        }

        public void UpdateMapTexture()
        {
            MinimapSizeMode sizeMode = m_globalMapSettings.SizeMode;

            int textureSize = sizeMode == MinimapSizeMode.Auto ? SettingsManager.VisibilityRange : (int)sizeMode;
            textureSize = MathUtils.Clamp(textureSize, 32, 256);

            if (m_mapRenderTarget == null || m_mapRenderTarget.Width != textureSize)
            {
                m_mapRenderTarget?.Dispose();
                m_mapRenderTarget = new RenderTarget2D(textureSize, textureSize, 1, ColorFormat.Rgba8888, DepthFormat.None);
            }

            Vector3 playerPosition = m_componentPlayer.ComponentBody.Position;
            MapCenterPosition = playerPosition;

            RenderTarget2D previous = Display.RenderTarget;
            Display.RenderTarget = m_mapRenderTarget;
            Display.Clear(Color.Black);

            FlatBatch2D batch = m_primitivesRenderer2D.FlatBatch();

            int playerX = Terrain.ToCell(playerPosition.X);
            int playerZ = Terrain.ToCell(playerPosition.Z);

            int half = textureSize / 2;

            IReadOnlyDictionary<Point2, ChunkMapData> chunkMap = m_subsystemWorldMap.ChunkMap;

            for (int localX = 0; localX < textureSize; localX++)
            {
                for (int localZ = 0; localZ < textureSize; localZ++)
                {
                    int worldX = playerX + half - localX;
                    int worldZ = localZ + playerZ - half;

                    int chunkX = worldX >> 4;
                    int chunkZ = worldZ >> 4;

                    Color color = Color.Transparent;

                    if (chunkMap.TryGetValue(new Point2(chunkX, chunkZ), out ChunkMapData chunk))
                    {
                        int lx = worldX & 15;
                        int lz = worldZ & 15;

                        color = chunk.GetShadedColor(lx, lz);

                        if (color == Color.Transparent)
                            color = chunk.GetColor(lx, lz);
                    }

                    if (color.A != 0)
                    {
                        Vector2 pos = new Vector2(localX, localZ);
                        batch.QueueQuad(pos, pos + Vector2.One, 0f, color);
                    }
                }
            }

            m_primitivesRenderer2D.Flush();
            Display.RenderTarget = previous;
            m_mapTexture = m_mapRenderTarget;
        }

        public void UpdateMinimapWidget()
        {
            if (MapTexture == null)
                return;

            if (!m_globalMapSettings.Enable)
            {
                if (m_minimapRoot != null)
                    m_minimapRoot.IsVisible = false;

                return;
            }

            GameWidget gameWidget = m_componentPlayer.GameWidget;

            if (m_minimapRoot == null)
            {
                m_minimapRoot = gameWidget.Children.Find<AutoSizeCanvasWidget>("MinimapRoot", false);
                if (m_minimapRoot == null)
                    m_minimapRoot = CreateMinimap(m_controlsContainer);
            }

            if (m_minimapRoot == null)
                return;

            m_minimapRoot.IsVisible = true;

            float frameSize = FrameImageSize * (MapSize / (FrameImageSize - 2f * BorderThickness));
            float scale = m_globalMapSettings.DisplayScale;

            m_minimapViewport = m_minimapViewport == null ? m_minimapRoot.Children.Find<CanvasWidget>("MinimapViewport", false) : m_minimapViewport;
            if (m_minimapViewport != null)
                m_minimapViewport.Size = new Vector2(frameSize * scale);

            m_minimapContents = m_minimapContents == null ? m_minimapRoot.Children.Find<MinimapWidget>("MinimapContents", false) : m_minimapContents;
            if (m_minimapContents != null)
            {
                m_minimapContents.ComponentMinimap = this;
                m_minimapContents.Size = new Vector2(MapSize * scale);
            }

            m_minimapInputOverlay = m_minimapInputOverlay == null ? m_minimapRoot.Children.Find<BevelledButtonWidget>("MinimapInputOverlay", false) : m_minimapInputOverlay;
            if (m_minimapInputOverlay != null)
            {
                m_minimapInputOverlay.Size = new Vector2(MapSize * scale);
                if (m_minimapInputOverlay.IsClicked || IsOpenWorldMapPressed())
                    ScreensManager.SwitchScreen("WorldMap", new Object[] { this });
            }

            if (m_minimapLabels.Count == 0)
            {
                foreach (Widget child in m_minimapRoot.AllChildren)
                {
                    if (child is LabelWidget label)
                        m_minimapLabels.Add(label);
                }
            }

            foreach (LabelWidget label in m_minimapLabels)
                label.FontScale = scale;

            Vector2 screenSize = gameWidget.ActualSize;
            WidgetUtils.SetAnchor(m_minimapRoot, screenSize, m_globalMapSettings.Anchor, m_globalMapSettings.MarginX, m_globalMapSettings.MarginY);
        }

        public AutoSizeCanvasWidget CreateMinimap(CanvasWidget parent)
        {
            float frameSize = FrameImageSize * (MapSize / (FrameImageSize - 2f * BorderThickness));
            float scale = m_globalMapSettings.DisplayScale;

            AutoSizeCanvasWidget minimapRoot = new AutoSizeCanvasWidget
            {
                Name = "MinimapRoot"
            };

            StackPanelWidget row = new StackPanelWidget
            {
                Direction = LayoutDirection.Horizontal,
                Margin = new Vector2(0f)
            };
            minimapRoot.Children.Add(row);

            WidgetUtils.AddLabel(row, "W", Color.White, 1f, false, new Vector2(0f), WidgetAlignment.Center, WidgetAlignment.Near);

            StackPanelWidget col = new StackPanelWidget
            {
                Direction = LayoutDirection.Vertical,
                Margin = new Vector2(0f)
            };
            row.Children.Add(col);

            WidgetUtils.AddLabel(col, "N", Color.White, 1f, false, new Vector2(0f), WidgetAlignment.Far, WidgetAlignment.Center);

            CanvasWidget minimapViewport = new CanvasWidget
            {
                Name = "MinimapViewport",
                Size = new Vector2(frameSize * scale)
            };
            col.Children.Add(minimapViewport);
            m_minimapViewport = minimapViewport;

            RectangleWidget frame = new RectangleWidget
            {
                Name = "Frame",
                VerticalAlignment = WidgetAlignment.Center,
                HorizontalAlignment = WidgetAlignment.Center,
                FillColor = Color.White,
                OutlineColor = Color.Transparent,
                Subtexture = ContentManager.Get<Subtexture>("Textures/Map/MinimapFrame"),
                TextureLinearFilter = false
            };
            minimapViewport.Children.Add(frame);

            MinimapWidget minimapContents = new MinimapWidget
            {
                Name = "MinimapContents",
                VerticalAlignment = WidgetAlignment.Center,
                HorizontalAlignment = WidgetAlignment.Center,
                Size = new Vector2(MapSize * scale),
                ComponentMinimap = this
            };
            minimapViewport.Children.Add(minimapContents);
            m_minimapContents = minimapContents;

            BevelledButtonWidget minimapInputOverlay = new BevelledButtonWidget
            {
                Name = "MinimapInputOverlay",
                Size = new Vector2(MapSize * scale),
                VerticalAlignment = WidgetAlignment.Center,
                HorizontalAlignment = WidgetAlignment.Center,
                BevelSize = 0f,
                BevelColor = Color.Transparent,
                CenterColor = Color.Transparent
            };
            minimapViewport.Children.Add(minimapInputOverlay);
            m_minimapInputOverlay = minimapInputOverlay;

            minimapInputOverlay.Children.Find<CanvasWidget>("BevelledButton.Canvas").Margin = new Vector2(0f);

            WidgetUtils.AddLabel(col, "S", Color.White, 1f, false, new Vector2(0f), WidgetAlignment.Near, WidgetAlignment.Center);

            WidgetUtils.AddLabel(row, "E", Color.White, 1f, false, new Vector2(0f), WidgetAlignment.Center, WidgetAlignment.Far);

            parent.Children.Insert(0, minimapRoot);

            m_minimapLabels.Clear();
            foreach (Widget child in minimapRoot.AllChildren)
            {
                if (child is LabelWidget label)
                    m_minimapLabels.Add(label);
            }

            return minimapRoot;
        }

        public void SetUpdateRate(MinimapUpdateRate updateRate)
        {
            m_updateInterval = 1.0 / RateToFps[updateRate];
        }

        public MinimapUpdateRate GetUpdateRate()
        {
            float fps = (float)(1.0 / m_updateInterval);

            MinimapUpdateRate best = MinimapUpdateRate.Normal;
            float bestDiff = float.MaxValue;

            foreach (KeyValuePair<MinimapUpdateRate, float> pair in RateToFps)
            {
                float diff = MathUtils.Abs(pair.Value - fps);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = pair.Key;
                }
            }

            return best;
        }

        public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
        {
            base.Load(valuesDictionary, idToEntityMap);
            m_componentPlayer = Entity.FindComponent<ComponentPlayer>(true);
            m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(true);
            m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
            m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
            m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true);
            m_subsystemWorldMap = Project.FindSubsystem<SubsystemWorldMap>(true);
            m_subsystemMapMarkers = Project.FindSubsystem<SubsystemMapMarkers>(true);

            m_controlsContainer = m_componentPlayer.GameWidget.Children.Find<CanvasWidget>("ControlsContainer");

            m_globalMapSettings = MapSettingsManager.GlobalMapSettings;
            m_lastUpdateRate = m_globalMapSettings.UpdateRate;
            SetUpdateRate(m_lastUpdateRate);
        }

        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            base.Save(valuesDictionary, entityToIdMap);
        }
    }
}
