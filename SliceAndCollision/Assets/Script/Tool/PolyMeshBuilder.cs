using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Babeltime.Utils
{
    public static class PolyMeshBuilder
    {

        public static void BuildMesh(List<Vector3> aTris, string aPath, string aName)
        {
            if (aTris == null || aTris.Count ==0 || aPath?.Length == 0)
            {
                Debug.Log("BuildMesh Fail");
                return;
            }

            Dictionary<Vector3, int> point2Idx = new Dictionary<Vector3, int>();

            Mesh output = new Mesh();

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
                    uv.Add(new Vector2(0.5f, 0.5f));
                }
                submesh.Add(point2Idx[p1]);

                if (!point2Idx.ContainsKey(p2))
                {
                    point2Idx.Add(p2, verIdx++);
                    vertices.Add(p2);
                    uv.Add(new Vector2(0.5f, 0.5f));
                }
                submesh.Add(point2Idx[p2]);
                if (!point2Idx.ContainsKey(p3))
                {
                    point2Idx.Add(p3, verIdx++);
                    vertices.Add(p3);
                    uv.Add(new Vector2(0.5f, 0.5f));
                }
                submesh.Add(point2Idx[p3]);
            }

            output.SetVertices(vertices);
            output.SetTriangles(submesh, 0);
            output.SetUVs(0, uv);

            output.RecalculateBounds();

            output.name = aName;

            AssetDatabase.CreateAsset(output, $"{aPath}/{aName}.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Save mesh {output.name} done!");
        }


    }

}
