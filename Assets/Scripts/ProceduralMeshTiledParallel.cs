using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using ProceduralMeshes.Streams;

namespace ProceduralMeshes {

	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(StreamingAssetsElevationLoader))]
	public sealed class ProceduralMeshTiledParallel : MonoBehaviour {

		public Camera mainCamera;
		int deepestLevel;
		int numberOfLevels;
		List<int4> tileRanges;

		public string projectName = "Antarctica";
		public float VE = 1;
		
		public List<int> textureOrder;
		[SerializeField] StreamingAssetsElevationLoader elevationLoader;

		ElevationMetadata elevationInfo;
		AllTexturesMetadata texturesInfo;
		MeshTilingSchemeDetermine meshTilingScheme;
		int DEMwidth;
		int DEMheight;
		float verticalScaleFactor;
		float zScaleFactor;
		float midH;
		float hRange;
        int pyramidLevels;
		Mesh mesh;
		bool regenerationRequested;
		bool generationInProgress;

        List<int> texturePyramidLevels;
        List<string> textureNames;
		int numberOfTextures;
		int maximumDescentLevel;
		Vector3 cameraPosition;
		Quaternion cameraRotation;

		void Awake () {
			elevationInfo = new ElevationMetadata();
			texturesInfo = new AllTexturesMetadata();
			mesh = new Mesh { name = "Procedural Tiled Mesh" };
			GetComponent<MeshFilter>().mesh = mesh;
			cameraPosition = mainCamera.transform.position;
			cameraRotation = mainCamera.transform.rotation;
		}

		void Start () => regenerationRequested = true;

		void OnValidate () => regenerationRequested = true;

		void Update () {

			if (!regenerationRequested || generationInProgress) {
				if (mainCamera.transform.position != cameraPosition)
				{
					cameraPosition = mainCamera.transform.position;
					regenerationRequested = true;
				}
				else if (mainCamera.transform.rotation != cameraRotation)
				{
					cameraRotation = mainCamera.transform.rotation;
					regenerationRequested = true;
				}
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
			elevationInfo.LoadMetadata(Path.Combine(Application.streamingAssetsPath, "Projects", projectName,"dem_metadata.json"));
			texturesInfo.LoadMetadataFolder(projectName);
			DEMwidth = elevationInfo.width;
			DEMheight = elevationInfo.height;
			verticalScaleFactor = VE / (256*elevationInfo.dem_resolution_x);
			zScaleFactor = elevationInfo.dem_resolution_y / elevationInfo.dem_resolution_x;
			midH = elevationInfo.mid_h;
			hRange = elevationInfo.h_range;
			pyramidLevels = elevationInfo.pyramid_levels;
			texturePyramidLevels = texturesInfo.pyramidLevels;
			textureNames = texturesInfo.textureNames;
			numberOfTextures = texturesInfo.numberOfTextures;
			maximumDescentLevel = pyramidLevels;
			if (texturePyramidLevels != null) {
				foreach (int textureLevel in texturePyramidLevels) {
					maximumDescentLevel = Math.Max(maximumDescentLevel, textureLevel);
				}
			}
			deepestLevel = pyramidLevels-1;
			numberOfLevels = pyramidLevels;
            tileRanges = new List<int4>(pyramidLevels);
            for (int level = 0; level < pyramidLevels; level++) {
                tileRanges.Add(new int4(0, 0, 0, 0));		
			}	
		}

		void OnDestroy () {
			if (mesh != null) {
				Destroy(mesh);
			}
		}

		int VertexCount = 0;
		int IndexCount = 0;

		public TileLoadWork[] BuildTileLoadWork () {
			ValidateInputs();

			var work = new List<TileLoadWork>();
			int vertexStart = 0;
			int triangleStart = 0;
			int4 innerRange = InitialInnerRange();


			for (int rangeIndex = 0; rangeIndex < numberOfLevels; rangeIndex++) {
				int4 range = tileRanges[rangeIndex];
				int demLevel = deepestLevel - rangeIndex;
				int levelVertexStart = vertexStart;
				for (int tileZ = range.y; tileZ < range.w; tileZ++) {
					for (int tileX = range.x; tileX < range.z; tileX++) {
						if (IsInside(tileX, tileZ, innerRange)) {
							continue;
						}
							int vertexUpLeft=GetVertexUpLeft(tileX,tileZ,range,innerRange,levelVertexStart,vertexStart);
							int vertexUp=GetVertexUp(tileX,tileZ,range,innerRange,levelVertexStart,vertexStart);
							int vertexUpMiddle=GetVertexUpMiddle(tileX,tileZ,innerRange,levelVertexStart);
							int vertexUpRight=GetVertexUpRight(tileX,tileZ,range,innerRange,levelVertexStart,vertexStart);						
							int vertexRight=GetVertexRight(tileX,tileZ,innerRange,levelVertexStart,vertexStart);					
							int vertexRightMiddle=GetVertexRightMiddle(tileX,tileZ,innerRange,levelVertexStart);							
							int vertexDownRight=GetVertexDownRight(tileX,tileZ,range,innerRange,levelVertexStart,vertexStart);						
							int vertexDownMiddle=GetVertexDownMiddle(tileX,tileZ,innerRange,levelVertexStart);						
							int vertexDown=GetVertexDown(tileX,tileZ,range,innerRange,levelVertexStart,vertexStart);						
							int vertexDownLeft=GetVertexDownLeft(tileX,tileZ,range,innerRange,levelVertexStart,vertexStart);						
							int vertexLeftMiddle=GetVertexLeftMiddle(tileX,tileZ,innerRange,levelVertexStart);						
							int vertexLeft=GetVertexLeft(tileX,tileZ,innerRange,levelVertexStart,vertexStart);					
							bool transitionCase = tileZ == innerRange.w-1;
							
						work.Add(new TileLoadWork {
							level = demLevel,
							tileX = tileX,
							tileZ = tileZ,
							vertexStart = vertexStart,
							triangleStart=triangleStart,
							vertexUpLeft=vertexUpLeft,
							vertexUp=vertexUp,
							vertexUpMiddle=vertexUpMiddle,
							vertexUpRight=vertexUpRight,
							vertexRight=vertexRight,
							vertexRightMiddle=vertexRightMiddle,
							vertexDownRight=vertexDownRight,
							vertexDownMiddle=vertexDownMiddle,
							vertexDown=vertexDown,
							vertexDownLeft=vertexDownLeft,
							vertexLeftMiddle=vertexLeftMiddle,
							vertexLeft=vertexLeft,
							transitionCase = transitionCase
						});
						vertexStart += 65536;
						triangleStart += 130050;
						int edgeOne=0;
						int edgeTwo=1;
						if (!transitionCase)
						{
							triangleStart+=510;
							edgeOne+=1;
						}
						if ((vertexDown>=0 && vertexDownMiddle>=0) || (vertexUp>=0 && vertexUpMiddle>=0))
						{
							triangleStart+=255;
							edgeOne+=1;
						}
						if (tileX != innerRange.z-1)
						{
							triangleStart+=510;
							edgeTwo+=1;
						}
						if ((vertexRight>=0 && vertexRightMiddle>=0) || (vertexLeft>=0 && vertexLeftMiddle>=0))
						{
							triangleStart+=255;
							edgeTwo+=1;
						}				
						triangleStart+=edgeOne*edgeTwo;		
					}
				}

				innerRange = range / 2;
			}

			return work.ToArray();
		}

		public NativeArray<TileRowWork> BuildVertexRowWork (Allocator allocator, out int expectedSampleCount) {
			return BuildVertexRowWork(BuildTileLoadWork(), allocator, out expectedSampleCount);
		}

		NativeArray<TileRowWork> BuildVertexRowWork (
			TileLoadWork[] tileWork,
			Allocator allocator,
			out int expectedSampleCount
		) {
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
				if (vertexLeft<0)
				{
					vertexLeftOtherRow=-1;
				}
				if (vertexRight<0)
				{
					vertexRightOtherRow=-1;
				}
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
							vertexDown=tile.vertexStart+(row+2)*256;
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
									if (vertexLeft>=0)
									{
										vertexLeftOtherRow = tile.vertexDownLeft;	
									}
									if (vertexRight>=0)
									{
										vertexRightOtherRow = tile.vertexDownRight;	
									}
								}
								else
								{
									if (vertexLeft>=0)
									{
										vertexLeftOtherRow+=256;
									}
									if (vertexRight>=0)
									{
										vertexRightOtherRow+=256;
									}
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

			yield return LoadTileSamples(tileWork, onLoaded, onError);
		}

		IEnumerator LoadTileSamples (
			TileLoadWork[] tileWork,
			Action<NativeArray<float>> onLoaded,
			Action<string> onError
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
			var meshTilingScheme = new MeshTilingSchemeDetermine(mainCamera,zScaleFactor,pyramidLevels,projectName, texturePyramidLevels, numberOfTextures, maximumDescentLevel,textureOrder, DEMwidth, DEMheight);
			meshTilingScheme.DetermineAll();
			deepestLevel = meshTilingScheme.deepestLevel;
			numberOfLevels = meshTilingScheme.numberOfLevels;
			Debug.Log(new int3(pyramidLevels,deepestLevel,numberOfLevels));
			tileRanges = meshTilingScheme.tileRanges.GetRange(pyramidLevels-deepestLevel-1,numberOfLevels);
			VertexCount = CalculateCounts().vertexCount;
			IndexCount = CalculateCounts().triangleCount * 3;
			Debug.Log(new int2(VertexCount,IndexCount));
			NativeArray<float> tileSamples = default;
			try {
				TileLoadWork[] tileWork;
				try {
					tileWork = BuildTileLoadWork();
				}
				catch (Exception exception) {
					ReportLoadError(exception.Message, null);
					yield break;
				}

				bool samplesLoaded = false;
				string loadError = null;

				yield return LoadTileSamples(
					tileWork,
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

				GenerateMeshFromSamples(tileSamples, tileWork);
				tileSamples = default;
			}
			finally {
				if (tileSamples.IsCreated) {
					tileSamples.Dispose();
				}
				generationInProgress = false;
			}
		}

		void GenerateMeshFromSamples (NativeArray<float> tileSamples, TileLoadWork[] tileWork) {
			NativeArray<TileRowWork> rowWork = default;
			Mesh.MeshDataArray meshDataArray = default;
			bool meshDataAllocated = false;
			bool meshDataApplied = false;

			try {
				rowWork = BuildVertexRowWork(tileWork, Allocator.TempJob, out int expectedSampleCount);
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
				mesh.bounds = Bounds;
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

		static int GetVertexUpLeft (int tileX, int tileZ, int4 range, int4 innerRange, int v0, int v1)
		{
			if (tileX == innerRange.z && tileZ == innerRange.w)
			{
				return v0-65281;
			}
			else if (tileX >= innerRange.z && tileZ >= innerRange.w)
			{
				return v1-(range.z-range.x)*65536-65281;
			}
			else
			{
				return -1;
			}
		}  

		static int GetVertexUp (int tileX, int tileZ, int4 range, int4 innerRange, int v0, int v1)
		{
			if (tileZ == innerRange.w && tileX<innerRange.x)
			{
				return v1-(range.z-range.x-innerRange.z+innerRange.x)*65536;
			}
			else if (tileZ == innerRange.w && tileX<innerRange.z)
			{
				return v0-(innerRange.z-tileX)*131072;
			}
			else if (tileZ>=innerRange.w)
			{
				return v1-(range.z-range.x)*65536;
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
				return v0-(innerRange.z-tileX-1)*131072-65536;
			}
			else
			{
				return -1;
			}
		}  

		static int GetVertexUpRight (int tileX, int tileZ, int4 range, int4 innerRange, int v0, int v1)
		{
			if (tileX >= innerRange.x-1 && tileX<innerRange.z-1 && tileZ == innerRange.w)
			{
				return v0-(innerRange.z-innerRange.x)*131072;
			}
			else if (tileX < innerRange.x && tileZ == innerRange.w)
			{
				return v1-(range.z-range.x-innerRange.z+innerRange.x-2)*65536-65024;
			}
			else if (tileX < innerRange.z && tileZ >= innerRange.w)
			{
				return v1-(range.z-range.x-2)*65536-65024;
			}
			else
			{
				return -1;
			}
		}  	


		static int GetVertexRight (int tileX, int tileZ, int4 innerRange, int v0, int v1)
		{
			if (tileX<innerRange.z-1 && (tileZ<innerRange.y || tileZ>=innerRange.w || tileX<innerRange.x-1))
			{
				return v1+65536;
			}
			else if  (tileX==innerRange.x-1 && tileZ>=innerRange.y && tileZ<innerRange.w)
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
			if  (tileX==innerRange.x-1 && tileZ>=innerRange.y && tileZ<innerRange.w)
			{
				return v0-(innerRange.w-tileZ)*(innerRange.z-innerRange.x)*262144+131072;
			}
			else
			{
				return -1;
			}
		}  		

		static int GetVertexDownRight (int tileX, int tileZ, int4 range, int4 innerRange, int v0, int v1)
		{
			if ((tileZ<innerRange.y-1 && tileX<range.z-1) || (tileZ == innerRange.y-1 && tileX<innerRange.x-1 && tileZ<range.w-1)) 
			{
				return v1+(range.z-range.x+1)*65536+65280;
			}
			else if  (tileZ==innerRange.y-1 && tileX<innerRange.z-1 && tileZ<range.w-1)
			{
				return v0-(innerRange.w-innerRange.y)*(innerRange.z-innerRange.x)*262144+(tileX-innerRange.x+1)*131072+65280;
			}
			else if (tileZ < innerRange.w-1 && tileX<innerRange.x-1)
			{
				return v1+(innerRange.x-range.x+1)*65536+65280;
			}
			else if (tileZ < innerRange.w-1 && tileX==innerRange.x-1 && tileX<range.z-1)
			{
				return v0-(innerRange.w-tileZ)*(innerRange.z-innerRange.x)*262144+65280;
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
				return v0-(innerRange.w-innerRange.y)*(innerRange.z-innerRange.x)*262144+(tileX-innerRange.x+1)*131072+65536+65280;
			}
			else
			{
				return -1;
			}
		}  

		static int GetVertexDown (int tileX, int tileZ, int4 range, int4 innerRange, int v0, int v1)
		{
			if (tileZ<innerRange.y-1 || (tileZ == innerRange.y-1 && tileX<innerRange.x && tileZ<range.w-1)) 
			{
				return v1+(range.z-range.x)*65536+65280;
			}
			else if  (tileZ==innerRange.y-1 && tileX>=innerRange.x && tileX<innerRange.z)
			{
				return v0-(innerRange.w-innerRange.y)*(innerRange.z-innerRange.x)*262144+(tileX-innerRange.x)*131072+65280;
			}
			else if (tileZ<innerRange.w-1)
			{
				return v1+(range.z-range.x-innerRange.z+innerRange.x)*65536+65280;				
			}
			else
			{
				return -1;
			}
		}  				
		static int GetVertexDownLeft (int tileX, int tileZ, int4 range, int4 innerRange, int v0, int v1)
		{
			if (tileX >= innerRange.z && tileZ < innerRange.y-1)
			{
				return v1+(range.z-range.x-1)*65536+65535;
			}
			else if (tileX > innerRange.z && tileZ < innerRange.w-1)
			{
				return v1+(range.z-range.x-innerRange.z+innerRange.x-1)*65536+65535;
			}
			else if (tileX == innerRange.z && tileZ < innerRange.w-1)
			{
				return v0-(innerRange.z-innerRange.x)*((innerRange.w-tileZ-2)*262144+131072)-1;
			}
			else
			{
				return -1;
			}
		}  		

		static int GetVertexLeftMiddle (int tileX, int tileZ, int4 innerRange, int v0)
		{
			if  (tileX==innerRange.z && tileZ>=innerRange.y && tileZ<innerRange.w)
			{
				return v0-(innerRange.w-tileZ-1)*(innerRange.z-innerRange.x)*262144-65281;
			}
			else
			{
				return -1;
			}
		}  		

		static int GetVertexLeft (int tileX, int tileZ, int4 innerRange, int v0, int v1)
		{
			if (tileX>=innerRange.z && (tileZ<innerRange.y || tileZ>=innerRange.w || tileX>innerRange.z))
			{
				return v1-65281;
			}
			else if  (tileX==innerRange.z && tileZ>=innerRange.y && tileZ<innerRange.w)
			{
				return v0-((innerRange.w-tileZ-1)*262144+131072)*(innerRange.z-innerRange.x)-65281;
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
			if (numberOfLevels <= 0 || tileRanges == null || tileRanges.Count < numberOfLevels) {
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
			int vertexIndex = work.vertexStart + work.row * 256;
			int triangleIndex = work.triangleStart;
			int parallelRowIndex = work.vertexDown;
			int parallelRowNextIndex = work.vertexDown+1;
			bool generateAbove = work.vertexUp >= 0;
			if (generateAbove)
			{
				parallelRowIndex = work.vertexUp;
				parallelRowNextIndex = work.vertexUp+1;
			}
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
						streams.SetTriangle(triangleIndex, new int3(work.vertexLeft, vertexIndex,work.vertexLeft+256));
						streams.SetTriangle(triangleIndex+1, new int3(work.vertexLeft+256, vertexIndex,work.vertexDown));
						streams.SetTriangle(triangleIndex+2, new int3(work.vertexLeft+256,work.vertexDown,work.vertexLeftOtherRow));
						triangleIndex+=3;
				}						
				else
				{
					if (generateAbove)
					{
						streams.SetTriangle(triangleIndex, new int3(work.vertexLeftOtherRow, work.vertexUp,work.vertexLeft));
						streams.SetTriangle(triangleIndex+1, new int3(work.vertexLeft, work.vertexUp,vertexIndex));
						triangleIndex+=2;
					}
					else if (generateBelow)
					{
						streams.SetTriangle(triangleIndex, new int3(work.vertexLeft, vertexIndex,work.vertexLeftOtherRow));
						streams.SetTriangle(triangleIndex+1, new int3(work.vertexLeftOtherRow, vertexIndex,work.vertexDown));
						triangleIndex+=2;
					}					
				}
			}
			for (int x = 0; x < 256; x++, vertexIndex++) {
				float fractionalZ = coordinateStartZ + levelScale * work.row / 255f;
				var vertex = new Vertex();
				vertex.position.x = coordinateStartX + levelScale * x / 255f;
				vertex.position.y = (tileSamples[vertexIndex] - midH) * VerticalScaleFactor;//+VerticalScaleFactor*1500f*(float)Math.Sin((float)x+(float)work.row);
				vertex.position.z = fractionalZ * ZScaleFactor;
				vertex.normal.y = 1f;
				vertex.texCoord0 = new float2(vertex.position.x, fractionalZ);
				vertex.tangent.xw = new float2(1f, -1f);
				streams.SetVertex(vertexIndex, vertex);
				if (x==255)
				{
					if (work.vertexRight>=0)
					{
						if (work.stitchOnRight && generateBelow)
						{
							streams.SetTriangle(triangleIndex, new int3(vertexIndex,work.vertexRight,work.vertexRight+256));
							streams.SetTriangle(triangleIndex+1, new int3(vertexIndex,work.vertexRight+256,vertexRightDown));
							streams.SetTriangle(triangleIndex+2, new int3(vertexRightDown,work.vertexRight+256,work.vertexRightOtherRow));
							triangleIndex+=3;
						}						
						else
						{
							if (generateAbove)
							{
								streams.SetTriangle(triangleIndex, new int3(vertexIndex, vertexRightUp, work.vertexRightOtherRow));
								streams.SetTriangle(triangleIndex+1, new int3(vertexIndex, work.vertexRightOtherRow,work.vertexRight));
								triangleIndex+=2;
							}
							else if (generateBelow)
							{
								streams.SetTriangle(triangleIndex, new int3(vertexRightDown, vertexIndex,work.vertexRight));
								streams.SetTriangle(triangleIndex+1, new int3(vertexRightDown,work.vertexRight,work.vertexRightOtherRow));
								triangleIndex+=2;
							}					
						}		
					}			
				}
				else
				{
					if (generateBelow)
					{
						if (stitchingMode)
						{
							streams.SetTriangle(triangleIndex, new int3(vertexIndex,parallelRowIndex+1,parallelRowIndex));
							streams.SetTriangle(triangleIndex+1, new int3(vertexIndex,vertexIndex+1,parallelRowIndex+1));
							streams.SetTriangle(triangleIndex+2, new int3(parallelRowIndex+1,vertexIndex+1,parallelRowNextIndex));
							triangleIndex+=3;						
						}
						else
						{
							streams.SetTriangle(triangleIndex, new int3(vertexIndex, vertexIndex+1,parallelRowIndex));
							streams.SetTriangle(triangleIndex+1, new int3(parallelRowIndex, vertexIndex+1,parallelRowNextIndex));
							triangleIndex+=2;							
						}
					}
					else if (generateAbove)
					{
						if (stitchingMode)
						{
							streams.SetTriangle(triangleIndex, new int3(parallelRowIndex,parallelRowIndex+1,vertexIndex));
							streams.SetTriangle(triangleIndex+1, new int3(vertexIndex,parallelRowIndex+1,vertexIndex+1));
							streams.SetTriangle(triangleIndex+2, new int3(parallelRowIndex+1,parallelRowNextIndex,vertexIndex+1));
							triangleIndex+=3;						
						}
						else
						{
							streams.SetTriangle(triangleIndex, new int3(vertexIndex, parallelRowIndex, parallelRowNextIndex));
							streams.SetTriangle(triangleIndex+1, new int3(vertexIndex, parallelRowNextIndex, vertexIndex+1));
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