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

        public static void BuildRingMesh(List<Vector3> aTris, List<Vector3> aOutter, List<Vector3> aInner, string aName, out Mesh output)
        {
            if (aTris == null || aTris.Count == 0)
            {
                Debug.Log("BuildRingMesh Fail");
                output = null;
                return;
            }

            string name = aName;

            Vector3 offset = Img2PolyParser.MeshRoot;

            Dictionary<Vector3, int> point2Idx = new Dictionary<Vector3, int>();

            Dictionary<Vector3, float> point2UV = new Dictionary<Vector3, float>();
            foreach(var p in aOutter) { point2UV[p] = 1.0f; }
            foreach(var p in aInner) { point2UV[p] = 0f; }

            output = new Mesh();

            List<Vector3> vertices = new List<Vector3>();

            List<int> submesh = new List<int>();

            List<Vector2> uv = new List<Vector2>();

            int verIdx = 0;

            for (int i = 0; i < aTris.Count; i += 3)
            {
                var p1 = aTris[i];
                var p2 = aTris[i + 1];
                var p3 = aTris[i + 2];

                if (!point2Idx.ContainsKey(p1))
                {
                    point2Idx.Add(p1, verIdx++);
                    vertices.Add(p1 + offset);
                    float u = point2UV.GetValueOrDefault(p1, 0);
                    uv.Add(new Vector2(u, u));
                }
                submesh.Add(point2Idx[p1]);

                if (!point2Idx.ContainsKey(p2))
                {
                    point2Idx.Add(p2, verIdx++);
                    vertices.Add(p2 + offset);
                    float u = point2UV.GetValueOrDefault(p2, 0);
                    uv.Add(new Vector2(u, u));
                }
                submesh.Add(point2Idx[p2]);
                if (!point2Idx.ContainsKey(p3))
                {
                    point2Idx.Add(p3, verIdx++);
                    vertices.Add(p3 + offset);
                    float u = point2UV.GetValueOrDefault(p3, 0);
                    uv.Add(new Vector2(u, u));
                }
                submesh.Add(point2Idx[p3]);
            }

            output.SetVertices(vertices);
            output.SetTriangles(submesh, 0);
            output.SetUVs(0, uv);

            output.RecalculateBounds();

            output.name = name;

            Debug.Log($"Build ring mesh {output.name} done!");

        }

        public static void BuildBaseMesh(List<Vector3> aTris, Texture2D aTex, out Mesh output)
        {
            if (aTris == null || aTris.Count ==0)
            {
                Debug.Log("BuildMesh Fail");
                output = null;
                return;
            }

            string aName = aTex.name;

            Vector3 offset = Img2PolyParser.MeshRoot;

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
                    vertices.Add(p1 + offset);
                    uv.Add(MappingUVtoTex(p1, aTex));
                }
                submesh.Add(point2Idx[p1]);

                if (!point2Idx.ContainsKey(p2))
                {
                    point2Idx.Add(p2, verIdx++);
                    vertices.Add(p2 + offset);
                    uv.Add(MappingUVtoTex(p2, aTex));
                }
                submesh.Add(point2Idx[p2]);
                if (!point2Idx.ContainsKey(p3))
                {
                    point2Idx.Add(p3, verIdx++);
                    vertices.Add(p3 + offset);
                    uv.Add(MappingUVtoTex(p3, aTex));
                }
                submesh.Add(point2Idx[p3]);
            }

            output.SetVertices(vertices);
            output.SetTriangles(submesh, 0);
            output.SetUVs(0, uv);

            output.RecalculateBounds();

            output.name = aName;

            Debug.Log($"Build base mesh {output.name} done!");
        }

        public static void SaveMesh(Mesh aMesh, string aPath, string aName)
        {
            AssetDatabase.CreateAsset(aMesh, $"{aPath}/{aName}.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void StoreAssetToPath(List<Vector3> aOutlines, string aPath, string aBaseName, string aRingName)
        {
            //create base 
            var go = new GameObject();
            go.name = aBaseName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            //create ring 
            var ring = new GameObject();
            ring.name = aRingName;
            ring.transform.SetParent(go.transform);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.identity;
            ring.transform.localScale = Vector3.one;

            Shader tmpShader = Shader.Find("Unlit/Texture");
            Material mat = new Material(tmpShader);

            //setup ring mesh 
            var ringMeshFilter = ring.AddComponent<MeshFilter>();
            var ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{aPath}/{aRingName}.asset");
            ringMeshFilter.mesh = ringMesh;
            var ringMeshRender = ring.AddComponent<MeshRenderer>();
            ringMeshRender.material = mat;

            //setup base mesh 
            var meshFilter = go.AddComponent<MeshFilter>();
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{aPath}/{aBaseName}.asset");
            meshFilter.mesh = mesh;
            var meshRender = go.AddComponent<MeshRenderer>();
            meshRender.material = mat;

            //setup Polygon Collider 2D 
            var pc2D = go.AddComponent<PolygonCollider2D>();
            List<Vector2> points = new List<Vector2>();
            foreach(var p in aOutlines)
            {
                points.Add(new Vector2(p.x, p.y));
            }
            pc2D.points = points.ToArray();

            PrefabUtility.SaveAsPrefabAsset(go, $"{aPath}/{aBaseName}.prefab");
        }


    }

}
