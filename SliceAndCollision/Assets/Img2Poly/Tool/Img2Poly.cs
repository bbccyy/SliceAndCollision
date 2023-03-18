using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.PackageManager.UI;
using Unity.VisualScripting;

namespace Babeltime.Utils
{
    public class Img2Poly : EditorWindow
    {
        private static string JSON_PATH = Application.dataPath + "/StreamingFile";
        private static string JSON_NAME = "/Img2PolySettings.json";

        [SerializeField]
        public string input_path = "Assets/Src/InputImgs/Batch_1";

        [SerializeField]
        public string output_path = "Assets/Src/Outputs/Batch_1";

        [SerializeField]
        public float minThresholdInAngle = 0.1f;    //2线段夹角若小于此值时可合并(对较长线段适用)

        [SerializeField]
        public int minThresholdInPixels = 5;        //该数量像素尺寸定义为"小"线段 

        [SerializeField]
        public float maxThresholdInAngle = 22.0f;   //2线段夹角若小于此值时可合并(对较短线段适用) 

        [SerializeField]
        public int maxThresholdInPixels = 15;       //该数量像素尺寸定义为"长"线段 

        [SerializeField]
        public int rootOffsetMode = 0;              //0: Mesh的原点在图片左下角，1:图片中心点 

        [SerializeField]
        public float onePixelSize = 0.01f;          //定义1p的大小，会影响Mesh的尺寸 

        [SerializeField]
        public int extrudePixelNum = 10;            //可正可负，定义突出边缘的尺寸 

        private void SyncParams()
        {
            Img2PolyParser.minThresholdInPixels = minThresholdInPixels;
            Img2PolyParser.maxThresholdInPixels = maxThresholdInPixels;
            Img2PolyParser.minThresholdInAngle = minThresholdInAngle;
            Img2PolyParser.maxThresholdInAngle = maxThresholdInAngle;
            Img2PolyParser.rootMode = rootOffsetMode;
            Img2PolyParser.OnePixelSize = onePixelSize;
            Img2PolyParser.ShiftedPixel = extrudePixelNum;
        }

        static string storedData;                   //json数据 

        static Img2Poly window;

        string[] offsetMode = new string[] { "图片左下角", "图片中心" };

        [MenuItem("Tool/Image To Polygon")]
        public static void GeneratePolygon()
        {
            window = GetWindow<Img2Poly>(false, "Image to Polygon", false);
            window.position = new Rect(new Vector2(500, 200), new Vector2(500, 500));
            window.Show();

            if (storedData != null)
                JsonUtility.FromJsonOverwrite(storedData, window);
        }


        private void OnGUI()
        {
            GUILayout.Space(20);
            GUILayout.Label("输入输出路径:");
            input_path = EditorGUILayout.TextField("Input Path", input_path, GUILayout.Width(500));
            output_path = EditorGUILayout.TextField("Output Path", output_path, GUILayout.Width(500));

            GUILayout.Space(20);
            GUILayout.Label("Mesh属性相关:");
            rootOffsetMode = EditorGUILayout.Popup("Mesh原点对齐到", rootOffsetMode, offsetMode);
            GUILayout.Label("[注]像素大小决定了生成Mesh的尺寸");
            onePixelSize = EditorGUILayout.FloatField("单像素的尺寸(米)", onePixelSize);
            extrudePixelNum = EditorGUILayout.IntSlider("边缘轮廓宽度(像素) ", extrudePixelNum, -20, 20);

            GUILayout.Space(20);
            GUILayout.Label("描边准确性相关参数:");
            minThresholdInPixels = EditorGUILayout.IntField("定义短线段(像素)", minThresholdInPixels);
            maxThresholdInPixels = EditorGUILayout.IntField("定义长线段(像素)", maxThresholdInPixels);
            minThresholdInAngle = EditorGUILayout.FloatField("夹角<此值时合并(短线段)", minThresholdInAngle);
            maxThresholdInAngle = EditorGUILayout.FloatField("夹角<此值时合并(长线段)", maxThresholdInAngle);

            GUILayout.Space(20);
            if (GUILayout.Button("Generate", GUILayout.Width(100), GUILayout.Height(50)))
            {
                StartProcessing(input_path, output_path);
            }
        }

        private void StartProcessing(string pathIn, string pathOut)
        {
            SyncParams();
            Img2PolyParser.Instance.LoadImgAssetInPath(pathIn);
            Img2PolyParser.Instance.ScheduleWork(pathOut);
        }


        public void OnEnable()
        {
            storedData = load();
        }

        public void OnDisable()
        {
            var data = JsonUtility.ToJson(window, true);
            save(data);
        }

        void save(string aData)
        {
            if (!Directory.Exists(JSON_PATH))
                Directory.CreateDirectory(JSON_PATH);

            StreamWriter sw = new StreamWriter(JSON_PATH + JSON_NAME);
            sw.WriteLine(aData);
            sw.Close();
        }

        string load()
        {
            string js = null;
            if (File.Exists(JSON_PATH + JSON_NAME))
            {
                StreamReader sr = new StreamReader(JSON_PATH + JSON_NAME);

                js = sr.ReadToEnd();
                sr.Close();
            }
            return js;  
        }


    }
}


