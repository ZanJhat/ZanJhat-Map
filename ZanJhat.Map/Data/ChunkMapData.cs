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
    public class MapViewCache : IDisposable
    {
        public Point2 CenterChunk;
        public RenderTarget2D RenderTarget;
        public bool IsValid;
        public double LastUsedTime;
        public List<MapPixelPatch> PendingPatches = new List<MapPixelPatch>(SubsystemWorldMap.MaxShadingPixelsPerFrame);

        public void Dispose() => RenderTarget?.Dispose();
    }

    public struct MapPixelPatch
    {
        public int Px;
        public int Py;
        public Color Color;
    }

    public class ChunkMapData
    {
        private Color[] Colors = new Color[256];
        private Color[] ShadedColors = new Color[256];
        private byte[] Heights = new byte[256];

        public ChunkMapData()
        {
            for (int i = 0; i < 256; i++)
            {
                Colors[i] = Color.Transparent;
                ShadedColors[i] = Color.Transparent;
            }
        }

        // Dùng Dịch Bit (<< 4) và ( | ) để gộp tọa độ cực nhanh thay vì dùng phép nhân (+) (*)
        public void SetColor(int localX, int localZ, Color color) => Colors[localX | (localZ << 4)] = color;
        public Color GetColor(int localX, int localZ) => Colors[localX | (localZ << 4)];

        public void SetShadedColor(int localX, int localZ, Color color) => ShadedColors[localX | (localZ << 4)] = color;
        public Color GetShadedColor(int localX, int localZ) => ShadedColors[localX | (localZ << 4)];

        public void SetHeight(int localX, int localZ, int y) => Heights[localX | (localZ << 4)] = (byte)MathUtils.Clamp(y, 0, 255);
        public int GetHeight(int localX, int localZ) => Heights[localX | (localZ << 4)];
    }
}