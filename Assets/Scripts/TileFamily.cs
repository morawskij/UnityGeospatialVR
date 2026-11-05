using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ProceduralMeshes;
using ProceduralMeshes.Streams;

namespace ProceduralMeshes {
    public class TileFamily
    {
        int deepestLevel;
		int numberOfLevels;
        List<int4> tileRanges;
        List<Mesh> tileMeshes;
        List<Texture2D> tileTextures;
        string projectName;
        ExternalElevationLoader elevationLoader;
		float midH;
		float hRange;
		float zScaleFactor;
        float verticalScaleFactor;
        public TileFamily(int deepestLevel, int numberOfLevels, List<int4> tileRanges, float midH, float hRange, float zScaleFactor, string projectName, ExternalElevationLoader elevationLoader, float verticalScaleFactor)
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
        }

        public void GenerateTileMeshes()
        {
            if (elevationLoader == null)
            {
                throw new InvalidOperationException("An ExternalElevationLoader is required.");
            }

            var work = new List<TileWork>();
			int4 innerRange = InitialInnerRange();
            try
            {
                for (int level = 0; level < numberOfLevels; level++)
                {
                    int4 range = tileRanges[level];
                    int demLevel = deepestLevel - level;
                    for (int x = range.x; x < range.z; x++)
                    {
                        for (int z = range.y; z < range.w; z++)
                        {
                            if (IsInside(x, z, innerRange))
                            {
                                continue;
                            }

                            work.Add(new TileWork
                            {
                                level = demLevel,
                                x = x,
                                z = z,
                                elevation = elevationLoader.LoadTile(projectName, demLevel, x, z, midH)
                            });
                        }
                    }
                    innerRange = range / 2;
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
                            meshes[i], meshDataArray[i], tile.elevation, tile.level, tile.x, tile.z,
                            midH, hRange, zScaleFactor, verticalScaleFactor,
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

        Texture2D LoadTileTexture(TileWork tile)
        {
            string path = Path.Combine(
                Application.streamingAssetsPath,
                "Projects",
                projectName,
                tile.level.ToString(),
                $"{tile.x}_{tile.z}_0.png"
            );
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false))
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidDataException($"Could not load tile texture: {path}");
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        struct TileWork
        {
            public int level;
            public int x;
            public int z;
            public NativeArray<float> elevation;
        }

		int4 InitialInnerRange () => new int4(
			tileRanges[0].z, tileRanges[0].w,
			tileRanges[0].z, tileRanges[0].w
		);

		static bool IsInside (int tileX, int tileZ, int4 innerRange) =>
            tileX >= innerRange.x && tileX < innerRange.z &&
            tileZ >= innerRange.y && tileZ < innerRange.w;

    }
}