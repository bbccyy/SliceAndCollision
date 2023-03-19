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

        public static float minThresholdInAngle = 0.1f;    //2线段夹角若小于此值时可合并(对较长线段适用)
        public static int minThresholdInPixels = 5;        //该数量像素尺寸定义为"小"线段
        public static float maxThresholdInAngle = 22.0f;   //2线段夹角若小于此值时可合并(对较短线段适用)
        public static int maxThresholdInPixels = 15;       //该数量像素尺寸定义为"长"线段 
        public static int rootMode = 0;

        public static Vector3 MeshRoot = Vector3.zero;      //输出Mesh做整体偏移用 

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
                //需要先更新下offset 
                if (rootMode == 0) 
                    MeshRoot = Vector3.zero;
                else
                    MeshRoot = new Vector3(-tex.width * OnePixelSize * 0.5f, -tex.height * OnePixelSize * 0.5f, 0);

                //TODO: 多线程处理Tex，一个线程负责一张Tex 
                Work(tex, aPathOut);
            }
        }

        public void Work(Texture2D aTex, string aOutpath)
        {
            Debug.Log($"start process tex {aTex.name}");
            List<Vector3> baseOutline = null;

            //(1)提取图片轮廓边缘，形成Polygon 
            //(1.1)边缘检测 
            var detector = new OutlineDetector();
            detector.EatTexture(aTex);
            detector.Detect();
            detector.RetriveOutline(out baseOutline);
            //(1.2)边缘合并/优化 
            List<Vector3> refinedOutline = null;
            OutlinePostprocess.TryConbineSegments(baseOutline, out refinedOutline);

            //(2)构建并保存基础Mesh
            //(2.1)基础三角形化 
            List<Vector3> tris = null;
            OutlinePostprocess.Triangulation(refinedOutline, null, out tris);
            //OutlinePostprocess.TriangulaitonHaze(refinedOutline, out tris); //这个API运行到一半会报错:( 
            //(2.2)构建基本Mesh
            Mesh baseMesh = null;
            PolyMeshBuilder.BuildBaseMesh(tris, aTex, out baseMesh);
            //(2.3)保存Mesh到本地
            PolyMeshBuilder.SaveMesh(baseMesh, aOutpath, baseMesh.name);

            //(3)构建边缘轮廓带Mesh 
            //(3.1)先Shift轮廓线 
            List<Vector3> shiftedOutline = null;
            OutlinePostprocess.ShiftOutlineBasedOnNormalDir(refinedOutline, shiftedDelta, out shiftedOutline);
            //(3.2)三角形化
            List<Vector3> outer, inner;
            if (shiftedDelta > 0)
            {   //目前采用手动切三角形，无需内圈反向 (以后改用CDT时需要) 
                //refinedOutline.Reverse();  //向外扩展，对应内圈是refinedOutline，修改为CW 
                outer = shiftedOutline;
                inner = refinedOutline;
            }
            else
            {
                //shiftedOutline.Reverse();   //向内扩展，内圈就是shiftedOutline，修改为CW 
                outer = refinedOutline;
                inner = shiftedOutline;
            }
            List<Vector3> tris2 = null;
            OutlinePostprocess.RingTriangulation(outer, inner, out tris2);
            //(3.3)构建轮廓Mesh
            Mesh outlineMesh = null;
            PolyMeshBuilder.BuildRingMesh(tris2, outer, inner, $"{baseMesh.name}_ring", out outlineMesh);
            //(3.4)保存Mesh到本地
            PolyMeshBuilder.SaveMesh(outlineMesh, aOutpath, outlineMesh.name);

            //(4)拼装prefab 
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