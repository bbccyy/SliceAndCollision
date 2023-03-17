using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using sm = Babeltime.SimpleMath.SimpleMath;
using EarClipperLib;

namespace Babeltime.Utils
{
    public static class OutlinePostprocess  
    {

        public static void TryConbineSegments(in List<Vector3> aInput, out List<Vector3> aOutput)
        {
            List<Vector3> loopInputs = aInput;
            List<Vector3> loopOutputs = null;
            int offset = Random.Range(0, 2);  //0 or 1 
            offset = 0;
            int ct = 0;
            while (TryConbineOnce(in loopInputs, out loopOutputs, offset))
            {
                ct++;
                //offset = Random.Range(0, 2);
                loopInputs = loopOutputs;
                loopOutputs = null;
            }

            aOutput = loopOutputs == null ? aInput : loopOutputs;

            Debug.LogWarning($"data size = {aOutput.Count}, ct = {ct}, orign size = {aInput.Count}");
        }

        private static bool TryConbineOnce(in List<Vector3> aInput, out List<Vector3> aOutput, int aOffset)
        {
            if (aInput.Count <= 100)
            {
                aOutput = aInput;
                return false;
            }
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
                else
                {
                    aOutput.Add(aInput[i]);
                    aOutput.Add(aInput[i+1]);
                    aOutput.Add(aInput[i+2]);
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


        private static void V3toV3m(in List<Vector3> aIn, out List<Vector3m> aOut)
        {
            aOut = new List<Vector3m>(aIn.Count);
            foreach(var elm in aIn)
            {
                aOut.Add(new Vector3m(elm.x, elm.y, elm.z));
            }
        }

        private static void V3mtoV3(in List<Vector3m> aIn, out List<Vector3> aOut)
        {
            aOut = new List<Vector3>(aIn.Count);
            foreach (var elm in aIn)
            {
                aOut.Add(new Vector3((float)elm.X.ToDouble(), (float)elm.Y.ToDouble(), (float)elm.Z.ToDouble()));
            }
        }

        public static void Triangulation(in List<Vector3> aCCW, in List<Vector3> aCW, out List<Vector3> aOutputs)
        {
            List<Vector3m> points = null;
            V3toV3m(in aCCW, out points);

            List<List<Vector3m>> holes = null;
            if (aCW != null && aCW.Count > 0) 
            {
                List<Vector3m> hole;
                V3toV3m(in aCW, out hole);
                holes = new List<List<Vector3m>>();
                holes.Add(hole);
            }

            EarClipping earClipping = new EarClipping();
            earClipping.SetPoints(points, holes);
            earClipping.Triangulate();
            var res = earClipping.Result;

            V3mtoV3(res, out aOutputs);
        }

        public static void ShiftOutlineBasedOnNormalDir(List<Vector3> aOriginOutlines, float aDelta, out List<Vector3> aOutput)
        {
            aOutput = new List<Vector3>();




        }

    }

}
