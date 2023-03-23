using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Search;
using UnityEngine;


namespace Babeltime.Utils
{
    public class OutlineDetector
    {
        public static ObjectPool<Cell> CellPool = new ObjectPool<Cell>(100000);

        public static int seqBentSeqThreshold = 2; //判断是否构成“直线”->“外折”->“直线”的直线长度(像素个数) 
        public static int longSeqThreshold = 3;
        public const int SafeLoopingNum = 100000;

        private Context ctx;

        public enum CellType
        {
            Undefine,
            In,
            Out,
        }

        public enum OutlineState
        {
            Undefined = 0,
            Sequence = 1,       //连续点 -> 对应田字内有2个(非对角)InCell -> 需记录连续个数 
            InnerBent = 2,      //内折点 -> 对应田字内只有1个InCell
            OutterBent = 3,     //外转点 -> 对应田字内有3个InCell
            Zigzag = 4,         //连续弯折 
        }

        public enum FourDir //田字区域的各个方向 
        {
            LeftDown = 0,  //aka self 
            LeftUp = 1,
            RightUp = 2,
            RightDown = 3,

            _Ct = 4
        }

        public enum DiagonalDir
        {
            LeftDownToRightUp = 0,
            LeftUpToRightDown = 1,
        }

        public static Array dirArray = Enum.GetValues(typeof(FourDir));

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

        //用于在田字区域中只有1个(或3个)内点时，给定上一个轮廓点，快速查询下一个轮廓点的朝向 
        public static Dictionary<FourDir, Dictionary<CrossDir, CrossDir>> OneInFourLUT = new Dictionary<FourDir, Dictionary<CrossDir, CrossDir>>()
        {
            { FourDir.LeftDown, new Dictionary<CrossDir, CrossDir>(){ { CrossDir.Left, CrossDir.Down }, { CrossDir.Down, CrossDir.Left } } },
            { FourDir.LeftUp, new Dictionary<CrossDir, CrossDir>(){ { CrossDir.Left, CrossDir.Up }, { CrossDir.Up, CrossDir.Left } } },
            { FourDir.RightUp, new Dictionary<CrossDir, CrossDir>(){ { CrossDir.Up, CrossDir.Right }, { CrossDir.Right, CrossDir.Up } } },
            { FourDir.RightDown, new Dictionary<CrossDir, CrossDir>(){ { CrossDir.Down, CrossDir.Right }, { CrossDir.Right, CrossDir.Down } } },
        };

        public static Dictionary<DiagonalDir, Dictionary<CrossDir, CrossDir>> DiagonalLUT = new Dictionary<DiagonalDir, Dictionary<CrossDir, CrossDir>>()
        {
            {DiagonalDir.LeftDownToRightUp, new Dictionary<CrossDir, CrossDir>()
            {
                { CrossDir.Left, CrossDir.Down }, { CrossDir.Down, CrossDir.Left }, { CrossDir.Up, CrossDir.Right }, { CrossDir.Right, CrossDir.Up }
            } },
            {DiagonalDir.LeftUpToRightDown, new Dictionary<CrossDir, CrossDir>()
            {
                { CrossDir.Left, CrossDir.Up }, { CrossDir.Up, CrossDir.Left }, { CrossDir.Down, CrossDir.Right }, { CrossDir.Right, CrossDir.Down }
            } }
        };

        public static Dictionary<DiagonalDir, Dictionary<CrossDir, FourDir>> DiagonalCellLUT = new Dictionary<DiagonalDir, Dictionary<CrossDir, FourDir>>()
        {
            {DiagonalDir.LeftDownToRightUp, new Dictionary<CrossDir, FourDir>()
            {
                { CrossDir.Left, FourDir.LeftDown }, { CrossDir.Down, FourDir.LeftDown }, { CrossDir.Up, FourDir.RightUp }, { CrossDir.Right, FourDir.RightUp }
            } },
            {DiagonalDir.LeftUpToRightDown, new Dictionary<CrossDir, FourDir>()
            {
                { CrossDir.Left, FourDir.LeftUp }, { CrossDir.Down, FourDir.RightDown }, { CrossDir.Up, FourDir.LeftUp }, { CrossDir.Right, FourDir.RightDown }
            } }
        };

