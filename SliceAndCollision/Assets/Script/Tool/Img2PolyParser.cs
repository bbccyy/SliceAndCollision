using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

using Babeltime.SimpleMath;

namespace Babeltime.Utils
{ 
    public class Img2PolyParser {

        public static Img2PolyParser Instance = new Img2PolyParser();

        public List<List<Vector3>> datas = new List<List<Vector3>>(); //todo: delete it 

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

        private List<OutlineDetector> workers = new List<OutlineDetector>(0);
        public void LoadImgAssetInPath(string pathIn)
        {
            Debug.Log($"input path dir is: {pathIn}");

            var pngNames = GetFilesInfoList(pathIn);

            bool flag = true;

            foreach(var name in pngNames)
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
                //if (tex.name == "11_0008_2")
                if (flag)
                {
                    //flag = false;
                    
                    var data = new List<Vector3>();
                    workers.Add(new OutlineDetector());
                    workers[workers.Count - 1].EatTexture(tex);
                    workers[workers.Count - 1].Detect();
                    workers[workers.Count - 1].RetriveOutline(out data);

                    List<Vector3> outputs;
                    OutlinePostprocess.TryConbineSegments(data, out outputs);

                    data = outputs;

                    datas.Add(data);
                }

                foreach (var wk in workers)
                {
                    wk.Reset();
                }
                workers.Clear();

            }
            //TODO: ���̴߳���Tex��һ���̸߳���һ��Tex 


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