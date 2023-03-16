using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using sm = Babeltime.SimpleMath.SimpleMath;

namespace Babeltime.Utils
{
    public static class OutlinePostprocess  
    {

        public static void TryConbineSegments(in List<Vector3> aInput, out List<Vector3> aOutput)
        {
            List<Vector3> loopInputs = aInput;
            List<Vector3> loopOutputs = null;
            int offset = Random.Range(0, 2);  //0 or 1 
            while (TryConbineOnce(in loopInputs, out loopOutputs, offset))
            {
                offset = Random.Range(0, 2);
                loopInputs = loopOutputs;
                loopOutputs = null;
            }

            aOutput = loopOutputs == null ? aInput : loopOutputs;
        }

        private static bool TryConbineOnce(in List<Vector3> aInput, out List<Vector3> aOutput, int aOffset)
        {
            aOutput = new List<Vector3>();
            bool hasChg = false;
            for (int i = aOffset; i < aInput.Count - 2; i += 3)
            {
                var segA = aInput[i + 1] - aInput[i];
                var segB = aInput[i + 2] - aInput[i+1];
                float theta = sm.AngleOfSeg(segA, segB); 
                if (DynamicThreshold(segA, segB, theta))
                {
                    aOutput.Add(aInput[i]); 
                    aOutput.Add(aInput[i+2]);
                    hasChg = true;
                }
            }
            return hasChg;
        }

        private static bool DynamicThreshold(Vector3 aSegA, Vector3 aSegB, float aTheta)
        {
            float mag2 = aSegA.magnitude * aSegB.magnitude;
            float min2 = Mathf.Pow(Img2PolyParser.minThresholdInPixels * Img2PolyParser.OnePixelSize, 2);
            float max2 = Mathf.Pow(Img2PolyParser.maxThresholdInPixels * Img2PolyParser.OnePixelSize, 2);

            mag2 = Mathf.Clamp(mag2, min2, max2);
            float r = (mag2 - min2) / (max2 - min2);

            float targetTheta = Mathf.SmoothStep(Img2PolyParser.maxThresholdInAngle, Img2PolyParser.minThresholdInAngle, r);

            return aTheta <= targetTheta;
        }


    }

}
