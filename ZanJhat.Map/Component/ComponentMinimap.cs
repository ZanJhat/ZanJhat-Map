using Engine;
using Engine.Graphics;
using Engine.Media;
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
        public MinimapSettings m_minimapSettings;

        private static readonly Dictionary<MinimapUpdateRate, float> RateToFps = new()
        {
            { MinimapUpdateRate.VeryLow, 0.5f },
            { MinimapUpdateRate.Low, 1f },
            { MinimapUpdateRate.Normal, 2f },
            { MinimapUpdateRate.High, 4f },
            { MinimapUpdateRate.Ultra, 10f }
        };

        public Vector3 MapCenterPosition;

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt)
        {
            if (m_minimapSettings.UpdateRate != m_lastUpdateRate)
            {
                m_lastUpdateRate = m_minimapSettings.UpdateRate;
                SetUpdateRate(m_lastUpdateRate);
            }

            double currentTime = Time.RealTime;

            bool canUpdate = currentTime - m_lastUpdateTime >= m_updateInterval;

            if (m_minimapSettings.Enable && canUpdate && (MapTexture == null || IsPlayerPositionChanged() || Time.PeriodicEvent(10.0, 0.0)))
            {
                m_lastUpdateTime = currentTime;
                UpdateMapTexture();
            }

            UpdateMinimapWidget();
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
            MinimapSizeMode sizeMode = m_minimapSettings.SizeMode;

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

            Display.Clear(Color.Transparent);

            FlatBatch2D batch = m_primitivesRenderer2D.FlatBatch();

            Vector3 lightDir = MapManager.GetLightDirection(m_subsystemSky);

            int playerX = Terrain.ToCell(playerPosition.X);
            int playerZ = Terrain.ToCell(playerPosition.Z);

            int half = textureSize / 2;

            for (int localX = 0; localX < textureSize; localX++)
            {
                for (int localZ = 0; localZ < textureSize; localZ++)
                {
                    int worldX = playerX + half - localX;
                    int worldZ = localZ + playerZ - half;

                    Color baseColor = MapManager.GetBlockColor(m_subsystemTerrain, worldX, worldZ);

                    float dx, dz;

                    MapManager.GetHeightGradient((x, z) => m_subsystemTerrain.Terrain.GetTopHeight(x, z), worldX, worldZ, m_minimapSettings.ShadingMode, out dx, out dz);

                    Color shadedColor = MapManager.ApplyHeightShading(baseColor, dx, dz, lightDir);

                    Vector2 pos = new Vector2(localX, localZ);

                    batch.QueueQuad(
                        pos,
                        pos + Vector2.One,
                        0f,
                        shadedColor
                    );
                }
            }

            m_primitivesRenderer2D.Flush();

            Display.RenderTarget = previous;

            m_mapTexture = m_mapRenderTarget;
        }

        public void UpdateMinimapWidget()
        {
            GameWidget gameWidget = m_componentPlayer.GameWidget;

            if (gameWidget == null || MapTexture == null)
                return;

            AutoSizeCanvasWidget minimap = gameWidget.Children.Find<AutoSizeCanvasWidget>("Minimap", false);

            if (!m_minimapSettings.Enable)
            {
                if (minimap != null)
                    minimap.IsVisible = false;

                return;
            }

            if (minimap == null)
            {
                CanvasWidget controlsContainer = gameWidget.Children.Find<CanvasWidget>("ControlsContainer");
                minimap = CreateMinimap(controlsContainer);
            }

            if (minimap == null)
                return;

            minimap.IsVisible = true;

            float frameSize = FrameImageSize * (MapSize / (FrameImageSize - 2f * BorderThickness));
            float scale = m_minimapSettings.DisplayScale;

            CanvasWidget minimapViewport = minimap.Children.Find<CanvasWidget>("MinimapViewport", false);
            if (minimapViewport != null)
            {
                minimapViewport.Size = new Vector2(frameSize * scale);
            }

            MinimapWidget minimapWidget = minimap.Children.Find<MinimapWidget>("MapTexture", false);
            if (minimapWidget != null)
            {
                minimapWidget.ComponentMinimap = this;
                minimapWidget.Size = new Vector2(MapSize * scale);
            }

            BevelledButtonWidget inputOverlay = minimap.Children.Find<BevelledButtonWidget>("MinimapInputOverlay", false);
            if (inputOverlay != null)
            {
                inputOverlay.Size = new Vector2(MapSize * scale);
                if (inputOverlay.IsClicked)
                {
                    ScreensManager.SwitchScreen("WorldMap", new Object[] { this });
                }
            }

            foreach (Widget child in minimap.AllChildren)
            {
                if (child is LabelWidget label)
                {
                    label.FontScale = scale;
                }
            }

            Vector2 screenSize = gameWidget.ActualSize;
            WidgetUtils.SetAnchor(minimap, screenSize, m_minimapSettings.Anchor, m_minimapSettings.MarginX, m_minimapSettings.MarginY);
        }

        public AutoSizeCanvasWidget CreateMinimap(CanvasWidget parent)
        {
            float frameSize = FrameImageSize * (MapSize / (FrameImageSize - 2f * BorderThickness));
            float scale = m_minimapSettings.DisplayScale;

            AutoSizeCanvasWidget canvas = new AutoSizeCanvasWidget
            {
                Name = "Minimap"
            };

            StackPanelWidget stackPanel = new StackPanelWidget
            {
                Direction = LayoutDirection.Horizontal,
                Margin = new Vector2(0f)
            };
            canvas.Children.Add(stackPanel);

            WidgetUtils.AddLabel(stackPanel, "W", Color.White, 1f, false, new Vector2(0f), WidgetAlignment.Center, WidgetAlignment.Near);

            StackPanelWidget stackPanel2 = new StackPanelWidget
            {
                Direction = LayoutDirection.Vertical,
                Margin = new Vector2(0f)
            };
            stackPanel.Children.Add(stackPanel2);

            WidgetUtils.AddLabel(stackPanel2, "N", Color.White, 1f, false, new Vector2(0f), WidgetAlignment.Far, WidgetAlignment.Center);

            CanvasWidget canvas2 = new CanvasWidget
            {
                Name = "MinimapViewport",
                Size = new Vector2(frameSize * scale)
            };
            stackPanel2.Children.Add(canvas2);

            RectangleWidget rectangle = new RectangleWidget
            {
                Name = "Frame",
                VerticalAlignment = WidgetAlignment.Center,
                HorizontalAlignment = WidgetAlignment.Center,
                FillColor = Color.White,
                OutlineColor = Color.Transparent,
                Subtexture = ContentManager.Get<Subtexture>("Textures/Map/MinimapFrame"),
                TextureLinearFilter = false
            };
            canvas2.Children.Add(rectangle);

            MinimapWidget miniMap = new MinimapWidget
            {
                Name = "MapTexture",
                VerticalAlignment = WidgetAlignment.Center,
                HorizontalAlignment = WidgetAlignment.Center,
                Size = new Vector2(MapSize * scale),
                ComponentMinimap = this
            };
            canvas2.Children.Add(miniMap);

            BevelledButtonWidget button = new BevelledButtonWidget
            {
                Name = "MinimapInputOverlay",
                Size = new Vector2(MapSize * scale),
                VerticalAlignment = WidgetAlignment.Center,
                HorizontalAlignment = WidgetAlignment.Center,
                BevelSize = 0f,
                BevelColor = Color.Transparent,
                CenterColor = Color.Transparent
            };
            canvas2.Children.Add(button);

            button.Children.Find<CanvasWidget>("BevelledButton.Canvas").Margin = new Vector2(0f);

            WidgetUtils.AddLabel(stackPanel2, "S", Color.White, 1f, false, new Vector2(0f), WidgetAlignment.Near, WidgetAlignment.Center);

            WidgetUtils.AddLabel(stackPanel, "E", Color.White, 1f, false, new Vector2(0f), WidgetAlignment.Center, WidgetAlignment.Far);

            parent.Children.Insert(0, canvas);

            return canvas;
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

            m_minimapSettings = MapSettingsManager.MinimapSettings;

            m_lastUpdateRate = m_minimapSettings.UpdateRate;
            SetUpdateRate(m_lastUpdateRate);
        }

        public override void Save(ValuesDictionary valuesDictionary, EntityToIdMap entityToIdMap)
        {
            base.Save(valuesDictionary, entityToIdMap);
        }
    }
}
