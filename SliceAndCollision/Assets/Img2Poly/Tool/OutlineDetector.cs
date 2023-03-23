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

        public static int seqBentSeqThreshold = 2; //�ж��Ƿ񹹳ɡ�ֱ�ߡ�->�����ۡ�->��ֱ�ߡ���ֱ�߳���(���ظ���) 
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
            Sequence = 1,       //������ -> ��Ӧ��������2��(�ǶԽ�)InCell -> ���¼�������� 
            InnerBent = 2,      //���۵� -> ��Ӧ������ֻ��1��InCell
            OutterBent = 3,     //��ת�� -> ��Ӧ��������3��InCell
            Zigzag = 4,         //�������� 
        }

        public enum FourDir //��������ĸ������� 
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

            public List<Vector3> PolygonOutlines = new List<Vector3>();

            public Queue<OutlineTracker> queue = new Queue<OutlineTracker>();
            public LinkedList<OutlineTracker> lqueue = new LinkedList<OutlineTracker>();

            public Texture2D texture;
            public int texWidth = 0;
            public int texHeight = 0;
            public Cell[,] mTable;
            public CrossDir lastCrossDir; //��һ��ȷ�Ϲ��������㴦�ڵ�ǰ���ֲ��Ե��ĸ���λ(�ǵü�ʱˢ��) 
            public Cell nextCross;

            public List<Cell> History = new List<Cell>();  //���ܴ�������2��Cell��ͬһ�������(��һ�����ۣ���һ������) 
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
                    mTable[aX, aY].Reinit(col.a > 0 ? CellType.In : CellType.Out, aX, aY);
                }

                return mTable[aX, aY];
            }
            public void RegisterHistoryCell(Cell aCell)
            {   //History����׷����һ�����ʹ���InCell�����Ҳ��Debug�� 
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
                {   //��ʼ״̬ 
                    OutlineTracker firstTk = OutlineTrakerPool.Get();
                    firstTk.Reinit(aCurSt);
                    firstTk.keyCell = aKeyCell;
                    lqueue.AddLast(firstTk);
                    RegisterSamplePoint(aKeyCell);  //��һ����ض����� 
                    return;
                }

                var latestTk = lqueue.Last(); 
                if (latestTk.st == OutlineState.Sequence && aCurSt == OutlineState.Sequence)
                {   //�ϲ���������
                    latestTk.keyCell = aKeyCell;
                    latestTk.seqCount++;
                    return;
                }

                //�ж������������������������ 
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

                //���������µ�������׷��״̬ 
                OutlineTracker nextTk = OutlineTrakerPool.Get();
                nextTk.Reinit(aCurSt);
                nextTk.keyCell = aKeyCell;
                lqueue.AddLast(nextTk);
            }

            public void Dispose()
            {
                int y = texHeight - 1;
                if (History.Count > 0)
                    y = History[0].y + 1; //��Ҫ��1�㣬����Out���͵�Cell�������FirstCell������ 
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
            Init,       //��ʼ��������Ѱ�ҵ�һ�����λ��
            FirstPoint, //�����һ���㣬ȷ����һ����(���ʾ���CalCross�������) 
            CalFour,    //�������ƶ�����һ��ȷ�����������ϣ�����������䣬�������ж�״̬  
            OneInFour,  //��ֻ��1���ڵ�
            TwoInFour,  //����2���ڵ�
            ThreeInFour,//����3���ڵ�
            DiagonalInFour, //����2���Խ��ڵ�
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
                    aCtx.nextCross = aCtx.GetOrInitCellAt(x - 1, y); //��һ���ڲ������ߵĵ�(�����ϽǶ�Ӧ��������) 
                    return FSM.FirstPoint;
                }
            }
            
            Debug.LogError("Unable to find In cells");
            return FSM.Error;
        }

        public FSM FirstPoint(Context aCtx)
        {
            //�Ȼ�ȡĿ��㣬�����Ӧ���ǵ�һ��Cell 
            var leftDownCell = aCtx.nextCross;
            var firstCell = aCtx.GetOrInitCellAt(leftDownCell.x + 1, leftDownCell.y); //��һ���ڵ��Ȼ��nextCross���Ҳ� 
            if (firstCell == null)
                return FSM.Error;

            aCtx.RegisterHistoryCell(firstCell); //��һ��InCell�ǵ���Ψһ���ڵ�InCell����Ӧ����History 
            //aCtx.RegisterSamplePoint(firstCell); //��һ��Cellһ����Ϊ��������� 

            //���leftDownCell�����·�2��������  
            leftDownCell.CrossPointVisited = true;
            var leftDownDownCell = aCtx.GetOrInitCellAt(leftDownCell.x, leftDownCell.y - 1); //�·��������Ӧ����Cell 
            if (leftDownDownCell != null) leftDownDownCell.CrossPointVisited = true;

            //Ϊ��һ�ֵ���׼�� 
            aCtx.lastCrossDir = CrossDir.Left; //��Ϊ��һ�ֱ�Ȼ����һ�����ָ�������ô��ǰ��ʮ�����ĵ�����һ���������������Left  

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
            aCtx.nextCross.CrossPointVisited = true;  //662  (1500 - 1 - 920 = 579)

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

            
            int InCount = InList.Count;
            if (InCount == 2 && CrossOppDirLUT[InList[0]] == InList[1])
            {
                InCount = 4; //����2���Խ��ڵ����� 
                aCtx.nextCross.CrossPointVisited = false; 
            }

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

            //��Ҫ����һ����һ���������ָ�����Ĳ��� 
            aCtx.UpdateNextWorkingFourParams(nextDir);

            //����������׷��״̬(�����ڲ����ܻᴥ���������)
            aCtx.UpdateTracker(OutlineState.InnerBent, InCell); //DiagonalInFourʹ��InnerBent 

            //���۵���Ҫ�������(ע��˲�����Ҫ�ڸ���������׷��״̬֮�����) 
            //aCtx.RegisterSamplePoint(InCell);                   //ʹ��InCell������� 

            //ע����ʹ���Cell
            aCtx.RegisterHistoryCell(InCell);

            return FSM.CalFour;
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
            //aCtx.RegisterSamplePoint(InCell);                   //ʹ��InCell������� 

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

            //ȷ����һ���ƶ����� 
            var sub_lut = OneInFourLUT[OutCellDir];
            if (sub_lut == null) { Debug.Log("ThreeInFour err"); return FSM.Error; }
            var nextDir = sub_lut[aCtx.lastCrossDir];
            if (nextDir == CrossDir.Undifined) { Debug.Log("ThreeInFour err"); return FSM.Error; }

            //ȷ��δ���ʵ�InCellλ�� 
            Cell InCell = null;  //Ҫ�ҵĵ� 
            Cell lastHistoryCell = aCtx.History[aCtx.History.Count - 1]; //���ʹ��ĵ� 
            Cell cornerCell = aCtx.WorkingFour[(int)CrossOppDirLUT[OutCellDir]]; //OutCell�Ķ����ǵ� 
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

            //�������ֲ��� 
            aCtx.UpdateNextWorkingFourParams(nextDir);

            //����������׷��״̬(�����ڲ����ܻᴥ���������)
            aCtx.UpdateTracker(OutlineState.OutterBent, InCell); 

            //ע����ʹ���Cell
            aCtx.RegisterHistoryCell(InCell);

            return FSM.CalFour;
        }

        public FSM MeetEnd(Context aCtx)
        {
            aCtx.UpdateTracker(OutlineState.OutterBent, null);
            aCtx.UpdateTracker(OutlineState.OutterBent, null); 
            aCtx.UpdateTracker(OutlineState.OutterBent, null); //��ʹTracker�еĲ����㶼��� 

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

