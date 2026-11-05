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
    public class CameraPositionBasedTiling 
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
        NativeArray<float> roughTopography;

        public CameraPositionBasedTiling(Camera mainCamera, float zScale, string projectName, List<int> texturePyramidLevels, int numberOfTextures, int maximumDescentLevel, List<int> textureOrder, float minimumHeight, NativeArray<float> roughTopography) {
            this.mainCamera = mainCamera;
            this.zScale = zScale;
            this.projectName = projectName;
            this.texturePyramidLevels = texturePyramidLevels;
            this.numberOfTextures = numberOfTextures;
            this.maximumDescentLevel = maximumDescentLevel;
            this.textureOrder = textureOrder;
            this.minimumHeight = minimumHeight;
            this.roughTopography = roughTopography;
            tileRanges = new List<int4>(pyramidLevels);
            for (int level = 0; level < pyramidLevels; level++) {
                tileRanges.Add(new int4((int)Math.Pow(2,pyramidLevels-level-1), (int)Math.Pow(2,pyramidLevels-level-1), 0, 0));
            }

        }

        public int deepestLevel=0;
        public int numberOfLevels=1;
        public List<int4> tileRanges;

        public void DetermineTileRanges(float margin) {
            float pixelAngle = 2f * Mathf.Atan(
                Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                / mainCamera.pixelHeight
            );

            float pixelAngleFactor = 2f * Mathf.Tan(0.5f * pixelAngle);
            Vector3 cameraPosition = mainCamera.transform.position;
            Vector2 referencePoint = new Vector2(Math.Max(0,Math.Min(1,cameraPosition.x)),Math.Max(0,Math.Min(zScale,cameraPosition.z)));
            float cameraHeight = cameraPosition.y-GetHeightAt(referencePoint.x,referencePoint.y);
            if (cameraHeight <= 0) 
            {
                deepestLevel = 0;
            }
            else
            {
                float pixelSizeAtCameraHeight = cameraHeight * pixelAngleFactor;
                deepestLevel = (int)Mathf.Min(Mathf.Max(Mathf.Ceil(Mathf.Log(1f/pixelSizeAtCameraHeight,2))-8, 0), maximumDescentLevel - 1);
            }
            Debug.Log("Deepest level");
            Debug.Log(deepestLevel);
            if (deepestLevel == 0) {
                numberOfLevels = 1;
                tileRanges[0] = new int4(0, 0, 1, 1);
            }
            else
            {
                Vector3 worldBottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(-margin, -margin,cameraHeight));
                Vector3 worldBottomRight = mainCamera.ViewportToWorldPoint(new Vector3(1+margin, -margin,cameraHeight));
                Vector3 worldTopLeft = mainCamera.ViewportToWorldPoint(new Vector3(-margin, 1+margin,cameraHeight));
                Vector3 worldTopRight = mainCamera.ViewportToWorldPoint(new Vector3(1+margin, 1+margin,cameraHeight));
                float minimumX = Mathf.Min(Mathf.Min(worldBottomLeft.x, worldBottomRight.x), Mathf.Min(worldTopLeft.x, worldTopRight.x));
                float minimumZ = Mathf.Min(Mathf.Min(worldBottomLeft.z, worldBottomRight.z), Mathf.Min(worldTopLeft.z, worldTopRight.z));
                float maximumX = Mathf.Max(Mathf.Max(worldBottomLeft.x, worldBottomRight.x), Mathf.Max(worldTopLeft.x, worldTopRight.x));
                float maximumZ = Mathf.Max(Mathf.Max(worldBottomLeft.z, worldBottomRight.z), Mathf.Max(worldTopLeft.z, worldTopRight.z));
                for (int i = 0; i <= deepestLevel; i++) {
                    float levelPixel = Mathf.Pow(2, deepestLevel - i - 1);
                    float levelRadius = levelPixel/pixelAngleFactor;
                    float planeRadius = Mathf.Sqrt(Mathf.Pow(levelRadius, 2) - Mathf.Pow(cameraHeight, 2));
                    float2 xRange = new float2(cameraPosition.x - planeRadius, cameraPosition.x + planeRadius);
                    float2 zRange = new float2(cameraPosition.z - planeRadius, cameraPosition.z + planeRadius);
                    int4 rangeBoundsAbsolute = new int4(0,0,(int)Mathf.Pow(2, deepestLevel-i), (int)Mathf.Pow(2, deepestLevel-i));
                    int4 rangeBoundsScreen = new int4((int)(minimumX/levelPixel), (int)(minimumZ/levelPixel), (int)Mathf.Ceil(maximumX/levelPixel), (int)Mathf.Ceil(maximumZ/levelPixel));
                    int4 rangeBounds = new int4(
                        Mathf.Max(rangeBoundsAbsolute.x, rangeBoundsScreen.x),
                        Mathf.Max(rangeBoundsAbsolute.y, rangeBoundsScreen.y),
                        Mathf.Min(rangeBoundsAbsolute.z, rangeBoundsScreen.z),
                        Mathf.Min(rangeBoundsAbsolute.w, rangeBoundsScreen.w)
                    );
                    tileRanges[i] = new int4((int)(Mathf.Max(xRange.x, rangeBounds.x)/levelPixel), (int)(Mathf.Max(zRange.x, rangeBounds.y)/levelPixel), (int)(Mathf.Ceil(Mathf.Min(xRange.y, rangeBounds.z)/levelPixel)), (int)(Mathf.Ceil(Mathf.Min(zRange.y, rangeBounds.w)/levelPixel)));
                    if (tileRanges[i].x == rangeBounds.x && tileRanges[i].y == rangeBounds.y && tileRanges[i].z == rangeBounds.z && tileRanges[i].w == rangeBounds.w)
                    {
                        numberOfLevels = i + 1;
                        break;
                    }
                }   
            }

        }

        public float GetHeightAt(float x, float z)
        {
            if (x<0 || x>1 || z<0 || z > zScale)
            {
                return 0f;
            }
            else
            {
                return roughTopography[256*((int)(256*z/zScale)+(int)(256*x))];
            }
        }

    }
}