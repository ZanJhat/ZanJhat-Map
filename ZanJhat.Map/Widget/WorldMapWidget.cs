using System.Collections.Generic;
using Engine;
using Engine.Graphics;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public class WorldMapWidget : InputGestureWidget
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

        public Texture2D PlayerArrowTexture;
        public float ArrowSize;
        public static Color[] ArrowColors = new Color[4]
        {
            Color.Red,
            Color.Cyan,
            Color.White,
            Color.Green
        };

        public Subtexture CrossSubtexture;

        public bool MapRotatable;
        public bool AcceptInput;
        public Vector2 Grab;

        private float m_blockSize;
        private Vector2 m_center;
        public Point2? m_chosenChunkCoord;
        private Vector2 m_grabBeforeDrag;

        public float BlockSize
        {
            get => m_blockSize;
            set => m_blockSize = MathUtils.Clamp(value, 1f, 10f);
        }

        public static float DefaultBlockSize => 5f;
        public Vector3 CrossWorldPosition;

        public WorldMapWidget()
        {
            Size = new Vector2(float.PositiveInfinity);
            ArrowSize = 32f;
            BlockSize = DefaultBlockSize;
            MapRotatable = false;
            AcceptInput = true;
            m_chosenChunkCoord = null;
            PlayerArrowTexture = ContentManager.Get<Texture2D>("Textures/Map/PlayerArrow");
            CrossSubtexture = ContentManager.Get<Subtexture>("Textures/Atlas/Crosshair");
        }

        public override void Update()
        {
            base.Update();

            // Lưu lại vị trí Grab trước khi drag
            if (!Drag.HasValue)
            {
                m_grabBeforeDrag = Grab;
            }

            if (!AcceptInput)
                return;

            // Zoom map
            if (Scroll.HasValue)
            {
                if (Scroll.Value > 0f)
                    BlockSize++;
                else if (Scroll.Value < 0f)
                    BlockSize--;
            }

            // Pan map (kéo map)
            if (Drag.HasValue)
            {
                Vector2 dragOffset = Drag.Value / BlockSize * 1.5f;
                Grab = m_grabBeforeDrag + dragOffset;
            }

            // Click chọn chunk
            if (IsClicked && m_componentPlayer != null)
            {
                AudioManager.PlaySound("Audio/UI/ButtonClick", 1f, 0f, 0f);

                Vector2 clickOffsetFromCenter = WidgetToScreen(ClickedPosition.Value) - (m_center + Grab * BlockSize);

                Vector2 worldClickPosition = new Vector2(
                    m_componentPlayer.ComponentBody.Position.X - (clickOffsetFromCenter.X / BlockSize),
                    m_componentPlayer.ComponentBody.Position.Z + (clickOffsetFromCenter.Y / BlockSize)
                );

                Point2 clickedChunk = new Point2(
                    Terrain.ToCell(worldClickPosition.X) >> 4,
                    Terrain.ToCell(worldClickPosition.Y) >> 4
                );

                // Nếu click lại chunk đang chọn thì bỏ chọn
                if (m_chosenChunkCoord.HasValue && m_chosenChunkCoord.Value == clickedChunk)
                    m_chosenChunkCoord = null;
                else
                    m_chosenChunkCoord = clickedChunk;
            }

            // Xoá Chunk đang chọn nếu người chơi đi ra khỏi phạm vi Map Texture
            if (m_chosenChunkCoord.HasValue && m_componentPlayer != null)
            {
                int mapCenterChunkX = Terrain.ToCell(m_componentPlayer.ComponentBody.Position.X) >> 4;
                int mapCenterChunkZ = Terrain.ToCell(m_componentPlayer.ComponentBody.Position.Z) >> 4;

                // Texture 4096x4096 blocks tương đương giới hạn bán kính 128 Chunks từ tâm.
                if (MathUtils.Abs(m_chosenChunkCoord.Value.X - mapCenterChunkX) > 128 ||
                    MathUtils.Abs(m_chosenChunkCoord.Value.Y - mapCenterChunkZ) > 128)
                {
                    m_chosenChunkCoord = null;
                }
            }

            // Tính toán CrossWorldPosition
            if (m_componentPlayer != null && m_subsystemTerrain != null)
            {
                // 1. Tính toạ độ X, Z theo offset của Map (Grab)
                float crossX = m_componentPlayer.ComponentBody.Position.X + Grab.X;
                float crossZ = m_componentPlayer.ComponentBody.Position.Z - Grab.Y;

                // 2. Chuyển sang toạ độ Cell để lấy Height
                int cellX = Terrain.ToCell(crossX);
                int cellZ = Terrain.ToCell(crossZ);

                // 3. Lấy chiều cao Y cao nhất của block tại vị trí đó
                float crossY = 0f;

                int chunkX = cellX >> 4;
                int chunkZ = cellZ >> 4;

                if (m_subsystemWorldMap.ChunkMap.TryGetValue(new Point2(chunkX, chunkZ), out ChunkMapData chunk))
                {
                    int localX = cellX & 15;
                    int localZ = cellZ & 15;

                    crossY = chunk.GetHeight(localX, localZ);
                }
                else
                {
                    crossY = m_subsystemTerrain.Terrain.GetTopHeight(cellX, cellZ);
                }

                // 4. Gán vào biến
                CrossWorldPosition = new Vector3(crossX, crossY, crossZ);
            }
        }

        public override void Draw(DrawContext dc)
        {
            if (m_componentMinimap == null)
                return;

            // Tính toán các điểm tọa độ góc màn hình
            CalculateScreenCoordinates(
                out Vector2 screenTopLeft,
                out Vector2 screenTopRight,
                out Vector2 screenBottomRight,
                out Vector2 screenBottomLeft);

            // Xử lý vẽ world map và các thành phần bên trong
            QueueWorldMap(screenTopLeft, screenTopRight, screenBottomRight, screenBottomLeft, 0); // Layer 0

            Vector3 globalSpawnPosition = m_subsystemPlayers.GlobalSpawnPosition;

            if (m_subsystemPlayers != null)
                QueueCustomIcon(screenTopLeft, screenBottomRight, ContentManager.Get<Texture2D>("Textures/Gui/RatingStar"), globalSpawnPosition, 32f, Color.Yellow, 1); // Layer 1

            Vector3 spawnPosition = m_componentMinimap.m_componentPlayer.PlayerData.SpawnPosition;

            if (globalSpawnPosition != spawnPosition && spawnPosition != Vector3.Zero)
                QueueCustomIcon(screenTopLeft, screenBottomRight, ContentManager.Get<Texture2D>("Textures/Gui/UpdateChecking"), spawnPosition, 32f, Color.White, 2); // Layer 2

            if (m_subsystemMapMarkers != null)
                QueueMapMarkers(screenTopLeft, screenBottomRight, 3); // Layer 3 - 96

            QueuePlayerArrows(screenTopLeft, screenBottomRight, 98); // Layer 98
            QueueChunkBorder(screenTopLeft, screenBottomRight, 99); // Layer 99
            QueueCrosshair(100); // Layer 100

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

        private void QueueWorldMap(Vector2 screenTopLeft, Vector2 screenTopRight, Vector2 screenBottomRight, Vector2 screenBottomLeft, int layer)
        {
            Texture2D worldMapTexture = m_subsystemWorldMap.GetWorldMapTexture(m_componentPlayer.ComponentBody.Position);

            if (worldMapTexture == null)
                return;

            TexturedBatch2D worldMapBatch = m_primitivesRenderer2D.TexturedBatch(worldMapTexture, false, layer, null, null, null, SamplerState.PointClamp);

            Vector2 textureSize = new Vector2(worldMapTexture.Width, worldMapTexture.Height);

            // Lấy toạ độ thực của Player
            float playerX = m_componentPlayer.ComponentBody.Position.X;
            float playerZ = m_componentPlayer.ComponentBody.Position.Z;

            // Tìm lại chính xác tâm Map lúc nãy đã render trong CreateCenteredMap
            int chunkX = Terrain.ToCell(playerX) >> 4;
            int chunkZ = Terrain.ToCell(playerZ) >> 4;
            int mapCenterWorldX = chunkX * 16 + 8;
            int mapCenterWorldZ = chunkZ * 16 + 8;

            // Chấm điểm trung tâm của UV có bù trừ phần số lẻ (fraction) của Player
            // Cộng thêm 1f ở X để bù lỗi offset rìa block do X bị đảo ngược
            Vector2 textureCenterOffset = new Vector2(
                textureSize.X / 2f + (mapCenterWorldX + 1f - playerX),
                textureSize.Y / 2f + (playerZ - mapCenterWorldZ)
            ) - Grab;

            Matrix rotationMatrix = Matrix.Identity;

            if (MapRotatable)
            {
                Vector3 forward = m_componentPlayer.ComponentBody.Matrix.Forward;

                float mapAngle = MathUtils.Atan2(forward.X, -forward.Z);

                rotationMatrix =
                    Matrix.CreateTranslation(-textureCenterOffset.X, -textureCenterOffset.Y, 0f)
                    * Matrix.CreateRotationZ(-mapAngle)
                    * Matrix.CreateTranslation(textureCenterOffset.X, textureCenterOffset.Y, 0f);
            }

            float visibleWidthInBlocks = (screenBottomRight.X - screenTopLeft.X) / BlockSize;
            float visibleHeightInBlocks = (screenBottomRight.Y - screenTopLeft.Y) / BlockSize;

            Vector2 uvTopLeft = textureCenterOffset + new Vector2(-visibleWidthInBlocks / 2f, -visibleHeightInBlocks / 2f);
            Vector2 uvTopRight = textureCenterOffset + new Vector2(visibleWidthInBlocks / 2f, -visibleHeightInBlocks / 2f);
            Vector2 uvBottomRight = textureCenterOffset + new Vector2(visibleWidthInBlocks / 2f, visibleHeightInBlocks / 2f);
            Vector2 uvBottomLeft = textureCenterOffset + new Vector2(-visibleWidthInBlocks / 2f, visibleHeightInBlocks / 2f);

            Vector2.Transform(ref uvTopLeft, ref rotationMatrix, out Vector2 transformedUvTopLeft);
            Vector2.Transform(ref uvTopRight, ref rotationMatrix, out Vector2 transformedUvTopRight);
            Vector2.Transform(ref uvBottomRight, ref rotationMatrix, out Vector2 transformedUvBottomRight);
            Vector2.Transform(ref uvBottomLeft, ref rotationMatrix, out Vector2 transformedUvBottomLeft);

            worldMapBatch.QueueQuad(
                screenTopLeft, screenTopRight, screenBottomRight, screenBottomLeft, 0f,
                new Vector2(transformedUvTopLeft.X / textureSize.X, transformedUvTopLeft.Y / textureSize.Y),
                new Vector2(transformedUvTopRight.X / textureSize.X, transformedUvTopRight.Y / textureSize.Y),
                new Vector2(transformedUvBottomRight.X / textureSize.X, transformedUvBottomRight.Y / textureSize.Y),
                new Vector2(transformedUvBottomLeft.X / textureSize.X, transformedUvBottomLeft.Y / textureSize.Y),
                Color.White);
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

                    QueueSquare(screenTopLeft, screenBottomRight, markerPosition, 32f, marker.Color, markerLayer, !clampToEdge);
                    QueueSquareOutline(screenTopLeft, screenBottomRight, markerPosition, 32f, Color.White, markerLayer + 1, !clampToEdge);
                    QueueText(screenTopLeft, screenBottomRight, firstLetter, markerPosition, 1.5f, Color.White, markerLayer + 2, !clampToEdge);
                }

                i++;
            }
        }

        private void QueuePlayerArrows(Vector2 screenTopLeft, Vector2 screenBottomRight, int layer)
        {
            TexturedBatch2D playerArrowBatch = m_primitivesRenderer2D.TexturedBatch(PlayerArrowTexture, false, layer, null, null, null, SamplerState.PointClamp);

            ReadOnlyList<ComponentPlayer> componentPlayers = m_subsystemPlayers.ComponentPlayers;

            for (int i = 0; i < componentPlayers.Count; i++)
            {
                ComponentPlayer componentPlayer = componentPlayers[i];

                if (componentPlayer == m_componentPlayer || !m_subsystemGameInfo.WorldSettings.IsFriendlyFireEnabled)
                {
                    Vector3 playerPos = componentPlayer.ComponentBody.Position;

                    Vector2 screenPos = WorldToScreen(playerPos);

                    if (!WidgetUtils.IsPointInBounds(screenPos, screenTopLeft, screenBottomRight))
                        continue;

                    Color arrowColor = ArrowColors[i % ArrowColors.Length];

                    Vector3 forward = componentPlayer.ComponentBody.Matrix.Forward;
                    float angle = MathUtils.Atan2(-forward.X, -forward.Z);

                    float half = ArrowSize / 2f;

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

        private void QueueChunkBorder(Vector2 screenTopLeft, Vector2 screenBottomRight, int layer)
        {
            FlatBatch2D chunkBorderBatch = m_primitivesRenderer2D.FlatBatch(layer);

            if (m_chosenChunkCoord.HasValue && !MapRotatable)
            {
                // Tính biên thế giới của chunk đang chọn
                float chunkWestX = m_chosenChunkCoord.Value.X * 16f;
                float chunkNorthZ = m_chosenChunkCoord.Value.Y * 16f;

                float chunkEastX = chunkWestX + 16f;

                Vector2 selectedChunkScreenPosition = new Vector2(
                    m_center.X - (chunkEastX - m_componentPlayer.ComponentBody.Position.X - Grab.X) * BlockSize,
                    m_center.Y + (chunkNorthZ - m_componentPlayer.ComponentBody.Position.Z + Grab.Y) * BlockSize
                );

                if (WidgetUtils.IsPointInBounds(selectedChunkScreenPosition, screenTopLeft, screenBottomRight)
                    && WidgetUtils.IsPointInBounds(selectedChunkScreenPosition + new Vector2(16f * BlockSize), screenTopLeft, screenBottomRight))
                {
                    Color borderColor = Color.Black;

                    for (float borderOffset = 0f; borderOffset <= 2f; borderOffset += 2f)
                    {
                        chunkBorderBatch.QueueQuad(
                            selectedChunkScreenPosition + new Vector2(borderOffset),
                            selectedChunkScreenPosition + new Vector2(5f * BlockSize, BlockSize) - new Vector2(borderOffset),
                            0f, borderColor);

                        chunkBorderBatch.QueueQuad(
                            selectedChunkScreenPosition + new Vector2(11f * BlockSize, 0f) + new Vector2(borderOffset),
                            selectedChunkScreenPosition + new Vector2(16f * BlockSize, 1f * BlockSize) - new Vector2(borderOffset),
                            0f, borderColor);

                        chunkBorderBatch.QueueQuad(
                            selectedChunkScreenPosition + new Vector2(0f, 15f * BlockSize) + new Vector2(borderOffset),
                            selectedChunkScreenPosition + new Vector2(5f * BlockSize, 16f * BlockSize) - new Vector2(borderOffset),
                            0f, borderColor);

                        chunkBorderBatch.QueueQuad(
                            selectedChunkScreenPosition + new Vector2(11f * BlockSize, 15f * BlockSize) + new Vector2(borderOffset),
                            selectedChunkScreenPosition + new Vector2(16f * BlockSize, 16f * BlockSize) - new Vector2(borderOffset),
                            0f, borderColor);

                        chunkBorderBatch.QueueQuad(
                            selectedChunkScreenPosition + new Vector2(borderOffset),
                            selectedChunkScreenPosition + new Vector2(BlockSize, 5f * BlockSize) - new Vector2(borderOffset),
                            0f, borderColor);

                        chunkBorderBatch.QueueQuad(
                            selectedChunkScreenPosition + new Vector2(0f, 11f * BlockSize) + new Vector2(borderOffset),
                            selectedChunkScreenPosition + new Vector2(1f * BlockSize, 16f * BlockSize) - new Vector2(borderOffset),
                            0f, borderColor);

                        chunkBorderBatch.QueueQuad(
                            selectedChunkScreenPosition + new Vector2(15f * BlockSize, 0f) + new Vector2(borderOffset),
                            selectedChunkScreenPosition + new Vector2(16f * BlockSize, 5f * BlockSize) - new Vector2(borderOffset),
                            0f, borderColor);

                        chunkBorderBatch.QueueQuad(
                            selectedChunkScreenPosition + new Vector2(15f * BlockSize, 11f * BlockSize) + new Vector2(borderOffset),
                            selectedChunkScreenPosition + new Vector2(16f * BlockSize, 16f * BlockSize) - new Vector2(borderOffset),
                            0f, borderColor);

                        borderColor = Color.White;
                    }
                }
            }
        }

        private void QueueCrosshair(int layer)
        {
            TexturedBatch2D crosshairBatch = m_primitivesRenderer2D.TexturedBatch(CrossSubtexture.Texture, false, layer, null, null, null, SamplerState.PointClamp);

            if (AcceptInput)
            {
                float crossSize = 25f;

                crosshairBatch.QueueQuad(
                    m_center - new Vector2(crossSize / 2f),
                    m_center + new Vector2(crossSize / 2f),
                    0f,
                    CrossSubtexture.TopLeft,
                    CrossSubtexture.BottomRight,
                    Color.White);
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
            Vector3 playerPos = m_componentPlayer.ComponentBody.Position;

            Vector2 delta = worldPosition.XZ - playerPos.XZ;

            return new Vector2(
                m_center.X - (delta.X - Grab.X) * BlockSize,
                m_center.Y + (delta.Y + Grab.Y) * BlockSize
            );
        }

        private Vector3 ScreenToWorld(Vector2 screenPos, float worldY)
        {
            Vector3 playerPos = m_componentPlayer.ComponentBody.Position;

            float deltaX = (m_center.X - screenPos.X) / BlockSize + Grab.X;
            float deltaZ = (screenPos.Y - m_center.Y) / BlockSize - Grab.Y;

            float worldX = playerPos.X + deltaX;
            float worldZ = playerPos.Z + deltaZ;

            return new Vector3(worldX, worldY, worldZ);
        }
    }
}
