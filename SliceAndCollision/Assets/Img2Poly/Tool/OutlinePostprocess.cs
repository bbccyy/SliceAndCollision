using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using sm = Babeltime.SimpleMath.SimpleMath;
using EarClipperLib;
using Haze;
using System.Linq;

namespace Babeltime.Utils
{
    public static class OutlinePostprocess  
    {

        public static void CombineSegmentsV2(in List<Vector3> aInputs, out List<Vector3> aOutput)
        {
            aOutput = new List<Vector3>();
            if (aInputs.Count < 3)
                return;

            var A = aInputs[0];
            var B = aInputs[1];
            var AB = B - A;
            AB.Normalize();
            aOutput.Add(A);
            //TODO: 基于AB,AC,AD的平均方向，作为当前推进的基准，倾向于实际点位于平均基准线的右侧！
            //这样做的好处是可以处理难以整合的Zigzag形状斜拉线 
            //偏移角度的阈值修正可以基于高斯函数 

            for (int i = 2; i < aInputs.Count; i++) 
            {
                var C = aInputs[i];
                var AC = C - A;
                AC.Normalize();
                var sina = Vector3.Cross(AB, AC).z; //sina > 0 说明点C在AB的左边，反之亦然 
                var theta = sina * 180f / Mathf.PI;
                if (Img2PolyParser.minCombineAngle < theta && theta < Img2PolyParser.maxCombineAngle)  //倾向于允许更多的外折(而拒绝内折) 
                {
                    //继续合并
                    continue;
                }

                aOutput.Add(aInputs[i - 1]);
                A = aInputs[i - 1];
                B = aInputs[i];
                AB = B - A;
                AB.Normalize();
            }

            aOutput.Add(aInputs.Last());
        }

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
            int i = aOffset;
            for (; i < aInput.Count - 2; i += 3)
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
            for (; i < aInput.Count; i++)
            {
                aOutput.Add(aInput[i]);
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


        public static void RingTriangulationMirror(in List<Vector3> aOuter, in List<Vector3> aInner, out List<Vector3> aOutputs)
        {
            aOutputs = new List<Vector3>();

            for (int i= 0; i < aOuter.Count - 1; i++)
            {
                aOutputs.Add(aOuter[i]);        //CCW order 
                aOutputs.Add(aOuter[i + 1]);
                aOutputs.Add(aInner[i]);

                aOutputs.Add(aInner[i]);
                aOutputs.Add(aOuter[i + 1]);
                aOutputs.Add(aInner[i + 1]); 
            }

            aOutputs.Add(aOuter[aOuter.Count - 1]);
            aOutputs.Add(aOuter[0]);
            aOutputs.Add(aInner[aOuter.Count - 1]);

            aOutputs.Add(aInner[aOuter.Count - 1]);
            aOutputs.Add(aOuter[0]);
            aOutputs.Add(aInner[0]);
        }

        public static void RingTriangulation(in List<Vector3> aOuter, in List<Vector3> aInner, out List<Vector3> aOutputs)
        {
            aOutputs = null;

            if (aOuter.Count < 3 || aInner.Count < 3) return;

            if (aOuter.Count == aInner.Count)
            {
                RingTriangulationMirror(aOuter, aInner, out aOutputs);
                return;
            }

            //TODO:
            Debug.LogWarning("Unsupported condition when RingTriangulation");
        }

        public static void TriangulaitonHaze(in List<Vector3> aCCW, out List<Vector3> aOutputs)
        {
            List<Vector2> inputs = null;
            V3toV2(aCCW, out inputs);
            var tris = Triangulator.Triangulate(inputs);

            aOutputs = new List<Vector3>();
            foreach (var tri in tris)
            {
                aOutputs.Add(new Vector3(tri.a.x, tri.a.y, 0));
                aOutputs.Add(new Vector3(tri.b.x, tri.b.y, 0));
                aOutputs.Add(new Vector3(tri.c.x, tri.c.y, 0));
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
            earClipping.SetPoints(points, holes);  //吐槽下，用carClipping切Ring真是蛋疼 
            earClipping.Triangulate();
            var res = earClipping.Result;

            V3mtoV3(res, out aOutputs);
        }

        public static void TryTrimIntersectedOutline(List<Vector2> aOutline, out List<Vector2> aTrimed)
        {
            var shifted = aOutline;

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
                        trimed.Add(shifted[i - 1]);
                        trimed.Add(shifted[i]);
                    }
                }
            }
            trimed.Add(shifted[shifted.Count - 1]);  //补上最后一个点 
           
            aTrimed = trimed;
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

            //将原始点按照两侧线段法线方向的合力方向偏移给定距离 
            List<Vector2> shifted = new List<Vector2>();
            Vector2 lastN = sm.GetLeftNormal(origV2[0] - origV2[origV2.Count - 1]);
            Vector2 curN, mergeN;

            origV2.Add(origV2[0]);

            for (int i = 0; i < origV2.Count-1; i++)
            {
                curN = sm.GetLeftNormal(origV2[i+1] - origV2[i]);
                mergeN = curN + lastN;
                mergeN.Normalize();
                shifted.Add(origV2[i] + mergeN * aDelta);
                lastN = curN;
            }

            //裁剪部分可能向内交错的连线 
            List<Vector2> trimed;
            //TryTrimIntersectedOutline(shifted, out trimed);

            trimed = shifted;  //第一个版本先不考虑trim 

            V2toV3(trimed, out aOutput);
        }

    }

}