        //用于在田字区域中只有2个内点时，给定上一个轮廓点，快速查询下一个轮廓点的朝向 
        //注意，必须排除对角内点的情况，目前不支持 
        public static Dictionary<CrossDir, CrossDir> TwoInFourLUT = new Dictionary<CrossDir, CrossDir>()
        {
            {CrossDir.Left, CrossDir.Right}, {CrossDir.Right, CrossDir.Left},
            {CrossDir.Up, CrossDir.Down}, {CrossDir.Down, CrossDir.Up},
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
            public static ObjectPool<OutlineTracker> OutlineTrakerPool = new ObjectPool<OutlineTracker>(10000);
            public class OutlineTracker : IPoolable 
            {
                public OutlineState st = OutlineState.Undefined;
                public int seqCount = 0;
                public Cell keyCell = null;
                public void Reinit(OutlineState aState) { st = aState; seqCount = 0; }
                public void Recycle() { keyCell = null; }
            }

            public List<Vector3> PolygonOutlines = new List<Vector3>();

            public Queue<OutlineTracker> queue = new Queue<OutlineTracker>();
            public LinkedList<OutlineTracker> lqueue = new LinkedList<OutlineTracker>();

            public Texture2D texture;
            public int texWidth = 0;
            public int texHeight = 0;
            public Cell[,] mTable;
            public CrossDir lastCrossDir; //上一个确认过的轮廓点处于当前田字测试的哪个方位(记得及时刷新) 
            public Cell nextCross;

            public List<Cell> History = new List<Cell>();  //可能存在连续2个Cell是同一个的情况(上一次内折，下一次外折) 
            public List<Cell> SamplePoint = new List<Cell>(); 

            public Cell[] WorkingFour = new Cell[(int)FourDir._Ct];

            public void init(Texture2D tex)
            {
                texture = tex;
                texWidth = tex.width;
                texHeight = tex.height;
                mTable = new Cell[texWidth, texHeight];
                History.Clear();
                queue.Clear();
                PolygonOutlines.Clear();
                WorkingFour = new Cell[(int)FourDir._Ct];
                SamplePoint.Clear();
                nextCross = null;

            }

            //输入参数: 十字中心轮廓点对应的Cell位置(中心点左下角那个Cell) 
            //输出: 参考下图的田字区域 
            //  1 | 2
            // --------
            //  0 | 3
            public void FillUpWorkingFour(int aCellX, int aCellY)
            {
                foreach(FourDir dir in dirArray)
                {
                    if (dir == FourDir._Ct) continue;
                    var offset = FourDirOffsetLUT[((int)dir)];
                    var cell = GetOrInitCellAt(aCellX + offset.Item1, aCellY + offset.Item2);
                    WorkingFour[(int)dir] = cell;  //cell may be null 
                }
            }
            public void UpdateNextWorkingFourParams(CrossDir aNextDir, Cell aCenterCell = null)
            {
                //是否使用输入Cell作为指定的轮廓中心点  
                if (aCenterCell != null) nextCross = aCenterCell;

                //重置参数 
                var curCell = nextCross; //这是田字中心轮廓点对应的Cell 
                nextCross = null;
                lastCrossDir = CrossDir.Undifined;
                switch (aNextDir)
                {
                    case CrossDir.Left:
                        nextCross = GetOrInitCellAt(curCell.x - 1, curCell.y);
                        lastCrossDir = CrossDir.Right;
                        break;
                    case CrossDir.Right:
                        nextCross = GetOrInitCellAt(curCell.x + 1, curCell.y);
                        lastCrossDir = CrossDir.Left;
                        break;
                    case CrossDir.Up:
                        nextCross = GetOrInitCellAt(curCell.x, curCell.y + 1);
                        lastCrossDir = CrossDir.Down;
                        break;
                    case CrossDir.Down:
                        nextCross = GetOrInitCellAt(curCell.x, curCell.y - 1);
                        lastCrossDir = CrossDir.Up;
                        break;
                    default:
                        Debug.LogError("UpdateNextWorkingFourParams err");
                        return;
                }
                return;
            }
            public Cell GetOrInitCellAt(int aX, int aY)
            {
                if (aX < 0 || aX >= texWidth || aY < 0 || aY >= texHeight)
                    return null;

                if (mTable[aX, aY] == null)
                {
                    mTable[aX, aY] = CellPool.Get();
                    var col = texture.GetPixel(aX, aY);
                    mTable[aX, aY].Reinit(col.a > 0 ? CellType.In : CellType.Out, aX, aY);
                }

                return mTable[aX, aY];
            }
            public void RegisterHistoryCell(Cell aCell)
            {   //History用于追踪上一个访问过的InCell，最后也可Debug用 
                History.Add(aCell);
            }
            public void RegisterSamplePoint(Cell aCell)
            {
                if (SamplePoint.LastOrDefault() != aCell) 
                    SamplePoint.Add(aCell);
            }
            public void UpdateTracker(OutlineState aCurSt, Cell aKeyCell)
            {
                if (lqueue.Count == 0)
                {   //初始状态 
                    OutlineTracker firstTk = OutlineTrakerPool.Get();
                    firstTk.Reinit(aCurSt);
                    firstTk.keyCell = aKeyCell;
                    lqueue.AddLast(firstTk);
                    RegisterSamplePoint(aKeyCell);  //第一个点必定采样 
                    return;
                }

                var latestTk = lqueue.Last(); 
                if (latestTk.st == OutlineState.Sequence && aCurSt == OutlineState.Sequence)
                {   //合并连续序列
                    latestTk.keyCell = aKeyCell;
                    latestTk.seqCount++;
                    return;
                }

                //判断条件，输出采样，避免过拟合 
                if(lqueue.Count == 3)
                {
                    var oldestTk = lqueue.First();
                    lqueue.RemoveFirst();  //Dequeue first 
                    if (oldestTk.st == OutlineState.Sequence && latestTk.st == OutlineState.Sequence
                       && lqueue.First().st == OutlineState.OutterBent &&
                       oldestTk.seqCount >= seqBentSeqThreshold && latestTk.seqCount >= seqBentSeqThreshold)
                    {
                        if (oldestTk.keyCell != null) 
                            RegisterSamplePoint(oldestTk.keyCell); //Seq(3) + Out + Seq(3) 
                    }
                    else if (oldestTk.st == OutlineState.Sequence && latestTk.st == OutlineState.Sequence
                       && lqueue.First().st == OutlineState.InnerBent)
                    {
                        if (oldestTk.keyCell != null)
                            RegisterSamplePoint(lqueue.First().keyCell); //Seq + [In] + Seq 
                    }
                    else if (oldestTk.st == OutlineState.Sequence && oldestTk.seqCount >= longSeqThreshold && 
                        lqueue.First().st == OutlineState.InnerBent)
                    {
                        RegisterSamplePoint(lqueue.First().keyCell); //Seq(>=5) + [In] + Any
                    }
                    else if (oldestTk.st == OutlineState.InnerBent && lqueue.First().st == OutlineState.Sequence)
                    {
                        RegisterSamplePoint(oldestTk.keyCell); //InBent + Seq 
                    }

                    OutlineTrakerPool.Restore(oldestTk);
                }

                //向队列添加新的轮廓线追踪状态 
                OutlineTracker nextTk = OutlineTrakerPool.Get();
                nextTk.Reinit(aCurSt);
                nextTk.keyCell = aKeyCell;
                lqueue.AddLast(nextTk);
            }

            public void Dispose()
            {
                int y = texHeight - 1;
                if (History.Count > 0)
                    y = History[0].y + 1; //需要多1层，鉴于Out类型的Cell会出现在FirstCell的上面 
                History.Clear();
                SamplePoint.Clear();
                PolygonOutlines.Clear();
                WorkingFour = null;
                for (; y >= 0; y--)
                {
                    bool find = false;
                    for (int x = 0; x < texWidth; x++)
                    {
                        if (mTable[x,y] != null)
                        {
                            CellPool.Restore(mTable[x, y]);
                            mTable[x, y] = null;
                            find = true;
                        }
                    }
                    if (!find) break;
                }
                texWidth = 0; texHeight = 0; 
                texture = null;
                nextCross = null;

                foreach(var elm in queue)
                {
                    OutlineTrakerPool.Restore(elm);
                }
                queue.Clear();
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
            DiagonalInFour, //若有2个对角内点
            MeetEnd,    //找到终点的逻辑 
            Done,       //用于退出主循环的状态 
            Error,      //迭代次数过多，或者陷入问题后，进入此分支 
        }

        public FSM InitFirst(Context aCtx)
        {
            if (aCtx == null || aCtx.texWidth <= 0)
                return FSM.Error;

            const int minDelta = 5;  //5行一跳 

            int y = aCtx.texHeight - 2; int x = 0;
            bool find = false;
            for ( ; y >= 0; y-= minDelta)
            {
                for (x = 1; x < aCtx.texWidth; x++)
                {
                    if (aCtx.texture.GetPixel(x, y).a > 0)
                    {
                        find = true;
                    }
                    if (find) break;
                }
                if (find) break;
            }

            if (!find) return FSM.Error;

            while(find && ++y < aCtx.texHeight)
            {
                find = false;
                for (x = 0; x < aCtx.texWidth; x++)
                {
                    if (aCtx.texture.GetPixel(x, y).a > 0)
                    {
                        find = true;
                        break;
                    }
                }
            }

            y--;

            for (x = 0; x < aCtx.texWidth; x++)
            {
                if (aCtx.texture.GetPixel(x, y).a > 0)
                {
                    //find and init first cell 
                    aCtx.nextCross = aCtx.GetOrInitCellAt(x - 1, y); //第一个内部点的左边的点(其右上角对应田字中心) 
                    return FSM.FirstPoint;
                }
            }
            
            Debug.LogError("Unable to find In cells");
            return FSM.Error;
        }

        public FSM FirstPoint(Context aCtx)
        {
            //先获取目标点，这个点应该是第一个Cell 
            var leftDownCell = aCtx.nextCross;
            var firstCell = aCtx.GetOrInitCellAt(leftDownCell.x + 1, leftDownCell.y); //第一个内点必然在nextCross的右侧 
            if (firstCell == null)
                return FSM.Error;

            aCtx.RegisterHistoryCell(firstCell); //第一个InCell是当次唯一存在的InCell，理应放入History 
            //aCtx.RegisterSamplePoint(firstCell); //第一个Cell一定作为采样点输出 

            //标记leftDownCell和其下方2个轮廓点  
            leftDownCell.CrossPointVisited = true;
            var leftDownDownCell = aCtx.GetOrInitCellAt(leftDownCell.x, leftDownCell.y - 1); //下方轮廓点对应左下Cell 
            if (leftDownDownCell != null) leftDownDownCell.CrossPointVisited = true;

            //为下一轮迭代准备 
            aCtx.lastCrossDir = CrossDir.Left; //因为下一轮必然右移一格田字格区域，那么当前的十字中心点在下一轮来看，就是左侧Left  

            //确定下一个轮廓点中心点 
            aCtx.nextCross = firstCell; //意味着当前Cell右上角的轮廓点是下一次迭代的田字中心点 

            //更新轮廓点追踪状态 
            aCtx.UpdateTracker(OutlineState.InnerBent, firstCell); 

            return FSM.CalFour; //下一个状态是填充田字区域，并基于填充结果执行状态切换 
        }

        public FSM CalFour(Context aCtx)
        {
            if (aCtx.nextCross == null)
                return FSM.Error;

            if (aCtx.nextCross.CrossPointVisited)
            {
                return FSM.MeetEnd; //结束条件触发 
            }

            //标记当前的田字中心点为“访问过”状态 
            aCtx.nextCross.CrossPointVisited = true;  //662  (1500 - 1 - 920 = 579)

            //填充田字一共4个cell，左下角cell同时对应十字中心轮廓点 
            var curCrossCenter = aCtx.nextCross;
            aCtx.FillUpWorkingFour(curCrossCenter.x, curCrossCenter.y);

            //计算内点 
            List<FourDir> InList = new List<FourDir>(); 
            foreach (FourDir dir in dirArray)
            {
                if (dir == FourDir._Ct) continue;
                if (aCtx.WorkingFour[(int)dir] == null) continue;
                if (aCtx.WorkingFour[(int)dir].type == CellType.In) InList.Add(dir);
            }

            
            int InCount = InList.Count;
            if (InCount == 2 && CrossOppDirLUT[InList[0]] == InList[1])
            {
                InCount = 4; //处理2个对角内点的情况 
                aCtx.nextCross.CrossPointVisited = false; 
            }

            //判断跳转状态 
            FSM ret = FSM.Error;
            switch(InCount)
            {
                case 1:
                    ret = FSM.OneInFour;
                    break;
                case 2:
                    ret = FSM.TwoInFour;
                    break;
                case 3:
                    ret = FSM.ThreeInFour;
                    break;
                case 4:
                    ret = FSM.DiagonalInFour;
                    break;
                default:
                    break; //0 or 5 or more is Invalid -> return Error 
            }

            return ret;
        }

        public FSM DiagonalInFour(Context aCtx)
        {
            if (aCtx.lastCrossDir == CrossDir.Undifined) { Debug.Log("DiagonalInFour err"); return FSM.Error; }

            DiagonalDir dir = DiagonalDir.LeftUpToRightDown;
            if (aCtx.WorkingFour[(int)FourDir.LeftDown] != null && aCtx.WorkingFour[(int)FourDir.LeftDown].type == CellType.In)
                dir = DiagonalDir.LeftDownToRightUp;

            var incellDir = DiagonalCellLUT[dir][aCtx.lastCrossDir];
            var InCell = aCtx.WorkingFour[(int)incellDir];
            if (InCell == null || InCell.type != CellType.In) { Debug.Log("DiagonalInFour err"); return FSM.Error; }

            var nextDir = DiagonalLUT[dir][aCtx.lastCrossDir];

            //需要重置一下上一轮配置田字格区域的参数 
            aCtx.UpdateNextWorkingFourParams(nextDir);

            //更新轮廓线追踪状态(调用内部可能会触发输出采样)
            aCtx.UpdateTracker(OutlineState.InnerBent, InCell); //DiagonalInFour使用InnerBent 

            //内折点需要输出采样(注意此操作需要在更新轮廓线追踪状态之后进行) 
            //aCtx.RegisterSamplePoint(InCell);                   //使用InCell输出采样 

            //注册访问过的Cell
            aCtx.RegisterHistoryCell(InCell);

            return FSM.CalFour;
        }

        public FSM OneInFour(Context aCtx)
        {
            if (aCtx.lastCrossDir == CrossDir.Undifined) { Debug.Log("OneInFour err"); return FSM.Error;}

            //确定是哪一个方位的Cell 
            FourDir InCellDir = FourDir._Ct;
            Cell InCell = null;
            foreach (FourDir dir in dirArray)
            {
                if (dir == FourDir._Ct) continue;
                InCell = aCtx.WorkingFour[(int)dir];
                if (InCell == null) continue;
                if (InCell.type == CellType.In)
                {
                    InCellDir = dir; 
                    break;
                }
            }
            if (InCellDir == FourDir._Ct) { Debug.Log("OneInFour err"); return FSM.Error;}

            //确定下一个移动方向 
            var sub_lut = OneInFourLUT[InCellDir];
            if (sub_lut == null) { Debug.Log("OneInFour err"); return FSM.Error;}
            var nextDir = sub_lut[aCtx.lastCrossDir];
            if (nextDir == CrossDir.Undifined) { Debug.Log("OneInFour err"); return FSM.Error;}

            //需要重置一下上一轮配置田字格区域的参数 
            aCtx.UpdateNextWorkingFourParams(nextDir);

            //更新轮廓线追踪状态(调用内部可能会触发输出采样)
            aCtx.UpdateTracker(OutlineState.InnerBent, InCell); //涉及输出采样点，一律使用InCell 

            //内折点需要输出采样(注意此操作需要在更新轮廓线追踪状态之后进行)
            //aCtx.RegisterSamplePoint(InCell);                   //使用InCell输出采样 

            //注册访问过的Cell
            aCtx.RegisterHistoryCell(InCell);

            return FSM.CalFour;
        }

        public FSM TwoInFour(Context aCtx)
        {
            if (aCtx.lastCrossDir == CrossDir.Undifined) { Debug.Log("TwoInFour err"); return FSM.Error; }

            //确定是哪一个方位的Cell 
            FourDir InCellDir = FourDir._Ct;
            Cell InCell = null; 
            Cell lastHistoryCell = aCtx.History[aCtx.History.Count - 1]; 
            foreach (FourDir dir in dirArray)
            {
                if (dir == FourDir._Ct) continue;
                InCell = aCtx.WorkingFour[(int)dir];
                if (InCell == null) continue;
                if (InCell.type == CellType.In && InCell != lastHistoryCell) //必然有一个Cell不是上一次访问的点 
                {
                    InCellDir = dir;
                    break;
                }
            }
            if (InCellDir == FourDir._Ct) { Debug.Log("TwoInFour err"); return FSM.Error; }

            //确定下一个移动方向 
            var nextDir = TwoInFourLUT[aCtx.lastCrossDir]; 
            if (nextDir == CrossDir.Undifined) { Debug.Log("TwoInFour err"); return FSM.Error; }

            //重置上轮参数 
            aCtx.UpdateNextWorkingFourParams(nextDir);

            //更新轮廓线追踪状态(调用内部可能会触发输出采样)
            aCtx.UpdateTracker(OutlineState.Sequence, InCell); //涉及输出采样点，一律使用InCell 

            //注册访问过的Cell
            aCtx.RegisterHistoryCell(InCell);

            return FSM.CalFour;
        }

        public FSM ThreeInFour(Context aCtx)
        {
            if (aCtx.lastCrossDir == CrossDir.Undifined) { Debug.Log("ThreeInFour err"); return FSM.Error; }

            //确定 OutCell 在哪一个方位 
            FourDir OutCellDir = FourDir._Ct;
            Cell OutCell = null;
            //Cell lastHistoryCell = aCtx.History[aCtx.History.Count - 1];
            foreach (FourDir dir in dirArray)
            {
                if (dir == FourDir._Ct) continue;
                OutCell = aCtx.WorkingFour[(int)dir];
                if (OutCell == null || OutCell.type == CellType.Out)
                {   //null对应边界之外的情况，目前还不兼容贴边描线，不过这里不影响计算也就无所谓了 
                    OutCellDir = dir;
                    break;
                }
            }
            if (OutCellDir == FourDir._Ct) { Debug.Log("ThreeInFour err"); return FSM.Error; }

            //确定下一个移动方向 
            var sub_lut = OneInFourLUT[OutCellDir];
            if (sub_lut == null) { Debug.Log("ThreeInFour err"); return FSM.Error; }
            var nextDir = sub_lut[aCtx.lastCrossDir];
            if (nextDir == CrossDir.Undifined) { Debug.Log("ThreeInFour err"); return FSM.Error; }

            //确定未访问的InCell位置 
            Cell InCell = null;  //要找的点 
            Cell lastHistoryCell = aCtx.History[aCtx.History.Count - 1]; //访问过的点 
            Cell cornerCell = aCtx.WorkingFour[(int)CrossOppDirLUT[OutCellDir]]; //OutCell的对面那点 
            foreach (FourDir dir in dirArray)
            {
                if (dir == FourDir._Ct) continue;
                InCell = aCtx.WorkingFour[(int)dir];
                if (InCell == null) continue;
                if (InCell.type == CellType.In && 
                    InCell != lastHistoryCell && 
                    InCell != cornerCell)  
                {
                    break; //find it! 
                }
                InCell = null;
            }
            if (InCell == null) { Debug.Log("ThreeInFour err"); return FSM.Error; }

            //重置上轮参数 
            aCtx.UpdateNextWorkingFourParams(nextDir);

            //更新轮廓线追踪状态(调用内部可能会触发输出采样)
            aCtx.UpdateTracker(OutlineState.OutterBent, InCell); 

            //注册访问过的Cell
            aCtx.RegisterHistoryCell(InCell);

            return FSM.CalFour;
        }

        public FSM MeetEnd(Context aCtx)
        {
            aCtx.UpdateTracker(OutlineState.OutterBent, null);
            aCtx.UpdateTracker(OutlineState.OutterBent, null); 
            aCtx.UpdateTracker(OutlineState.OutterBent, null); //迫使Tracker中的采样点都输出 

            foreach (var cell in aCtx.SamplePoint)
            {
                Vector3 curPos = new Vector3(
                    (float)cell.x * Img2PolyParser.OnePixelSize,
                    (float)cell.y * Img2PolyParser.OnePixelSize,
                    0
                    );

                aCtx.PolygonOutlines.Add(curPos);
            }

            return FSM.Done;
        }

        public void Detect()
        {
            if (ctx == null) return;

            var currentState = FSM.Init;
            int loopingCount = 0;

            while (loopingCount < SafeLoopingNum && 
                    (currentState != FSM.Done && currentState != FSM.Error))
            {
                switch(currentState)
                {
                    case FSM.Init:
                        currentState = InitFirst(ctx);
                        break;
                    case FSM.FirstPoint:
                        currentState = FirstPoint(ctx);
                        break;
                    case FSM.CalFour:
                        currentState = CalFour(ctx);
                        break;
                    case FSM.OneInFour:
                        currentState = OneInFour(ctx); 
                        break;
                    case FSM.TwoInFour:
                        currentState= TwoInFour(ctx);
                        break;
                    case FSM.ThreeInFour:
                        currentState = ThreeInFour(ctx);
                        break;
                    case FSM.DiagonalInFour:
                        currentState = DiagonalInFour(ctx);
                        break;
                    case FSM.MeetEnd:
                        currentState = MeetEnd(ctx);
                        break;
                    default:
                        Debug.LogError("Unknown FSM State");
                        return;
                }
                loopingCount++;
            }

            if (loopingCount >= SafeLoopingNum)
            {
                Debug.LogError("Over Looping!");
            }

            return;
        }

        public void RetriveOutline(out List<Vector3> aPoints)
        {
            aPoints = null;
            if (ctx != null)
                aPoints = ctx.PolygonOutlines;
        }

        public void Reset()
        {
            if (ctx != null) ctx.Dispose();
            ctx = null;
        }
    }
}

