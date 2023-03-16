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

        public List<Vector3> data; //todo: delete it 

        public static float minThresholdInAngle = 5.0f;    //2�߶μн���С�ڴ�ֵʱ�ɺϲ�(�Խϳ��߶�����)
        public static int minThresholdInPixels = 5;        //���������سߴ綨��Ϊ"С"�߶�
        public static float maxThresholdInAngle = 30.0f;   //2�߶μн���С�ڴ�ֵʱ�ɺϲ�(�Խ϶��߶�����)
        public static int maxThresholdInPixels = 30;       //���������سߴ綨��Ϊ"��"�߶� 

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

        private List<OutlineDetector> works = new List<OutlineDetector>(0);
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
                //Debug.Log($"tex name {tex.name}");
                if (flag)
                {
                    flag = false;
                    //works.Add(new OutlineDetector());
                    //works[works.Count - 1].EatTexture(tex);
                    //works[works.Count - 1].Detect();
                    //works[works.Count - 1].RetriveOutline(out data);
                    Vector3 a1 = new Vector3(0, 0, 0);
                    Vector3 a2 = new Vector3(0.3f, 2, 0);
                    Vector3 b1 = new Vector3(0, 0, 0);
                    Vector3 b2 = new Vector3(-0.1f, 2, 0);
                    var theta = SimpleMath.SimpleMath.AngleOfSeg(a2 - a1, b2-b1);
                    var theta2 = Vector3.Angle(b2 - b1, a2 - a1);
                    Debug.Log($"theta = {theta}, theta2 = {theta2} in arcs");
                }


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