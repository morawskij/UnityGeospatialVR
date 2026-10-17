using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ProceduralMeshes.Streams;

namespace ProceduralMeshes {

	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(StreamingAssetsElevationLoader))]
	public sealed class ProceduralMeshTiledParallel : MonoBehaviour {

		public int deepestLevel;
		public int numberOfLevels;
		public int4[] tileRanges;

		public string projectName = "Antarctica";
		[SerializeField] StreamingAssetsElevationLoader elevationLoader;

		ElevationMetadata elevationInfo;
		float verticalScaleFactor;
		float zScaleFactor;
		float midH;
		float hRange;
        int pyramidLevels;
		Mesh mesh;
		bool regenerationRequested;
		bool generationInProgress;

		void Awake () {
			elevationInfo = new ElevationMetadata();
			mesh = new Mesh { name = "Procedural Tiled Mesh" };
			GetComponent<MeshFilter>().mesh = mesh;
		}

		void Start () => regenerationRequested = true;

		void OnValidate () => regenerationRequested = true;

		void Update () {
			if (!regenerationRequested || generationInProgress) {
				return;
			}

			regenerationRequested = false;
			try {
				LoadProjectMetadata();
				ValidateInputs();
			}
			catch (Exception exception) {
				Debug.LogException(exception, this);
				return;
			}

			generationInProgress = true;
			StartCoroutine(GenerateMeshWhenTilesAreLoaded());
		}

		void LoadProjectMetadata () {
			elevationInfo.LoadMetadata($"Assets/StreamingAssets/Projects/{projectName}/dem_metadata.json");
			verticalScaleFactor = elevationInfo.VE / elevationInfo.dem_resolution_x;
			zScaleFactor = elevationInfo.dem_resolution_y / elevationInfo.dem_resolution_x;
			midH = elevationInfo.mid_h;
			hRange = elevationInfo.h_range;
			pyramidLevels = elevationInfo.pyramid_levels;
		}

		void OnDestroy () {
			if (mesh != null) {
				Destroy(mesh);
			}
		}

		public int VertexCount => CalculateCounts().vertexCount;
		public int IndexCount => CalculateCounts().triangleCount * 3;

		public TileLoadWork[] BuildTileLoadWork () {
			ValidateInputs();

			var work = new List<TileLoadWork>();
			int vertexStart = 0;
			int sampleStart = 0;
			int4 innerRange = InitialInnerRange();

			for (int rangeIndex = 0; rangeIndex < numberOfLevels; rangeIndex++) {
				int4 range = tileRanges[rangeIndex];
				int demLevel = deepestLevel - rangeIndex;
				for (int tileZ = range.y; tileZ < range.w; tileZ++) {
					for (int tileX = range.x; tileX < range.z; tileX++) {
						if (IsInside(tileX, tileZ, innerRange)) {
							continue;
						}

						work.Add(new TileLoadWork {
							level = demLevel,
							tileX = tileX,
							tileZ = tileZ,
							vertexStart = vertexStart,
							sampleStart = sampleStart
						});
						vertexStart += StreamingAssetsElevationLoader.SampleCount;
						sampleStart += StreamingAssetsElevationLoader.SampleCount;
					}
				}

				innerRange = range / 2;
			}

			return work.ToArray();
		}

		public NativeArray<TileRowWork> BuildVertexRowWork (Allocator allocator, out int expectedSampleCount) {
			TileLoadWork[] tileWork = BuildTileLoadWork();
			var work = new List<TileRowWork>(tileWork.Length * StreamingAssetsElevationLoader.SamplesPerSide);
			foreach (TileLoadWork tile in tileWork) {
				for (int row = 0; row < StreamingAssetsElevationLoader.SamplesPerSide; row++) {
					work.Add(new TileRowWork {
						level = tile.level,
						tileX = tile.tileX,
						tileZ = tile.tileZ,
						row = row,
						vertexStart = tile.vertexStart,
						sampleStart = tile.sampleStart
					});
				}
			}

			expectedSampleCount = tileWork.Length * StreamingAssetsElevationLoader.SampleCount;
			return new NativeArray<TileRowWork>(work.ToArray(), allocator);
		}

		public IEnumerator LoadTileSamples (
			Action<NativeArray<float>> onLoaded,
			Action<string> onError = null
		) {
			if (elevationLoader == null) {
				elevationLoader = GetComponent<StreamingAssetsElevationLoader>();
			}
			if (elevationLoader == null) {
				ReportLoadError("Assign a StreamingAssetsElevationLoader or add one to this GameObject.", onError);
				yield break;
			}
			if (onLoaded == null) {
				ReportLoadError("A callback for the concatenated tile samples is required.", onError);
				yield break;
			}

			TileLoadWork[] tileWork = null;
			string validationError = null;
			try {
				tileWork = BuildTileLoadWork();
			}
			catch (Exception exception) {
				validationError = exception.Message;
			}
			if (validationError != null) {
				ReportLoadError(validationError, onError);
				yield break;
			}

			var allSamples = new NativeArray<float>(
				tileWork.Length * StreamingAssetsElevationLoader.SampleCount,
				Allocator.Persistent,
				NativeArrayOptions.UninitializedMemory
			);
			bool samplesTransferred = false;
			try {
				foreach (TileLoadWork tile in tileWork) {
					NativeArray<float> tileSamples = default;
					string loadError = null;
					yield return elevationLoader.LoadTile(
						projectName,
						tile.level,
						tile.tileX,
						tile.tileZ,
						samples => tileSamples = samples,
						error => loadError = error
					);

					try {
						if (loadError != null || !tileSamples.IsCreated) {
							ReportLoadError(loadError ?? "Tile loading finished without returning samples.", onError);
							yield break;
						}

						NativeArray<float>.Copy(
							tileSamples, 0, allSamples, tile.sampleStart,
							StreamingAssetsElevationLoader.SampleCount
						);
					}
					finally {
						if (tileSamples.IsCreated) {
							tileSamples.Dispose();
						}
					}
				}

				samplesTransferred = true;
				onLoaded(allSamples);
			}
			finally {
				if (!samplesTransferred && allSamples.IsCreated) {
					allSamples.Dispose();
				}
			}
		}

		IEnumerator GenerateMeshWhenTilesAreLoaded () {
			NativeArray<float> tileSamples = default;
			try {
				bool samplesLoaded = false;
				string loadError = null;

				yield return LoadTileSamples(
					samples => {
						tileSamples = samples;
						samplesLoaded = true;
					},
					message => loadError = message
				);

				if (!samplesLoaded) {
					ReportLoadError(loadError ?? "Tile loading ended without producing samples.", null);
					yield break;
				}

				GenerateMeshFromSamples(tileSamples);
				tileSamples = default;
			}
			finally {
				if (tileSamples.IsCreated) {
					tileSamples.Dispose();
				}
				generationInProgress = false;
			}
		}

		void GenerateMeshFromSamples (NativeArray<float> tileSamples) {
			NativeArray<TileRowWork> rowWork = default;
			Mesh.MeshDataArray meshDataArray = default;
			bool meshDataAllocated = false;
			bool meshDataApplied = false;

			try {
				rowWork = BuildVertexRowWork(Allocator.TempJob, out int expectedSampleCount);
				if (tileSamples.Length != expectedSampleCount) {
					throw new InvalidOperationException(
						$"Expected {expectedSampleCount} elevation samples but received {tileSamples.Length}."
					);
				}

				meshDataArray = Mesh.AllocateWritableMeshData(1);
				meshDataAllocated = true;
				Mesh.MeshData meshData = meshDataArray[0];
				var streams = new SingleStream();
				streams.Setup(meshData, Bounds, VertexCount, IndexCount);

				NativeArray<int> indices = meshData.GetIndexData<int>();
				for (int i = 0; i < indices.Length; i++) {
					indices[i] = 0;
				}

				ScheduleVertexRows(tileSamples, rowWork, streams).Complete();
				Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
				meshDataApplied = true;
			}
			catch (Exception exception) {
				Debug.LogException(exception, this);
			}
			finally {
				if (rowWork.IsCreated) {
					rowWork.Dispose();
				}
				if (meshDataAllocated && !meshDataApplied) {
					meshDataArray.Dispose();
				}
				if (tileSamples.IsCreated) {
					tileSamples.Dispose();
				}
			}
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
			new Vector3(0.5f, 0f, zScaleFactor / 2f),
			new Vector3(1f, hRange * verticalScaleFactor, zScaleFactor)
		);

		static bool IsInside (int tileX, int tileZ, int4 innerRange) =>
			tileZ >= innerRange.y && tileZ < innerRange.w &&
			tileX >= innerRange.x && tileX < innerRange.z;

		static void ReportLoadError (string message, Action<string> onError) {
			if (onError != null) {
				onError(message);
			}
			else {
				Debug.LogError(message);
			}
		}

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
			if (numberOfLevels > pyramidLevels) {
				throw new InvalidOperationException("numberOfLevels cannot exceed the DEM pyramid level count.");
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

	public struct TileLoadWork {
		public int level;
		public int tileX;
		public int tileZ;
		public int vertexStart;
		public int sampleStart;
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