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

        public static int seqBentSeqThreshold = 5; //�ж��Ƿ񹹳ɡ�ֱ�ߡ�->�����ۡ�->��ֱ�ߡ���ֱ�߳���(���ظ���) 

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
            Sequence = 1,       //������ -> ��Ӧ��������2��(�ǶԽ�)InCell -> ���¼�������� 
            InnerBent = 2,      //���۵� -> ��Ӧ������ֻ��1��InCell
            OutterBent = 3,     //��ת�� -> ��Ӧ��������3��InCell
        }

        public enum FourDir //��������ĸ������� 
        {
            LeftDown = 0,  //aka self 
            LeftUp = 1,
            RightUp = 2,
            RightDown = 3,

            _Ct = 4
        }

        public static Array dirArray = Enum.GetValues(typeof(FourDir));

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

        //����������������ֻ��1��(��3��)�ڵ�ʱ��������һ�������㣬���ٲ�ѯ��һ��������ĳ��� 
        public static Dictionary<FourDir, Dictionary<CrossDir, CrossDir>> OneInFourLUT = new Dictionary<FourDir, Dictionary<CrossDir, CrossDir>>()
        {
            { FourDir.LeftDown, new Dictionary<CrossDir, CrossDir>(){ { CrossDir.Left, CrossDir.Down }, { CrossDir.Down, CrossDir.Left } } },
            { FourDir.LeftUp, new Dictionary<CrossDir, CrossDir>(){ { CrossDir.Left, CrossDir.Up }, { CrossDir.Up, CrossDir.Left } } },
            { FourDir.RightUp, new Dictionary<CrossDir, CrossDir>(){ { CrossDir.Up, CrossDir.Right }, { CrossDir.Right, CrossDir.Up } } },
            { FourDir.RightDown, new Dictionary<CrossDir, CrossDir>(){ { CrossDir.Down, CrossDir.Right }, { CrossDir.Right, CrossDir.Down } } },
        };

        //����������������ֻ��2���ڵ�ʱ��������һ�������㣬���ٲ�ѯ��һ��������ĳ��� 
        //ע�⣬�����ų��Խ��ڵ�������Ŀǰ��֧�� 
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
            public CrossDir lastCrossDir; //��һ��ȷ�Ϲ��������㴦�ڵ�ǰ���ֲ��Ե��ĸ���λ(�ǵü�ʱˢ��) 
            public Cell nextCross;

            public List<Cell> History = new List<Cell>();  //���ܴ�������2��Cell��ͬһ�������(��һ�����ۣ���һ������) 
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

            //�������: ʮ�������������Ӧ��Cellλ��(���ĵ����½��Ǹ�Cell) 
            //���: �ο���ͼ���������� 
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
                //�Ƿ�ʹ������Cell��Ϊָ�����������ĵ�  
                if (aCenterCell != null) nextCross = aCenterCell;

                //���ò��� 
                var curCell = nextCross; //�������������������Ӧ��Cell 
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
                History.Add(aCell);  //History��ҪΪ��Debug�� 
            }
            public void RegisterSamplePoint(Cell aCell)
            {
                SamplePoint.Add(aCell);
            }
            public void UpdateTracker(OutlineState aCurSt, Cell aKeyCell)
            {
                if (queue.Count == 0)
                {   //��ʼ״̬ 
                    OutlineTracker firstTk = OutlineTrakerPool.Get();
                    firstTk.Reinit(aCurSt);
                    firstTk.keyCell = aKeyCell;
                    queue.Enqueue(firstTk);
                    return;
                }

                var latestTk = queue.ElementAt(queue.Count - 1);
                if (latestTk.st == OutlineState.Sequence && aCurSt == OutlineState.Sequence)
                {   //�ϲ���������
                    latestTk.keyCell = aKeyCell;
                    latestTk.seqCount++;
                    return;
                }

                //���������µ�������׷��״̬ 
                OutlineTracker nextTk = OutlineTrakerPool.Get();
                nextTk.Reinit(aCurSt);
                nextTk.keyCell = aKeyCell;
                queue.Enqueue(nextTk);

                if(queue.Count == 4)
                {   //����������ҪDequeue��ͬʱִ�б�Ҫ��� 
                    var oldestTk = queue.Dequeue();
                    if (oldestTk.st == OutlineState.Sequence && latestTk.st == OutlineState.Sequence
                        && queue.Peek().st == OutlineState.OutterBent && 
                        oldestTk.seqCount >= seqBentSeqThreshold && latestTk.seqCount >= seqBentSeqThreshold)
                    {
                        if (oldestTk.keyCell != null) RegisterSamplePoint(oldestTk.keyCell); //��Ҫ��������� 
                    }
                    OutlineTrakerPool.Restore(oldestTk);
                }
            }
            public void Dispose()
            {
                int y = texHeight - 1;
                if (History.Count > 0)
                    y = History[0].y + 1; //��Ҫ��1�㣬����Out���͵�Cell�������FirstCell������ 
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

            const int minDelta = 5;  //5��һ�� 

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
            //�Ȼ�ȡĿ��㣬�����Ӧ���ǵ�һ��Cell 
            var firstCell = aCtx.nextCross;
            if (firstCell == null)
                return FSM.Error;

            aCtx.RegisterHistoryCell(firstCell); //��⣬Debug�� 
            aCtx.RegisterSamplePoint(firstCell); //��һ��Cellһ����Ϊ��������� 

            //���firstCell���º�����2�������� (ע�⣬��Ҫ�ڴ�ʱ���firstCell�������ĵ��Ƿ�����CalFour�׶�) 
            var leftCell = aCtx.GetOrInitCellAt(firstCell.x - 1, firstCell.y); //���ϵ��������Ӧ���Cell
            if (leftCell != null) leftCell.CrossPointVisited = true;
            var leftDownCell = aCtx.GetOrInitCellAt(firstCell.x - 1, firstCell.y - 1); //�����������Ӧ����Cell 
            if (leftDownCell != null) leftDownCell.CrossPointVisited = true;

            //Ϊ��һ�ֵ���׼�� 
            aCtx.lastCrossDir = CrossDir.Left; //��Ϊ��һ�ֱ�Ȼ����һ�����ָ� 

            //ȷ����һ�����������ĵ� 
            aCtx.nextCross = firstCell; //��ζ�ŵ�ǰCell���Ͻǵ�����������һ�ε������������ĵ� 

            //����������׷��״̬ 
            aCtx.UpdateTracker(OutlineState.InnerBent, firstCell); 

            return FSM.CalFour; //��һ��״̬������������򣬲����������ִ��״̬�л� 
        }

        public FSM CalFour(Context aCtx)
        {
            if (aCtx.nextCross == null)
                return FSM.Error;

            if (aCtx.nextCross.CrossPointVisited)
            {
                return FSM.MeetEnd; //������������ 
            }

            //��ǵ�ǰ���������ĵ�Ϊ�����ʹ���״̬ 
            aCtx.nextCross.CrossPointVisited = true;

            //�������һ��4��cell�����½�cellͬʱ��Ӧʮ������������ 
            var curCrossCenter = aCtx.nextCross;
            aCtx.FillUpWorkingFour(curCrossCenter.x, curCrossCenter.y);

            //�����ڵ� 
            List<FourDir> InList = new List<FourDir>(); 
            foreach (FourDir dir in dirArray)
            {
                if (dir == FourDir._Ct) continue;
                if (aCtx.WorkingFour[(int)dir] == null) continue;
                if (aCtx.WorkingFour[(int)dir].type == CellType.In) InList.Add(dir);
            }

            //2���Խ��ڵ�Ŀǰ�ǷǷ��� 
            int InCount = InList.Count;
            if (InCount == 2 && CrossOppDirLUT[InList[0]] == InList[1]) return FSM.Error;

            //�ж���ת״̬ 
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

            //ȷ������һ����λ��Cell 
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

            //ȷ����һ���ƶ����� 
            var sub_lut = OneInFourLUT[InCellDir];
            if (sub_lut == null) { Debug.Log("OneInFour err"); return FSM.Error;}
            var nextDir = sub_lut[aCtx.lastCrossDir];
            if (nextDir == CrossDir.Undifined) { Debug.Log("OneInFour err"); return FSM.Error;}

            //��Ҫ����һ����һ���������ָ�����Ĳ��� 
            aCtx.UpdateNextWorkingFourParams(nextDir);

            //����������׷��״̬(�����ڲ����ܻᴥ���������)
            aCtx.UpdateTracker(OutlineState.InnerBent, InCell); //�漰��������㣬һ��ʹ��InCell 

            //���۵���Ҫ�������(ע��˲�����Ҫ�ڸ���������׷��״̬֮�����)
            aCtx.RegisterSamplePoint(InCell);                   //ʹ��InCell������� 

            //ע����ʹ���Cell
            aCtx.RegisterHistoryCell(InCell);

            return FSM.CalFour;
        }

        public FSM TwoInFour(Context aCtx)
        {
            if (aCtx.lastCrossDir == CrossDir.Undifined) { Debug.Log("TwoInFour err"); return FSM.Error; }

            //ȷ������һ����λ��Cell 
            FourDir InCellDir = FourDir._Ct;
            Cell InCell = null; 
            Cell lastHistoryCell = aCtx.History[aCtx.History.Count - 1]; 
            foreach (FourDir dir in dirArray)
            {
                if (dir == FourDir._Ct) continue;
                InCell = aCtx.WorkingFour[(int)dir];
                if (InCell == null) continue;
                if (InCell.type == CellType.In && InCell != lastHistoryCell) //��Ȼ��һ��Cell������һ�η��ʵĵ� 
                {
                    InCellDir = dir;
                    break;
                }
            }
            if (InCellDir == FourDir._Ct) { Debug.Log("TwoInFour err"); return FSM.Error; }

            //ȷ����һ���ƶ����� 
            var nextDir = TwoInFourLUT[aCtx.lastCrossDir]; 
            if (nextDir == CrossDir.Undifined) { Debug.Log("TwoInFour err"); return FSM.Error; }

            //�������ֲ��� 
            aCtx.UpdateNextWorkingFourParams(nextDir);

            //����������׷��״̬(�����ڲ����ܻᴥ���������)
            aCtx.UpdateTracker(OutlineState.Sequence, InCell); //�漰��������㣬һ��ʹ��InCell 

            //ע����ʹ���Cell
            aCtx.RegisterHistoryCell(InCell);

            return FSM.CalFour;
        }

        public FSM ThreeInFour(Context aCtx)
        {
            if (aCtx.lastCrossDir == CrossDir.Undifined) { Debug.Log("ThreeInFour err"); return FSM.Error; }

            //ȷ�� OutCell ����һ����λ 
            FourDir OutCellDir = FourDir._Ct;
            Cell OutCell = null;
            //Cell lastHistoryCell = aCtx.History[aCtx.History.Count - 1];
            foreach (FourDir dir in dirArray)
            {
                if (dir == FourDir._Ct) continue;
                OutCell = aCtx.WorkingFour[(int)dir];
                if (OutCell == null || OutCell.type == CellType.Out)
                {   //null��Ӧ�߽�֮��������Ŀǰ���������������ߣ��������ﲻӰ�����Ҳ������ν�� 
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

