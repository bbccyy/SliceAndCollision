using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Babeltime.Utils
{
    //[ExecuteInEditMode]
    public class Img2PolyDebuger : MonoBehaviour
    {
        static Color[] ColLut = new Color[] { Color.blue, Color.green, Color.red, Color.white, Color.black};

        public Mesh mesh;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

            var datas = Img2PolyParser.Instance.datas;

            if (datas != null)
            {
                for (int i = 0; i < datas.Count; ++i)
                {
                    var col = ColLut[i % ColLut.Length];
                    var data = datas.ElementAt(i);

                    //DrawLine(data);
                    DrawTris(data, col);
                }
            }

            Debug.DrawLine(new Vector3(1,0,0), new Vector3(-1,0,0), Color.red);
        }

        public static void DrawTris(List<Vector3> aTris, Color aCol)
        {
            if (aTris == null || aTris.Count == 0) return;

            for (int i = 0; i < aTris.Count; i+=3)
            {
                var p1 = aTris[i];
                var p2 = aTris[i+1];
                var p3 = aTris[i+2];
                Debug.DrawLine(p1, p2, aCol);
                Debug.DrawLine(p2, p3, aCol);
                Debug.DrawLine(p3, p1, aCol);
            }

        }

        public static void DrawLine(List<Vector3> aPoint)
        {
            if (aPoint.Count == 0) return;

            for (int i = 0; i < aPoint.Count-1; i++)
            {
                var p1 = aPoint[i];
                var p2 = aPoint[i+1];
                Debug.DrawLine(p1, p2, i %2 == 0 ?Color.red : Color.yellow);
            }

            Debug.DrawLine(aPoint[aPoint.Count-1], aPoint[0], Color.red);
        }
    }
}
