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
        public float minThresholdInAngle = 0.1f;    //2�߶μн���С�ڴ�ֵʱ�ɺϲ�(�Խϳ��߶�����)

        [SerializeField]
        public int minThresholdInPixels = 5;        //���������سߴ綨��Ϊ"С"�߶� 

        [SerializeField]
        public float maxThresholdInAngle = 22.0f;   //2�߶μн���С�ڴ�ֵʱ�ɺϲ�(�Խ϶��߶�����) 

        [SerializeField]
        public int maxThresholdInPixels = 15;       //���������سߴ綨��Ϊ"��"�߶� 

        [SerializeField]
        public int rootOffsetMode = 0;              //0: Mesh��ԭ����ͼƬ���½ǣ�1:ͼƬ���ĵ� 

        [SerializeField]
        public float onePixelSize = 0.01f;          //����1p�Ĵ�С����Ӱ��Mesh�ĳߴ� 

        [SerializeField]
        public int extrudePixelNum = 10;            //�����ɸ�������ͻ����Ե�ĳߴ� 

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

        static string storedData;                   //json���� 

        static Img2Poly window;

        string[] offsetMode = new string[] { "ͼƬ���½�", "ͼƬ����" };

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
            GUILayout.Label("�������·��:");
            input_path = EditorGUILayout.TextField("Input Path", input_path, GUILayout.Width(500));
            output_path = EditorGUILayout.TextField("Output Path", output_path, GUILayout.Width(500));

            GUILayout.Space(20);
            GUILayout.Label("Mesh�������:");
            rootOffsetMode = EditorGUILayout.Popup("Meshԭ����뵽", rootOffsetMode, offsetMode);
            GUILayout.Label("[ע]���ش�С����������Mesh�ĳߴ�");
            onePixelSize = EditorGUILayout.FloatField("�����صĳߴ�(��)", onePixelSize);
            extrudePixelNum = EditorGUILayout.IntSlider("��Ե�������(����) ", extrudePixelNum, -20, 20);

            GUILayout.Space(20);
            GUILayout.Label("���׼ȷ����ز���:");
            minThresholdInPixels = EditorGUILayout.IntField("������߶�(����)", minThresholdInPixels);
            maxThresholdInPixels = EditorGUILayout.IntField("���峤�߶�(����)", maxThresholdInPixels);
            minThresholdInAngle = EditorGUILayout.FloatField("�н�<��ֵʱ�ϲ�(���߶�)", minThresholdInAngle);
            maxThresholdInAngle = EditorGUILayout.FloatField("�н�<��ֵʱ�ϲ�(���߶�)", maxThresholdInAngle);

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


