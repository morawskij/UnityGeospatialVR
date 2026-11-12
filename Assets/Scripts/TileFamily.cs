using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralMeshes {
    public class TileFamily
    {
        int deepestLevel;
		int numberOfLevels;
        int pyramidLevels;
        List<int4> tileRanges;
        List<Mesh> tileMeshes;
        List<Texture2D> tileTextures;
        string projectName;
        ExternalElevationLoader elevationLoader;
        int numberOfTextures;
		List<int> textureOrder;
		List<float> textureOpacities;
        List<int> texturePyramidLevels;
		float midH;
		float hRange;
		float zScaleFactor;
        float verticalScaleFactor;
        public TileFamily(int deepestLevel, int numberOfLevels, List<int4> tileRanges, float midH, float hRange, float zScaleFactor, string projectName, ExternalElevationLoader elevationLoader, float verticalScaleFactor, int numberOfTextures, List<int> textureOrder, List<float> textureOpacities, int pyramidLevels, List<int> texturePyramidLevels)
        {
            this.deepestLevel = deepestLevel;
            this.numberOfLevels = numberOfLevels;
            this.tileRanges = tileRanges;
            this.midH = midH;
            this.hRange = hRange;
            this.zScaleFactor = zScaleFactor;
            this.verticalScaleFactor = verticalScaleFactor;
            this.projectName = projectName;
            this.elevationLoader = elevationLoader;
            this.tileMeshes = new List<Mesh>();
            this.tileTextures = new List<Texture2D>();
            this.numberOfTextures = numberOfTextures;
            this.textureOrder = textureOrder;
            this.textureOpacities = textureOpacities;
            this.pyramidLevels = pyramidLevels;
            this.texturePyramidLevels = texturePyramidLevels;
        }

        List<TileWork> GenerateTileWorkList()
        {
            var work = new List<TileWork>();
			int4 innerRange = default;
            for (int level = 0; level < numberOfLevels; level++)
            {
                int4 range = tileRanges[level];
                int demLevel = deepestLevel - level;
                for (int x = range.x; x < range.z; x++)
                {
                    for (int z = range.y; z < range.w; z++)
                    {
                        if (level > 0){
                            if (IsInside(2*x, 2*z, innerRange))
                            {
                                continue;
                            }   
                        }
                        int4 holes = new int4(GetFromBool(InnerIsInside(2*x, 2*z, innerRange)), GetFromBool(InnerIsInside(2*x+1, 2*z, innerRange)), GetFromBool(InnerIsInside(2*x, 2*z+1, innerRange)), GetFromBool(InnerIsInside(2*x+1, 2*z+1, innerRange)));
                        int levelExcess = 0;
                        NativeArray<float> elevation = default;
                        if (demLevel >= pyramidLevels)
                        {
                            levelExcess = demLevel-pyramidLevels+1;
                            int referencePower = (int)Math.Pow(2,levelExcess);
                            elevation = elevationLoader.LoadTile(projectName, pyramidLevels-1, (int)Math.Floor((float)x/(float)referencePower), (int)Math.Floor((float)z/(float)referencePower), midH);
                        }
                        else
                        {
                            elevation = elevationLoader.LoadTile(projectName, demLevel, x, z, midH);
                        }
                        work.Add(new TileWork
                        {
                            level = demLevel,
                            levelExcess = levelExcess,
                            x = x,
                            z = z,
                            holes = holes,
                            elevation = elevation
                        });
                    }
                }
                innerRange = range;
            }
            return work;
        }

        public void GenerateTileMeshes()
        {
            if (elevationLoader == null)
            {
                throw new InvalidOperationException("An ExternalElevationLoader is required.");
            }
            var work = new List<TileWork>();
            int4 innerRange = default;
            try{
                for (int level = 0; level < numberOfLevels; level++)
                {
                    int4 range = tileRanges[level];
                    int demLevel = deepestLevel - level;
                    for (int x = range.x; x < range.z; x++)
                    {
                        for (int z = range.y; z < range.w; z++)
                        {
                            if (level > 0){
                                if (IsInside(2*x, 2*z, innerRange))
                                {
                                    continue;
                                }   
                            }
                            int4 holes = new int4(GetFromBool(InnerIsInside(2*x, 2*z, innerRange)), GetFromBool(InnerIsInside(2*x+1, 2*z, innerRange)), GetFromBool(InnerIsInside(2*x, 2*z+1, innerRange)), GetFromBool(InnerIsInside(2*x+1, 2*z+1, innerRange)));
                            int levelExcess = 0;
                            NativeArray<float> elevation = default;
                            if (demLevel >= pyramidLevels)
                            {
                                levelExcess = demLevel-pyramidLevels+1;
                                int referencePower = (int)Math.Pow(2,levelExcess);
                                elevation = elevationLoader.LoadTile(projectName, pyramidLevels-1, (int)Math.Floor((float)x/(float)referencePower), (int)Math.Floor((float)z/(float)referencePower), midH);
                            }
                            else
                            {
                                elevation = elevationLoader.LoadTile(projectName, demLevel, x, z, midH);
                            }
                            work.Add(new TileWork
                            {
                                level = demLevel,
                                levelExcess = levelExcess,
                                x = x,
                                z = z,
                                holes = holes,
                                elevation = elevation
                            });
                        }
                    }
                    innerRange = range;
                }
                if (work.Count == 0)
                {
                    return;
                }

                GenerateMeshes(work);
            }
            finally
            {
                foreach (TileWork tile in work)
                {
                    if (tile.elevation.IsCreated)
                    {
                        tile.elevation.Dispose();
                    }
                }
            }
        }

        public void updateTextures(List<int> newTextureOrder, List<float> newTextureOpacities)
        {
            textureOrder=newTextureOrder;
            textureOpacities=newTextureOpacities;
            List<TileWork> work = GenerateTileWorkList();
            for (int i = 0; i < work.Count; i++)
            {
                Debug.Log(i);
                TileWork tile = work[i];
                tileTextures[i] = LoadTileTexture(tile);
            }
        }

        int GetFromBool(bool b) => b ? 1 : 0;
        void GenerateMeshes(List<TileWork> work)
        {
            var meshes = new Mesh[work.Count];
            var textures = new Texture2D[work.Count];
            Mesh.MeshDataArray meshDataArray = default;
            bool meshDataApplied = false;
            bool resultsTransferred = false;
            JobHandle combinedHandle = default;
            try
            {
                meshDataArray = Mesh.AllocateWritableMeshData(work.Count);
                for (int i = 0; i < work.Count; i++)
                {
                    TileWork tile = work[i];
                    meshes[i] = new Mesh { name = $"Terrain Tile {tile.level}/{tile.x}/{tile.z}" };
                    textures[i] = LoadTileTexture(tile);
                    combinedHandle = JobHandle.CombineDependencies(
                        combinedHandle,
                        MeshJobTiled<TerrainTile, SingleStream>.ScheduleParallel(
                            meshes[i], meshDataArray[i], tile.elevation, tile.level, tile.levelExcess, tile.x, tile.z,
                            midH, hRange, zScaleFactor, verticalScaleFactor, tile.holes,
                            default
                        )
                    );
                }

                combinedHandle.Complete();
                Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, meshes);
                meshDataApplied = true;
                tileMeshes.AddRange(meshes);
                tileTextures.AddRange(textures);
                resultsTransferred = true;
            }
            finally
            {
                combinedHandle.Complete();
                if (!meshDataApplied)
                {
                    meshDataArray.Dispose();
                }
                if (!resultsTransferred)
                {
                    foreach (Mesh mesh in meshes)
                    {
                        if (mesh != null)
                        {
                            UnityEngine.Object.Destroy(mesh);
                        }
                    }
                    foreach (Texture2D texture in textures)
                    {
                        if (texture != null)
                        {
                            UnityEngine.Object.Destroy(texture);
                        }
                    }
                }
            }
        }

        public IReadOnlyList<Mesh> TileMeshes => tileMeshes;
        public IReadOnlyList<Texture2D> TileTextures => tileTextures;

        public void ReleaseGeneratedAssets () {
            foreach (Mesh mesh in tileMeshes) {
                if (mesh != null) {
                    UnityEngine.Object.Destroy(mesh);
                }
            }
            tileMeshes.Clear();

            foreach (Texture2D texture in tileTextures) {
                if (texture != null) {
                    UnityEngine.Object.Destroy(texture);
                }
            }
            tileTextures.Clear();
        }

        Texture2D LoadTileTexture(TileWork tile)
        {
            return LoadTileTexture(tile.x,tile.z,tile.level);
        }

        Texture2D LoadTileTexture(int tileX, int tileZ, int level)
        {
            var ultimateTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var pixels = new Color[65536];
            Array.Fill(pixels, Color.clear);
            for (var i=0; i<numberOfTextures; i++)
            {

                int3 textureTileNumbers = LevelExcessTextureInfo(level, texturePyramidLevels[textureOrder[i]]-1, tileX, tileZ);
                string path = Path.Combine(
                    "Projects",
                    projectName,
                    textureTileNumbers.z.ToString(),
                    $"{textureTileNumbers.x}_{textureTileNumbers.y}_{textureOrder[i]}.png"
                );
                var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                var newPixels = new Color[65536];
                try
                {
                    ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false);
                    if (level == textureTileNumbers.z)
                    {
                        newPixels = texture.GetPixels();                           
                    }
                    else
                    {
                        int4 coordinateRange = TextureLoaderCoordinateRange(tileX, tileZ, level-textureTileNumbers.z);
                        for (var j=0; j<256; j++)
                        {
                            for (var k=0; k<256; k++)
                            {
                                int sourceX = (int)(coordinateRange.x + (float)k/(float)256*(coordinateRange.z-coordinateRange.x));
                                int sourceY = (int)(coordinateRange.y + (float)j/(float)256*(coordinateRange.w-coordinateRange.y));
                                newPixels[j*256+k] = texture.GetPixel(sourceX, sourceY);
                            }
                        }
                    }              
                }
                catch (Exception)
                {
                    
                    Array.Fill(newPixels, Color.clear); 
                }
                for (var j=0; j<pixels.Length; j++)
                {
                    Color source = newPixels[j];
                    source.a *= textureOpacities[textureOrder[i]];
                    pixels[j] = Over(source, pixels[j]);
                }
                UnityEngine.Object.Destroy(texture);   
            }
            ultimateTexture.SetPixels(pixels);
            ultimateTexture.Apply();
            ultimateTexture.wrapMode = TextureWrapMode.Clamp;
            ultimateTexture.filterMode = FilterMode.Bilinear;
            return ultimateTexture;
        }

        struct TileWork
        {
            public int level;
            public int levelExcess;
            public int x;
            public int z;
            public int4 holes;
            public NativeArray<float> elevation;
        }

		static bool IsInside (int tileX, int tileZ, int4 innerRange) =>
            tileX >= innerRange.x && tileX+1 < innerRange.z &&
            tileZ >= innerRange.y && tileZ+1 < innerRange.w;

		static bool InnerIsInside (int tileX, int tileZ, int4 innerRange) =>
            tileX >= innerRange.x && tileX < innerRange.z &&
            tileZ >= innerRange.y && tileZ < innerRange.w;

        static int3 LevelExcessTextureInfo(int level, int maximumLevel, int tileX, int tileZ)
        {
            return new int3(TileNumberConverter(level-maximumLevel, tileX), TileNumberConverter(level-maximumLevel, tileZ),(int)Math.Min(level,maximumLevel));
        }

        static int TileNumberConverter(int levelExcess, int tileNumber)
        {
            return tileNumber / (int)Math.Pow(2, (int)Math.Max(0,levelExcess));
        }

        static int2 TileInnerCoordinates(int levelExcess, int tileNumber)
        {
            int referencePower = (int)Math.Pow(2,levelExcess);
            int baseCoordinate = (int)((tileNumber - TileNumberConverter(levelExcess, tileNumber)*referencePower)*Math.Pow(2,8-levelExcess));
            return new int2(baseCoordinate,baseCoordinate+(int)Math.Pow(2,8-levelExcess));
        }

        static int4 TextureLoaderCoordinateRange(int tileX, int tileZ, int levelExcess)
        {
            int referencePower = (int)Math.Pow(2,levelExcess);
            int2 xRange = TileInnerCoordinates(levelExcess, tileX);
            int2 zRange = TileInnerCoordinates(levelExcess, tileZ);
            return new int4(xRange.x, zRange.x, xRange.y, zRange.y);
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
    }
}