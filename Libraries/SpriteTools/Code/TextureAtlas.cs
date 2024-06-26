using System;
using System.Collections.Generic;
using Sandbox;

namespace SpriteTools;

/// <summary>
/// A class that combines multiple textures into a single texture.
/// </summary>
public class TextureAtlas
{
    public int Size { get; private set; }

    Texture Texture;
    int MaxFrameSize;
    static Dictionary<string, Texture> Cache = new();

    /// <summary>
    /// Create a texture atlas from a list of texture paths. Returns null if there was an error and the texture cannot be loaded.
    /// </summary>
    /// <param name="texturePaths"></param>
    /// <returns></returns>
    public static TextureAtlas FromSprites(List<string> texturePaths)
    {
        var atlas = new TextureAtlas();
        atlas.Size = (int)Math.Ceiling(Math.Sqrt(texturePaths.Count));

        var key = string.Join(",", texturePaths);
        if (Cache.TryGetValue(key, out var cachedTexture))
        {
            atlas.Texture = cachedTexture;
            atlas.MaxFrameSize = cachedTexture.Width / atlas.Size;
            return atlas;
        }

        List<Texture> textures = new();
        atlas.MaxFrameSize = 0;
        foreach (var path in texturePaths)
        {
            if (!FileSystem.Mounted.FileExists(path))
            {
                Log.Error($"TextureAtlas: Texture file not found: {path}");
                continue;
            }
            var texture = Texture.Load(FileSystem.Mounted, path);
            textures.Add(texture);
            atlas.MaxFrameSize = Math.Max(atlas.MaxFrameSize, Math.Max(texture.Width, texture.Height));
        }
        atlas.MaxFrameSize += 2;

        int imageSize = atlas.Size * atlas.MaxFrameSize;
        int x = 0;
        int y = 0;
        byte[] textureData = new byte[imageSize * imageSize * 4];
        foreach (var texture in textures)
        {
            if (x + texture.Width > imageSize)
            {
                x = 0;
                y += atlas.MaxFrameSize;
            }
            if (y + texture.Height > imageSize)
            {
                Log.Error("TextureAtlas: Texture too large for atlas");
                continue;
            }

            var pixels = texture.GetPixels();

            for (int i = 0; i < texture.Width; i++)
            {
                for (int j = 0; j < texture.Height; j++)
                {
                    var index = (x + 1 + i + (y + 1 + j) * imageSize) * 4;
                    var textureIndex = i + j * texture.Width;
                    textureData[index] = pixels[textureIndex].r;
                    textureData[index + 1] = pixels[textureIndex].g;
                    textureData[index + 2] = pixels[textureIndex].b;
                    textureData[index + 3] = pixels[textureIndex].a;
                }
            }

            x += atlas.MaxFrameSize;
        }

        var builder = Texture.Create(imageSize, imageSize);
        builder.WithData(textureData);
        builder.WithMips(0);
        atlas.Texture = builder.Finish();

        Cache[key] = atlas.Texture;

        return atlas;
    }

    public static TextureAtlas FromSpritesheet(string path, List<Rect> spriteRects)
    {
        var atlas = new TextureAtlas();
        atlas.Size = (int)Math.Ceiling(Math.Sqrt(spriteRects.Count));

        var key = path + string.Join(",", spriteRects);
        if (Cache.TryGetValue(key, out var cachedTexture))
        {
            atlas.Texture = cachedTexture;
            atlas.MaxFrameSize = cachedTexture.Width / atlas.Size;
            return atlas;
        }

        if (!FileSystem.Mounted.FileExists(path))
        {
            Log.Error($"TextureAtlas: Texture file not found: {path}");
            return null;
        }

        foreach (var rect in spriteRects)
        {
            atlas.MaxFrameSize = (int)Math.Max(atlas.MaxFrameSize, Math.Max(rect.Width, rect.Height));
        }
        atlas.MaxFrameSize += 2;

        var spritesheet = Texture.Load(FileSystem.Mounted, path);
        var pixels = spritesheet.GetPixels();

        int imageSize = atlas.Size * atlas.MaxFrameSize;
        byte[] textureData = new byte[imageSize * imageSize * 4];
        foreach (var rect in spriteRects)
        {
            int x = 0;
            int y = 0;
            if (x + rect.Width > imageSize)
            {
                x = 0;
                y += atlas.MaxFrameSize;
            }
            if (y + rect.Height > imageSize)
            {
                Log.Error("TextureAtlas: Texture too large for atlas");
                continue;
            }

            for (int i = 0; i < rect.Width; i++)
            {
                for (int j = 0; j < rect.Height; j++)
                {
                    var index = (x + 1 + i + (y + 1 + j) * imageSize) * 4;
                    var textureIndex = (int)(rect.Left + i + (rect.Top + j) * spritesheet.Width);
                    textureData[index] = pixels[textureIndex].r;
                    textureData[index + 1] = pixels[textureIndex].g;
                    textureData[index + 2] = pixels[textureIndex].b;
                    textureData[index + 3] = pixels[textureIndex].a;
                }
            }

            x += atlas.MaxFrameSize;
        }

        var builder = Texture.Create(imageSize, imageSize);
        builder.WithData(textureData);
        builder.WithMips(0);
        atlas.Texture = builder.Finish();

        Cache[key] = atlas.Texture;

        return atlas;
    }

    public Vector2 GetFrameTiling()
    {
        // inset by 1 pixel to avoid bleeding
        return new Vector2(MaxFrameSize - 2, MaxFrameSize - 2) / ((float)MaxFrameSize * Size);
    }

    public Vector2 GetFrameOffset(int index)
    {
        int x = index * MaxFrameSize % (Size * MaxFrameSize);
        int y = index * MaxFrameSize / (Size * MaxFrameSize) * MaxFrameSize;
        x += 1;
        y += 1;
        return new Vector2(x, y) / (float)(Size * MaxFrameSize);
    }

    // Cast to texture
    public static implicit operator Texture(TextureAtlas atlas)
    {
        return atlas.Texture;
    }
}