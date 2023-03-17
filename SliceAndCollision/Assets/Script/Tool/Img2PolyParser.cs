using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

using Babeltime.SimpleMath;
using Unity.VisualScripting;
using static Unity.IO.LowLevel.Unsafe.AsyncReadManagerMetrics;
using UnityEngine.UIElements;
using System.Collections.Concurrent;

namespace Babeltime.Utils
{ 
    public class Img2PolyParser {

        public static Img2PolyParser Instance = new Img2PolyParser();

        public ConcurrentBag<List<Vector3>> datas = new ConcurrentBag<List<Vector3>>();  

        private List<Texture2D> loadedTex2D = new List<Texture2D>();

        public static float minThresholdInAngle = 0.1f;    //2�߶μн���С�ڴ�ֵʱ�ɺϲ�(�Խϳ��߶�����)
        public static int minThresholdInPixels = 5;        //���������سߴ綨��Ϊ"С"�߶�
        public static float maxThresholdInAngle = 22.0f;   //2�߶μн���С�ڴ�ֵʱ�ɺϲ�(�Խ϶��߶�����)
        public static int maxThresholdInPixels = 15;       //���������سߴ綨��Ϊ"��"�߶� 

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
                //TODO: ���̴߳���Tex��һ���̸߳���һ��Tex 
                Work(tex, aPathOut);
            }
        }

        public void Work(Texture2D aTex, string aOutpath)
        {
            Debug.Log($"start process tex {aTex.name}");
            List<Vector3> data;

            //��Ե��� 
            var detector = new OutlineDetector();
            detector.EatTexture(aTex);
            detector.Detect();
            detector.RetriveOutline(out data);

            //��Ե�ϲ�/�Ż� 
            List<Vector3> outputs;
            OutlinePostprocess.TryConbineSegments(data, out outputs);

            //���������λ� 
            List<Vector3> tris;
            OutlinePostprocess.Triangulation(outputs, null, out tris);

            //����ΪMesh
            PolyMeshBuilder.BuildMesh(tris, aOutpath, aTex.name);

            datas.Add(tris);
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