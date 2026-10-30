using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;

namespace ProceduralMeshes {

	public class AllTexturesMetadata {

        public int numberOfTextures = 0;
        public List<int> pyramidLevels;
        public List<string> textureNames;

        public void LoadMetadataFolder(string projectName) {
            string textureMetadataPattern = $"texture*metadata.json";
            List<string> metadataFiles = new List<string>(Directory.GetFiles($"Assets/StreamingAssets/Projects/{projectName}", textureMetadataPattern));
            numberOfTextures = metadataFiles.Count;
            pyramidLevels = new List<int>(numberOfTextures);
            textureNames = new List<string>(numberOfTextures);
            for (int i = 0; i<numberOfTextures; i++)
            {
                TextureMetadata currentTextureMetadata = new TextureMetadata();
                currentTextureMetadata.TextureLoadMetadata($"Assets/StreamingAssets/Projects/{projectName}/texture_{i.ToString()}_metadata.json");
                textureNames.Add(currentTextureMetadata.name);
                pyramidLevels.Add(currentTextureMetadata.pyramid_levels);             
            }
        }

	}

    public class TextureMetadata
    {
        public string name;
        public int pyramid_levels;

        public void TextureLoadMetadata(TextAsset metadataAsset) {
            TextureLoadMetadataJson(metadataAsset.text);
        }

        public void TextureLoadMetadata(string metadataPath) {
            TextureLoadMetadataJson(System.IO.File.ReadAllText(metadataPath));
        }

        private void TextureLoadMetadataJson(string json) {
            TextureMetadata metadata = JsonUtility.FromJson<TextureMetadata>(json);
            name = metadata.name;
            pyramid_levels = metadata.pyramid_levels;
        }


    }
}
