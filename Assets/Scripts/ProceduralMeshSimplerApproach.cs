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
		[NonSerialized] float validatedVE;
		[NonSerialized] bool hasValidatedVE;

        List<int> texturePyramidLevels;
        List<string> textureNames;
		int numberOfTextures;
		int maximumDescentLevel;
		Vector3 cameraPosition;
		Quaternion cameraRotation;
		TileFamily tileFamily;
		List<GameObject> tileObjects = new List<GameObject>();

		int generateRarely = 50;
		int generateCounter = 50;

		void Awake () {
			elevationInfo = new ElevationMetadata();
			texturesInfo = new AllTexturesMetadata();
			cameraPosition = mainCamera.transform.position;
			cameraRotation = mainCamera.transform.rotation;
			LoadProjectMetadata();
			elevationLoader = new ExternalElevationLoader();
			roughTopography = elevationLoader.LoadTile(projectName,0,0,0,midH);
		}

		void Start () => regenerationRequested = true;

		void OnValidate () {
			if (!hasValidatedVE || VE != validatedVE) {
				for (int i = 0; i < tileObjects.Count; i++) {
					if (tileObjects[i] != null) {
						tileObjects[i].transform.localScale = new Vector3(1f, VE, 1f);
					}
				}
				validatedVE = VE;
				if (!hasValidatedVE) {
					regenerationRequested = true;
					hasValidatedVE = true;
				}
				return;
			}
			generateCounter=50;
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
			LoadProjectMetadata();

			generationInProgress = true;
			StartCoroutine(GenerateMeshWhenTilesAreLoaded());
		}

		IEnumerator GenerateMeshWhenTilesAreLoaded () {
			//meshTilingScheme = new CameraPositionBasedTiling(mainCamera, zScaleFactor, projectName, texturePyramidLevels, numberOfTextures, maximumDescentLevel, textureOrder, -midH, roughTopography);
			//meshTilingScheme.DetermineTileRanges(0.3f);
			if (generateCounter >= generateRarely)
			{
				tileFamily = new TileFamily(deepestLevel, numberOfLevels, tileRanges, midH, hRange, zScaleFactor, projectName, elevationLoader, verticalScaleFactor, numberOfTextures,textureOrder, textureOpacities, pyramidLevels);
				tileFamily.GenerateTileMeshes();
				CreateTileObjects();		
				generateCounter = 0;		
			}
			else
			{
				generationInProgress = false;
				generateCounter++;
			}
			yield break;
		}

		void CreateTileObjects () {
			Material sourceMaterial = GetComponent<MeshRenderer>().sharedMaterial;
			for (int i = 0; i < tileObjects.Count; i++) {
				if (tileObjects[i] != null) {
					Destroy(tileObjects[i]);
				}
			}
			tileObjects.Clear();

			for (int i = 0; i < tileFamily.TileMeshes.Count; i++) {
				GameObject tileObject = new GameObject(tileFamily.TileMeshes[i].name);
				tileObjects.Add(tileObject);
				tileObject.transform.SetParent(transform, false);
				tileObject.AddComponent<MeshFilter>().sharedMesh = tileFamily.TileMeshes[i];
				MeshRenderer tileRenderer = tileObject.AddComponent<MeshRenderer>();
				//tileObject.MeshFilter.RecalculateNormals();
				if (sourceMaterial != null) {
					Material tileMaterial = new Material(sourceMaterial);
					tileMaterial.mainTexture = tileFamily.TileTextures[i];
					tileRenderer.sharedMaterial = tileMaterial;
				}
			}
			generationInProgress = false;
		}

		void LoadProjectMetadata () {
			elevationInfo.LoadMetadata(Path.Combine("Projects", projectName,"dem_metadata.json"));
			texturesInfo.LoadMetadataFolder(projectName);
			DEMwidth = elevationInfo.width;
			DEMheight = elevationInfo.height;
			verticalScaleFactor = 1f / (256*elevationInfo.dem_resolution_x);
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
			/*deepestLevel = pyramidLevels-1;
			numberOfLevels = pyramidLevels;
            tileRanges = new List<int4>(pyramidLevels);
            for (int level = 0; level < pyramidLevels; level++) {
                tileRanges.Add(new int4(0, 0, 0, 0));		
			}	*/
		}
	}
}