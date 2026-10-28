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

        List<int> textureOrder;
        public string projectName;

        public MeshTilingSchemeDetermine(Camera mainCamera, float zScale, int pyramidLevels, string projectName, List<int> texturePyramidLevels, int numberOfTextures, int maximumDescentLevel, List<int> textureOrder) {
            this.mainCamera = mainCamera;
            this.zScale = zScale;
            this.pyramidLevels = pyramidLevels;
            this.projectName = projectName;
            this.texturePyramidLevels = texturePyramidLevels;
            this.numberOfTextures = numberOfTextures;
            this.maximumDescentLevel = maximumDescentLevel;
            this.textureOrder = textureOrder;
            tileRanges = new List<int4>(pyramidLevels);
            for (int level = 0; level < pyramidLevels; level++) {
                tileRanges.Add(new int4((int)Math.Pow(2,pyramidLevels-level-1), (int)Math.Pow(2,pyramidLevels-level-1), 0, 0));
            }
        }

        public int deepestLevel=0;
        public int numberOfLevels=1;
        public List<int4> tileRanges;

        public List<string> texturesToGenerate = new List<string>();

        public void Determine (int level, int2 tileCoords, List<int> textureIndices) {
            // Work in progress
            if (level<pyramidLevels)
            {
                tileRanges[pyramidLevels-level] = new int4(Math.Min(tileRanges[level].x,tileCoords.x),Math.Min(tileRanges[level].y,tileCoords.y),Math.Min(tileRanges[level].z,tileCoords.x+1),Math.Min(tileRanges[level].w,tileCoords.y+1));
            }
            //string levelPath = Path.Combine(Application.streamingAssetsPath, "Projects", projectName, level.ToString());
            //string texturePattern = $"{tileCoords.x}_{tileCoords.y}*.png";
            //List<string> newTextures = new List<string>(Directory.GetFiles(levelPath, texturePattern));
            bool descentNeeded = false;
            if (level<maximumDescentLevel)     
            {
                float levelScale = 1f / (1 << level);
                float4 physicalExtent = new float4(tileCoords.x*levelScale,tileCoords.y*levelScale,(tileCoords.x+1)*levelScale,(tileCoords.y+1)*levelScale);
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
                    textureIndicesSubtiles.Insert(j,new List<int>(textureIndices));
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

        public void DetermineAll() {
            Debug.Log(projectName);
            List<int> textureIndices =Enumerable.Range(0, numberOfTextures).ToList();
            Determine(0,new int2(0,0),textureIndices);
            Debug.Log(tileRanges);
            FixTileRanges();
            texturesToGenerate=SortAssetPaths(texturesToGenerate,textureOrder);
        }

        public void FixTileRanges()
        {
            deepestLevel=pyramidLevels;
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
                        tileRanges[i]=new int4(tileRanges[i].x-1,tileRanges[i].y,tileRanges[i].z,tileRanges[i].w);
                    }
                    if (tileRanges[i].y%2==1)
                    {
                        tileRanges[i]=new int4(tileRanges[i].x,tileRanges[i].y-1,tileRanges[i].z,tileRanges[i].w);
                    }
                    if (tileRanges[i].z%2==1)
                    {
                        tileRanges[i]=new int4(tileRanges[i].x,tileRanges[i].y,tileRanges[i].z+1,tileRanges[i].w);
                    }
                    if (tileRanges[i].w%2==1)
                    {
                        tileRanges[i]=new int4(tileRanges[i].x,tileRanges[i].y,tileRanges[i].z,tileRanges[i].w+1);
                    }
                    int testLength = (int)Math.Pow(2,pyramidLevels-i-1);
                    if (tileRanges[i].x == 0 && tileRanges[i].y == 0 && tileRanges[i].z == testLength && tileRanges[i].w == testLength)
                    {
                        numberOfLevels = deepestLevel-pyramidLevels+i;
                        break;
                    }
                    else
                    {
                        testLength/=2;
                        tileRanges[i+1] = new int4(Math.Max(0,Math.Min(tileRanges[i+1].x,tileRanges[i].x/2-1)),Math.Max(0,Math.Min(tileRanges[i+1].y,tileRanges[i].y/2-1)),Math.Min(testLength,Math.Max(tileRanges[i+1].z,tileRanges[i].z/2+1)),Math.Min(testLength,Math.Max(tileRanges[i+1].w,tileRanges[i].w/2+1)));
                    }
                }
            }
        }
        public static List<string> SortAssetPaths(
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
        }    
    }
}