using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Babeltime.Utils
{ 
    public class Img2PolyParser {

        public static Img2PolyParser Instance = new Img2PolyParser();

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
                    works.Add(new OutlineDetector());
                    works[works.Count - 1].EatTexture(tex);
                    works[works.Count - 1].Detect();
                }
                
            }
            //TODO: 多线程处理Tex，一个线程负责一张Tex 


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