using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Unity.Mathematics;

namespace ProceduralMeshes {
    public class MeshTilingSchemeDetermine
    {
        public Camera mainCamera;
        public float zScale;
        public int pyramidLevels;
        List<int> texturePyramidLevels;
		int numberOfTextures;
        int maximumDescentLevel;

        int DEMwidth;
        int DEMheight;

        List<int> textureOrder;
        public string projectName;

        float minimumHeight;

        public MeshTilingSchemeDetermine(Camera mainCamera, float zScale, int pyramidLevels, string projectName, List<int> texturePyramidLevels, int numberOfTextures, int maximumDescentLevel, List<int> textureOrder, int DEMwidth, int DEMheight, float minimumHeight) {
            this.mainCamera = mainCamera;
            this.zScale = zScale;
            this.pyramidLevels = pyramidLevels;
            this.projectName = projectName;
            this.texturePyramidLevels = texturePyramidLevels;
            this.numberOfTextures = numberOfTextures;
            this.maximumDescentLevel = maximumDescentLevel;
            this.textureOrder = textureOrder;
            this.DEMwidth = DEMwidth;
            this.DEMheight = DEMheight;
            this.minimumHeight = minimumHeight;
            tileRanges = new List<int4>(pyramidLevels);
            for (int level = 0; level < pyramidLevels; level++) {
                tileRanges.Add(new int4((int)Math.Pow(2,pyramidLevels-level-1), (int)Math.Pow(2,pyramidLevels-level-1), 0, 0));
            }
        }

        public int deepestLevel=0;
        public int numberOfLevels=1;
        public int rejections = 0;
        public List<int4> tileRanges;
        public Texture2D finalTexture;

        public List<string> texturesToGenerate = new List<string>();

        public void Determine (int level, int2 tileCoords, List<int> textureIndices) {
            // Work in progress
            float levelScale = 1f / (1 << level);
            float4 physicalExtent = new float4(tileCoords.x*levelScale,tileCoords.y*levelScale,(tileCoords.x+1)*levelScale,(tileCoords.y+1)*levelScale);
            if (WeCareAboutThisTile(physicalExtent,0.3f))
            {
                if (level<pyramidLevels)
                {
                    tileRanges[pyramidLevels-level-1] = new int4(Math.Min(tileRanges[level].x,tileCoords.x),Math.Min(tileRanges[level].y,tileCoords.y),Math.Max(tileRanges[level].z,tileCoords.x+1),Math.Max(tileRanges[level].w,tileCoords.y+1));
                }
                bool descentNeeded = false;
                if (level<maximumDescentLevel)     
                {
                    Vector3 extentPixelOne = mainCamera.WorldToScreenPoint(new Vector3(physicalExtent.x,0,physicalExtent.y));
                    Vector3 extentPixelTwo = mainCamera.WorldToScreenPoint(new Vector3(physicalExtent.z,0,physicalExtent.y));
                    Vector3 extentPixelThree = mainCamera.WorldToScreenPoint(new Vector3(physicalExtent.z,0,physicalExtent.w));
                    Vector3 extentPixelFour = mainCamera.WorldToScreenPoint(new Vector3(physicalExtent.x,0,physicalExtent.w));
                    float4 distances = new float4(Vector3.Distance(extentPixelOne,extentPixelTwo),Vector3.Distance(extentPixelTwo,extentPixelThree),Vector3.Distance(extentPixelThree,extentPixelFour),Vector3.Distance(extentPixelFour,extentPixelOne)); 
                    float maxObservedTileSide = Math.Max(Math.Max(distances.x,distances.y),Math.Max(distances.z,distances.w));
                    descentNeeded = maxObservedTileSide > 256f;
                }
                if (descentNeeded)
                {
                    List<List<int>> textureIndicesSubtiles = new List<List<int>>(4);
                    for (var j=0; j<4; j++)
                    {
                        textureIndicesSubtiles.Add(new List<int>(textureIndices));
                    }
                    for (var i=0; i<textureIndices.Count; i++)
                    {
                        bool we_need_this_tile = !descentNeeded || texturePyramidLevels[textureIndices[i]]==level+1;
                        for (var j=0; j<4; j++)
                        {
                            string child_texture_tile = Path.Combine(Application.streamingAssetsPath, "Projects", projectName, (level+1).ToString(),$"{2*tileCoords.x+j/2}_{tileCoords.y+j%2}_{textureIndices[i]}.png");
                            if (!File.Exists(child_texture_tile))
                            {
                                we_need_this_tile = true;
                                textureIndicesSubtiles[j].Remove(textureIndices[i]);
                            }
                        }
                        if (we_need_this_tile)
                        {
                            string this_texture_tile = Path.Combine(Application.streamingAssetsPath, "Projects", projectName, level.ToString(),$"{tileCoords.x}_{tileCoords.y}_{textureIndices[i]}.png");
                            texturesToGenerate.Add(this_texture_tile);
                        }
                    }
                    for (var j=0; j<4; j++)
                    {
                        if (textureIndicesSubtiles[j].Count > 0)
                        {
                            Determine(level+1,new int2(2*tileCoords.x+j/2,2*tileCoords.y+j%2),textureIndicesSubtiles[j]);                         
                        }                        
                    }       
                }
                else
                {
                    for (var i=0; i<textureIndices.Count; i++)
                    {
                        string this_texture_tile = Path.Combine(Application.streamingAssetsPath, "Projects", projectName, level.ToString(),$"{tileCoords.x}_{tileCoords.y}_{textureIndices[i]}.png");
                        texturesToGenerate.Add(this_texture_tile);
                    }
                }
            }
        }

