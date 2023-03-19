using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

using System.Collections.Concurrent;

namespace Babeltime.Utils
{ 
    public class Img2PolyParser {

        public static Img2PolyParser Instance = new Img2PolyParser();

        public static ConcurrentBag<List<Vector3>> datas = new ConcurrentBag<List<Vector3>>();  

        private static List<Texture2D> loadedTex2D = new List<Texture2D>();

        public static float minThresholdInAngle = 0.1f;    //2�߶μн���С�ڴ�ֵʱ�ɺϲ�(�Խϳ��߶�����)
        public static int minThresholdInPixels = 5;        //���������سߴ綨��Ϊ"С"�߶�
        public static float maxThresholdInAngle = 22.0f;   //2�߶μн���С�ڴ�ֵʱ�ɺϲ�(�Խ϶��߶�����)
        public static int maxThresholdInPixels = 15;       //���������سߴ綨��Ϊ"��"�߶� 
        public static int rootMode = 0;

        public static Vector3 MeshRoot = Vector3.zero;      //���Mesh������ƫ���� 

        private static float shiftedDelta
        {
            get { return (float)ShiftedPixel * OnePixelSize; }
        }

        private static int shiftedPixel = 0;
        public static int ShiftedPixel
        {
            get {
                if (shiftedPixel == 0)
                    return 10;
                return shiftedPixel; }
            set { shiftedPixel = value; }
        }

        private static float onePixelSize = 0;
        public static float OnePixelSize
        {
            get
            {
                if (onePixelSize <= 0)
                    return 0.01f;
                return onePixelSize;
            }
            set
            {
                onePixelSize = value;
            }
        }

        public void ScheduleWork(string aPathOut)
        {
            Debug.Log($"output path dir is: {aPathOut}");

            if (loadedTex2D.Count == 0)
            {
                Debug.LogWarning("ScheduleWork Fail, No src tex2d");
                return;
            }

            foreach (var tex in loadedTex2D)
            {
                //��Ҫ�ȸ�����offset 
                if (rootMode == 0) 
                    MeshRoot = Vector3.zero;
                else
                    MeshRoot = new Vector3(-tex.width * OnePixelSize * 0.5f, -tex.height * OnePixelSize * 0.5f, 0);

                //TODO: ���̴߳���Tex��һ���̸߳���һ��Tex 
                Work(tex, aPathOut);
            }
        }

        public void Work(Texture2D aTex, string aOutpath)
        {
            Debug.Log($"start process tex {aTex.name}");
            List<Vector3> baseOutline = null;

            //(1)��ȡͼƬ������Ե���γ�Polygon 
            //(1.1)��Ե��� 
            var detector = new OutlineDetector();
            detector.EatTexture(aTex);
            detector.Detect();
            detector.RetriveOutline(out baseOutline);
            //(1.2)��Ե�ϲ�/�Ż� 
            List<Vector3> refinedOutline = null;
            OutlinePostprocess.TryConbineSegments(baseOutline, out refinedOutline);

            //(2)�������������Mesh
            //(2.1)���������λ� 
            List<Vector3> tris = null;
            OutlinePostprocess.Triangulation(refinedOutline, null, out tris);
            //OutlinePostprocess.TriangulaitonHaze(refinedOutline, out tris); //���API���е�һ��ᱨ��:( 
            //(2.2)��������Mesh
            Mesh baseMesh = null;
            PolyMeshBuilder.BuildBaseMesh(tris, aTex, out baseMesh);
            //(2.3)����Mesh������
            PolyMeshBuilder.SaveMesh(baseMesh, aOutpath, baseMesh.name);

            //(3)������Ե������Mesh 
            //(3.1)��Shift������ 
            List<Vector3> shiftedOutline = null;
            OutlinePostprocess.ShiftOutlineBasedOnNormalDir(refinedOutline, shiftedDelta, out shiftedOutline);
            //(3.2)�����λ�
            List<Vector3> outer, inner;
            if (shiftedDelta > 0)
            {   //Ŀǰ�����ֶ��������Σ�������Ȧ���� (�Ժ����CDTʱ��Ҫ) 
                //refinedOutline.Reverse();  //������չ����Ӧ��Ȧ��refinedOutline���޸�ΪCW 
                outer = shiftedOutline;
                inner = refinedOutline;
            }
            else
            {
                //shiftedOutline.Reverse();   //������չ����Ȧ����shiftedOutline���޸�ΪCW 
                outer = refinedOutline;
                inner = shiftedOutline;
            }
            List<Vector3> tris2 = null;
            OutlinePostprocess.RingTriangulation(outer, inner, out tris2);
            //(3.3)��������Mesh
            Mesh outlineMesh = null;
            PolyMeshBuilder.BuildRingMesh(tris2, outer, inner, $"{baseMesh.name}_ring", out outlineMesh);
            //(3.4)����Mesh������
            PolyMeshBuilder.SaveMesh(outlineMesh, aOutpath, outlineMesh.name);

            //(4)ƴװprefab 
            PolyMeshBuilder.StoreAssetToPath(refinedOutline, aOutpath, baseMesh.name, outlineMesh.name);

            //datas.Add(tris);
            //datas.Add(tris2);  //quick show 
            detector.Reset();
        }

        public void LoadImgAssetInPath(string pathIn)
        {
            Debug.Log($"input path dir is: {pathIn}");

            var pngNames = GetFilesInfoList(pathIn);

            //bool flag = true;
            loadedTex2D.Clear();

            foreach (var name in pngNames)
            {
                //string fullpath = pathIn + name;
                string fullpath = $"{pathIn}/{name}";
                //Debug.Log(fullpath);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(fullpath);
                if (tex == null)
                {
                    Debug.LogWarning($"Fail to load tex at {fullpath}");
                }
                Debug.Log($"tex name {tex.name}");
                loadedTex2D.Add(tex);
            }
            
        }

        private List<string> GetFilesInfoList(string rootPath)
        {
            List<string> list = new List<string>();

            DirectoryInfo dirInfo = new DirectoryInfo(rootPath);
            FileSystemInfo[] filesInfo = dirInfo.GetFileSystemInfos();

            foreach (var item in filesInfo)
            {
                if (item.Attributes != FileAttributes.Directory)
                {
                    if (item.Extension == ".png")
                    {
                        list.Add(item.Name);
                    } 
                }
            }
            return list;
        }

    }

}