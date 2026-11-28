using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralMeshes {

	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	public sealed class ProceduralMeshSimplerApproach : MonoBehaviour {

		public Camera mainCamera;
		public int deepestLevel;
		public int numberOfLevels;
		public List<int4> tileRanges;

		public string projectName = "Antarctica";
		public float VE = 1f;

		public List<int> textureOrder;
		public List<float> textureOpacities;
		[SerializeField] ExternalElevationLoader elevationLoader;

		NativeArray<float> roughTopography;
		ElevationMetadata elevationInfo;
		AllTexturesMetadata texturesInfo;
		CameraPositionBasedTiling meshTilingScheme;
		GameObject uiController;

		int oldDeepestLevel = 0;
		int oldNumberOfLevels = 1;
		string oldProjectName;
		public List<int4> levelRanges;
		public List<int4> oldTileRanges = new List<int4>();		

		int DEMwidth;
		int DEMheight;
		float verticalScaleFactor;
		float zScaleFactor;
		float overallScaleFactor;
		float midH;
		float hRange;
        int pyramidLevels;
		Mesh mesh;
		bool regenerationRequested;
		bool generationInProgress;
		[NonSerialized] float validatedVE;

		[NonSerialized] List<float> validatedTextureOpacities = new List<float>();
		[NonSerialized] List<int> validatedTextureOrder = new List<int>();
		[NonSerialized] bool hasValidatedTextures;

        List<int> texturePyramidLevels;
        List<int> textureNativeLevels;
        List<int> textureNativeTilesX;
        List<int> textureNativeTilesZ;
	    List<bool> textureVisibilityToggles;
        List<string> textureNames;
		int numberOfTextures;
		int maximumDescentLevel;
		Vector3 cameraPosition;
		Quaternion cameraRotation;
		TileFamily tileFamily;
		List<string> toBeRemoved = default;
		Dictionary<TileID,GameObject> tileObjects = new Dictionary<TileID,GameObject>();

		void Awake () {
			oldTileRanges.Add(new int4(0,0,0,0));
			oldProjectName = projectName;
			elevationInfo = new ElevationMetadata();
			texturesInfo = new AllTexturesMetadata();
			cameraPosition = mainCamera.transform.position;
			cameraRotation = mainCamera.transform.rotation;
			LoadProjectMetadata();
			elevationLoader = new ExternalElevationLoader();
			roughTopography = elevationLoader.LoadTile(projectName,0,0,0,midH);
			validatedTextureOrder.AddRange(textureOrder);
			validatedTextureOpacities.AddRange(textureOpacities);
		}

		void Start () => regenerationRequested = true;

		public GameObject CreateLayerUI(
			string projectName,
			List<int> textureOrder,
			List<int> nativeLevels,
			List<int> nativeTilesX,
			List<int> nativeTilesZ,
			List<bool> visibilityToggles,
			List<float> textureOpacities,
			List<string> textureNames,
			int numberOfTextures,
			Camera mainCamera)
		{
			// Create the Canvas GameObject
			GameObject canvasObject = new GameObject("UI_Canvas");

			// Add Canvas
			Canvas canvas = canvasObject.AddComponent<Canvas>();

			// Configure Canvas
			canvas.renderMode = RenderMode.ScreenSpaceCamera;
			canvas.worldCamera = mainCamera;
			canvas.planeDistance = 1f;
			canvas.sortingOrder = 10;

			// Add CanvasScaler
			CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();

			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1920, 1080);
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			scaler.matchWidthOrHeight = 0.5f;

			// Add GraphicRaycaster
			canvasObject.AddComponent<GraphicRaycaster>();

			// Add LayerControllerUI
			LayerControllerUI layerUI =
				canvasObject.AddComponent<LayerControllerUI>();

			// Initialize it with your project data
			layerUI.Initialize(
				projectName,
				textureOrder,
				nativeLevels,
				nativeTilesX,
				nativeTilesZ,
				visibilityToggles,
				textureOpacities,
				textureNames,
				numberOfTextures,
				mainCamera
			);

			return canvasObject;
		}

		void OnValidate () {
			if (VE != validatedVE) {
				foreach (TileID key in tileObjects.Keys)
				{
					tileObjects[key].transform.localScale = new Vector3(1f, VE, 1f);
				}
				validatedVE = VE;
				return;
			} 
			if (tileFamily != null)
			{
				for (int l=0; l<numberOfTextures; l++)
				{
					if (textureOrder[l]!=validatedTextureOrder[l] || textureOpacities[l] != validatedTextureOpacities[l])
					{
						for (int m=0; m<numberOfTextures; m++)
						{
							validatedTextureOrder[m] = textureOrder[m];
							validatedTextureOpacities[m] = textureOpacities[m];
						}
						tileFamily.ChangeTextures(textureOrder,textureOpacities);
						foreach (TileID key in tileObjects.Keys)
						{
							Material sourceMaterial = GetComponent<MeshRenderer>().sharedMaterial;
							MeshRenderer tileRenderer = tileObjects[key].GetComponent<MeshRenderer>();
							Material tileMaterial = new Material(sourceMaterial);
							tileMaterial.mainTexture = tileFamily.LoadTileTexture(key.x, key.z, key.level);
							tileRenderer.sharedMaterial = tileMaterial;
						}
						return;
					}
				}	
			}
			if (projectName != oldProjectName)
			{
				LoadProjectMetadata();
				oldProjectName = projectName;
				return;
			}
			regenerationRequested = true;
		}

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
			

			generationInProgress = true;
			StartCoroutine(GenerateMeshWhenTilesAreLoaded());
		}

		IEnumerator GenerateMeshWhenTilesAreLoaded () {
			meshTilingScheme = new CameraPositionBasedTiling(mainCamera, zScaleFactor, maximumDescentLevel, midH, hRange, roughTopography, overallScaleFactor, verticalScaleFactor*VE, levelRanges);
			meshTilingScheme.DetermineTileRanges();
			numberOfLevels = meshTilingScheme.numberOfLevels;
			deepestLevel = meshTilingScheme.deepestLevel;
			tileRanges = new List<int4>(meshTilingScheme.tileRanges.GetRange(0,numberOfLevels));
			var nextTileFamily = new TileFamily(deepestLevel, numberOfLevels, new List<int4>(tileRanges.GetRange(0,numberOfLevels)), oldDeepestLevel, oldNumberOfLevels, new List<int4>(oldTileRanges.GetRange(0,oldNumberOfLevels)), midH, hRange, overallScaleFactor, zScaleFactor, projectName, elevationLoader, verticalScaleFactor, numberOfTextures, textureOrder, textureOpacities, pyramidLevels, texturePyramidLevels, textureVisibilityToggles);
			toBeRemoved = nextTileFamily.GenerateTileMeshes();
			tileFamily = nextTileFamily;
			Material sourceMaterial = GetComponent<MeshRenderer>().sharedMaterial;
			foreach (int i in Enumerable.Range(0, tileFamily.TileMeshes.Count))
			{
				GameObject tileObject = new GameObject(tileFamily.TileMeshes[i].name);
				TileID key = new TileID(tileFamily.TileMeshes[i].name);
				tileObjects.Add(key,tileObject);
				tileObject.transform.SetParent(transform, false);
				tileObject.AddComponent<MeshFilter>().sharedMesh = tileFamily.TileMeshes[i];
				MeshRenderer tileRenderer = tileObject.AddComponent<MeshRenderer>();
				if (sourceMaterial != null) {
					Material tileMaterial = new Material(sourceMaterial);
					tileMaterial.mainTexture = tileFamily.TileTextures[i];
					tileRenderer.sharedMaterial = tileMaterial;
				}			
				tileObjects[key].transform.localScale = new Vector3(1f, VE, 1f);	
				yield return null;
			}
			ReleaseCurrentTileAssets();		
			generationInProgress = false;	
			oldDeepestLevel = deepestLevel;
			oldNumberOfLevels = numberOfLevels;
			oldTileRanges = new List<int4>(tileRanges.GetRange(0,numberOfLevels));
		}

		void ReleaseCurrentTileAssets () {
			foreach (string fakeKey in toBeRemoved) {
				TileID key = new TileID(fakeKey);
				RemoveTile(key);
			}
		}

		void OnDestroy () {
			ReleaseCurrentTileAssets();
			if (roughTopography.IsCreated) {
				roughTopography.Dispose();
			}
		}

		void LoadProjectMetadata () {
			elevationInfo.LoadMetadata(Path.Combine("Projects", projectName,"dem_metadata.json"));
			texturesInfo.LoadMetadataFolder(projectName);
			DEMwidth = elevationInfo.width;
			DEMheight = elevationInfo.height;
			midH = elevationInfo.mid_h;
			hRange = elevationInfo.h_range;
			pyramidLevels = elevationInfo.pyramid_levels;
			texturePyramidLevels = texturesInfo.pyramidLevels;
			textureNativeLevels = texturesInfo.nativeLevels;
			textureNativeTilesX = texturesInfo.nativeTilesX;
			textureNativeTilesZ = texturesInfo.nativeTilesZ;
			textureNames = texturesInfo.textureNames;
			numberOfTextures = texturesInfo.numberOfTextures;
			textureVisibilityToggles = new List<bool>();
			textureVisibilityToggles.AddRange(Enumerable.Repeat(true,numberOfTextures));
			levelRanges = texturesInfo.levelRanges;
			maximumDescentLevel = pyramidLevels;
			if (texturePyramidLevels != null) {
				foreach (int textureLevel in texturePyramidLevels) {
					maximumDescentLevel = Math.Max(maximumDescentLevel, textureLevel);
				}
			}
			overallScaleFactor = Mathf.Pow(2,maximumDescentLevel-5);
			verticalScaleFactor = overallScaleFactor / (Mathf.Pow(2,pyramidLevels+7)*elevationInfo.dem_resolution_x);
			zScaleFactor = elevationInfo.dem_resolution_y / elevationInfo.dem_resolution_x;
			uiController = CreateLayerUI(projectName, textureOrder, textureNativeLevels, textureNativeTilesX, textureNativeTilesZ, textureVisibilityToggles, textureOpacities, textureNames, numberOfTextures, mainCamera);
		}

		public struct TileID {
		    public int level;
			public int x;
			public int z;

			public int hole0;
			public int hole1;
			public int hole2;
			public int hole3;
			public TileID(string levelxz)
			{
				string[] separateLevelXZ = levelxz.Split("/"); 
				this.level = int.Parse(separateLevelXZ[0]);
				this.x = int.Parse(separateLevelXZ[1]);
				this.z = int.Parse(separateLevelXZ[2]);
				this.hole0 = int.Parse(separateLevelXZ[3]);
				this.hole1 = int.Parse(separateLevelXZ[4]);
				this.hole2 = int.Parse(separateLevelXZ[5]);
				this.hole3 = int.Parse(separateLevelXZ[6]);
			}
		}

		public void RemoveTile(TileID id)
		{
			if (tileObjects.TryGetValue(id, out GameObject tile))
			{
				tileObjects.Remove(id);
				MeshRenderer tileRenderer = default;
				if (tile != null)
					tileRenderer = tile.GetComponent<MeshRenderer>();
					if (tileRenderer != null && tileRenderer.sharedMaterial != null) {
						Destroy(tileRenderer.sharedMaterial);
					}
					Destroy(tile);
			}
		}
	}
}