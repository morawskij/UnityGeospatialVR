using UnityEngine;
using System;
using Unity.Collections;

namespace ProceduralMeshes {

	public class ElevationMetadata {

        public int width=1;
        public int height=1;
        public float mid_h=0;
        public float h_range=0;
        public float VE = 1;
        public float dem_resolution_x=1;
        public float dem_resolution_y=1;
        public int pyramid_levels = 1;

        public void LoadMetadata(TextAsset metadataAsset) {
            LoadMetadataJson(metadataAsset.text);
        }

        public void LoadMetadata(string metadataPath) {
            LoadMetadataJson(System.IO.File.ReadAllText(metadataPath));
        }

        private void LoadMetadataJson(string json) {
            ElevationMetadata metadata = JsonUtility.FromJson<ElevationMetadata>(json);
            width = metadata.width;
            height = metadata.height;
            pyramid_levels = metadata.pyramid_levels;
            mid_h = metadata.mid_h;
            h_range = metadata.h_range;
            dem_resolution_x = metadata.dem_resolution_x;
            dem_resolution_y = metadata.dem_resolution_y;
        }

	}
}
