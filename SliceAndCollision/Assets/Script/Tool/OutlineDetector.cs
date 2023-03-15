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

        public enum FourDir //田字区域的各个方向 
        {
            LeftDown = 0,  //aka self 
            LeftUp = 1,
            RightUp = 2,
            RightDown = 3,

            _Ct = 4
        }

        public enum CrossDir  //田字中的十字端点(代表轮廓点方位) 
        {
            Undifined = 0,

            Left = 1,
            Right = 2,
            Up = 3,
            Down = 4,
        }

        //对应田字中 LeftDown，LeftUp，RightUp，RightDown 对应像素位置XY偏移值
        public static List<Tuple<int, int>> FourDirOffsetLUT = new List<Tuple<int, int>>() {
            new Tuple<int, int>(0,0), new Tuple<int, int>(0,1), 
            new Tuple<int, int>(1,1), new Tuple<int, int>(1,0), 
        };

        //用于快速查询FourDir的对立方向，既田字对角线  
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
            public CrossDir lastCross; //上一个确认过的轮廓点处于当前田字测试的哪个方位(记得及时刷新) 

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

            //输入十字中心点对应的Cell位置(中心点左下角那个Cell) 
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
            Init,       //初始化操作，寻找第一个点的位置
            FirstPoint, //处理第一个点，确定下一个点(本质就是CalCross的特殊版) 
            CalFour,    //将田字移动到上一步确定的轮廓点上，进行数据填充，并初步判断状态  
            OneInFour,  //若只有1个内点
            TwoInFour,  //若有2个内点
            ThreeInFour,//若有3个内点
            MeetEnd,    //找到终点的逻辑 
            Done,       //用于退出主循环的状态 
            Error,      //迭代次数过多，或者陷入问题后，进入此分支 
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
            //先处理采样点输出 
            var firstCell = aCtx.GetFirstCell();
            if (firstCell == null)
                return FSM.Error;
            aCtx.RegisterSamplePoint(firstCell); //第一个点一定作为采样点输出 

            //再研究十字区域内的情况，给出下一步跳转状态的决策 
            

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