        public void DetermineAll(List<int> textureOrder, List<float> textureOpacities) {
            List<int> textureIndices = Enumerable.Range(0, numberOfTextures).ToList();
            Determine(0,new int2(0,0),textureIndices);
            FixTileRanges();
            var layers = BuildTextureLayers(texturesToGenerate, textureOrder, (int)Math.Pow(2, deepestLevel+8));
            finalTexture = CompositeLayers(layers, textureOpacities);
        }

        public void FixTileRanges()
        {
            deepestLevel=pyramidLevels-1;
            numberOfLevels=pyramidLevels;
            for (var i=0; i<pyramidLevels-1; i++)
            {
                if (tileRanges[i].x>=tileRanges[i].z || tileRanges[i].y>=tileRanges[i].w)
                {
                    deepestLevel--;
                    numberOfLevels--;
                }
                else
                {
                    if (tileRanges[i].x%2==1)
                    {
                        tileRanges[i]= new int4(tileRanges[i].x-1,tileRanges[i].y,tileRanges[i].z,tileRanges[i].w);
                    }
                    if (tileRanges[i].y%2==1)
                    {
                        tileRanges[i]= new int4(tileRanges[i].x,tileRanges[i].y-1,tileRanges[i].z,tileRanges[i].w);
                    }
                    if (tileRanges[i].z%2==1)
                    {
                        tileRanges[i]= new int4(tileRanges[i].x,tileRanges[i].y,tileRanges[i].z+1,tileRanges[i].w);
                    }
                    if (tileRanges[i].w%2==1)
                    {
                        tileRanges[i]= new int4(tileRanges[i].x,tileRanges[i].y,tileRanges[i].z,tileRanges[i].w+1);
                    }
                    int testLength = (int)Math.Ceiling(DEMwidth/Math.Pow(2,i+8));
                    int testHeight = (int)Math.Ceiling(DEMheight/Math.Pow(2,i+8));
                    /*if (tileRanges[i].z > testLength)
                    {
                         tileRanges[i] = new int4(tileRanges[i].x,tileRanges[i].y,testLength,tileRanges[i].w);
                    }
                    if (tileRanges[i].w > testHeight)
                    {
                        tileRanges[i] =  new int4(tileRanges[i].x,tileRanges[i].y,tileRanges[i].z,testHeight);
                    }
                    if (tileRanges[i].x == 0 && tileRanges[i].y == 0 && tileRanges[i].z == testLength && tileRanges[i].w == testHeight)
                    {
                        tileRanges[i] = new int4(0,0,testLength,testHeight);
                        numberOfLevels = deepestLevel-pyramidLevels+i+2;
                        break;
                    }
                    else
                    {*/
                        testLength = (int)Math.Ceiling(DEMwidth/Math.Pow(2,i+9));
                        testHeight = (int)Math.Ceiling(DEMheight/Math.Pow(2,i+9));
                        tileRanges[i+1] = new int4(Math.Max(0,Math.Min(tileRanges[i+1].x,tileRanges[i].x/2-1)),Math.Max(0,Math.Min(tileRanges[i+1].y,tileRanges[i].y/2-1)),Math.Min(testLength,Math.Max(tileRanges[i+1].z,tileRanges[i].z/2+1)),Math.Min(testHeight,Math.Max(tileRanges[i+1].w,tileRanges[i].w/2+1)));
                    //}
                }
            }
        }

