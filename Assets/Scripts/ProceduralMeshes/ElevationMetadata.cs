using UnityEngine;
using System;

namespace ProceduralMeshes {
    [System.Serializable]
    public class ElevationMetadata
    {
        public int width;
        public int height;
        public float mid_h;
        public float h_range;
        public float dem_resolution_x;
        public float dem_resolution_y;
    }
}