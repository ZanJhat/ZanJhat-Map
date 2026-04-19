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
using System.Globalization;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public class SubsystemWorldMap : SubsystemBlockBehavior, IUpdateable
    {
        public SubsystemSky m_subsystemSky;
        public SubsystemPlayers m_subsystemPlayers;

        private Dictionary<Point2, ChunkMapData> m_chunkMap = new();
        public IReadOnlyDictionary<Point2, ChunkMapData> ChunkMap => m_chunkMap;

        private HashSet<Point2> m_dirtyColumns = new();
        public bool m_isDirty = true;

        private RenderTarget2D m_cachedMapRenderTarget;

        private Point2 m_lastCenterChunk;

        public WorldMapSettings m_worldMapSettings;

        private MapShadingMode m_lastMapShadingMode;

        public override int[] HandledBlocks
        {
            get
            {
                List<int> blockIds = new List<int>();
                foreach (Block block in BlocksManager.Blocks)
                {
                    if (block != null)
                    {
                        blockIds.Add(block.BlockIndex);
                    }
                }
                return blockIds.ToArray();
            }
        }

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt)
        {
            if (m_dirtyColumns.Count > 0)
            {
                foreach (Point2 p in m_dirtyColumns)
                {
                    UpdateSingleCell(p.X, p.Y);
                }

                m_dirtyColumns.Clear();
            }
        }

        public override void OnChunkInitialized(TerrainChunk chunk)
        {
            int chunkX = chunk.Coords.X;
            int chunkZ = chunk.Coords.Y;

            Point2 chunkPos = new Point2(chunkX, chunkZ);

            ChunkMapData chunkMap = new ChunkMapData();
            Terrain terrain = SubsystemTerrain.Terrain;

            for (int localX = 0; localX < 16; localX++)
            {
                for (int localZ = 0; localZ < 16; localZ++)
                {
                    int worldX = (chunkX << 4) + localX;
                    int worldZ = (chunkZ << 4) + localZ;

                    Color color = MapManager.GetBlockColor(SubsystemTerrain, worldX, worldZ);
                    chunkMap.SetColor(localX, localZ, color);

                    int topHeight = MapManager.GetTopHeight(terrain, worldX, worldZ);
                    chunkMap.SetHeight(localX, localZ, topHeight);
                }
            }

            m_chunkMap[chunkPos] = chunkMap;
            m_isDirty = true;
        }

        public override void OnBlockAdded(int value, int oldValue, int x, int y, int z)
        {
            m_dirtyColumns.Add(new Point2(x, z));
        }

        public override void OnBlockRemoved(int value, int newValue, int x, int y, int z)
        {
            m_dirtyColumns.Add(new Point2(x, z));
        }

        public override void OnBlockModified(int value, int oldValue, int x, int y, int z)
        {
            m_dirtyColumns.Add(new Point2(x, z));
        }

        private void UpdateSingleCell(int worldX, int worldZ)
        {
            ChunkMapData chunkMap = GetOrCreateChunk(worldX, worldZ);

            int localX = worldX & 15;
            int localZ = worldZ & 15;

            Terrain terrain = SubsystemTerrain.Terrain;

            Color color = MapManager.GetBlockColor(SubsystemTerrain, worldX, worldZ);
            chunkMap.SetColor(localX, localZ, color);

            int topHeight = MapManager.GetTopHeight(terrain, worldX, worldZ);
            chunkMap.SetHeight(localX, localZ, topHeight);

            m_isDirty = true;
        }

        public ChunkMapData GetOrCreateChunk(int worldX, int worldZ)
        {
            int chunkX = worldX >> 4;
            int chunkZ = worldZ >> 4;

            Point2 chunkPos = new Point2(chunkX, chunkZ);

            if (!m_chunkMap.TryGetValue(chunkPos, out ChunkMapData chunk))
            {
                chunk = new ChunkMapData();
                m_chunkMap.Add(chunkPos, chunk);
            }

            return chunk;
        }

        public void CleanupOutOfRangeChunks()
        {
            List<Point2> toRemove = new List<Point2>();

            foreach (Point2 chunkPos in m_chunkMap.Keys)
            {
                TerrainChunk chunk = SubsystemTerrain.Terrain.GetChunkAtCoords(chunkPos.X, chunkPos.Y);

                if (chunk == null)
                    toRemove.Add(chunkPos);
            }

            foreach (Point2 p in toRemove)
                m_chunkMap.Remove(p);

            if (toRemove.Count > 0)
                m_isDirty = true;
        }

        public Texture2D GetWorldMapTexture(Vector3 playerPosition)
        {
            int chunkX = Terrain.ToCell(playerPosition.X) >> 4;
            int chunkZ = Terrain.ToCell(playerPosition.Z) >> 4;

            Point2 currentChunk = new Point2(chunkX, chunkZ);

            if (m_cachedMapRenderTarget == null || m_isDirty || m_lastMapShadingMode != m_worldMapSettings.ShadingMode || currentChunk != m_lastCenterChunk)
            {
                m_cachedMapRenderTarget?.Dispose();
                m_cachedMapRenderTarget = CreateCenteredMap(playerPosition);
                m_isDirty = false;
                m_lastMapShadingMode = m_worldMapSettings.ShadingMode;
                m_lastCenterChunk = currentChunk;
            }

            return m_cachedMapRenderTarget;
        }

        public RenderTarget2D CreateCenteredMap(Vector3 playerPosition)
        {
            const int size = 4096;
            int half = size / 2;

            int chunkX = Terrain.ToCell(playerPosition.X) >> 4;
            int chunkZ = Terrain.ToCell(playerPosition.Z) >> 4;
            int mapCenterWorldX = chunkX * 16 + 8;
            int mapCenterWorldZ = chunkZ * 16 + 8;

            RenderTarget2D renderTarget = new RenderTarget2D(size, size, 1, ColorFormat.Rgba8888, DepthFormat.None);

            RenderTarget2D previous = Display.RenderTarget;

            Display.RenderTarget = renderTarget;
            Display.Clear(Color.Transparent);

            PrimitivesRenderer2D renderer = new PrimitivesRenderer2D();
            FlatBatch2D batch = renderer.FlatBatch();
            Vector3 lightDir = MapManager.GetLightDirection(m_subsystemSky);

            foreach (KeyValuePair<Point2, ChunkMapData> pair in m_chunkMap)
            {
                int cX = pair.Key.X;
                int cZ = pair.Key.Y;
                ChunkMapData chunk = pair.Value;

                for (int lx = 0; lx < 16; lx++)
                {
                    for (int lz = 0; lz < 16; lz++)
                    {
                        int worldX = (cX << 4) + lx;
                        int worldZ = (cZ << 4) + lz;

                        // Tính toán tọa độ pixel dựa trên mốc tĩnh của Chunk
                        int pixelX = half - (worldX - mapCenterWorldX);
                        int pixelY = half + (worldZ - mapCenterWorldZ);

                        if (pixelX < 0 || pixelX >= size || pixelY < 0 || pixelY >= size)
                            continue;

                        Color baseColor = chunk.GetColor(lx, lz);

                        if (baseColor.A == 0)
                            continue;

                        int h = chunk.GetHeight(lx, lz);

                        float dx, dz;

                        MapManager.GetHeightGradient((x, z) => GetHeightSafe(x, z, h), worldX, worldZ, m_worldMapSettings.ShadingMode, out dx, out dz);

                        Color shadedColor = MapManager.ApplyHeightShading(baseColor, dx, dz, lightDir);

                        batch.QueueQuad(new Vector2(pixelX, pixelY), new Vector2(pixelX + 1, pixelY + 1), 0f, shadedColor);
                    }
                }
            }

            renderer.Flush();
            Display.RenderTarget = previous;

            return renderTarget;
        }

        private int GetHeightSafe(int worldX, int worldZ, int fallback)
        {
            int chunkX = worldX >> 4;
            int chunkZ = worldZ >> 4;

            if (m_chunkMap.TryGetValue(new Point2(chunkX, chunkZ), out ChunkMapData chunkMap))
            {
                return chunkMap.GetHeight(worldX & 15, worldZ & 15);
            }

            return fallback;
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);

            m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
            m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);

            m_worldMapSettings = MapSettingsManager.WorldMapSettings;

            m_lastMapShadingMode = m_worldMapSettings.ShadingMode;

            if (valuesDictionary.ContainsKey("ChunkCount"))
            {
                int chunkCount = valuesDictionary.GetValue<int>("ChunkCount");

                m_chunkMap.Clear();

                for (int i = 0; i < chunkCount; i++)
                {
                    ValuesDictionary chunkDict = valuesDictionary.GetValue<ValuesDictionary>("Chunk" + i);

                    int chunkX = chunkDict.GetValue<int>("ChunkX");
                    int chunkZ = chunkDict.GetValue<int>("ChunkZ");
                    string base64 = chunkDict.GetValue<string>("Data");
                    byte[] data = Convert.FromBase64String(base64);

                    ChunkMapData chunk = DeserializeChunk(data);

                    m_chunkMap[new Point2(chunkX, chunkZ)] = chunk;
                }
            }
        }

        private ChunkMapData DeserializeChunk(byte[] data)
        {
            ChunkMapData chunk = new ChunkMapData();
            int index = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    byte r = data[index++];
                    byte g = data[index++];
                    byte b = data[index++];
                    byte a = data[index++];

                    chunk.SetColor(x, z, new Color(r, g, b, a));

                    byte h = data[index++];
                    chunk.SetHeight(x, z, h);
                }
            }

            return chunk;
        }

        public override void Save(ValuesDictionary valuesDictionary)
        {
            base.Save(valuesDictionary);

            if (!m_worldMapSettings.SaveChunkMap)
                return;

            valuesDictionary.SetValue("ChunkCount", m_chunkMap.Count);

            int i = 0;
            foreach (KeyValuePair<Point2, ChunkMapData> pair in m_chunkMap)
            {
                ValuesDictionary chunkDict = new ValuesDictionary();

                chunkDict.SetValue("ChunkX", pair.Key.X);
                chunkDict.SetValue("ChunkZ", pair.Key.Y);

                byte[] data = SerializeChunk(pair.Value);
                string base64 = Convert.ToBase64String(data);
                chunkDict.SetValue("Data", base64);

                valuesDictionary.SetValue("Chunk" + i, chunkDict);

                i++;
            }
        }

        private byte[] SerializeChunk(ChunkMapData chunk)
        {
            byte[] data = new byte[16 * 16 * 5];
            int index = 0;

            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    Color c = chunk.GetColor(x, z);

                    data[index++] = c.R;
                    data[index++] = c.G;
                    data[index++] = c.B;
                    data[index++] = c.A;

                    data[index++] = (byte)chunk.GetHeight(x, z);
                }
            }

            return data;
        }
    }

    public class ChunkMapData
    {
        private Color[,] Colors = new Color[16, 16];
        private byte[,] Heights = new byte[16, 16];

        public ChunkMapData()
        {
            for (int x = 0; x < 16; x++)
            {
                for (int z = 0; z < 16; z++)
                {
                    Colors[x, z] = Color.Transparent;
                }
            }
        }

        public void SetColor(int localX, int localZ, Color color)
        {
            Colors[localX, localZ] = color;
        }

        public Color GetColor(int localX, int localZ)
        {
            return Colors[localX, localZ];
        }

        public void SetHeight(int localX, int localZ, int y)
        {
            Heights[localX, localZ] = (byte)MathUtils.Clamp(y, 0, 255);
        }

        public int GetHeight(int localX, int localZ)
        {
            return Heights[localX, localZ];
        }
    }
}
