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
        public float midH;
        public float verticalScaleFactor;
        int maximumDescentLevel;

        int DEMwidth;
        int DEMheight;
        NativeArray<float> roughTopography;

        public CameraPositionBasedTiling(Camera mainCamera, float zScale, int maximumDescentLevel, float midH, NativeArray<float> roughTopography, float verticalScaleFactor) {
            this.mainCamera = mainCamera;
            this.zScale = zScale;
            this.maximumDescentLevel = maximumDescentLevel;
            this.midH = midH;
            this.roughTopography = roughTopography;
            this.verticalScaleFactor = verticalScaleFactor;
            tileRanges = new List<int4>(maximumDescentLevel+1);
            for (int level = 0; level <=maximumDescentLevel; level++) {
                tileRanges.Add(new int4((int)Math.Pow(2,maximumDescentLevel-level), (int)Math.Pow(2,maximumDescentLevel-level), 0, 0));
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
                float pixelSizeAtCameraHeight = (float)Math.Sqrt(Math.Pow(cameraHeight,2)+Math.Pow(cameraPosition.x-referencePoint.x,2)+Math.Pow(cameraPosition.z-referencePoint.y,2)) * pixelAngleFactor;
                deepestLevel = (int)Mathf.Min(Mathf.Max(Mathf.Ceil(Mathf.Log(1f/pixelSizeAtCameraHeight,2))-8, 0), maximumDescentLevel - 1);
            }
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
                float2 xRange = new float2(cameraPosition.x,cameraPosition.x);
                float2 zRange = new float2(cameraPosition.z,cameraPosition.z);

                for (int i = 0; i <= deepestLevel; i++) {
                    float levelTile = Mathf.Pow(2, - deepestLevel + i);
                    float levelPixel = levelTile/256f;
                    float levelRadius = levelPixel/pixelAngleFactor;
                    xRange = new float2(GetFinalDistance(xRange.x,0,levelRadius,true,false),GetFinalDistance(xRange.y,1,levelRadius,true,true));
                    zRange = new float2(GetFinalDistance(zRange.x,0,levelRadius,false,false),GetFinalDistance(zRange.y,zScale,levelRadius,false,true));
                    int2 xTileRange = new int2((int)(xRange.x / levelTile),(int)Mathf.Ceil(xRange.y / levelTile));
                    int2 zTileRange = new int2((int)(zRange.x / levelTile),(int)Mathf.Ceil(zRange.y / levelTile));
                    if (xTileRange.x == xTileRange.y)
                    {
                        float missingLower = xRange.x-xTileRange.x*levelTile;
                        float missingHigher = xTileRange.y*levelTile-xRange.y;
                        if (missingLower < missingHigher)
                        {
                            xTileRange = new int2(xTileRange.x-1,xTileRange.y);
                            xRange = new float2(xTileRange.x*levelTile,xRange.y);
                        }
                        else
                        {
                            xTileRange = new int2(xTileRange.x,xTileRange.y+1);
                            xRange = new float2(xRange.x,xTileRange.y*levelTile);                            
                        }
                    }
                    if (zTileRange.x == zTileRange.y)
                    {
                        float missingLower = zRange.x-zTileRange.x*levelTile;
                        float missingHigher = zTileRange.y*levelTile-zRange.y;
                        if (missingLower < missingHigher)
                        {
                            zTileRange = new int2(zTileRange.x-1,zTileRange.y);
                            zRange = new float2(zTileRange.x*levelTile,zRange.y);
                        }
                        else
                        {
                            zTileRange = new int2(zTileRange.x,zTileRange.y+1);
                            zRange = new float2(zRange.x,zTileRange.y*levelTile);                            
                        }
                    }
                    int4 rangeBounds = new int4(0,0,(int)Mathf.Pow(2, deepestLevel-i), (int)Mathf.Pow(2, deepestLevel-i));
                    /*int4 rangeBoundsAbsolute = new int4(0,0,(int)Mathf.Pow(2, deepestLevel-i), (int)Mathf.Pow(2, deepestLevel-i));
                    int4 rangeBoundsScreen = new int4((int)(minimumX/levelTile), (int)(minimumZ/levelTile), (int)Mathf.Ceil(maximumX/levelTile), (int)Mathf.Ceil(maximumZ/levelTile));
                    int4 rangeBounds = new int4(
                        Mathf.Max(rangeBoundsAbsolute.x, rangeBoundsScreen.x),
                        Mathf.Max(rangeBoundsAbsolute.y, rangeBoundsScreen.y),
                        Mathf.Min(rangeBoundsAbsolute.z, rangeBoundsScreen.z),
                        Mathf.Min(rangeBoundsAbsolute.w, rangeBoundsScreen.w)
                    );*/
                    tileRanges[i] = new int4((int)Mathf.Max(xTileRange.x, rangeBounds.x), (int)Mathf.Max(zTileRange.x, rangeBounds.y), (int)Mathf.Min(xTileRange.y, rangeBounds.z), (int)Mathf.Min(zTileRange.y, rangeBounds.w));
                    if (tileRanges[i].x == rangeBounds.x && tileRanges[i].y == rangeBounds.y && tileRanges[i].z == rangeBounds.z && tileRanges[i].w == rangeBounds.w)
                    {
                        numberOfLevels = i + 1;
                        break;
                    }
                }   
            }

        }
        int GetFromBool(bool b) => b ? 1 : 0;

        /*public float4 GetScreenBounds(Camera mainCamera)
        {
            for (int i=0; i<Screen.width; i++)
            {
                for (int j=0; j<)
            }
        }*/

        public float GetFinalDistance(float boundMin, float boundMax, float levelRadius, bool horizontal, bool increasing)
        {
            Vector3 cameraPosition = mainCamera.transform.position;
            float newPosition = (boundMin+boundMax)/2;
            float referencePosition = cameraPosition.x*GetFromBool(horizontal)+cameraPosition.z*GetFromBool(!horizontal);
            if (Mathf.Abs(boundMax - boundMin) < 0.0078f)
            {
                return newPosition;
            }
            else
            {
                float currentPlaneRadius = Mathf.Abs(referencePosition-newPosition);
                float2 foot = GetFootPosition(currentPlaneRadius,horizontal,increasing);
                float newPlaneRadius = GetPlaneRadius(levelRadius,cameraPosition.y-GetHeightAt(foot.x,foot.y));
                if (Mathf.Abs(newPlaneRadius - currentPlaneRadius) < 0.0039f)
                {
                    return newPosition;
                }
                else if (newPlaneRadius > currentPlaneRadius)
                {
                    return GetFinalDistance(newPosition,boundMax,levelRadius,horizontal,increasing);
                }
                else
                {
                    return GetFinalDistance(boundMin,newPosition,levelRadius,horizontal,increasing);
                }
            }
        }
        public float2 GetFootPosition (float planeRadius, bool horizontal, bool increasing)
        {
            return new float2(mainCamera.transform.position.x+GetFromBool(horizontal)*(2*GetFromBool(increasing)-1)*planeRadius, mainCamera.transform.position.z+GetFromBool(!horizontal)*(2*GetFromBool(increasing)-1)*planeRadius);
        }

        public float GetPlaneRadius(float levelRadius, float cameraHeight)
        {
            return Mathf.Sqrt(Mathf.Pow(levelRadius, 2) - Mathf.Pow(cameraHeight, 2));
        }

        public float GetHeightAt(float x, float z)
        {
            if (x<0 || x>1 || z<0 || z > zScale)
            {
                return 0f;
            }
            else
            {
                return verticalScaleFactor*(roughTopography[257*(int)(256*z/zScale)+(int)(256*x)]-midH);
            }
        }

    }
}