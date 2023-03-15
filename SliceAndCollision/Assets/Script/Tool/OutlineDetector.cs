using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Babeltime.Utils
{
    public class OutlineDetector
    {
        public static ObjectPool<Cell> CellPool = new ObjectPool<Cell>(100000);

        private static float onePixelSize = 0;
        public static float OnePixelSize { 
            get {
                if (onePixelSize <= 0)
                    return 0.01f;
                return onePixelSize; 
            } 
            set { 
                onePixelSize = value; 
            }
        }

        private Context ctx;

        public enum CellType
        {
            Undefine,
            In,
            Out,
        }

        public enum FourDir //��������ĸ������� 
        {
            LeftDown = 0,  //aka self 
            LeftUp = 1,
            RightUp = 2,
            RightDown = 3,

            _Ct = 4
        }

        public enum CrossDir  //�����е�ʮ�ֶ˵�(���������㷽λ) 
        {
            Undifined = 0,

            Left = 1,
            Right = 2,
            Up = 3,
            Down = 4,
        }

        //��Ӧ������ LeftDown��LeftUp��RightUp��RightDown ��Ӧ����λ��XYƫ��ֵ
        public static List<Tuple<int, int>> FourDirOffsetLUT = new List<Tuple<int, int>>() {
            new Tuple<int, int>(0,0), new Tuple<int, int>(0,1), 
            new Tuple<int, int>(1,1), new Tuple<int, int>(1,0), 
        };

        //���ڿ��ٲ�ѯFourDir�Ķ������򣬼����ֶԽ���  
        public static Dictionary<FourDir, FourDir> CrossOppDirLUT = new Dictionary<FourDir, FourDir>()
        {
            { FourDir.LeftDown, FourDir.RightUp },{ FourDir.RightUp, FourDir.LeftDown },
            { FourDir.LeftUp, FourDir.RightDown },{ FourDir.RightDown, FourDir.LeftUp },
        };

        public class Cell : IPoolable
        {
            public CellType type = CellType.Undefine; 
            public int x = 0;   //in pixel idx 
            public int y = 0;

            public bool CrossPointVisited = false;

            public void Reinit(CellType aType, int aPixelX, int aPixelY)
            {
                type = aType; x = aPixelX; y = aPixelY;
            }

            public void Recycle()
            {
                type = CellType.Undefine;
                x = 0; y = 0;
                CrossPointVisited = false;
            }
        }

        public class Context
        {
            public Texture2D texture;
            public int texWidth = 0;
            public int texHeight = 0;
            public Cell[,] mTable;
            public CrossDir lastCross; //��һ��ȷ�Ϲ��������㴦�ڵ�ǰ���ֲ��Ե��ĸ���λ(�ǵü�ʱˢ��) 

            public List<Cell> History = new List<Cell>();
            public List<Cell> SamplePoint = new List<Cell>();

            public List<Cell> WorkingFour = new List<Cell>((int)FourDir._Ct);

            public void init(Texture2D tex)
            {
                texture = tex;
                texWidth = tex.width;
                texHeight = tex.height;
                mTable = new Cell[texWidth, texHeight];
                History.Clear();

            }

            //����ʮ�����ĵ��Ӧ��Cellλ��(���ĵ����½��Ǹ�Cell) 
            //  1 | 2
            // --------
            //  0 | 3
            public void FillUpWorkingFour(int aCellX, int aCellY)
            {
                foreach(FourDir dir in Enum.GetValues(typeof(FourDir)))
                {
                    if (dir == FourDir._Ct) continue;
                    var offset = FourDirOffsetLUT[((int)dir)];
                    var cell = GetOrInitCellAt(aCellX + offset.Item1, aCellY + offset.Item2);
                    WorkingFour[(int)dir] = cell;  //cell may be null 
                }
            }
            public Cell GetOrInitCellAt(int aX, int aY)
            {
                if (aX < 0 || aX >= texWidth || aY < 0 || aY >= texHeight)
                    return null;

                if (mTable[aX, aY] == null)
                {
                    mTable[aX, aY] = CellPool.Get();
                    var col = texture.GetPixel(aX, aY);
                    mTable[aX, aY].Reinit(col.a >= 1 ? CellType.In : CellType.Out, aX, aY);
                }

                return mTable[aX, aY];
            }

            public void AddCell(Cell aCell)
            {
                History.Add(aCell);
            }

            public void RegisterSamplePoint(Cell aCell)
            {
                SamplePoint.Add(aCell);
            }

            public Cell GetFirstCell()
            {
                if (History.Count == 0)
                    return null;
                return History[0];
            }

            public void Dispose()
            {
                History.Clear();
                SamplePoint.Clear();
                for (int i = 0; i < mTable.GetLength(0); i++)
                {
                    for (int j = 0; j < mTable.GetLength(1); i++)
                    {
                        if (mTable[i,j] != null)
                        {
                            CellPool.Restore(mTable[i, j]);
                            mTable[i, j] = null;
                        }
                    }
                }
                texWidth = 0; texHeight = 0; 
                texture = null;
            }

        }

        public void EatTexture(Texture2D aTex)
        {
            if (ctx != null)
            {
                ctx.Dispose();
            }
            ctx = new Context();
            ctx.init(aTex);
        }

        public enum FSM
        {
            Init,       //��ʼ��������Ѱ�ҵ�һ�����λ��
            FirstPoint, //�����һ���㣬ȷ����һ����(���ʾ���CalCross�������) 
            CalFour,    //�������ƶ�����һ��ȷ�����������ϣ�����������䣬�������ж�״̬  
            OneInFour,  //��ֻ��1���ڵ�
            TwoInFour,  //����2���ڵ�
            ThreeInFour,//����3���ڵ�
            MeetEnd,    //�ҵ��յ���߼� 
            Done,       //�����˳���ѭ����״̬ 
            Error,      //�����������࣬������������󣬽���˷�֧ 
        }

        public FSM InitFirst(Context aCtx)
        {
            if (aCtx == null || aCtx.texWidth <= 0)
                return FSM.Error;

            for (int y = aCtx.texHeight - 1; y >= 0; y--)
            {
                for (int x = 0; x < aCtx.texWidth; x++)
                {
                    if (aCtx.texture.GetPixel(x, y).a >= 1)
                    {
                        //find first cell 
                        var cell = CellPool.Get();
                        cell.Reinit(CellType.In, x, y);
                        aCtx.AddCell(cell);
                        return FSM.FirstPoint;
                    }
                }
            }
            Debug.LogError("Unable to find In cells");
            return FSM.Error;
        }

        public FSM FirstPoint(Context aCtx)
        {
            //�ȴ����������� 
            var firstCell = aCtx.GetFirstCell();
            if (firstCell == null)
                return FSM.Error;
            aCtx.RegisterSamplePoint(firstCell); //��һ����һ����Ϊ��������� 

            //���о�ʮ�������ڵ������������һ����ת״̬�ľ��� 
            

            return FSM.Error;
        }

        public void Detect()
        {
            if (ctx == null) return;

            var currentState = FSM.Init;

            while (currentState != FSM.Done || currentState != FSM.Error)
            {
                switch(currentState)
                {
                    case FSM.Init:
                        currentState = InitFirst(ctx);
                        break;
                    case FSM.FirstPoint:
                        break;
                    default:
                        Debug.LogError("Unknown FSM State");
                        return;
                }
            }


        }


    }
}

