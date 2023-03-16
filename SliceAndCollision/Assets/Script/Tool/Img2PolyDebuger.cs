using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Babeltime.Utils
{
    [ExecuteInEditMode]
    public class Img2PolyDebuger : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            var data = Img2PolyParser.Instance.data;

            if (data != null)
            {
                DrawLine(data);
            }
            Debug.DrawLine(new Vector3(1,0,0), new Vector3(-1,0,0), Color.red);
        }

        public static void DrawLine(List<Vector3> aPoint)
        {
            if (aPoint.Count == 0) return;

            for (int i = 0; i < aPoint.Count-1; i+=2)
            {
                var p1 = aPoint[i];
                var p2 = aPoint[i+1];
                Debug.DrawLine(p1, p2, Color.red);
            }

            Debug.DrawLine(aPoint[aPoint.Count-1], aPoint[0], Color.red);
        }
    }
}
