using Engine;
using System.Collections.Generic;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public static class FurnitureMapRenderer
    {
        // Bộ nhớ đệm màu tổng của từng mẫu nội thất để tối ưu FPS cho minimap
        private static Dictionary<int, Color> m_furnitureMapColorCache = new Dictionary<int, Color>();

        public static Color GetFurnitureAverageMapColor(FurnitureDesign design, SubsystemTerrain terrain)
        {
            if (design == null || design.m_values == null)
            {
                return Color.Transparent;
            }

            // Kiểm tra xem thiết kế này đã được tính màu trước đó chưa
            if (m_furnitureMapColorCache.TryGetValue(design.Hash, out Color cachedColor))
            {
                return cachedColor;
            }

            float totalR = 0, totalG = 0, totalB = 0;
            int visiblePixelsCount = 0;
            int res = design.Resolution;

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    for (int y = res - 1; y >= 0; y--)
                    {
                        int value = design.m_values[x + y * res + z * res * res];

                        if (!FurnitureDesign.IsValueTransparent(value))
                        {
                            int contents = Terrain.ExtractContents(value);
                            Block block = BlocksManager.Blocks[contents];

                            // 1. Tính màu Tint (sơn/nhuộm)
                            Color tintColor = Color.White;
                            if (block is IPaintableBlock paintableBlock)
                            {
                                int? paintColor = paintableBlock.GetPaintColor(value);
                                tintColor = SubsystemPalette.GetColor(terrain, paintColor);
                            }
                            else if (block is WaterBlock)
                            {
                                tintColor = BlockColorsMap.Water.Lookup(12, 12);
                            }
                            else if (block is CarpetBlock)
                            {
                                int color2 = CarpetBlock.GetColor(Terrain.ExtractData(value));
                                tintColor = SubsystemPalette.GetFabricColor(terrain, color2);
                            }

                            // 2. Lấy Texture Slot mặt trên cùng (Face = 4)
                            int textureSlot = block.GetFaceTextureSlot(4, value);

                            // 3. Đã thay thế bằng hàm gọi từ bộ nhớ đệm helper
                            Color baseColor = MapColorHelper.GetBaseColorFromTextureSlot(textureSlot);

                            // 4. Trộn màu (Multiply)
                            Color finalColor = new Color(
                                (byte)(baseColor.R * tintColor.R / 255),
                                (byte)(baseColor.G * tintColor.G / 255),
                                (byte)(baseColor.B * tintColor.B / 255),
                                (byte)255
                            );

                            totalR += finalColor.R;
                            totalG += finalColor.G;
                            totalB += finalColor.B;
                            visiblePixelsCount++;

                            // Đã chạm voxel cao nhất, chuyển sang cột (x, z) tiếp theo
                            break;
                        }
                    }
                }
            }

            Color resultColor = Color.Transparent;
            if (visiblePixelsCount > 0)
            {
                resultColor = new Color(
                    (byte)(totalR / visiblePixelsCount),
                    (byte)(totalG / visiblePixelsCount),
                    (byte)(totalB / visiblePixelsCount),
                    (byte)255
                );
            }

            // Lưu kết quả vào bộ nhớ đệm để sử dụng cho lần render tiếp theo
            m_furnitureMapColorCache[design.Hash] = resultColor;

            return resultColor;
        }

        // Gọi hàm này nếu bạn cần xoá cache (vd: Khi người chơi thoát / vào lại world)
        public static void ClearCache()
        {
            m_furnitureMapColorCache.Clear();
        }
    }
}
