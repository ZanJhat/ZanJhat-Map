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
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public static class MapManager
    {
        private static readonly Dictionary<string, BlockPixelData> BlockPixelDatas = new Dictionary<string, BlockPixelData>();

        private static readonly Dictionary<string, BlockPixelData> ModBlockPixelDatas = new Dictionary<string, BlockPixelData>();

        private static readonly Dictionary<string, Func<BlockColorContext, Color>> ColorHandlers = new();

        private static readonly Dictionary<int, string> BlockNameCache = new();

        public static void OnProjectLoaded()
        {
            BlockNameCache.Clear();
        }

        public static void RegisterBlockColorHandler(string blockName, Func<BlockColorContext, Color> handler)
        {
            ColorHandlers[blockName] = handler;
        }

        public static void Initialize()
        {
            LoadBlocksPixelColor();
            LoadModBlockPixelColor(ModsManager.ModList);
        }

        public static void LoadBlocksPixelColor()
        {
            BlockPixelDatas.Clear();
            XElement blockElements = ContentManager.Get<XElement>("BlockPixelColor");
            ParseBlockPixelData(blockElements, BlockPixelDatas);
        }

        public static void LoadModBlockPixelColor(IEnumerable<ModEntity> mods)
        {
            ModBlockPixelDatas.Clear();

            foreach (ModEntity mod in mods)
            {
                mod.GetFiles(".bpd", (filename, stream) =>
                {
                    try
                    {
                        XElement xml = XElement.Load(stream);
                        ParseBlockPixelData(xml, ModBlockPixelDatas);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Failed to load .bpd [{filename}]: {ex}");
                    }
                });
            }
        }

        public static void ParseBlockPixelData(XElement element, Dictionary<string, BlockPixelData> dictionary)
        {
            if (element == null)
            {
                Log.Warning("ParseBlockPixelData: XElement is null.");
                return;
            }

            int total = 0;
            int success = 0;
            int error = 0;

            foreach (XElement blockElement in element.Elements("Block"))
            {
                total++;

                try
                {
                    // Name
                    XAttribute nameAttr = blockElement.Attribute("Name");

                    if (nameAttr == null || string.IsNullOrWhiteSpace(nameAttr.Value))
                    {
                        Log.Warning($"Block #{total}: Invalid or missing Name");
                        error++;
                        continue;
                    }

                    string name = nameAttr.Value;

                    // NeedChange
                    bool needChange = false;
                    XAttribute needChangeAttr = blockElement.Attribute("NeedChange");

                    if (needChangeAttr != null)
                        bool.TryParse(needChangeAttr.Value, out needChange);

                    // Color
                    Color color = Color.White;
                    XAttribute colorAttr = blockElement.Attribute("Color");

                    if (colorAttr != null)
                    {
                        string[] parts = colorAttr.Value.Split(',');

                        if (parts.Length >= 3 &&
                            byte.TryParse(parts[0].Trim(), out byte r) &&
                            byte.TryParse(parts[1].Trim(), out byte g) &&
                            byte.TryParse(parts[2].Trim(), out byte b))
                        {
                            byte a = 255;

                            if (parts.Length >= 4 &&
                                byte.TryParse(parts[3].Trim(), out byte parsedA))
                            {
                                a = parsedA;
                            }
                            color = new Color(r, g, b, a);
                        }
                        else
                        {
                            Log.Warning($"Block #{name}: Invalid Color format");
                        }
                    }

                    // Duplicate check
                    if (dictionary.ContainsKey(name))
                    {
                        Log.Warning($"Block #{name}: Duplicate entry. Overwriting");
                    }

                    dictionary[name] = new BlockPixelData
                    {
                        BlockName = name,
                        NeedChangeWithEnvironment = needChange,
                        Color = color
                    };

                    success++;
                }
                catch (Exception ex)
                {
                    Log.Warning($"Block #{total}: Unexpected error: {ex.Message}");
                    error++;
                }
            }

            Log.Information($"BlockPixelData parsed: Total={total}, Success={success}, Error={error}");
        }

        public static Vector3 GetLightDirection(SubsystemSky subsystemSky)
        {
            if (subsystemSky == null)
                return Vector3.Normalize(new Vector3(0.5f, 1f, 0.5f));

            float timeOfDay = subsystemSky.m_subsystemTimeOfDay.Midday;
            float angle = MathUtilsEx.TwoPi * (timeOfDay - subsystemSky.m_subsystemTimeOfDay.Midday);

            Matrix sunMatrix = Matrix.CreateRotationZ(-angle) * Matrix.CreateRotationX(subsystemSky.CalculateSeasonAngle());

            Vector3 lightDir = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, sunMatrix));
            return lightDir;
        }

        public static int GetTopHeight(Terrain terrain, int x, int z)
        {
            if (terrain == null)
                return 0;

            int y = terrain.GetTopHeight(x, z);

            while (y > 0)
            {
                int content = terrain.GetCellContents(x, y, z);

                if (content != 0 && !SkippableBlocks.Contains(content))
                    break;

                y--;
            }

            return y;
        }

        public static int GetTopCellValue(SubsystemTerrain subsystemTerrain, int x, int z)
        {
            if (subsystemTerrain == null)
                return 0;

            Terrain terrain = subsystemTerrain.Terrain;
            int y = GetTopHeight(terrain, x, z);

            int value = terrain.GetCellValue(x, y, z);
            int contents = Terrain.ExtractContents(value);

            if (IsSeaPlant(contents))
                return Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<WaterBlock>());

            return value;
        }

        public static Color GetBlockColor(SubsystemTerrain subsystemTerrain, int worldX, int worldZ)
        {
            if (subsystemTerrain == null)
                return Color.Black;

            Terrain terrain = subsystemTerrain.Terrain;

            int topY = GetTopHeight(terrain, worldX, worldZ);
            int value = terrain.GetCellValue(worldX, topY, worldZ);
            int contents = Terrain.ExtractContents(value);

            int waterIndex = BlocksManager.GetBlockIndex<WaterBlock>();

            if (IsSeaPlant(contents))
            {
                value = Terrain.MakeBlockValue(waterIndex);
                contents = waterIndex;
            }

            // Nếu không phải nước → trả về bình thường
            if (contents != waterIndex)
            {
                Color baseColor = GetBlockColor(subsystemTerrain, value, worldX, worldZ);
                return baseColor;
            }

            // Có nước

            int waterTopY = topY;
            int depth = 0;

            // Tìm đáy nước
            while (waterTopY > 0)
            {
                int v = terrain.GetCellValue(worldX, waterTopY, worldZ);
                int c = Terrain.ExtractContents(v);

                if (c != 0 && c != waterIndex && !IsSeaPlant(c))
                    break;

                depth++;
                waterTopY--;
            }

            // Lấy block đáy
            int bottomValue = terrain.GetCellValue(worldX, waterTopY, worldZ);
            Color bottomColor = GetBlockColor(subsystemTerrain, bottomValue, worldX, worldZ);

            // Lấy màu nước
            Color waterColor = GetBlockColor(
                subsystemTerrain,
                Terrain.MakeBlockValue(BlocksManager.GetBlockIndex<WaterBlock>()),
                worldX,
                worldZ);

            // Blend theo độ sâu
            return ColorUtils.BlendWithDistance(bottomColor, waterColor, depth, 8f, 0.75f);
        }

        public static Color GetBlockColor(SubsystemTerrain subsystemTerrain, int value, int x, int z)
        {
            if (subsystemTerrain == null)
                return Color.Black;

            int contents = Terrain.ExtractContents(value);
            Block block = BlocksManager.Blocks[contents];

            if (!BlockNameCache.TryGetValue(contents, out string blockName))
            {
                blockName = block.GetType().Name;
                BlockNameCache[contents] = blockName;
            }

            BlockPixelData blockPixelData;

            if (!ModBlockPixelDatas.TryGetValue(blockName, out blockPixelData))
            {
                if (!BlockPixelDatas.TryGetValue(blockName, out blockPixelData))
                    return Color.Black;
            }

            Color blockColor = blockPixelData.Color;

            blockColor = ApplyEnvironmentColor(blockName, blockColor, block, contents, value, subsystemTerrain, x, z, blockPixelData.NeedChangeWithEnvironment);

            return blockColor;
        }

        private static Color ApplyEnvironmentColor(string blockName, Color baseColor, Block block, int contents, int value, SubsystemTerrain subsystemTerrain, int x, int z, bool needChange)
        {
            if (!needChange)
                return baseColor;

            Terrain terrain = subsystemTerrain.Terrain;

            if (ColorHandlers.TryGetValue(blockName, out var handler))
            {
                return handler(new BlockColorContext
                {
                    BaseColor = baseColor,
                    Block = block,
                    Contents = contents,
                    Value = value,
                    SubsystemTerrain = subsystemTerrain,
                    Terrain = terrain,
                    X = x,
                    Z = z
                });
            }

            int y = terrain.GetTopHeight(x, z);

            if (contents == BlocksManager.GetBlockIndex<GrassBlock>())
            {
                return baseColor * BlockColorsMap.Grass.Lookup(terrain, x, y, z);
            }
            else if (contents == BlocksManager.GetBlockIndex<WaterBlock>())
            {
                return baseColor * BlockColorsMap.Water.Lookup(terrain, x, y, z);
            }
            else if (block is DeciduousLeavesBlock deciduousLeavesBlock)
            {
                return deciduousLeavesBlock.GetLeavesBlockColor(value, terrain, x, y, z);
            }
            else if (block is EvergreenLeavesBlock evergreenLeavesBlock)
            {
                return evergreenLeavesBlock.GetLeavesBlockColor(value, terrain, x, y, z);
            }

            return baseColor;
        }

        public static Color ApplyHeightShading(Color baseColor, float dx, float dz, Vector3 lightDir)
        {
            Vector3 normal = Vector3.Normalize(new Vector3(-dx, 1f, -dz));

            float diffuse = MathUtils.Clamp(Vector3.Dot(normal, lightDir), 0f, 1f);

            float ambient = 0.4f;
            float shade = ambient + diffuse * 0.6f;

            return new Color(
                (byte)MathUtils.Clamp(baseColor.R * shade, 0, 255),
                (byte)MathUtils.Clamp(baseColor.G * shade, 0, 255),
                (byte)MathUtils.Clamp(baseColor.B * shade, 0, 255),
                baseColor.A
            );
        }

        private static readonly HashSet<int> SkippableBlocks = new()
        {
            BlocksManager.GetBlockIndex<TallGrassBlock>(),
            BlocksManager.GetBlockIndex<IvyBlock>(),
            BlocksManager.GetBlockIndex<GlassBlock>(),
            BlocksManager.GetBlockIndex<FramedGlassBlock>(),
            BlocksManager.GetBlockIndex<WindowBlock>(),
            BlocksManager.GetBlockIndex<LightbulbBlock>(),
            BlocksManager.GetBlockIndex<TargetBlock>()
        };

        private static bool IsSeaPlant(int content)
        {
            return content == BlocksManager.GetBlockIndex<SeaUrchinBlock>()
                || content == BlocksManager.GetBlockIndex<StarfishBlock>()
                || content == BlocksManager.GetBlockIndex<KelpBlock>()
                || content == BlocksManager.GetBlockIndex<SeagrassBlock>();
        }

        public static void GetHeightGradient(Func<int, int, int> getHeight, int x, int z, MapShadingMode mode, out float dx, out float dz)
        {
            if (mode == MapShadingMode.None)
            {
                dx = 0;
                dz = 0;
                return;
            }

            int hN = getHeight(x, z - 1);
            int hS = getHeight(x, z + 1);
            int hE = getHeight(x + 1, z);
            int hW = getHeight(x - 1, z);

            if (mode == MapShadingMode.Fast)
            {
                dx = (hE - hW) * 0.5f;
                dz = (hS - hN) * 0.5f;
                return;
            }

            // MapShadingMode.Smooth
            int hNE = getHeight(x + 1, z - 1);
            int hNW = getHeight(x - 1, z - 1);
            int hSE = getHeight(x + 1, z + 1);
            int hSW = getHeight(x - 1, z + 1);

            dx =
                (hNE + 2 * hE + hSE) -
                (hNW + 2 * hW + hSW);

            dz =
                (hSW + 2 * hS + hSE) -
                (hNW + 2 * hN + hNE);

            dx *= 0.125f;
            dz *= 0.125f;
        }
    }

    public enum MapShadingMode
    {
        None,
        Fast,
        Smooth
    }

    public struct BlockColorContext
    {
        public Color BaseColor;
        public Block Block;
        public int Contents;
        public int Value;
        public SubsystemTerrain SubsystemTerrain;
        public Terrain Terrain;
        public int X;
        public int Z;
    }
}
