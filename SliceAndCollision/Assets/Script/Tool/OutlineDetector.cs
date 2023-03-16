using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static Babeltime.Utils.OutlineDetector;

namespace Babeltime.Utils
{
    public class OutlineDetector
    {
        public static ObjectPool<Cell> CellPool = new ObjectPool<Cell>(100000);

        public static int seqBentSeqThreshold = 5; //判断是否构成“直线”->“外折”->“直线”的直线长度(像素个数) 

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

        public enum OutlineState
        {
            Undefined = 0,
            Sequence = 1,       //连续点 -> 对应田字内有2个(非对角)InCell -> 需记录连续个数 
            InnerBent = 2,      //内折点 -> 对应田字内只有1个InCell
            OutterBent = 3,     //外转点 -> 对应田字内有3个InCell
        }

        public enum FourDir //田字区域的各个方向 
        {
            LeftDown = 0,  //aka self 
            LeftUp = 1,
            RightUp = 2,
            RightDown = 3,

            _Ct = 4
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

            public Queue<OutlineTracker> queue = new Queue<OutlineTracker>();

            public Texture2D texture;
            public int texWidth = 0;
            public int texHeight = 0;
            public Cell[,] mTable;
            public CrossDir lastCrossDir; //上一个确认过的轮廓点处于当前田字测试的哪个方位(记得及时刷新) 
            public Cell nextCross;

            public List<Cell> History = new List<Cell>();  //可能存在连续2个Cell是同一个的情况(上一次内折，下一次外折) 
            public List<Cell> SamplePoint = new List<Cell>(); 

            public List<Cell> WorkingFour = new List<Cell>((int)FourDir._Ct);

            public void init(Texture2D tex)
            {
                texture = tex;
                texWidth = tex.width;
                texHeight = tex.height;
                mTable = new Cell[texWidth, texHeight];
                History.Clear();
                queue.Clear();
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
                    mTable[aX, aY].Reinit(col.a >= 1 ? CellType.In : CellType.Out, aX, aY);
                }

                return mTable[aX, aY];
            }
            public void RegisterHistoryCell(Cell aCell)
            {
                History.Add(aCell);  //History主要为了Debug用 
            }
            public void RegisterSamplePoint(Cell aCell)
            {
                SamplePoint.Add(aCell);
            }
            public void UpdateTracker(OutlineState aCurSt, Cell aKeyCell)
            {
                if (queue.Count == 0)
                {   //初始状态 
                    OutlineTracker firstTk = OutlineTrakerPool.Get();
                    firstTk.Reinit(aCurSt);
                    firstTk.keyCell = aKeyCell;
                    queue.Enqueue(firstTk);
                    return;
                }

                var latestTk = queue.ElementAt(queue.Count - 1);
                if (latestTk.st == OutlineState.Sequence && aCurSt == OutlineState.Sequence)
                {   //合并连续序列
                    latestTk.keyCell = aKeyCell;
                    latestTk.seqCount++;
                    return;
                }

                //向队列添加新的轮廓线追踪状态 
                OutlineTracker nextTk = OutlineTrakerPool.Get();
                nextTk.Reinit(aCurSt);
                nextTk.keyCell = aKeyCell;
                queue.Enqueue(nextTk);

                if(queue.Count == 4)
                {   //超出上限需要Dequeue，同时执行必要检测 
                    var oldestTk = queue.Dequeue();
                    if (oldestTk.st == OutlineState.Sequence && latestTk.st == OutlineState.Sequence
                        && queue.Peek().st == OutlineState.OutterBent && 
                        oldestTk.seqCount >= seqBentSeqThreshold && latestTk.seqCount >= seqBentSeqThreshold)
                    {
                        if (oldestTk.keyCell != null) RegisterSamplePoint(oldestTk.keyCell); //需要采样这个点 
                    }
                    OutlineTrakerPool.Restore(oldestTk);
                }
            }
            public void Dispose()
            {
                int y = texHeight - 1;
                if (History.Count > 0)
                    y = History[0].y + 1; //需要多1层，鉴于Out类型的Cell会出现在FirstCell的上面 
                History.Clear();
                SamplePoint.Clear();
                queue.Clear();
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

            const int minDelta = 5;  //5行一跳 

            int y = aCtx.texHeight - 2; int x = 0;
            bool find = false;
            for ( ; y >= 0; y-= minDelta)
            {
                for (x = 1; x < aCtx.texWidth; x++)
                {
                    if (aCtx.texture.GetPixel(x, y).a >= 1)
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
                    if (aCtx.texture.GetPixel(x, y).a >= 1)
                    {
                        find = true;
                        break;
                    }
                }
            }

            y--;

            for (x = 0; x < aCtx.texWidth; x++)
            {
                if (aCtx.texture.GetPixel(x, y).a >= 1)
                {
                    //find and init first cell 
                    var cell = CellPool.Get();
                    cell.Reinit(CellType.In, x, y);
                    aCtx.nextCross = cell;
                    return FSM.FirstPoint;
                }
            }
            
            Debug.LogError("Unable to find In cells");
            return FSM.Error;
        }

        public FSM FirstPoint(Context aCtx)
        {
            //先获取目标点，这个点应该是第一个Cell 
            var firstCell = aCtx.nextCross;
            if (firstCell == null)
                return FSM.Error;

            aCtx.RegisterHistoryCell(firstCell); //入库，Debug用 
            aCtx.RegisterSamplePoint(firstCell); //第一个Cell一定作为采样点输出 

            //标记firstCell左下和左上2个轮廓点 (注意，不要在此时标记firstCell本身，中心点标记发生在CalFour阶段) 
            var leftCell = aCtx.GetOrInitCellAt(firstCell.x - 1, firstCell.y); //左上的轮廓点对应左侧Cell
            if (leftCell != null) leftCell.CrossPointVisited = true;
            var leftDownCell = aCtx.GetOrInitCellAt(firstCell.x - 1, firstCell.y - 1); //左下轮廓点对应左下Cell 
            if (leftDownCell != null) leftDownCell.CrossPointVisited = true;

            //为下一轮迭代准备 
            aCtx.lastCrossDir = CrossDir.Left; //因为下一轮必然右移一格田字格 

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
            aCtx.nextCross.CrossPointVisited = true;

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

            //2个对角内点目前是非法的 
            int InCount = InList.Count;
            if (InCount == 2 && CrossOppDirLUT[InList[0]] == InList[1]) return FSM.Error;

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
                default:
                    break; //0 or 4 or more is Invalid -> return Error 
            }

            return ret;
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
            aCtx.RegisterSamplePoint(InCell);                   //使用InCell输出采样 

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




            return FSM.CalFour;
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
                        break;
                    default:
                        Debug.LogError("Unknown FSM State");
                        return;
                }
            }


        }


    }
}

