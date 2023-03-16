using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Babeltime.Utils
{
    //[ExecuteInEditMode]
    public class Img2PolyDebuger : MonoBehaviour
    {
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
                foreach(var data in datas)
                    DrawLine(data);
            }
            Debug.DrawLine(new Vector3(1,0,0), new Vector3(-1,0,0), Color.red);
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
