using UnityEngine;
using System;
using Unity.Collections;

namespace ProceduralMeshes {

	public class Elevation {

        public int width=1;
        public int height=1;
        public NativeArray<float> values;
        public float mid_h=0;
        public float h_range=0;
        public float VE = 1;
        public float dem_resolution_x=1;
        public float dem_resolution_y=1;

        public void LoadMetadata(TextAsset metadataAsset) {
            ElevationMetadata metadata = JsonUtility.FromJson<ElevationMetadata>(metadataAsset.text);
            width = metadata.width;
            height = metadata.height;
            mid_h = metadata.mid_h;
            h_range = metadata.h_range;
            dem_resolution_x = metadata.dem_resolution_x;
            dem_resolution_y = metadata.dem_resolution_y;
        }

        public void Load(TextAsset elevationData, TextAsset elevationMetadata) {
            if (elevationData == null || elevationMetadata == null) {
                return;
            }

            LoadMetadata(elevationMetadata);
            byte[] bytes = elevationData.bytes;
            int sampleCount = width*height;

            if (bytes.Length != sampleCount * sizeof(float)) {
                throw new ArgumentException("Elevation data size does not match its dimensions.");
            }

            if (values.IsCreated) {
                values.Dispose();
            }

            values = new NativeArray<float>(sampleCount, Allocator.Persistent);
            for (int i = 0; i < sampleCount; i++) {
                values[i] = BitConverter.ToSingle(bytes, i * sizeof(float));
            }
        }

	}
}
