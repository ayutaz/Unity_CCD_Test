// Pcx - Point cloud importer & renderer for Unity
// https://github.com/keijiro/Pcx

using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Pcx
{
    [UnityEditor.AssetImporters.ScriptedImporter(1, "ply")]
    public class PlyImporter : UnityEditor.AssetImporters.ScriptedImporter
    {
        #region ScriptedImporter implementation

        public enum ContainerType { Mesh, ComputeBuffer, Texture  }

        [SerializeField] private ContainerType _containerType = ContainerType.Mesh;

        public override async void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext context)
        {
            if (_containerType == ContainerType.Mesh)
            {
                // Mesh container
                // Create a prefab with MeshFilter/MeshRenderer.
                var gameObject = new GameObject();
                var mesh = await DataImporter.ImportAsMesh(context.assetPath);

                var meshFilter = gameObject.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;

                var meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = GetDefaultMaterial();

                context.AddObjectToAsset("prefab", gameObject);
                if (mesh != null) context.AddObjectToAsset("mesh", mesh);

                context.SetMainObject(gameObject);
            }
            else if (_containerType == ContainerType.ComputeBuffer)
            {
                // ComputeBuffer container
                // Create a prefab with PointCloudRenderer.
                var gameObject = new GameObject();
                var data = await DataImporter.ImportAsPointCloudData(context.assetPath);

                var renderer = gameObject.AddComponent<PointCloudRenderer>();
                renderer.sourceData = data;

                context.AddObjectToAsset("prefab", gameObject);
                if (data != null) context.AddObjectToAsset("data", data);

                context.SetMainObject(gameObject);
            }
            else // _containerType == ContainerType.Texture
            {
                // Texture container
                // No prefab is available for this type.
                var data = await DataImporter.ImportAsBakedPointCloud(context.assetPath);
                if (data != null)
                {
                    context.AddObjectToAsset("container", data);
                    context.AddObjectToAsset("position", data.positionMap);
                    context.AddObjectToAsset("color", data.colorMap);
                    context.SetMainObject(data);
                }
            }
        }

        #endregion

        #region Internal utilities

        private static Material GetDefaultMaterial()
        {
            // Via package manager
            var path_upm = "Packages/jp.keijiro.pcx/Editor/Default Point.mat";
            // Via project asset database
            var path_prj = "Assets/Pcx/Editor/Default Point.mat";
            return AssetDatabase.LoadAssetAtPath<Material>(path_upm) ??
                   AssetDatabase.LoadAssetAtPath<Material>(path_prj);
        }

        #endregion
        #region Reader implementation
    }
    #endregion
}