        private sealed class TileInfo
        {
            public string Path;
            public int Level;
            public int X;
            public int Y;
        }

        private static readonly Regex TilePathRegex = new Regex(
            @"[/\\](?<level>\d+)[/\\](?<x>\d+)_(?<y>\d+)_(?<t>\d+)\.png$",
            RegexOptions.Compiled);

        public static List<Texture2D> BuildTextureLayers(
            List<string> paths,
            List<int> tPermutation,
            int outputSize)
        {
            var tilesByTexture = new Dictionary<int, List<TileInfo>>();

            foreach (string path in paths)
            {
                Match match = TilePathRegex.Match(path);
                if (!match.Success)
                    throw new FormatException($"Invalid asset path: {path}");

                int t = int.Parse(match.Groups["t"].Value);
                if (!tilesByTexture.TryGetValue(t, out var tiles))
                {
                    tiles = new List<TileInfo>();
                    tilesByTexture.Add(t, tiles);
                }

                tiles.Add(new TileInfo
                {
                    Path = path,
                    Level = int.Parse(match.Groups["level"].Value),
                    X = int.Parse(match.Groups["x"].Value),
                    Y = int.Parse(match.Groups["y"].Value)
                });
            }

            var result = new List<Texture2D>();

            // tPermutation gives the desired order of the output textures.
            foreach (int t in tPermutation)
            {
                if (!tilesByTexture.TryGetValue(t, out var tiles))
                    continue;

                // Draw coarse levels first, so finer levels are drawn on top.
                tiles = tiles.OrderBy(tile => tile.Level)
                            .ThenBy(tile => tile.X)
                            .ThenBy(tile => tile.Y)
                            .ToList();

                var pixels = new Color[outputSize * outputSize];

                foreach (TileInfo tileInfo in tiles)
                {
                    var tile = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(
                            tile, File.ReadAllBytes(tileInfo.Path), false))
                    {
                        UnityEngine.Object.Destroy(tile);
                        throw new InvalidDataException(
                            $"Could not load texture: {tileInfo.Path}");
                    }

                    tile.wrapMode = TextureWrapMode.Clamp;
                    tile.filterMode = FilterMode.Bilinear;

                    float tilesPerAxis = 1 << tileInfo.Level;
                    int x0 = Mathf.Clamp(
                        Mathf.FloorToInt(tileInfo.X / tilesPerAxis * outputSize),
                        0, outputSize);
                    int x1 = Mathf.Clamp(
                        Mathf.CeilToInt((tileInfo.X + 1) / tilesPerAxis * outputSize),
                        0, outputSize);
                    int y0 = Mathf.Clamp(
                        Mathf.FloorToInt(tileInfo.Y / tilesPerAxis * outputSize),
                        0, outputSize);
                    int y1 = Mathf.Clamp(
                        Mathf.CeilToInt((tileInfo.Y + 1) / tilesPerAxis * outputSize),
                        0, outputSize);

                    for (int py = y0; py < y1; py++)
                    for (int px = x0; px < x1; px++)
                    {
                        float u = (px + 0.5f) / outputSize * tilesPerAxis - tileInfo.X;
                        float v = (py + 0.5f) / outputSize * tilesPerAxis - tileInfo.Y;

                        int index = py * outputSize + px;
                        pixels[index] = Over(tile.GetPixelBilinear(u, v), pixels[index]);
                    }

                    UnityEngine.Object.Destroy(tile);
                }

                var output = new Texture2D(
                    outputSize, outputSize, TextureFormat.RGBA32, false);
                output.SetPixels(pixels);
                output.Apply();
                output.name = $"Texture_t{t}";
                result.Add(output);
            }

