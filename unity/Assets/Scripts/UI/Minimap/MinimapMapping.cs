using System;
using UnityEngine;

namespace Echelon.Unity.UI.Minimap
{
    public static class MinimapMapping
    {
        public const int SizePixels = 210;

        public static Vector2 MapToPixels(Vector2 position, Vector2 mapSize)
        {
            ValidateMapSize(mapSize);
            return new Vector2(
                position.x / mapSize.x * SizePixels,
                (1f - position.y / mapSize.y) * SizePixels);
        }

        public static Vector2 PixelsToMap(Vector2 pixelPosition, Vector2 mapSize)
        {
            ValidateMapSize(mapSize);
            return new Vector2(
                pixelPosition.x / SizePixels * mapSize.x,
                (1f - pixelPosition.y / SizePixels) * mapSize.y);
        }

        public static Vector2 MapToLocal(Vector2 position, Vector2 mapSize, Rect localRect)
        {
            ValidateMapSize(mapSize);
            return new Vector2(
                localRect.xMin + position.x / mapSize.x * localRect.width,
                localRect.yMin + position.y / mapSize.y * localRect.height);
        }

        public static Vector2 LocalToMap(Vector2 localPosition, Vector2 mapSize, Rect localRect, bool clampToMap = true)
        {
            ValidateMapSize(mapSize);
            if (localRect.width <= 0f || localRect.height <= 0f)
                throw new ArgumentException("The minimap rectangle must have a positive size.", nameof(localRect));
            float x = (localPosition.x - localRect.xMin) / localRect.width;
            float y = (localPosition.y - localRect.yMin) / localRect.height;
            if (clampToMap)
            {
                x = Mathf.Clamp01(x);
                y = Mathf.Clamp01(y);
            }
            return new Vector2(x * mapSize.x, y * mapSize.y);
        }

        public static Vector2 TerrainSamplePosition(int x, int y, Vector2 mapSize)
        {
            ValidateMapSize(mapSize);
            if (x < 0 || x >= SizePixels)
                throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y >= SizePixels)
                throw new ArgumentOutOfRangeException(nameof(y));
            return new Vector2(
                (x + 0.5f) / SizePixels * mapSize.x,
                (1f - (y + 0.5f) / SizePixels) * mapSize.y);
        }

        public static Rect CameraRectangle(
            MinimapCameraSnapshot camera,
            Vector2 mapSize,
            Rect localRect)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            ValidateMapSize(mapSize);
            Vector2 center = MapToLocal(camera.Focus, mapSize, localRect);
            float half = camera.Distance * 0.6f / mapSize.x * localRect.width;
            return new Rect(center - new Vector2(half, half), new Vector2(half, half) * 2f);
        }

        private static void ValidateMapSize(Vector2 mapSize)
        {
            if (mapSize.x <= 0f || mapSize.y <= 0f)
                throw new ArgumentException("Map dimensions must be positive.", nameof(mapSize));
        }
    }
}
