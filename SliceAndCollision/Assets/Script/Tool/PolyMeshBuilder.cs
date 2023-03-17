using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Babeltime.Utils
{
    public static class PolyMeshBuilder
    {

        private static Vector2 MappingUVtoTex(Vector3 aPos, Texture2D aTex)
        {
            Vector2 uv = new Vector2();

            float width = (float)aTex.width * Img2PolyParser.OnePixelSize;
            float height = (float)aTex.height * Img2PolyParser.OnePixelSize;

            uv.x = aPos.x / width;
            uv.y = aPos.y / height;

            return uv;
        }

        public static void BuildBaseMesh(List<Vector3> aTris, string aPath, Texture2D aTex, out Mesh output)
        {
            if (aTris == null || aTris.Count ==0 || aPath?.Length == 0)
            {
                Debug.Log("BuildMesh Fail");
                output = null;
                return;
            }

            string aName = aTex.name;

            Dictionary<Vector3, int> point2Idx = new Dictionary<Vector3, int>();

            output = new Mesh();

            List<Vector3> vertices = new List<Vector3>();

            List<int> submesh = new List<int>();

            List<Vector2> uv = new List<Vector2>();

            int verIdx = 0;

            for (int i = 0; i < aTris.Count; i+=3)
            {
                var p1 = aTris[i];
                var p2 = aTris[i+1];
                var p3 = aTris[i+2];

                if(!point2Idx.ContainsKey(p1)) 
                {  
                    point2Idx.Add(p1, verIdx++);
                    vertices.Add(p1);
                    uv.Add(MappingUVtoTex(p1, aTex));
                }
                submesh.Add(point2Idx[p1]);

                if (!point2Idx.ContainsKey(p2))
                {
                    point2Idx.Add(p2, verIdx++);
                    vertices.Add(p2);
                    uv.Add(MappingUVtoTex(p2, aTex));
                }
                submesh.Add(point2Idx[p2]);
                if (!point2Idx.ContainsKey(p3))
                {
                    point2Idx.Add(p3, verIdx++);
                    vertices.Add(p3);
                    uv.Add(MappingUVtoTex(p3, aTex));
                }
                submesh.Add(point2Idx[p3]);
            }

            output.SetVertices(vertices);
            output.SetTriangles(submesh, 0);
            output.SetUVs(0, uv);

            output.RecalculateBounds();

            output.name = aName;

            Debug.Log($"Save mesh {output.name} done!");
        }

        public static void SaveMesh(Mesh aMesh, string aPath, string aName)
        {
            AssetDatabase.CreateAsset(aMesh, $"{aPath}/{aName}.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void StoreAssetToPath(List<Vector3> aOutlines, string aPath, string aName)
        {
            var go = new GameObject();
            go.name = aName;
            var meshFilter = go.AddComponent<MeshFilter>();

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{aPath}/{aName}.asset");

            //meshFilter.sharedMesh = mesh;
            meshFilter.mesh = mesh;

            var meshRender = go.AddComponent<MeshRenderer>();

            Shader tmpShader = Shader.Find("Unlit/Texture");
            Material mat = new Material(tmpShader);
            //meshRender.SetMaterials(new List<Material>() { mat });
            meshRender.material = mat;

            var pc2D = go.AddComponent<PolygonCollider2D>();
            List<Vector2> points = new List<Vector2>();
            foreach(var p in aOutlines)
            {
                points.Add(new Vector2(p.x, p.y));
            }
            pc2D.points = points.ToArray();
            //pc2D.

            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            PrefabUtility.SaveAsPrefabAsset(go, $"{aPath}/{aName}.prefab");

            
        }


    }

}