            return result;
        }

        public static Texture2D CompositeLayers(
            List<Texture2D> layers,
            List<float> opacities)
        {
            if (layers.Count != opacities.Count)
                throw new ArgumentException("Supply one opacity per texture layer.");

            if (layers.Count == 0)
                throw new ArgumentException("At least one texture layer is required.");

            int width = layers[0].width;
            int height = layers[0].height;
            var pixels = new Color[width * height];

            foreach (Texture2D layer in layers)
            {
                if (layer.width != width || layer.height != height)
                    throw new ArgumentException("All texture layers must have the same size.");
            }

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                Color[] sourcePixels = layers[layerIndex].GetPixels();
                float opacity = Mathf.Clamp01(opacities[layerIndex]);

                for (int i = 0; i < pixels.Length; i++)
                {
                    Color source = sourcePixels[i];
                    source.a *= opacity;
                    pixels[i] = Over(source, pixels[i]);
                }
            }

            var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            output.SetPixels(pixels);
            output.Apply();
            return output;
        }

        private static Color Over(Color source, Color destination)
        {
            float outAlpha = source.a + destination.a * (1f - source.a);
            if (outAlpha <= 0f)
                return Color.clear;

            Color result;
            result.a = outAlpha;
            result.r = (source.r * source.a +
                        destination.r * destination.a * (1f - source.a)) / outAlpha;
            result.g = (source.g * source.a +
                        destination.g * destination.a * (1f - source.a)) / outAlpha;
            result.b = (source.b * source.a +
                        destination.b * destination.a * (1f - source.a)) / outAlpha;
            return result;
        }

        /*public static List<string> SortAssetPaths(
            List<string> paths,
            List<int> tPermutation)
        {
            // Map each t value to its position in the desired ordering.
            var tOrder = tPermutation
                .Select((t, index) => new { t, index })
                .ToDictionary(item => item.t, item => item.index);

            // Extract level, x, y, and t from each path.
            var regex = new Regex(
                @"[/\\](?<level>\d+)[/\\](?<x>\d+)_(?<y>\d+)_(?<t>\d+)\.png$",
                RegexOptions.Compiled);

            var parsedPaths = paths.Select(path =>
            {
                var match = regex.Match(path);

                if (!match.Success)
                    throw new FormatException($"Invalid asset path: {path}");

                return new
                {
                    Path = path,
                    Level = int.Parse(match.Groups["level"].Value),
                    X = int.Parse(match.Groups["x"].Value),
                    Y = int.Parse(match.Groups["y"].Value),
                    T = int.Parse(match.Groups["t"].Value)
                };
            });

            return parsedPaths
                .OrderBy(item => tOrder.TryGetValue(item.T, out int order)
                    ? order
                    : int.MaxValue)
                .ThenBy(item => item.Level)
                .ThenBy(item => item.X)
                .ThenBy(item => item.Y)
                .Select(item => item.Path)
                .ToList();
        }    */

        public bool WeCareAboutThisPoint (Vector3 viewportPoint, float margin)
        {
            return viewportPoint.x >= -margin && viewportPoint.x <= 1f + margin &&
                   viewportPoint.y >= -margin && viewportPoint.y <= 1f + margin; 
        }

        public bool WeCareAboutThisTile (float4 physicalExtent, float margin)
        {
            Vector3 extentOne = mainCamera.WorldToViewportPoint(new Vector3(physicalExtent.x,minimumHeight,physicalExtent.y));
            Vector3 extentTwo = mainCamera.WorldToViewportPoint(new Vector3(physicalExtent.z,minimumHeight,physicalExtent.y));
            Vector3 extentThree = mainCamera.WorldToViewportPoint(new Vector3(physicalExtent.z,minimumHeight,physicalExtent.w));
            Vector3 extentFour = mainCamera.WorldToViewportPoint(new Vector3(physicalExtent.x,minimumHeight,physicalExtent.w));
            return WeCareAboutThisPoint(extentOne,margin) || WeCareAboutThisPoint(extentTwo,margin) || WeCareAboutThisPoint(extentThree,margin) || WeCareAboutThisPoint(extentFour,margin);
        }
    }
}