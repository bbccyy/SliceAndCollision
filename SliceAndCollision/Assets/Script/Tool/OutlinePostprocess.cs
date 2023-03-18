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

        private static void V3toV2(in List<Vector3> aIn, out List<Vector2> aOut)
        {
            aOut = new List<Vector2>(aIn.Count);
            foreach(var elm in aIn)
            {
                aOut.Add(new Vector2(elm.x, elm.y));
            }
        }

        private static void V2toV3(in List<Vector2> aIn, out List<Vector3> aOut)
        {
            aOut = new List<Vector3>(aIn.Count);
            foreach (var elm in aIn)
            {
                aOut.Add(new Vector3(elm.x, elm.y, 0));
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

            if (aOriginOutlines.Count < 3)
            {
                Debug.LogError("Not a polygon!");
                aOutput = new List<Vector3>();
                return;
            }

            List<Vector2> origV2 = null;
            V3toV2(aOriginOutlines, out origV2); //CCW order 

            origV2.Add(origV2[0]);
            origV2.Add(origV2[1]);

            //将原始点按照两侧线段法线方向的合力方向偏移给定距离 
            List<Vector2> shifted = new List<Vector2>();
            Vector2 lastN = sm.GetLeftNormal(origV2[1] - origV2[0]);
            Vector2 curN, mergeN;
            for(int i = 1; i < origV2.Count-1; i++)
            {
                curN = sm.GetLeftNormal(origV2[i+1] - origV2[i]);
                mergeN = curN + lastN;
                mergeN.Normalize();
                shifted.Add(origV2[i] + mergeN * aDelta);
                lastN = curN;
            }

            //裁剪部分可能向内交错的连线 
            List<Vector2> trimed = new List<Vector2>();
            bool find = false;
            for (int i = 1; i < shifted.Count - 1; i++)
            {
                find = false;
                for (int j = shifted.Count - 2; j > i; j--)
                {
                    var p = sm.SegSegIntersection(shifted[i - 1], shifted[i], shifted[j], shifted[j + 1]);
                    if (p != Vector2.zero)
                    {
                        //find an intersection!
                        find = true;
                        if (trimed.Count > 0)
                            trimed.Add(p);
                        else
                        {
                            trimed.Add(shifted[i - 1]);
                            trimed.Add(p);
                        }
                        i = j + 1; //其实这里应该指向新加入的p点，但是处理起来太麻烦，偷懒指向相交线段B的终点了 
                        break;  
                    }
                }
                if (!find)
                {
                    if (trimed.Count > 0)
                        trimed.Add(shifted[i]);
                    else
                    {
                        trimed.Add(shifted[i-1]);
                        trimed.Add(shifted[i]);
                    }
                }
            }
            trimed.Add(shifted[shifted.Count - 1]);  //补上最后一个点 
            //[Note]此算法在去除相交内线段时有一处bug，那就是必须要求初始点不在待舍弃范围内，
            //不然此算法抛弃的是正确范围，保留需要舍弃的范围 
            //[Todo]可以在发生交错时判断一下丢弃部分占全部数据量的百分比，如果大于50%，则交换丢弃内容 

            V2toV3(trimed, out aOutput);
        }

    }

}
