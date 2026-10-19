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
			int4 innerRange = InitialInnerRange();

			for (int rangeIndex = 0; rangeIndex < numberOfLevels; rangeIndex++) {
				int4 range = tileRanges[rangeIndex];
				int demLevel = deepestLevel - rangeIndex;
				int levelVertexStart = vertexStart;
				int outerRangeHorizontal = range.z-range.x
				for (int tileX = range.x; tileX < range.z; tileX++) {
					for (int tileZ = range.y; tileZ < range.w; tileZ++) {
						if (IsInside(tileX, tileZ, innerRange)) {
							continue;
						}

						work.Add(new TileLoadWork {
							level = demLevel,
							tileX = tileX,
							tileZ = tileZ,
							vertexStart = vertexStart,
							triangleStart=TBD,
							vertexUpLeft=GetVertexUpLeft(tileX,tileZ,innerRange,levelVertexStart,vertexStart,outerRangeHorizontal),
							vertexUp=GetVertexUp(tileX,tileZ,innerRange,levelVertexStart,vertexStart,outerRangeHorizontal),
							vertexUpMiddle=GetVertexUpMiddle(tileX,tileZ,innerRange,levelVertexStart),
							vertexUpRight=GetVertexUpRight(tileX,tileZ,innerRange,levelVertexStart,vertexStart,outerRangeHorizontal),
							vertexRight=GetVertexRight(tileX,tileZ,innerRange,levelVertexStart,vertexStart,outerRangeHorizontal),
							vertexRightMiddle=GetVertexRightMiddle(tileX,tileZ,innerRange,levelVertexStart),
							vertexDownRight=GetVertexDownRight(tileX,tileZ,innerRange,levelVertexStart,vertexStart,outerRangeHorizontal,range.x),
							vertexDownMiddle=GetVertexDownMiddle(tileX,tileZ,innerRange,levelVertexStart),
							vertexDown=GetVertexDown(tileX,tileZ,innerRange,levelVertexStart,vertexStart,outerRangeHorizontal),
							vertexDownLeft=TBD,
							vertexLeftMiddle=TBD,
							vertexLeft=TBD,
							transitionCase = tileRanges.Z == innerRange.w
						});
						vertexStart += 256;
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
				int triangleStart = tile.triangleStart;
				int vertexUp = tile.vertexUp;
				int vertexUpMiddle = tile.vertexUpMiddle;
				bool generateAbove = tile.vertexUp>=0;
				bool generateBelow = tile.vertexDown>=0 || tile.transitionCase;
				bool stitchOnLeft = tile.vertexLeftMiddle>=0;
				bool stitchOnRight = tile.vertexRightMiddle>=0;
				int vertexLeftOtherRow = tile.vertexLeft+256;
				int vertexRightOtherRow = tile.vertexRight+256;
				int vertexDown = -1;
				int vertexDownMiddle = -1;
				if (generateBelow)
				{
					vertexDown=tile.vertexStart+256;
				}
				if (generateAbove)
				{
					vertexLeftOtherRow = tile.vertexUpLeft;
					vertexRightOtherRow = tile.vertexUpRight;
				}
				if (stitchOnLeft)
				{
					vertexLeftOtherRow+=256;
				}
				int vertexLeft = tile.vertexLeft;
				int vertexRight = tile.vertexRight;
				for (int row = 0; row < 256; row++) {
					work.Add(new TileRowWork {
						level = tile.level,
						tileX = tile.tileX,
						tileZ = tile.tileZ,
						row = row,
						vertexStart = tile.vertexStart,
						triangleStart = triangleStart,
						vertexUp = vertexUp,
						vertexUpMiddle = vertexUpMiddle,
						vertexLeftOtherRow = vertexLeftOtherRow,
						vertexLeft = vertexLeft,
						vertexDown = vertexDown,
						vertexDownMiddle = vertexDownMiddle,
						vertexRightOtherRow = vertexRightOtherRow,
						vertexRight = vertexRight,
						stitchOnLeft = stitchOnLeft,
						stitchOnRight = stitchOnRight
						});
						if (vertexRight>=0)
						{
							if (stitchOnRight)
							{
								triangleStart+=3;
							}
							else
							{
								triangleStart+=2;
							}
						}
						if (vertexLeft>=0)
						{
							if (stitchOnLeft)
							{
								triangleStart+=3;
							}
							else
							{
								triangleStart+=2;
							}
						}
						if ((generateAbove && row == 0 && tile.vertexUpMiddle>=0) || (generateBelow && row == 255 && tile.vertexDownMiddle>=0))
						{
							triangleStart+=765;
						}
						else if (generateAbove || generateBelow)
						{
							triangleStart+=510;
						}
						if (generateAbove)
						{
							vertexUp=tile.vertexStart+row*256;
						}
						if (generateBelow)
						{
							if (row==254)
							{
								vertexDown=tile.vertexDown;
								vertexDownMiddle=tile.vertexDownMiddle;	
							}
							else
							{
							vertexDown=tile.vertexStart+(row+1)*256;
							}
						}
						vertexUpMiddle=-1;
						if (stitchOnLeft)
						{
							vertexLeft = vertexLeftOtherRow;
							if (row == 126)
							{
								vertexLeftOtherRow = tile.vertexLeftMiddle;	
							}
							else
							{
								vertexLeftOtherRow+=512;
							}
						}
						else if (stitchOnRight)
						{
							vertexRight = vertexRightOtherRow;
							if (row == 126)
							{
								vertexRightOtherRow = tile.vertexRightMiddle;	
							}
							else
							{
								vertexRightOtherRow+=512;
							}							
						}
						else
						{
							if (vertexLeft>=0)
							{
								vertexLeft+=256;
							}
							if (vertexRight>=0)
							{
								vertexRight+=256;
							}
							if (generateAbove)
							{
								vertexLeftOtherRow=vertexLeft;	
								vertexRightOtherRow=vertexRight;
							}
							else
							{
								if (row == 254)
								{
									vertexLeftOtherRow = tile.vertexDownLeft;	
									vertexLeftOtherRow = tile.vertexDownRight;	
								}
								else
								{
									vertexLeftOtherRow+=256;
									vertexRightOtherRow+=256;
								}
							}
						}
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
							tileSamples, 0, allSamples, tile.vertexStart,
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

		static int GetVertexUpLeft (int tileX, int tileZ, int4 innerRange, int v0, int v1, int outerRangeHorizontal)
		{
			if (tileX == innerRange.z && tileZ == innerRange.w)
			{
				return v0-1;
			}
			else if (tileX >= innerRange.z || tileZ >= innerRange.w)
			{
				return v1-outerRangeHorizontal*65536-1;
			}
			else
			{
				return -1;
			}
		}  

		static int GetVertexUp (int tileX, int tileZ, int4 innerRange, int v0, int v1, int outerRangeHorizontal)
		{
			if (tileZ == innerRange.w && tileX<innerRange.x)
			{
				return v1-(outerRangeHorizontal-innerRange.z+innerRange.x)*65536+65280;
			}
			else if (tileZ == innerRange.w && tileX<innerRange.z)
			{
				return v0-(innerRange.z-tileX)*131072+65280;
			}
			else if (tileZ>=innerRange.w)
			{
				return v1-outerRangeHorizontal*65536+65280;
			}
			else
			{
				return -1;
			}
		}  

		static int GetVertexUpMiddle (int tileX, int tileZ, int4 innerRange, int v0)
		{
			if (tileZ == innerRange.w && tileX>=innerRange.x && tileX<innerRange.z)
			{
				return v0-(innerRange.z-tileX-1)*131072-256;
			}
			else
			{
				return -1;
			}
		}  

		static int GetVertexUpRight (int tileX, int tileZ, int4 innerRange, int v0, int v1, int outerRangeHorizontal)
		{
			if (tileX == innerRange.x-1 && tileZ == innerRange.w)
			{
				return v0-(innerRange.z-innerRange.x)*131072+65280;
			}
			else if (tileX < innerRange.x || tileZ == innerRange.w)
			{
				return v1-(outerRangeHorizontal-innerRange.z+innerRange.x-2)*65536+256;
			}
			else if (tileX < innerRange.x || tileZ > innerRange.w)
			{
				return v1-(outerRangeHorizontal-2)*65536+256;
			}
			else
			{
				return -1;
			}
		}  	


		static int GetVertexRight (int tileX, int tileZ, int4 innerRange, int v0, int v1, int outerRangeHorizontal)
		{
			if (tileX<innerRange.z && (tileZ<innerRange.y || tileZ>=innerRange.w || tileX<innerRange.x))
			{
				return v1+65536;
			}
			else if  (tileX==innerRange.x && tileZ>=innerRange.y && tileZ<innerRange.w)
			{
				return v0-(innerRange.w-tileZ)*(innerRange.z-innerRange.x)*262144;
			}
			else
			{
				return -1;
			}
		}  			

		static int GetVertexRightMiddle (int tileX, int tileZ, int4 innerRange, int v0)
		{
			if  (tileX==innerRange.x && tileZ>=innerRange.y && tileZ<innerRange.w)
			{
				return v0-(innerRange.w-tileZ)*(innerRange.z-innerRange.x)*262144+131072;
			}
			else
			{
				return -1;
			}
		}  		

		static int GetVertexDownRight (int tileX, int tileZ, int4 innerRange, int v0, int v1, int outerRangeHorizontal, int zeroOuterTile)
		{
			if (tileZ<innerRange.y-1 || (tileZ == innerRange.y-1 && tileX<innerRange.x-1)) 
			{
				return v1+(outerRangeHorizontal+1)*65536;
			}
			else if  (tileZ==innerRange.y-1 && tileX<innerRange.z)
			{
				return v0-(innerRange.w-innerRange.y)*(innerRange.z-innerRange.x)*262144+(tileX-innerRange.x+1)*131072;
			}
			else if (tileZ < innerRange.w && tileX<innerRange.x-1)
			{
				return v1+(innerRange.x-zeroOuterTile+1)*65536;
			}
			else if (tileZ < innerRange.w && tileX==innerRange.x-1)
			{
				return v0-(innerRange.w-tileZ)*(innerRange.z-innerRange.x)*262144;
			}
			else
			{
				return -1;
			}
		}  			

		static int GetVertexDownMiddle (int tileX, int tileZ, int4 innerRange, int v0)
		{
			if (tileZ == innerRange.y-1 && tileX>=innerRange.x && tileX<innerRange.z)
			{
				return v0-(innerRange.w-innerRange.y)*(innerRange.z-innerRange.x)*262144+(tileX-innerRange.x+1)*131072+65536;
			}
			else
			{
				return -1;
			}
		}  

		static int GetVertexDown (int tileX, int tileZ, int4 innerRange, int v0, int v1, int outerRangeHorizontal)
		{
			if (tileZ<innerRange.y-1 || (tileZ == innerRange.y-1 && tileX<innerRange.x)) 
			{
				return v1+outerRangeHorizontal*65536;
			}
			else if  (tileZ==innerRange.y-1 && tileX>=innerRange.x && tileX<innerRange.z)
			{
				return v0-(innerRange.w-innerRange.y)*(innerRange.z-innerRange.x)*262144+(tileX-innerRange.X)*131072;
			}
			else if (tileZ<innerRange.w-1)
			{
				return v1+(outerRangeHorizontal-innerRange.z+innerRange.x)*65536;				
			}
			else
			{
				return -1;
			}
		}  				


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
		public int triangleStart;
		public int vertexUpLeft;
		public int vertexUp;
		public int vertexUpMiddle;
		public int vertexUpRight;
		public int vertexRight;
		public int vertexRightMiddle;
		public int vertexDownRight;
		public int vertexDownMiddle;
		public int vertexDown;
		public int vertexDownLeft;
		public int vertexLeftMiddle;
		public int vertexLeft;
		public bool transitionCase;
	}

	public struct TileRowWork {
		public int level;
		public int tileX;
		public int tileZ;
		public int row;
		public int vertexStart;
		public int triangleStart;
		public int vertexUp;
		public int vertexUpMiddle;
		public int vertexLeftOtherRow;
		public int vertexLeft;
		public int vertexDown;
		public int vertexDownMiddle;
		public int vertexRightOtherRow;
		public int vertexRight;
		public bool stitchOnLeft;
		public bool stitchOnRight;
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
			int samplesIndex = work.row * StreamingAssetsElevationLoader.SamplesPerSide;
			int vertexIndex = work.vertexStart + samplesIndex;
			int triangleIndex = work.triangleStart;
			int parallelRowIndex = work.vertexDown;
			int parallelRowNextIndex = work.vertexDown+1;
			bool generateAbove = work.vertexUp >= 0;
			if (generateAbove)
			{
				parallelRowIndex = work.vertexUp;
				parallelRowNextIndex = work.vertexUp+1;
			}
			int rightShifter = 1;
			bool generateBelow = work.vertexDown >= 0;
			bool stitchingMode = work.vertexUpMiddle >= 0 || work.vertexDownMiddle >= 0;
			int vertexRightUp = work.vertexUp+255;
			int vertexRightDown = work.vertexDown+255;
			if (stitchingMode)
			{
				vertexRightUp = work.vertexUpMiddle+255;
				vertexRightDown = work.vertexDownMiddle+255;
			}
			if (work.vertexLeftOtherRow>=0)
			{
				if (work.stitchOnLeft && generateBelow)
				{
						streams.setTriangle(triangleIndex, int3(work.vertexLeft, vertexIndex,work.vertexLeft+256));
						streams.setTriangle(triangleIndex+1, int3(work.vertexLeft+256, vertexIndex,work.vertexDown));
						streams.setTriangle(triangleIndex+2, int3(work.vertexLeft+256,work.vertexDown,work.vertexLeftOtherRow));
						triangleIndex+=3;
				}						
				else
				{
					if (generateAbove)
					{
						streams.setTriangle(triangleIndex, int3(work.vertexLeftOtherRow, work.vertexUp,work.vertexLeft));
						streams.setTriangle(triangleIndex+1, int3(work.vertexLeft, work.vertexUp,vertexIndex));
						triangleIndex+=2;
					}
					else if (generateBelow)
					{
						streams.setTriangle(triangleIndex, int3(work.vertexLeft, vertexIndex,work.vertexLeftOtherRow));
						streams.setTriangle(triangleIndex+1, int3(work.vertexLeftOtherRow, vertexIndex,work.vertexDown));
						triangleIndex+=2;
					}					
				}
			}
			for (int x = 0; x < 256; x++, vertexIndex++, samplesIndex++) {
				float fractionalZ = coordinateStartZ + levelScale * work.row / 255f;
				var vertex = new Vertex();
				vertex.position.x = coordinateStartX + levelScale * x / 255f;
				vertex.position.y = (tileSamples[samplesIndex+x] - midH) * VerticalScaleFactor;
				vertex.position.z = fractionalZ * ZScaleFactor;
				vertex.normal.y = 1f;
				vertex.texCoord0 = new float2(vertex.position.x, fractionalZ);
				vertex.tangent.xw = new float2(1f, -1f);
				streams.SetVertex(vertexIndex+x, vertex);
				if (x==255 && work.vertexRight>=0)
				{
					if (work.stitchOnRight && generateBelow)
					{
						streams.setTriangle(triangleIndex, int3(vertexIndex,work.vertexRight,work.vertexRight+256));
						streams.setTriangle(triangleIndex+1, int3(vertexIndex,work.vertexRight+256,vertexRightDown));
						streams.setTriangle(triangleIndex+2, int3(vertexRightDown,work.vertexRight+256,work.vertexRightOtherRow));
						triangleIndex+=3;
					}						
					else
					{
						if (generateAbove)
						{
							streams.setTriangle(triangleIndex, int3(vertexIndex, vertexRightUp, work.vertexRightOtherRow));
							streams.setTriangle(triangleIndex+1, int3(vertexIndex, work.vertexRightOtherRow,work.vertexRight));
							triangleIndex+=2;
						}
						else if (generateBelow)
						{
							streams.setTriangle(triangleIndex, int3(vertexRightDown, vertexIndex,work.vertexRight));
							streams.setTriangle(triangleIndex+1, int3(vertexRightDown,work.vertexRight,work.vertexRightOtherRow));
							triangleIndex+=2;
						}					
					}					
				}
				else
				{
					if (generateBelow)
					{
						if (stitchingMode)
						{
							streams.setTriangle(triangleIndex, int3(vertexIndex,parallelRowIndex+1,parallelRowIndex));
							streams.setTriangle(triangleIndex+1, int3(vertexIndex,vertexIndex+1,parallelRowIndex+1));
							streams.setTriangle(triangleIndex+2, int3(parallelRowIndex+1,vertexIndex+1,parallelRowNextIndex));
							triangleIndex+=3;						
						}
						else
						{
							streams.setTriangle(triangleIndex, int3(vertexIndex, vertexIndex+1,parallelRowIndex));
							streams.setTriangle(triangleIndex+1, int3(parallelRowIndex, vertexIndex+1,parallelRowNextIndex));
							triangleIndex+=2;							
						}
					}
					else if (generateAbove)
					{
						if (stitchingMode)
						{
							streams.setTriangle(triangleIndex, int3(parallelRowIndex,parallelRowIndex+1,vertexIndex));
							streams.setTriangle(triangleIndex+1, int3(vertexIndex,parallelRowIndex+1,vertexIndex+1));
							streams.setTriangle(triangleIndex+2, int3(parallelRowIndex+1,parallelRowNextIndex,vertexIndex+1));
							triangleIndex+=3;						
						}
						else
						{
							streams.setTriangle(triangleIndex, int3(vertexIndex, parallelRowIndex, parallelRowNextIndex));
							streams.setTriangle(triangleIndex+1, int3(vertexIndex, parallelRowNextIndex, vertexIndex+1));
							triangleIndex+=2;							
						}						
					}
				}
				if (stitchingMode)
				{
					if (x==127)
					{
						parallelRowIndex+=65281;
					}
					else
					{
						parallelRowIndex+=2;
					}
					if (x==126)
					{
						parallelRowNextIndex+=65281;
					}
					else
					{
						parallelRowNextIndex+=2;
					}
				}
				else
				{
					parallelRowIndex+=1;
					parallelRowNextIndex+=1;
				}
			}
		}
	}
}