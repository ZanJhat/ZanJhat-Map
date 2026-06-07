using Engine;
using Engine.Graphics;
using Engine.Serialization;
using GameEntitySystem;
using System;
using System.Collections.Generic;
using System.IO;
using TemplatesDatabase;
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
        private HashSet<Point2> m_pendingShading = new();

        private List<MapViewCache> m_mapViews = new List<MapViewCache>();
        private const int MaxSplitScreens = 4;

        public GlobalMapSettings m_globalMapSettings;
        private MapShadingMode m_lastMapShadingMode;

        public const int MaxColumnsPerFrame = 512;
        public const int MaxShadingPixelsPerFrame = 1024;
        public const int MaxChunksToUnpack = 4;

        // --- CÁC BỘ ĐỆM (BUFFERS) ĐƯỢC TÁI SỬ DỤNG ĐỂ TRÁNH RÁC GC ---
        private List<Point2> m_tempColumns = new List<Point2>(MaxColumnsPerFrame);
        private List<Point2> m_tempShades = new List<Point2>(MaxShadingPixelsPerFrame);
        private HashSet<Point2> m_pixelsToShade = new HashSet<Point2>();
        private List<Point2> m_tempChunksToRemove = new List<Point2>();

        // DÙNG HÀNG ĐỢI (QUEUE) ĐỂ XỬ LÝ TỪ TRONG RA NGOÀI (FIFO) CHỐNG VÒNG ĐEN VÀ TRÁNH LAG LÚC VỪA VÀO WORLD
        private Queue<Point2> m_pendingChunkShading = new Queue<Point2>();

        public PrimitivesRenderer2D PrimitivesRenderer = new PrimitivesRenderer2D();

        private int[] m_handledBlocks;

        public override int[] HandledBlocks
        {
            get
            {
                if (m_handledBlocks == null)
                {
                    List<int> blockIds = new List<int>();

                    foreach (Block block in BlocksManager.Blocks)
                    {
                        if (block != null && block.BlockIndex != 0)
                            blockIds.Add(block.BlockIndex);
                    }

                    m_handledBlocks = blockIds.ToArray();
                }

                return m_handledBlocks;
            }
        }

        public UpdateOrder UpdateOrder => UpdateOrder.Default;

        public void Update(float dt)
        {
            if (m_lastMapShadingMode != m_globalMapSettings.ShadingMode)
            {
                m_lastMapShadingMode = m_globalMapSettings.ShadingMode;
                foreach (MapViewCache view in m_mapViews)
                    view.IsValid = false;

                foreach (KeyValuePair<Point2, ChunkMapData> kvp in m_chunkMap)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        for (int z = 0; z < 16; z++)
                        {
                            m_pendingShading.Add(new Point2((kvp.Key.X << 4) + x, (kvp.Key.Y << 4) + z));
                        }
                    }
                }
            }

            // Giải nén từ từ các Chunk vừa Load vào hệ thống Shading (Luôn ưu tiên Chunk tạo trước)
            if (m_pendingChunkShading.Count > 0 && m_pendingShading.Count < MaxShadingPixelsPerFrame)
            {
                int chunksToUnpack = Math.Min(m_pendingChunkShading.Count, MaxChunksToUnpack);
                for (int i = 0; i < chunksToUnpack; i++)
                {
                    Point2 cPos = m_pendingChunkShading.Dequeue();

                    for (int x = 0; x < 16; x++)
                    {
                        for (int z = 0; z < 16; z++)
                        {
                            m_pendingShading.Add(new Point2((cPos.X << 4) + x, (cPos.Y << 4) + z));
                        }
                    }
                }
            }

            if (m_dirtyColumns.Count > 0 || m_pendingShading.Count > 0)
            {
                m_pixelsToShade.Clear();

                // 1. Xử lý Dirty Columns
                if (m_dirtyColumns.Count > 0)
                {
                    m_tempColumns.Clear();

                    int count = 0;
                    foreach (Point2 p in m_dirtyColumns)
                    {
                        if (count >= MaxColumnsPerFrame)
                            break;

                        m_tempColumns.Add(p);
                        count++;
                    }

                    foreach (Point2 p in m_tempColumns)
                    {
                        m_dirtyColumns.Remove(p);
                        UpdateBaseData(p.X, p.Y);

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                m_pixelsToShade.Add(new Point2(p.X + dx, p.Y + dz));
                            }
                        }
                    }
                }

                // 2. Xử lý Pending Shading
                int remainCapacity = MaxShadingPixelsPerFrame - m_pixelsToShade.Count;

                if (m_pendingShading.Count > 0 && remainCapacity > 0)
                {
                    m_tempShades.Clear();

                    int count = 0;
                    foreach (Point2 p in m_pendingShading)
                    {
                        if (count >= remainCapacity)
                            break;

                        m_tempShades.Add(p);
                        count++;
                    }

                    foreach (Point2 p in m_tempShades)
                    {
                        m_pendingShading.Remove(p);
                        m_pixelsToShade.Add(p);
                    }
                }

                // 3. Tính toán màu Shading cuối cùng
                if (m_pixelsToShade.Count > 0)
                {
                    foreach (Point2 p in m_pixelsToShade)
                    {
                        Color shaded = RecalculateShadedColor(p.X, p.Y);
                        if (shaded.A == 0)
                            continue;

                        foreach (MapViewCache view in m_mapViews)
                        {
                            if (!view.IsValid || view.RenderTarget == null)
                                continue;

                            int mapCenterWorldX = view.CenterChunk.X * 16 + 8;
                            int mapCenterWorldZ = view.CenterChunk.Y * 16 + 8;
                            int size = view.RenderTarget.Width;
                            int half = size / 2;

                            int pixelX = half - (p.X - mapCenterWorldX);
                            int pixelY = half + (p.Y - mapCenterWorldZ);

                            if (pixelX >= 0 && pixelX < size && pixelY >= 0 && pixelY < size)
                            {
                                view.PendingPatches.Add(new MapPixelPatch
                                {
                                    Px = pixelX,
                                    Py = pixelY,
                                    Color = shaded
                                });
                            }
                        }
                    }
                }

                // 4. Dán (Patch) - Thay .Any() bằng vòng lặp thường
                bool hasPatches = false;
                foreach (MapViewCache view in m_mapViews)
                {
                    if (view.PendingPatches.Count > 0)
                    {
                        hasPatches = true;
                        break;
                    }
                }

                if (hasPatches)
                {
                    RenderTarget2D previous = Display.RenderTarget;
                    FlatBatch2D batch = PrimitivesRenderer.FlatBatch();

                    foreach (MapViewCache view in m_mapViews)
                    {
                        if (view.PendingPatches.Count > 0)
                        {
                            Display.RenderTarget = view.RenderTarget;
                            foreach (MapPixelPatch patch in view.PendingPatches)
                                batch.QueueQuad(new Vector2(patch.Px, patch.Py), new Vector2(patch.Px + 1, patch.Py + 1), 0f, patch.Color);

                            PrimitivesRenderer.Flush();
                            view.PendingPatches.Clear();
                        }
                    }
                    Display.RenderTarget = previous;
                }
            }
        }

        private void UpdateBaseData(int worldX, int worldZ)
        {
            ChunkMapData chunkMap = GetOrCreateChunk(worldX, worldZ);
            int localX = worldX & 15;
            int localZ = worldZ & 15;

            Color color = MapManager.GetBlockColor(SubsystemTerrain, worldX, worldZ);
            chunkMap.SetColor(localX, localZ, color);

            int topHeight = MapManager.GetTopHeight(SubsystemTerrain.Terrain, worldX, worldZ);
            chunkMap.SetHeight(localX, localZ, topHeight);
        }

        public ChunkMapData GetOrCreateChunk(int worldX, int worldZ)
        {
            Point2 chunkPos = new Point2(worldX >> 4, worldZ >> 4);
            if (!m_chunkMap.TryGetValue(chunkPos, out ChunkMapData chunk))
            {
                chunk = new ChunkMapData();
                m_chunkMap.Add(chunkPos, chunk);
            }
            return chunk;
        }

        private Color RecalculateShadedColor(int wx, int wz)
        {
            int cX = wx >> 4;
            int cZ = wz >> 4;
            if (!m_chunkMap.TryGetValue(new Point2(cX, cZ), out ChunkMapData chunk))
                return Color.Transparent;

            int lx = wx & 15;
            int lz = wz & 15;

            Color baseColor = chunk.GetColor(lx, lz);
            if (baseColor.A == 0)
                return Color.Transparent;

            int h = chunk.GetHeight(lx, lz);
            MapManager.GetHeightGradient((x, z) => GetHeightSafe(x, z, h), wx, wz, m_globalMapSettings.ShadingMode, out float dx, out float dz);

            Vector3 lightDir = MapManager.GetLightDirection(m_subsystemSky);
            Color shadedColor = MapManager.ApplyHeightShading(baseColor, dx, dz, lightDir);

            chunk.SetShadedColor(lx, lz, shadedColor);
            return shadedColor;
        }

        public override void OnChunkInitialized(TerrainChunk chunk)
        {
            int chunkX = chunk.Coords.X;
            int chunkZ = chunk.Coords.Y;
            Point2 chunkPos = new Point2(chunkX, chunkZ);

            // 1. Chỉ khởi tạo mảng dữ liệu rỗng và nạp vào Dictionary
            ChunkMapData chunkMap = new ChunkMapData();
            m_chunkMap[chunkPos] = chunkMap;

            // 2. Ném toàn bộ 256 pixel vào m_dirtyColumns để hàm Update() tự động lo liệu 
            // việc lấy Color, Height và tính toán Shading (vá viền 3x3).
            for (int localX = 0; localX < 16; localX++)
            {
                for (int localZ = 0; localZ < 16; localZ++)
                {
                    int worldX = (chunkX << 4) + localX;
                    int worldZ = (chunkZ << 4) + localZ;

                    m_dirtyColumns.Add(new Point2(worldX, worldZ));
                }
            }
        }

        public override void OnBlockAdded(int value, int oldValue, int x, int y, int z) => m_dirtyColumns.Add(new Point2(x, z));
        public override void OnBlockRemoved(int value, int newValue, int x, int y, int z) => m_dirtyColumns.Add(new Point2(x, z));
        public override void OnBlockModified(int value, int oldValue, int x, int y, int z) => m_dirtyColumns.Add(new Point2(x, z));

        public Texture2D GetWorldMapTexture(Vector3 playerPosition)
        {
            int chunkX = Terrain.ToCell(playerPosition.X) >> 4;
            int chunkZ = Terrain.ToCell(playerPosition.Z) >> 4;
            Point2 currentChunk = new Point2(chunkX, chunkZ);

            // Tìm Cache
            MapViewCache view = null;
            foreach (MapViewCache v in m_mapViews)
            {
                if (v.CenterChunk == currentChunk && v.IsValid)
                {
                    view = v;
                    break;
                }
            }

            if (view != null)
            {
                view.LastUsedTime = Time.RealTime;
                return view.RenderTarget;
            }

            if (m_mapViews.Count >= MaxSplitScreens)
            {
                // Tìm Cache cũ nhất
                MapViewCache oldest = m_mapViews[0];
                for (int i = 1; i < m_mapViews.Count; i++)
                {
                    if (m_mapViews[i].LastUsedTime < oldest.LastUsedTime)
                        oldest = m_mapViews[i];
                }
                view = oldest;
            }
            else
            {
                view = new MapViewCache();
                m_mapViews.Add(view);
            }

            if (view.RenderTarget == null)
            {
                view.RenderTarget = new RenderTarget2D(4096, 4096, 1, ColorFormat.Rgba8888, DepthFormat.None);
            }

            view.CenterChunk = currentChunk;
            view.IsValid = true;
            view.LastUsedTime = Time.RealTime;

            RedrawFullMap(view);

            return view.RenderTarget;
        }

        private void RedrawFullMap(MapViewCache view)
        {
            RenderTarget2D previous = Display.RenderTarget;
            Display.RenderTarget = view.RenderTarget;
            Display.Clear(Color.Transparent);

            PrimitivesRenderer2D renderer = new PrimitivesRenderer2D();
            FlatBatch2D batch = renderer.FlatBatch();

            int mapCenterWorldX = view.CenterChunk.X * 16 + 8;
            int mapCenterWorldZ = view.CenterChunk.Y * 16 + 8;
            int size = view.RenderTarget.Width;
            int half = size / 2;

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

                        int pixelX = half - (worldX - mapCenterWorldX);
                        int pixelY = half + (worldZ - mapCenterWorldZ);

                        if (pixelX < 0 || pixelX >= size || pixelY < 0 || pixelY >= size)
                            continue;

                        Color shadedColor = chunk.GetShadedColor(lx, lz);

                        if (shadedColor == Color.Transparent && chunk.GetColor(lx, lz).A != 0)
                        {
                            shadedColor = chunk.GetColor(lx, lz);
                        }

                        if (shadedColor.A != 0)
                        {
                            batch.QueueQuad(new Vector2(pixelX, pixelY), new Vector2(pixelX + 1, pixelY + 1), 0f, shadedColor);
                        }
                    }
                }
            }

            renderer.Flush();
            Display.RenderTarget = previous;
        }

        private int GetHeightSafe(int worldX, int worldZ, int fallback)
        {
            if (m_chunkMap.TryGetValue(new Point2(worldX >> 4, worldZ >> 4), out ChunkMapData chunkMap))
                return chunkMap.GetHeight(worldX & 15, worldZ & 15);

            return fallback;
        }

        public void CleanupOutOfRangeChunks()
        {
            m_tempChunksToRemove.Clear();

            foreach (Point2 pos in m_chunkMap.Keys)
            {
                if (SubsystemTerrain.Terrain.GetChunkAtCoords(pos.X, pos.Y) == null)
                    m_tempChunksToRemove.Add(pos);
            }

            foreach (Point2 p in m_tempChunksToRemove)
            {
                m_chunkMap.Remove(p);
            }

            if (m_tempChunksToRemove.Count > 0)
            {
                foreach (MapViewCache view in m_mapViews)
                    view.IsValid = false;
            }
        }

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_subsystemSky = Project.FindSubsystem<SubsystemSky>(true);
            m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);

            m_globalMapSettings = MapSettingsManager.GlobalMapSettings;
            m_lastMapShadingMode = m_globalMapSettings.ShadingMode;

            m_chunkMap.Clear();
            m_pendingChunkShading.Clear();

            // ĐỊNH DẠNG MỚI: SIÊU NHANH (Gộp thành 1 luồng nhị phân duy nhất)
            if (valuesDictionary.ContainsKey("MapDataBlob"))
            {
                byte[] data = Convert.FromBase64String(valuesDictionary.GetValue<string>("MapDataBlob"));
                using (MemoryStream ms = new MemoryStream(data))
                using (BinaryReader reader = new BinaryReader(ms))
                {
                    int chunkCount = reader.ReadInt32();
                    for (int i = 0; i < chunkCount; i++)
                    {
                        int cx = reader.ReadInt32();
                        int cz = reader.ReadInt32();
                        // 16 * 16 * 5 bytes = 1280
                        byte[] chunkData = reader.ReadBytes(1280);

                        ChunkMapData chunk = DeserializeChunk(chunkData);
                        Point2 chunkPos = new Point2(cx, cz);
                        m_chunkMap[chunkPos] = chunk;

                        m_pendingChunkShading.Enqueue(chunkPos);
                    }
                }
            }
            // HỖ TRỢ NGƯỢC (Tương thích với các Map đã lưu bằng phiên bản cũ)
            else if (valuesDictionary.ContainsKey("ChunkCount"))
            {
                int chunkCount = valuesDictionary.GetValue<int>("ChunkCount");
                for (int i = 0; i < chunkCount; i++)
                {
                    ValuesDictionary chunkDict = valuesDictionary.GetValue<ValuesDictionary>("Chunk" + i);
                    int chunkX = chunkDict.GetValue<int>("ChunkX");
                    int chunkZ = chunkDict.GetValue<int>("ChunkZ");
                    byte[] data = Convert.FromBase64String(chunkDict.GetValue<string>("Data"));

                    ChunkMapData chunk = DeserializeChunk(data);
                    Point2 chunkPos = new Point2(chunkX, chunkZ);
                    m_chunkMap[chunkPos] = chunk;

                    m_pendingChunkShading.Enqueue(chunkPos);
                }
            }
        }

        public override void Save(ValuesDictionary valuesDictionary)
        {
            base.Save(valuesDictionary);
            if (!m_globalMapSettings.SaveChunkMap || m_chunkMap.Count == 0)
                return;

            // GHI DỮ LIỆU SIÊU TỐC VÀO MEMORY STREAM
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(m_chunkMap.Count);

                foreach (KeyValuePair<Point2, ChunkMapData> pair in m_chunkMap)
                {
                    writer.Write(pair.Key.X);
                    writer.Write(pair.Key.Y);
                    writer.Write(SerializeChunk(pair.Value)); // Ghi thẳng 1280 bytes (16 * 16 * 5)
                }

                // Lưu toàn bộ map thành 1 KEY Base64 duy nhất
                valuesDictionary.SetValue("MapDataBlob", Convert.ToBase64String(ms.ToArray()));
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
                    chunk.SetColor(x, z, new Color(data[index++], data[index++], data[index++], data[index++]));
                    chunk.SetHeight(x, z, data[index++]);
                }
            }
            return chunk;
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

        public override void Dispose()
        {
            foreach (MapViewCache view in m_mapViews)
                view.Dispose();

            m_mapViews.Clear();
            base.Dispose();
        }
    }
}
