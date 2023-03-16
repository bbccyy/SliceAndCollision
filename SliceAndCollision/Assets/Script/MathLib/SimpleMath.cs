using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Babeltime.SimpleMath
{
    public static class SimpleMath
    {

        public static float AngleOfSeg(Vector3 aSegA, Vector3 aSegB)
        {   //不区分正负角度 -> Vector3.Angle
            //var segA = Vector3.Normalize(aSegA);
            //var segB = Vector3.Normalize(aSegB);
            //return Mathf.Acos(Vector3.Dot(segA, segB)) / 3.1415927f * 180.0f;
            return Vector3.Angle(aSegA, aSegB); 
        }


    }
}
