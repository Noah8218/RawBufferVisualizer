using System;
using System.Collections.Generic;

namespace RawBufferVisualizer.Core
{
    public sealed class RawImageTile
    {
        public int X { get; private set; }
        public int Y { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        public RawImageTile(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    public static class RawImageTilePlanner
    {
        public const int DefaultTileSize = 5000;

        public static IReadOnlyList<RawImageTile> CreateTiles(int width, int height)
        {
            return CreateTiles(width, height, DefaultTileSize);
        }

        public static IReadOnlyList<RawImageTile> CreateTiles(int width, int height, int tileSize)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException("width", "Width must be greater than zero.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException("height", "Height must be greater than zero.");
            }

            if (tileSize <= 0)
            {
                throw new ArgumentOutOfRangeException("tileSize", "Tile size must be greater than zero.");
            }

            var tiles = new List<RawImageTile>();
            for (var y = 0; y < height; y += tileSize)
            {
                var tileHeight = Math.Min(tileSize, height - y);
                for (var x = 0; x < width; x += tileSize)
                {
                    var tileWidth = Math.Min(tileSize, width - x);
                    tiles.Add(new RawImageTile(x, y, tileWidth, tileHeight));
                }
            }

            return tiles;
        }

        public static long EstimateBgraByteCount(RawImageDescriptor descriptor)
        {
            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }

            if (descriptor.Width <= 0 || descriptor.Height <= 0)
            {
                return 0;
            }

            return checked((long)descriptor.Width * descriptor.Height * 4);
        }
    }
}
