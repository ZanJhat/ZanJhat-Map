using Engine;
using Engine.Media;
using System;
using System.Collections.Generic;
using Game;
using ZanJhat.Core;

namespace ZanJhat.Map
{
    public static class MapColorHelper
    {
        private static Dictionary<int, Color> m_slotColors = new Dictionary<int, Color>();
        private static bool m_initialized = false;

        public static Color GetBaseColorFromTextureSlot(int slot)
        {
            if (!m_initialized)
            {
                Initialize();
            }
            return m_slotColors.TryGetValue(slot, out Color color) ? color : Color.White;
        }

        private static void Initialize()
        {
            try
            {
                // Đọc trực tiếp dữ liệu ảnh gốc (CPU) thay vì đọc từ GPU Texture2D
                Image image = ContentManager.Get<Image>("Textures/Blocks");

                int width = image.Width;
                int slotSize = width / 16; // Atlas block mặc định chia lưới 16x16

                // Quét qua toàn bộ 256 texture slot
                for (int s = 0; s < 256; s++)
                {
                    int startX = (s % 16) * slotSize;
                    int startY = (s / 16) * slotSize;

                    long r = 0, g = 0, b = 0;
                    int count = 0;

                    for (int y = 0; y < slotSize; y++)
                    {
                        for (int x = 0; x < slotSize; x++)
                        {
                            // Trích xuất pixel từ mảng 1D của Image
                            Color pixel = image.Pixels[(startY + y) * width + (startX + x)];

                            // Bỏ qua các pixel kính/rỗng để màu trung bình không bị nhạt đi
                            if (pixel.A > 128)
                            {
                                r += pixel.R;
                                g += pixel.G;
                                b += pixel.B;
                                count++;
                            }
                        }
                    }

                    if (count > 0)
                        m_slotColors[s] = new Color((byte)(r / count), (byte)(g / count), (byte)(b / count), (byte)255);
                    else
                        m_slotColors[s] = Color.Transparent;
                }
            }
            catch
            {
                // Dự phòng (fallback) nếu ảnh không tồn tại
            }

            m_initialized = true;
        }
    }
}
