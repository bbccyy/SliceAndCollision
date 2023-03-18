using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

namespace Babeltime.SimpleMath
{
    public static class SimpleMath
    {

        public static float AngleOfSeg(Vector3 aSegA, Vector3 aSegB)
        {   //�����������Ƕ� -> Vector3.Angle
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
                return lineIntersection;  //2�߶�ƽ�У�ֱ�ӷ���0 
            }

            Vector2 d1 = firstStart - lineIntersection;
            Vector2 d2 = firstEnd - lineIntersection;
            if (d1 != Vector2.zero && d2 != Vector2.zero) //�ų��ڶ˵��ϵ���� 
            {
                if (Vector2.Dot(d1, d2) > 0)
                {
                    return Vector2.zero; //û���߶ν��� 
                }
            }

            d1 = secondStart - lineIntersection;
            d2 = secondEnd - lineIntersection;
            if (d1 != Vector2.zero && d2 != Vector2.zero)
            {
                if (Vector2.Dot(d1, d2) > 0)
                {
                    return Vector2.zero; //û���߶ν��� 
                }
            }

            return lineIntersection;
        }

        public static Vector2 LineLineIntersection(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
        {
            /*
             * L1��L2������б�ʵ������
             * ֱ�߷���L1: ( y - y1 ) / ( y2 - y1 ) = ( x - x1 ) / ( x2 - x1 ) 
             * => y = [ ( y2 - y1 ) / ( x2 - x1 ) ]( x - x1 ) + y1
             * �� a = ( y2 - y1 ) / ( x2 - x1 )
             * �� y = a * x - a * x1 + y1   .........1
             * ֱ�߷���L2: ( y - y3 ) / ( y4 - y3 ) = ( x - x3 ) / ( x4 - x3 )
             * �� b = ( y4 - y3 ) / ( x4 - x3 )
             * �� y = b * x - b * x3 + y3 ..........2
             * 
             * ��� a = b������ֱ��ƽ�ȣ����� ���ⷽ�� 1,2����:
             * x = ( a * x1 - b * x3 - y1 + y3 ) / ( a - b )
             * y = a * x - a * x1 + y1
             * 
             * L1����б��, L2ƽ��Y��������
             * x = x3
             * y = a * x3 - a * x1 + y1
             * 
             * L1 ƽ��Y�ᣬL2����б�ʵ������
             * x = x1
             * y = b * x - b * x3 + y3
             * 
             * L1��L2��ƽ��Y��������
             * ��� x1 = x3����ôL1��L2�غϣ�����ƽ��
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
                case 0: //L1��L2��ƽ��Y��
                    {
                        if (firstStart.x == secondStart.x)
                        {
                            //throw new Exception("����ֱ�߻����غϣ���ƽ����Y�ᣬ�޷����㽻�㡣");
                            return Vector2.zero;
                        }
                        else
                        {
                            //throw new Exception("����ֱ�߻���ƽ�У���ƽ����Y�ᣬ�޷����㽻�㡣");
                            return Vector2.zero;
                        }
                    }
                case 1: //L1����б��, L2ƽ��Y��
                    {
                        float x = secondStart.x;
                        float y = (firstStart.x - x) * (-a) + firstStart.y;
                        return new Vector2(x, y);
                    }
                case 2: //L1 ƽ��Y�ᣬL2����б��
                    {
                        float x = firstStart.x;
                        //���������ƴ���ģ���һ���Ǵ���ġ�����ԶԱ�case 1 ���߼� ���з���
                        //Դcode:secondStart * x + secondStart * secondStart.x + p3.y;
                        float y = (secondStart.x - x) * (-b) + secondStart.y;
                        return new Vector2(x, y);
                    }
                case 3: //L1��L2������б��
                    {
                        if (a == b)
                        {
                            // throw new Exception("����ֱ��ƽ�л��غϣ��޷����㽻�㡣");
                            return new Vector2(0, 0);
                        }
                        float x = (a * firstStart.x - b * secondStart.x - firstStart.y + secondStart.y) / (a - b);
                        float y = a * x - a * firstStart.x + firstStart.y;
                        return new Vector2(x, y);
                    }
            }
            // throw new Exception("�����ܷ��������");
            return Vector2.zero;
        }

    }
}
