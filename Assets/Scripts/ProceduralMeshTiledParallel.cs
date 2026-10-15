using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ProceduralMeshes {

	public sealed class ProceduralMeshTiledParallel : MonoBehaviour {

		public int deepestLevel;
		public int numberOfLevels;
		public int4[] tileRanges;

		public string projectName = "Antarctica";

		ElevationMetadata elevationInfo;
		float verticalScaleFactor;
		float zScaleFactor;
		float midH;
		float hRange;
        int pyramidLevels;

		void Awake () {
			elevationInfo = new ElevationMetadata();
			elevationInfo.LoadMetadata($"Assets/StreamingAssets/Projects/{projectName}/dem_metadata.json");
			verticalScaleFactor = elevationInfo.VE / elevationInfo.dem_resolution_x;
			zScaleFactor = elevationInfo.dem_resolution_y / elevationInfo.dem_resolution_x;
			midH = elevationInfo.mid_h;
			hRange = elevationInfo.h_range;
            pyramidLevels = elevationInfo.pyramid_levels;
		}

		public int VertexCount => CalculateCounts().vertexCount;
		public int IndexCount => CalculateCounts().triangleCount * 3;

		public NativeArray<TileRowWork> BuildVertexRowWork (Allocator allocator, out int expectedSampleCount) {
			ValidateInputs();

			var work = new List<TileRowWork>();
			int vertexStart = 0;
			int sampleStart = 0;
			int4 innerRange = InitialInnerRange();

			for (int level = 0; level < numberOfLevels; level++) {
				int4 range = tileRanges[level];
				for (int tileZ = range.y; tileZ < range.w; tileZ++) {
					for (int tileX = range.x; tileX < range.z; tileX++) {
						if (IsInside(tileX, tileZ, innerRange)) {
							continue;
						}

						for (int row = 0; row < StreamingAssetsElevationLoader.SamplesPerSide; row++) {
							work.Add(new TileRowWork {
								level = level,
								tileX = tileX,
								tileZ = tileZ,
								row = row,
								vertexStart = vertexStart,
								sampleStart = sampleStart
							});
						}

						vertexStart += StreamingAssetsElevationLoader.SampleCount;
						sampleStart += StreamingAssetsElevationLoader.SampleCount;
					}
				}

				innerRange = range / 2;
			}

			expectedSampleCount = sampleStart;
			return new NativeArray<TileRowWork>(work.ToArray(), allocator);
		}

		public JobHandle ScheduleVertexRows<S> (
			NativeArray<float> tileSamples,
			NativeArray<TileRowWork> rowWork,
			S streams,
			JobHandle dependency = default
		) where S : struct, IMeshStreams {
			if (!tileSamples.IsCreated || !rowWork.IsCreated) {
				throw new ArgumentException("Tile samples and row work must be created before scheduling.");
			}

			var job = new VertexRowsJob<S> {
				tileSamples = tileSamples,
				rowWork = rowWork,
				streams = streams,
				VerticalScaleFactor = verticalScaleFactor,
				ZScaleFactor = zScaleFactor,
				midH = midH
			};
			return job.ScheduleParallel(rowWork.Length, 1, dependency);
		}

		public Bounds Bounds => new Bounds(
			Vector3.zero,
			new Vector3(1f, hRange * verticalScaleFactor, zScaleFactor)
		);

		static bool IsInside (int tileX, int tileZ, int4 innerRange) =>
			tileZ >= innerRange.y && tileZ < innerRange.w &&
			tileX >= innerRange.x && tileX < innerRange.z;

		int4 InitialInnerRange () => new int4(
			tileRanges[0].z, tileRanges[0].w,
			tileRanges[0].z, tileRanges[0].w
		);

		void ValidateInputs () {
			if (numberOfLevels <= 0 || tileRanges == null || tileRanges.Length < numberOfLevels) {
				throw new InvalidOperationException("Provide at least one tile range for each LoD level.");
			}
			if (deepestLevel < numberOfLevels-1 || deepestLevel >= pyramidLevels) {
				throw new InvalidOperationException("deepestLevel is out of bounds.");
			}
		}

		(int vertexCount, int triangleCount) CalculateCounts () {
			ValidateInputs();

			int vertexCount = 0;
			int triangleCount = 0;
			int4 innerRange = InitialInnerRange();

			for (int level = 0; level < numberOfLevels; level++) {
				int4 range = tileRanges[level];
				int tileSpanX = range.z - range.x;
				int tileSpanY = range.w - range.y;
				int innerSpanX = innerRange.z - innerRange.x;
				int innerSpanY = innerRange.w - innerRange.y;
				int tileCount = tileSpanX * tileSpanY;
				int innerTileCount = innerSpanX * innerSpanY;

				vertexCount += StreamingAssetsElevationLoader.SampleCount * (tileCount - innerTileCount);
				triangleCount +=
					512 * tileSpanX * (256 * (tileSpanY - innerSpanY) - 2) +
					1024 * tileSpanX +
					512 * innerSpanX +
					(256 * innerSpanY - 1) * (2 + 512 * (tileSpanX - innerSpanX));

				innerRange = range / 2;
			}

			return (vertexCount, triangleCount);
		}
	}

	public struct TileRowWork {
		public int level;
		public int tileX;
		public int tileZ;
		public int row;
		public int vertexStart;
		public int sampleStart;
	}

	public struct VertexRowsJob<S> : IJobFor where S : struct, IMeshStreams {
		[ReadOnly] public NativeArray<float> tileSamples;
		[ReadOnly] public NativeArray<TileRowWork> rowWork;
		public S streams;
		public float VerticalScaleFactor;
		public float ZScaleFactor;
		public float midH;

		public void Execute (int index) {
			TileRowWork work = rowWork[index];
			float levelScale = 1f / (1 << work.level);
			float coordinateStartX = work.tileX * levelScale;
			float coordinateStartZ = work.tileZ * levelScale;
			int vertexIndex = work.vertexStart + work.row * StreamingAssetsElevationLoader.SamplesPerSide;
			int sampleIndex = work.sampleStart + work.row * StreamingAssetsElevationLoader.SamplesPerSide;

			for (int x = 0; x < StreamingAssetsElevationLoader.SamplesPerSide; x++) {
				float fractionalZ = coordinateStartZ + levelScale * work.row / 255f;
				var vertex = new Vertex();
				vertex.position.x = coordinateStartX + levelScale * x / 255f;
				vertex.position.y = (tileSamples[sampleIndex + x] - midH) * VerticalScaleFactor;
				vertex.position.z = fractionalZ * ZScaleFactor;
				vertex.normal.y = 1f;
				vertex.texCoord0 = new float2(vertex.position.x, fractionalZ);
				vertex.tangent.xw = new float2(1f, -1f);
				streams.SetVertex(vertexIndex + x, vertex);
			}
		}
	}
}