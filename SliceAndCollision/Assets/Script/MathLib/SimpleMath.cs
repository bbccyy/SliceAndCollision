using System.Collections;
using System.Collections.Generic;
using System.Drawing;
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

        public static Vector2 GetLeftNormal(Vector2 aVec)
        {
            if (aVec == null || aVec == Vector2.zero) return new Vector2(0, 0);
            Vector2 n = new Vector2(-aVec.y, aVec.x);
            n.Normalize();
            return n;
        }

        public static Vector2 SegSegIntersection(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
        {
            var lineIntersection = LineLineIntersection(firstStart, firstEnd, secondStart, secondEnd); 
            if (lineIntersection == Vector2.zero)
            {
                return lineIntersection;  //2线段平行，直接返回0 
            }

            Vector2 d1 = firstStart - lineIntersection;
            Vector2 d2 = firstEnd - lineIntersection;
            if (d1 != Vector2.zero && d2 != Vector2.zero) //排除在端点上的情况 
            {
                if (Vector2.Dot(d1, d2) > 0)
                {
                    return Vector2.zero; //没有线段交点 
                }
            }

            d1 = secondStart - lineIntersection;
            d2 = secondEnd - lineIntersection;
            if (d1 != Vector2.zero && d2 != Vector2.zero)
            {
                if (Vector2.Dot(d1, d2) > 0)
                {
                    return Vector2.zero; //没有线段交点 
                }
            }

            return lineIntersection;
        }

        public static Vector2 LineLineIntersection(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
        {
            /*
             * L1，L2都存在斜率的情况：
             * 直线方程L1: ( y - y1 ) / ( y2 - y1 ) = ( x - x1 ) / ( x2 - x1 ) 
             * => y = [ ( y2 - y1 ) / ( x2 - x1 ) ]( x - x1 ) + y1
             * 令 a = ( y2 - y1 ) / ( x2 - x1 )
             * 有 y = a * x - a * x1 + y1   .........1
             * 直线方程L2: ( y - y3 ) / ( y4 - y3 ) = ( x - x3 ) / ( x4 - x3 )
             * 令 b = ( y4 - y3 ) / ( x4 - x3 )
             * 有 y = b * x - b * x3 + y3 ..........2
             * 
             * 如果 a = b，则两直线平等，否则， 联解方程 1,2，得:
             * x = ( a * x1 - b * x3 - y1 + y3 ) / ( a - b )
             * y = a * x - a * x1 + y1
             * 
             * L1存在斜率, L2平行Y轴的情况：
             * x = x3
             * y = a * x3 - a * x1 + y1
             * 
             * L1 平行Y轴，L2存在斜率的情况：
             * x = x1
             * y = b * x - b * x3 + y3
             * 
             * L1与L2都平行Y轴的情况：
             * 如果 x1 = x3，那么L1与L2重合，否则平等
             * 
            */
            float a = 0, b = 0;
            int state = 0;
            if (firstStart.x != firstEnd.x)
            {
                a = (firstEnd.y - firstStart.y) / (firstEnd.x - firstStart.x);
                state |= 1;
            }
            if (secondStart.x != secondEnd.x)
            {
                b = (secondEnd.y - secondStart.y) / (secondEnd.x - secondStart.x);
                state |= 2;
            }
            switch (state)
            {
                case 0: //L1与L2都平行Y轴
                    {
                        if (firstStart.x == secondStart.x)
                        {
                            //throw new Exception("两条直线互相重合，且平行于Y轴，无法计算交点。");
                            return Vector2.zero;
                        }
                        else
                        {
                            //throw new Exception("两条直线互相平行，且平行于Y轴，无法计算交点。");
                            return Vector2.zero;
                        }
                    }
                case 1: //L1存在斜率, L2平行Y轴
                    {
                        float x = secondStart.x;
                        float y = (firstStart.x - x) * (-a) + firstStart.y;
                        return new Vector2(x, y);
                    }
                case 2: //L1 平行Y轴，L2存在斜率
                    {
                        float x = firstStart.x;
                        //网上有相似代码的，这一处是错误的。你可以对比case 1 的逻辑 进行分析
                        //源code:secondStart * x + secondStart * secondStart.x + p3.y;
                        float y = (secondStart.x - x) * (-b) + secondStart.y;
                        return new Vector2(x, y);
                    }
                case 3: //L1，L2都存在斜率
                    {
                        if (a == b)
                        {
                            // throw new Exception("两条直线平行或重合，无法计算交点。");
                            return new Vector2(0, 0);
                        }
                        float x = (a * firstStart.x - b * secondStart.x - firstStart.y + secondStart.y) / (a - b);
                        float y = a * x - a * firstStart.x + firstStart.y;
                        return new Vector2(x, y);
                    }
            }
            // throw new Exception("不可能发生的情况");
            return Vector2.zero;
        }

    }
}
