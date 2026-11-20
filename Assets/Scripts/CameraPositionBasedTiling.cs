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
        public float upperZBound;
        public float midH;
        public float verticalScaleFactor;
        public float overallScaleFactor;
        int maximumDescentLevel;

        int DEMwidth;
        int DEMheight;
        NativeArray<float> roughTopography;

        public CameraPositionBasedTiling(Camera mainCamera, float zScale, int maximumDescentLevel, float midH, NativeArray<float> roughTopography, float overallScaleFactor, float verticalScaleFactor) {
            this.mainCamera = mainCamera;
            this.zScale = zScale;
            this.upperZBound = zScale*overallScaleFactor;
            this.maximumDescentLevel = maximumDescentLevel;
            this.midH = midH;
            this.roughTopography = roughTopography;
            this.overallScaleFactor = overallScaleFactor;
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
            Vector2 referencePoint = new Vector2(Math.Max(0,Math.Min(overallScaleFactor,cameraPosition.x)),Math.Max(0,Math.Min(upperZBound,cameraPosition.z)));
            float referenceHeight = GetHeightAt(referencePoint.x,referencePoint.y);
            float cameraHeight = cameraPosition.y-referenceHeight;
            if (cameraHeight <= 0) 
            {
                deepestLevel = 0;
            }
            else
            {
                float pixelSizeAtCameraHeight = (float)Math.Sqrt(Math.Pow(cameraHeight,2)+Math.Pow(cameraPosition.x-referencePoint.x,2)+Math.Pow(cameraPosition.z-referencePoint.y,2)) * pixelAngleFactor;
                deepestLevel = (int)Mathf.Min(Mathf.Max(Mathf.Ceil(Mathf.Log(overallScaleFactor/pixelSizeAtCameraHeight,2))-8, 0), maximumDescentLevel - 1);
            }
            if (deepestLevel == 0) {
                numberOfLevels = 1;
                tileRanges[0] = new int4(0, 0, 1, 1);
            }
            else
            {
                float4 screenBounds = GetScreenBounds(mainCamera);
                Debug.Log(screenBounds);
                float2 xRange = new float2(cameraPosition.x,cameraPosition.x);
                float2 zRange = new float2(cameraPosition.z,cameraPosition.z);
                int levelsToIgnore=0;
                for (int i = 0; i <= deepestLevel; i++) {
                    float levelTile = overallScaleFactor*Mathf.Pow(2, - deepestLevel + i);
                    float levelTileZ = levelTile*zScale;
                    float levelPixel = levelTile/256f;
                    float levelRadius = levelPixel/pixelAngleFactor;
                    xRange = new float2(GetFinalDistance(xRange.x,0,levelRadius,true,false),GetFinalDistance(xRange.y,overallScaleFactor,levelRadius,true,true));
                    zRange = new float2(GetFinalDistance(zRange.x,0,levelRadius,false,false),GetFinalDistance(zRange.y,upperZBound,levelRadius,false,true));
                    int2 xTileRange = new int2((int)(xRange.x / levelTile),(int)Mathf.Ceil(xRange.y / levelTile));
                    int2 zTileRange = new int2((int)(zRange.x / levelTileZ),(int)Mathf.Ceil(zRange.y / levelTileZ));
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
                        float missingLower = zRange.x-zTileRange.x*levelTileZ;
                        float missingHigher = zTileRange.y*levelTileZ-zRange.y;
                        if (missingLower < missingHigher)
                        {
                            zTileRange = new int2(zTileRange.x-1,zTileRange.y);
                            zRange = new float2(zTileRange.x*levelTileZ,zRange.y);
                        }
                        else
                        {
                            zTileRange = new int2(zTileRange.x,zTileRange.y+1);
                            zRange = new float2(zRange.x,zTileRange.y*levelTileZ);                            
                        }
                    }
                    int4 rangeBounds = new int4((int)(screenBounds.x/levelTile), (int)(screenBounds.y/levelTileZ), (int)Mathf.Ceil(screenBounds.z/levelTile), (int)Mathf.Ceil(screenBounds.w/levelTileZ));
                    tileRanges[i] = new int4((int)Mathf.Max(xTileRange.x, rangeBounds.x), (int)Mathf.Max(zTileRange.x, rangeBounds.y), (int)Mathf.Min(xTileRange.y, rangeBounds.z), (int)Mathf.Min(zTileRange.y, rangeBounds.w));
                    if (tileRanges[i].z<=tileRanges[i].x || tileRanges[i].w <= tileRanges[i].y)
                    {
                        levelsToIgnore++;
                    }
                    if (tileRanges[i].x <= rangeBounds.x && tileRanges[i].y <= rangeBounds.y && tileRanges[i].z >= rangeBounds.z && tileRanges[i].w >= rangeBounds.w)
                    {
                        numberOfLevels = i + 1;
                        break;
                    }
                }   
                if (levelsToIgnore > 0)
                {
                    deepestLevel-=levelsToIgnore;
                    numberOfLevels-=levelsToIgnore;
                    tileRanges.InsertRange(0,tileRanges.GetRange(levelsToIgnore,numberOfLevels));
                }
            }
        }
        int GetFromBool(bool b) => b ? 1 : 0;

        public float4 GetScreenBounds(Camera mainCamera)
        {
            float precision = 1f/Screen.height;
            float minimumX = overallScaleFactor;
            float maximumX = 0f;
            float minimumZ = upperZBound;
            float maximumZ = 0f;
            Plane plane = new Plane(Vector3.up, -midH*verticalScaleFactor);
            for (int i=0; i<(int)Mathf.Ceil(1/precision); i++)
            {
                Ray ray1 = mainCamera.ViewportPointToRay(new Vector3(0f, i*precision, 0f));
                Ray ray2 = mainCamera.ViewportPointToRay(new Vector3(1f, i*precision, 0f));
                if (plane.Raycast(ray1, out float distance))
                {
                    Vector3 intersection = ray1.GetPoint(distance);
                    minimumX = Mathf.Min(minimumX,intersection.x);
                    maximumX = Mathf.Max(maximumX,intersection.x);
                    minimumZ = Mathf.Min(minimumZ,intersection.z);
                    maximumZ = Mathf.Max(maximumZ,intersection.z);
                }
                if (plane.Raycast(ray2, out float distance2))
                {
                    Vector3 intersection = ray2.GetPoint(distance2);
                    minimumX = Mathf.Min(minimumX,intersection.x);
                    maximumX = Mathf.Max(maximumX,intersection.x);
                    minimumZ = Mathf.Min(minimumZ,intersection.z);
                    maximumZ = Mathf.Max(maximumZ,intersection.z);
                }
                if (minimumX<=0f && maximumX >= overallScaleFactor && minimumZ<=0f && maximumZ >= upperZBound)
                {
                    break;
                }
            }
            precision = 1f/Screen.width;
            for (int i=1; i<(int)Mathf.Ceil(1/precision)-1; i++)
            {
                Ray ray1 = mainCamera.ViewportPointToRay(new Vector3(i*precision, 0f, 0f));
                Ray ray2 = mainCamera.ViewportPointToRay(new Vector3(i*precision, 1f, 0f));
                if (plane.Raycast(ray1, out float distance3))
                {
                    Vector3 intersection = ray1.GetPoint(distance3);
                    minimumX = Mathf.Min(minimumX,intersection.x);
                    maximumX = Mathf.Max(maximumX,intersection.x);
                    minimumZ = Mathf.Min(minimumZ,intersection.z);
                    maximumZ = Mathf.Max(maximumZ,intersection.z);
                }
                if (plane.Raycast(ray2, out float distance4))
                {
                    Vector3 intersection = ray2.GetPoint(distance4);
                    minimumX = Mathf.Min(minimumX,intersection.x);
                    maximumX = Mathf.Max(maximumX,intersection.x);
                    minimumZ = Mathf.Min(minimumZ,intersection.z);
                    maximumZ = Mathf.Max(maximumZ,intersection.z);
                }
                if (minimumX<=0f && maximumX >= overallScaleFactor && minimumZ<=0f && maximumZ >= upperZBound)
                {
                    break;
                }
            }
            return new float4(Mathf.Max(0f,minimumX),Mathf.Max(0f,minimumZ),Mathf.Min(overallScaleFactor,maximumX),Mathf.Min(upperZBound,maximumZ));
            //return new float4(0f,0f,overallScaleFactor,upperZBound);
        }

        public float GetFinalDistance(float boundMin, float boundMax, float levelRadius, bool horizontal, bool increasing)
        {
            Vector3 cameraPosition = mainCamera.transform.position;
            float newPosition = (boundMin+boundMax)/2;
            float referencePosition = cameraPosition.x*GetFromBool(horizontal)+cameraPosition.z*GetFromBool(!horizontal);
            if (Mathf.Abs(boundMax - boundMin) < 0.0078f*overallScaleFactor)
            {
                return newPosition;
            }
            else
            {
                float currentPlaneRadius = Mathf.Abs(referencePosition-newPosition);
                float2 foot = GetFootPosition(currentPlaneRadius,horizontal,increasing);
                float newPlaneRadius = GetPlaneRadius(levelRadius,cameraPosition.y-GetHeightAt(foot.x,foot.y));
                if (Mathf.Abs(newPlaneRadius - currentPlaneRadius) < 0.0039f*overallScaleFactor)
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
            if (x<0 || x>overallScaleFactor || z<0 || z > upperZBound)
            {
                return 0f;
            }
            else
            {
                return verticalScaleFactor*(roughTopography[257*(int)(256*z/upperZBound)+(int)(256*x/overallScaleFactor)]-midH);
            }
        }

    }
}