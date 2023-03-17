using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Babeltime.Utils
{
    public class Img2Poly : EditorWindow
    {
        //TODO: 如何记录上一次的path修改? 
        [SerializeField]
        public string input_path = "Assets/Src/InputImgs/Batch_1";

        [SerializeField]
        public string output_path = "Assets/Src/Outputs/Batch_1";

        [MenuItem("Tool/Image To Polygon")]
        public static void GeneratePolygon()
        {
            GetWindow<Img2Poly>();
        }


        private void OnGUI()
        {
            input_path = EditorGUILayout.TextField("In Path", input_path, GUILayout.Width(700));
            output_path = EditorGUILayout.TextField("Out Path", output_path, GUILayout.Width(700));

            if (GUILayout.Button("Generate"))
            {
                StartProcessing(input_path, output_path);
            }
        }


        private void StartProcessing(string pathIn, string pathOut)
        {
            Img2PolyParser.Instance.LoadImgAssetInPath(pathIn);
            Img2PolyParser.Instance.ScheduleWork(pathOut);

        }


    }
}


