using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public class MinimapWidget : CanvasWidget
    {
        public ComponentMinimap ComponentMinimap
        {
            get => m_componentMinimap;
            set => m_componentMinimap = value;
        }

        private ComponentMinimap m_componentMinimap;

        private ComponentPlayer m_componentPlayer => m_componentMinimap?.m_componentPlayer;

        private SubsystemWorldMap m_subsystemWorldMap => m_componentMinimap?.m_subsystemWorldMap;

        private SubsystemTerrain m_subsystemTerrain => m_componentMinimap?.m_subsystemTerrain;

        private SubsystemPlayers m_subsystemPlayers => m_componentMinimap?.m_subsystemPlayers;

        private SubsystemGameInfo m_subsystemGameInfo => m_componentMinimap?.m_subsystemGameInfo;

        private SubsystemMapMarkers m_subsystemMapMarkers => m_componentMinimap?.m_subsystemMapMarkers;

        private PrimitivesRenderer2D m_primitivesRenderer2D = new PrimitivesRenderer2D();

        private Texture2D PlayerArrowTexture;

        public static Color[] ArrowColors = new Color[4]
        {
            Color.Red,
            Color.Cyan,
            Color.White,
            Color.Green
        };

        private Vector2 m_center;

        public MinimapWidget()
        {
            Size = new Vector2(float.PositiveInfinity);
            PlayerArrowTexture = ContentManager.Get<Texture2D>("Textures/Map/PlayerArrow");
        }

        public override void Update()
        {
            base.Update();
        }

        public override void Draw(DrawContext dc)
        {
            if (m_componentMinimap == null)
                return;

            float scaleX = new Vector2(GlobalTransform.M11, GlobalTransform.M12).Length();
            float size = ActualSize.X * scaleX * 0.1f;

            // Tính toán các điểm tọa độ góc màn hình
            CalculateScreenCoordinates(
                out Vector2 screenTopLeft,
                out Vector2 screenTopRight,
                out Vector2 screenBottomRight,
                out Vector2 screenBottomLeft);

            // Xử lý vẽ world map và các thành phần bên trong
            QueueMinimap(screenTopLeft, screenTopRight, screenBottomRight, screenBottomLeft, 0); // Layer 0

            Vector3 globalSpawnPosition = m_subsystemPlayers.GlobalSpawnPosition;
            if (m_subsystemPlayers != null)
                QueueCustomIcon(screenTopLeft, screenBottomRight, ContentManager.Get<Texture2D>("Textures/Gui/RatingStar"), globalSpawnPosition, size, Color.Yellow, 1); // Layer 1

            Vector3 spawnPosition = m_componentMinimap.m_componentPlayer.PlayerData.SpawnPosition;
            if (globalSpawnPosition != spawnPosition && spawnPosition != Vector3.Zero)
                QueueCustomIcon(screenTopLeft, screenBottomRight, ContentManager.Get<Texture2D>("Textures/Gui/UpdateChecking"), spawnPosition, size, Color.White, 2); // Layer 2

            if (m_subsystemMapMarkers != null)
                QueueMapMarkers(screenTopLeft, screenBottomRight, 3); // Layer 3

            QueuePlayerArrows(screenTopLeft, screenBottomRight, size, 100); // Layer 100

            m_primitivesRenderer2D.Flush();
        }

        // Các METHOD được sử dụng cho DRAW

        private void CalculateScreenCoordinates(out Vector2 screenTopLeft, out Vector2 screenTopRight, out Vector2 screenBottomRight, out Vector2 screenBottomLeft)
        {
            Matrix globalMatrix = GlobalTransform;

            Vector2 localTopLeft = Vector2.Zero;
            Vector2 localTopRight = new Vector2(ActualSize.X, 0f);
            Vector2 localBottomRight = ActualSize;
            Vector2 localBottomLeft = new Vector2(0f, ActualSize.Y);

            Vector2.Transform(ref localTopLeft, ref globalMatrix, out screenTopLeft);
            Vector2.Transform(ref localTopRight, ref globalMatrix, out screenTopRight);
            Vector2.Transform(ref localBottomRight, ref globalMatrix, out screenBottomRight);
            Vector2.Transform(ref localBottomLeft, ref globalMatrix, out screenBottomLeft);

            m_center = (screenTopLeft + screenBottomRight) / 2f;
        }

        private void QueueMinimap(Vector2 screenTopLeft, Vector2 screenTopRight, Vector2 screenBottomRight, Vector2 screenBottomLeft, int layer)
        {
            Texture2D miniMapTexture = m_componentMinimap.MapTexture;
            if (miniMapTexture == null)
                return;

            TexturedBatch2D minimapBatch = m_primitivesRenderer2D.TexturedBatch(miniMapTexture, false, layer, null, null, null, SamplerState.PointClamp);

            minimapBatch.QueueQuad(screenTopLeft, screenTopRight, screenBottomRight, screenBottomLeft, 0f, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), Color.White);
        }

        private void QueueCustomIcon(Vector2 screenTopLeft, Vector2 screenBottomRight, Texture2D texture, Vector3 worldPosition, float size, Color color, int layer, bool checkBounds = true)
        {
            if (texture == null || m_componentPlayer == null)
                return;

            TexturedBatch2D batch = m_primitivesRenderer2D.TexturedBatch(texture, false, layer, null, null, null, SamplerState.PointClamp);

            Vector2 screenPos = WorldToScreen(worldPosition);

            if (checkBounds && !WidgetUtils.IsPointInBounds(screenPos, screenTopLeft, screenBottomRight))
                return;

            float half = size / 2f;

            Vector2 p1 = screenPos + new Vector2(-half, -half);
            Vector2 p2 = screenPos + new Vector2(half, -half);
            Vector2 p3 = screenPos + new Vector2(half, half);
            Vector2 p4 = screenPos + new Vector2(-half, half);

            batch.QueueQuad(
                p1, p2, p3, p4,
                0f,
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
                color
            );
        }

        private void QueueSquare(Vector2 screenTopLeft, Vector2 screenBottomRight, Vector3 worldPosition, float size, Color color, int layer, bool checkBounds = true)
        {
            if (m_componentPlayer == null)
                return;

            FlatBatch2D batch = m_primitivesRenderer2D.FlatBatch(layer);

            Vector2 screenPos = WorldToScreen(worldPosition);

            if (checkBounds && !WidgetUtils.IsPointInBounds(screenPos, screenTopLeft, screenBottomRight))
                return;

            float half = size / 2f;

            Vector2 p1 = screenPos + new Vector2(-half, -half);
            Vector2 p2 = screenPos + new Vector2(half, -half);
            Vector2 p3 = screenPos + new Vector2(half, half);
            Vector2 p4 = screenPos + new Vector2(-half, half);

            batch.QueueQuad(
                p1,
                p2,
                p3,
                p4,
                0f,
                color
            );
        }

        private void QueueSquareOutline(Vector2 screenTopLeft, Vector2 screenBottomRight, Vector3 worldPosition, float size, Color color, int layer, bool checkBounds = true)
        {
            if (m_componentPlayer == null)
                return;

            FlatBatch2D batch = m_primitivesRenderer2D.FlatBatch(layer);

            Vector2 screenPos = WorldToScreen(worldPosition);

            if (checkBounds && !WidgetUtils.IsPointInBounds(screenPos, screenTopLeft, screenBottomRight))
                return;

            float half = size / 2f;

            Vector2 p1 = screenPos + new Vector2(-half, -half);
            Vector2 p2 = screenPos + new Vector2(half, -half);
            Vector2 p3 = screenPos + new Vector2(half, half);
            Vector2 p4 = screenPos + new Vector2(-half, half);

            batch.QueueLine(p1, p2, 0f, color);
            batch.QueueLine(p2, p3, 0f, color);
            batch.QueueLine(p3, p4, 0f, color);
            batch.QueueLine(p4, p1, 0f, color);
        }

        private void QueueText(Vector2 screenTopLeft, Vector2 screenBottomRight, string text, Vector3 worldPosition, float scale, Color color, int layer, bool checkBounds = true)
        {
            if (m_componentPlayer == null || string.IsNullOrEmpty(text))
                return;

            FontBatch2D batch = m_primitivesRenderer2D.FontBatch(LabelWidget.BitmapFont, layer, null, null, null, null);

            Vector2 screenPos = WorldToScreen(worldPosition);

            if (checkBounds && !WidgetUtils.IsPointInBounds(screenPos, screenTopLeft, screenBottomRight))
                return;

            batch.QueueText(
                text,
                screenPos,
                0f,
                color,
                TextAnchor.Center,
                new Vector2(scale)
            );
        }

        private void QueueMapMarkers(Vector2 screenTopLeft, Vector2 screenBottomRight, int layer)
        {
            float scaleX = new Vector2(GlobalTransform.M11, GlobalTransform.M12).Length();
            float size = ActualSize.X * scaleX * 0.125f;
            float scale = ActualSize.X * scaleX * 0.005f;

            int playerIndex = m_componentMinimap.m_componentPlayer.PlayerData.PlayerIndex;

            int i = 0;

            foreach (MapMarker marker in m_subsystemMapMarkers.Markers)
            {
                int ownerIndex = marker.PlayerIndex;
                if (ownerIndex != -1 && ownerIndex != playerIndex)
                    continue;

                int markerLayer = layer + i * 3;

                Vector3 markerPosition = new Vector3(marker.X + 0.5f, marker.Y, marker.Z + 0.5f);

                Vector2 screenPos = WorldToScreen(markerPosition);

                bool outside = !WidgetUtils.IsPointInBounds(screenPos, screenTopLeft, screenBottomRight);

                bool clampToEdge = marker.ClampToEdge;

                if (outside && clampToEdge)
                {
                    screenPos = new Vector2(
                        MathUtils.Clamp(screenPos.X, screenTopLeft.X, screenBottomRight.X),
                        MathUtils.Clamp(screenPos.Y, screenTopLeft.Y, screenBottomRight.Y)
                    );

                    markerPosition = ScreenToWorld(screenPos, markerPosition.Y);
                }

                if (marker.MarkerType == MarkerType.Death)
                {
                    QueueCustomIcon(screenTopLeft, screenBottomRight, ContentManager.Get<Texture2D>("Textures/Map/DeathMarker"), markerPosition, 32f, Color.White, markerLayer, !clampToEdge);
                }
                else
                {
                    string firstLetter = !string.IsNullOrEmpty(marker.Name) ? marker.Name[0].ToString() : "?";

                    QueueSquare(screenTopLeft, screenBottomRight, markerPosition, size, marker.Color, markerLayer, !clampToEdge);
                    QueueSquareOutline(screenTopLeft, screenBottomRight, markerPosition, size, Color.White, markerLayer + 1, !clampToEdge);
                    QueueText(screenTopLeft, screenBottomRight, firstLetter, markerPosition, scale, Color.White, markerLayer + 2, !clampToEdge);
                }

                i++;
            }
        }

        private void QueuePlayerArrows(Vector2 screenTopLeft, Vector2 screenBottomRight, float size, int layer)
        {
            TexturedBatch2D playerArrowBatch = m_primitivesRenderer2D.TexturedBatch(PlayerArrowTexture, false, layer, null, null, null, SamplerState.PointClamp);

            ReadOnlyList<ComponentPlayer> componentPlayers = m_subsystemPlayers.ComponentPlayers;

            for (int i = 0; i < componentPlayers.Count; i++)
            {
                ComponentPlayer componentPlayer = componentPlayers[i];

                if (componentPlayer == m_componentPlayer || !m_subsystemGameInfo.WorldSettings.IsFriendlyFireEnabled)
                {
                    Vector3 playerPos = componentPlayer == m_componentPlayer ? m_componentMinimap.MapCenterPosition : componentPlayer.ComponentBody.Position;

                    Vector2 screenPos = WorldToScreen(playerPos);

                    if (!WidgetUtils.IsPointInBounds(screenPos, screenTopLeft, screenBottomRight))
                        continue;

                    Color arrowColor = ArrowColors[i % ArrowColors.Length];

                    Vector3 forward = componentPlayer.ComponentBody.Matrix.Forward;
                    float angle = MathUtils.Atan2(-forward.X, -forward.Z);

                    float half = size / 2f;

                    Vector2 p1 = new Vector2(-half, -half);
                    Vector2 p2 = new Vector2(half, -half);
                    Vector2 p3 = new Vector2(half, half);
                    Vector2 p4 = new Vector2(-half, half);

                    float cos = MathUtils.Cos(angle);
                    float sin = MathUtils.Sin(angle);

                    p1 = Rotate(p1, cos, sin) + screenPos;
                    p2 = Rotate(p2, cos, sin) + screenPos;
                    p3 = Rotate(p3, cos, sin) + screenPos;
                    p4 = Rotate(p4, cos, sin) + screenPos;

                    playerArrowBatch.QueueQuad(
                        p1, p2, p3, p4,
                        0f,
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f),
                        arrowColor
                    );
                }
            }
        }

        public override void MeasureOverride(Vector2 parentAvailableSize)
        {
            IsDrawRequired = true;
            DesiredSize = Size;
        }

        private Vector2 Rotate(Vector2 v, float cos, float sin)
        {
            return new Vector2(
                v.X * cos - v.Y * sin,
                v.X * sin + v.Y * cos
            );
        }

        private Vector2 WorldToScreen(Vector3 worldPosition)
        {
            Vector3 myPos = m_componentMinimap.MapCenterPosition;

            float scaleX = new Vector2(GlobalTransform.M11, GlobalTransform.M12).Length();
            float blockSize = (ActualSize.X * scaleX) / m_componentMinimap.MapTexture.Width;

            Vector2 delta = worldPosition.XZ - myPos.XZ;

            return new Vector2(
                m_center.X - delta.X * blockSize,
                m_center.Y + delta.Y * blockSize
            );
        }

        private Vector3 ScreenToWorld(Vector2 screenPos, float worldY)
        {
            Vector3 myPos = m_componentMinimap.MapCenterPosition;

            float scaleX = new Vector2(GlobalTransform.M11, GlobalTransform.M12).Length();
            float blockSize = (ActualSize.X * scaleX) / m_componentMinimap.MapTexture.Width;

            float deltaX = (m_center.X - screenPos.X) / blockSize;
            float deltaZ = (screenPos.Y - m_center.Y) / blockSize;

            float worldX = myPos.X + deltaX;
            float worldZ = myPos.Z + deltaZ;

            return new Vector3(worldX, worldY, worldZ);
        }
    }
}
