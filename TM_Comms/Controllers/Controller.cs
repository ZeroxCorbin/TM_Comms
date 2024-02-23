using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TM_Comms.Controllers
{
    public enum SocketStates
    {
        Closed,
        Trying,
        Open,
        Exception
    }


    public class Controller
    {
        public delegate void RobotDataUpdatedDel();
        public event RobotDataUpdatedDel RobotDataUpdated;
        public double NominalOffset { get; } = -100;

        //private Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        public enum StateEnum
        {
            Timeout,
            OK,
            Exit
        }
        public enum WaitingForEnum
        {
            OK,
            QueueTag,
            ListenNodeEntry,
            ListenSend,
            MotionComplete,
            Status,
            BaseChange
        }

        public class ResultState
        {
            public StateEnum State { get; set; }
            public WaitingForEnum WaitingFor { get; set; }

            public string Script { get; set; }
        }

        public bool IsEsReady { get => isEsReady && EthernetSlaveController.IsConnected; }
        private bool isEsReady = false;

        public bool IsLnReady { get => ListenNodeController.IsConnected; }

        public bool IsReady => IsEsReady && IsLnReady;

        private object UpdateLock = new object();

        public class Tool
        {
            public string Name { get; set; } = "";
            public string Position { get; set; } = "0,0,0,0,0,0";
            [JsonIgnore]
            public string FilePath { get; set; } = "";
        }

        public string ToolsDirectory { get; set; }
        public Dictionary<string, Tool> Tools { get; } = new Dictionary<string, Tool>();

        public class Base
        {
            public string Name { get; set; }
            public string Position { get; set; }
            [JsonIgnore]
            public string FilePath { get; set; }
        }
        public string BasesDirectory { get; set; }
        public Dictionary<string, Base> Bases { get; } = new Dictionary<string, Base>();

        public string CurrentTool { get; private set; } = "";
        private MotionScriptBuilder.Position currentToolValue;
        public MotionScriptBuilder.Position CurrentToolValue
        {
            get
            {
                lock (lockObject)
                    return currentToolValue != null ? currentToolValue : new MotionScriptBuilder.Position("0,0,0,0,0,0");
            }
            private set
            {
                lock (lockObject)
                    currentToolValue = value;
            }
        }
        public string CurrentBase { get; private set; } = "";

        private object lockObject = new object();

        private MotionScriptBuilder.Cartesian currentPosition;
        public MotionScriptBuilder.Cartesian CurrentPosition
        {
            get
            {
                lock (lockObject)
                    return currentPosition != null ? currentPosition : new MotionScriptBuilder.Cartesian("0,0,0,0,0,0");
            }
            private set
            {
                lock (lockObject)
                    currentPosition = value;
            }
        }
        public MotionScriptBuilder.Joint CurrentJointPosition { get; private set; }

        public EthernetSlaveController EthernetSlaveController { get; } = new EthernetSlaveController();
        public ListenNodeController ListenNodeController { get; } = new ListenNodeController();
        public bool ListenNodeWait { get; private set; } = false;

        private bool WaitingForOK;
        private string WaitingForOK_ScriptID;

        private bool WaitingForStatus;
        private string WaitingForStatus_SubCommand;
        private string WaitingForStatus_Result;

        private bool WaitingForQueueTag;
        private string WaitingForQueueTag_TagNumber;

        private bool WaitingForListenSend;
        private string WaitingForListenSend_ScriptID;
        private string WaitingForListenSend_Result;

        private bool WaitingForListenNode;
        private string WaitingForListenNode_Name;

        public Controller()
        {
            EthernetSlaveController.SocketStateEvent += EthernetSlaveController_SocketStateEvent;
            EthernetSlaveController.EsStateEvent += EthernetSlaveController_EsStateEvent;

            //ListenNodeController.SocketStateEvent += ListenNodeController_SocketStateEvent;
            ListenNodeController.MessageEvent += ListenNodeController_MessageEvent;

            LoadToolsList();
            LoadBasesList();
        }

        public Controller(string toolsDirectory, string basesDirectory)
        {
            ToolsDirectory = toolsDirectory;
            BasesDirectory = basesDirectory;

            EthernetSlaveController.SocketStateEvent += EthernetSlaveController_SocketStateEvent;
            EthernetSlaveController.EsStateEvent += EthernetSlaveController_EsStateEvent;

            //ListenNodeController.SocketStateEvent += ListenNodeController_SocketStateEvent;
            ListenNodeController.MessageEvent += ListenNodeController_MessageEvent;

            LoadToolsList();
            LoadBasesList();
        }

        public Controller(string filesDirectory)
        {
            ToolsDirectory = filesDirectory;
            BasesDirectory = filesDirectory;

            EthernetSlaveController.SocketStateEvent += EthernetSlaveController_SocketStateEvent;
            EthernetSlaveController.EsStateEvent += EthernetSlaveController_EsStateEvent;

            //ListenNodeController.SocketStateEvent += ListenNodeController_SocketStateEvent;
            ListenNodeController.MessageEvent += ListenNodeController_MessageEvent;

            LoadToolsList();
            LoadBasesList();
        }

        public void Connect(string ip)
        {
            EthernetSlaveController.Connect(ip);
            ListenNodeController.Connect(ip);
        }

        public void Disconnect()
        {
            EthernetSlaveController.Disconnect();
            ListenNodeController.Disconnect();
        }

        public async Task<ResultState> GetListenNodeStatus(string nodeName = null)
        {
            ListenNodeController.Send(new ListenNode("00", ListenNode.Headers.TMSTA, "status").Message);
            return await WaitForStatus("00");
        }

        public async Task<bool> SetBase(string name)
        {
            if (CurrentBase.Equals(name))
                return true;

            if (!await IsListenNodeReady())
                return false;

            if (string.IsNullOrEmpty(name))
                name = "RobotBase";

            PreWaitForOK("base");

            ListenNodeController.Send(new ListenNode($"ChangeBase(\"{name}\")", ListenNode.Headers.TMSCT, "base").Message);

            var res = await WaitForOK();

            if (res.State == StateEnum.OK)
                await Task.Run(() =>
                {
                    DateTime start = DateTime.Now;
                    while (!CurrentBase.Equals(name))
                    {
                        if ((DateTime.Now - start) > TimeSpan.FromMilliseconds(1000))
                            break;
                        Thread.Sleep(1);
                    }
                });

            return CurrentBase.Equals(name);
        }

        public async Task<bool> SetBase(Base _base)
        {
            if (!await IsListenNodeReady())
                return false;

            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(_base.Name))
            {
                if (_base.Name.Equals("RobotBase"))
                {
                    if (CurrentBase.Equals(_base.Name))
                        return true;

                    sb.AppendLine($"ChangeBase(\"{_base.Name}\")");
                }
                else
                {
                    if (!string.IsNullOrEmpty(_base.Position))
                        sb.AppendLine($"Base[\"{_base.Name}\"].Value={{{_base.Position}}}");
                    sb.AppendLine($"ChangeBase(\"{_base.Name}\")");
                }
            }
            else
            {
                sb.AppendLine($"ChangeBase({_base.Position})");
            }

            PreWaitForOK("base");

            ListenNodeController.Send(new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT, "base").Message);

            var res = await WaitForOK();

            if (res.State == StateEnum.OK)
                await Task.Run(() =>
                {
                    DateTime start = DateTime.Now;
                    while (!CurrentBase.Equals(_base.Name))
                    {
                        if ((DateTime.Now - start) > TimeSpan.FromMilliseconds(1000))
                            break;
                        Thread.Sleep(1);
                    }
                });
            else
            {

            }
            return CurrentBase.Equals(_base.Name);
        }

        public async Task<ResultState> GetBaseCoords(string name)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            var ln = new ListenNode($"ListenSend(90, GetString(Base[\"{name}\"].Value))", ListenNode.Headers.TMSCT, "baseCoords");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend("90");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK = false;
                return value;
            }

            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }

        public async Task<bool> SetTool(string name)
        {
            if (!await IsListenNodeReady())
                return false;

            if (string.IsNullOrEmpty(name))
                return false;

            ListenNodeController.Send(new ListenNode($"ChangeTCP(\"{name}\")", ListenNode.Headers.TMSCT, "tool").Message);

            await Task.Run(() =>
            {
                DateTime start = DateTime.Now;
                while (!CurrentTool.Equals(name))
                {
                    if ((DateTime.Now - start) > TimeSpan.FromMilliseconds(1000))
                        break;
                    Thread.Sleep(1);
                }
            });

            return CurrentTool.Equals(name);
        }

        public async Task<bool> SetTool(Tool tool)
        {
            if (!await IsListenNodeReady())
                return false;

            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(tool.Name))
            {
                if (tool.Name.Equals("NOTOOL"))
                {
                    if (CurrentTool.Equals(tool.Name))
                        return true;

                    sb.AppendLine($"ChangeTCP(\"{tool.Name}\")");
                }
                else
                {
                    sb.AppendLine($"TCP[\"{tool.Name}\"].Value={{{tool.Position}}}");
                    sb.AppendLine($"ChangeTCP(\"{tool.Name})");
                }
            }
            else
            {
                sb.AppendLine($"ChangeTCP({tool.Position})");
            }

            PreWaitForOK("tool");

            ListenNodeController.Send(new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT, "tool").Message);

            var res = await WaitForOK();

            if (res.State == StateEnum.OK)
                await Task.Run(() =>
                {
                    DateTime start = DateTime.Now;
                    while (!CurrentTool.Equals(tool.Name))
                    {
                        if ((DateTime.Now - start) > TimeSpan.FromMilliseconds(1000))
                            break;
                        Thread.Sleep(1);
                    }
                });

            return CurrentTool.Equals(tool.Name);
        }

        public async Task<ResultState> GetToolCoords(string name)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            var ln = new ListenNode($"ListenSend(90, GetString(TCP[\"{name}\"].Value))", ListenNode.Headers.TMSCT, "toolCoords");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend("90");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK = false;
                return value;
            }

            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }

        public async Task<ResultState> AquireLandmark()
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            var ln = new ListenNode("ListenSend(90, GetString(Vision_DoJob(\"Landmark\"),2,0))", ListenNode.Headers.TMSCT, "land");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend("90");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK = false;   
                return value;
            }

            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }



        public async Task<ResultState> GetDist(MotionScriptBuilder.Position first, MotionScriptBuilder.Position second, int listenSendIndex = 0)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"ListenSend(9{listenSendIndex}, GetString(dist({{{first.ToCSV}}}, {{{second.ToCSV}}}), 10, 3))");

            var ln = new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT, "dist");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend($"9{listenSendIndex}");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK = false;
return value;
            }
                

            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }

        public async Task<ResultState> GetPoints2Coord(MotionScriptBuilder.Position origin, MotionScriptBuilder.Position xOffset, MotionScriptBuilder.Position yOffset, int listenSendIndex = 0)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"ListenSend(9{listenSendIndex}, GetString(points2coord({{{origin.ToCSV}}}, {{{xOffset.ToCSV}}}, {{{yOffset.ToCSV}}})))");

            var ln = new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT, "points2coord");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend($"9{listenSendIndex}");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK = false;
return value;
            }
                
            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }

        public async Task<ResultState> GetApplyTrans(MotionScriptBuilder.Position initial, MotionScriptBuilder.Position offset, bool initialPoint = false, int listenSendIndex = 0)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"ListenSend(9{listenSendIndex}, GetString(applytrans({{{initial.ToCSV}}}, {{{offset.ToCSV}}}, {initialPoint}), 10, 3))");

            var ln = new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT, "applytrans");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend($"9{listenSendIndex}");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK = false;
                return value;

            }
                
            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }

        public async Task<ResultState> GetInterPoint(MotionScriptBuilder.Position first, MotionScriptBuilder.Position second, double ratio, int listenSendIndex = 0)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"ListenSend(9{listenSendIndex}, GetString(interpoint({{{first.ToCSV}}}, {{{second.ToCSV}}}, {ratio}), 10, 3))");

            var ln = new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT, "interpoint");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend($"9{listenSendIndex}");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK   = false;
                return value;
            }
                
            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }


        public async Task<ResultState> GetTrans(MotionScriptBuilder.Position start, MotionScriptBuilder.Position second, bool referenceFirst = false, int listenSendIndex = 0)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"ListenSend(9{listenSendIndex}, GetString(trans({{{start.ToCSV}}}, {{{second.ToCSV}}}, {referenceFirst}), 10, 3))");

            var ln = new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT, "trans");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend($"9{listenSendIndex}");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK = false;
                return value;
            }

            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }

        public async Task<ResultState> GetChangeRef(MotionScriptBuilder.Position originalPoint, MotionScriptBuilder.Position originalFrame, MotionScriptBuilder.Position newFrame, int listenSendIndex = 0)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"ListenSend(9{listenSendIndex}, GetString(changeref({{{originalPoint.ToCSV}}}, {{{originalFrame.ToCSV}}}, {{{newFrame.ToCSV}}})))");

            var ln = new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT, "changeref");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend($"9{listenSendIndex}");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK = false;
                return value;
            }

            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }

        public async Task<ResultState> GetInverse(MotionScriptBuilder.Position trans, bool baseRelative = false, int listenSendIndex = 0)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"ListenSend(9{listenSendIndex}, GetString(inversetrans({{{trans.ToCSV}}}, {baseRelative})))");

            var ln = new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT, "inverse");

            PreWaitForOK(ln.ScriptID);
            PreWaitForListenSend($"9{listenSendIndex}");

            ListenNodeController.Send(ln.Message);

            ResultState value;
            if ((value = await WaitForListenSend()).State != StateEnum.OK)
            {
                WaitingForOK = false;
                return value;
            }

            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
                return res;

            return value;
        }

        public async Task<ResultState> MoveTo_Async(MotionScriptBuilder.MoveStep move, int queueTagNumber = 0)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            if (!string.IsNullOrEmpty(move.BaseName))
                if (!move.BaseName.Equals(CurrentBase))
                    if (!await SetBase(move.BaseName))
                        return new ResultState() { WaitingFor = WaitingForEnum.BaseChange, State = StateEnum.Timeout };

            var mb = new MotionScriptBuilder(new List<MotionScriptBuilder.MoveStep>() { move });
            var ln = mb.BuildMotionScript(false, queueTagNumber);

            return await WaitForMotion(ln, queueTagNumber);
        }

        public async Task<ResultState> MoveTo_Async(List<MotionScriptBuilder.MoveStep> moves, int queueTagNumber = 0)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            if (!moves[0].BaseName.Equals(CurrentBase))
                if (!await SetBase(moves[0].BaseName))
                    return new ResultState() { WaitingFor = WaitingForEnum.BaseChange, State = StateEnum.Timeout };

            var mb = new MotionScriptBuilder(moves);
            var ln = mb.BuildMotionScript(false, queueTagNumber);

            return await WaitForMotion(ln, queueTagNumber);
        }

        public async Task<ResultState> ExecuteScript_Async(string script, int queueTagNumber = 0, bool scriptExit = false)
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            var ln = new ListenNode(script);

            PreWaitForOK(ln.ScriptID);
            if (queueTagNumber > 0)
                PreWaitForQueueTag(queueTagNumber);

            ListenNodeController.Send(ln.Message);

            ResultState res;
            if ((res = await WaitForOK()).State != StateEnum.OK)
            {
                WaitingForQueueTag = false;
                return res;
            }

            if (queueTagNumber > 0)
                if ((res = await WaitForQueueTag()).State != StateEnum.OK)
                    return res;

            if (scriptExit)
                if ((res = await ScriptExit(1, "Listen1")).State != StateEnum.OK)
                    return res;

            return res;

        }

        public async Task<ResultState> ScriptExit(int mode = 0, string nodeName = "Listen1")
        {
            if (!await IsListenNodeReady())
                return new ResultState() { WaitingFor = WaitingForEnum.Status, State = StateEnum.Timeout };

            ListenNodeController.Send(new ListenNode($"ScriptExit({mode})", ListenNode.Headers.TMSCT, "exit").Message);

            return await WaitForListenNodeEntry(nodeName);
        }
        public async void StopMotion()
        {
            //EthernetSlaveController?.Send($"$TMSVR,20,local,2,Stick_Stop=1,*12\r\n");

            ListenNodeWait = true;



            //ListenNodeController.Send(new ListenNode("StopAndClearBuffer(2)\r\nScriptExit()\r\n", ListenNode.Headers.TMSCT, "stop").Message);
            //ListenNodeController.Send(new ListenNode("ScriptExit(1)", ListenNode.Headers.TMSCT, "local").Message);
            //await WaitForListenNode();

            await ScriptExit(1);

            //WaitingForOK = false;
            //WaitingForQueueTag = false;
            //WaitingForListenSend = false;
            //WaitingForListenNode = false;

            ListenNodeWait = false;
        }

        public async Task<bool> IsListenNodeReady(string nodeName = "Listen1")
        {
            var res = await GetListenNodeStatus(nodeName);

            if (res.State == StateEnum.OK)
                return res.Script.StartsWith("true");
            else
                return false;
        }

        private async Task<ResultState> WaitForMotion(ListenNode ln, int queueTagNumber, int timeout = 5000)
        {
            PreWaitForOK(ln.ScriptID);
            PreWaitForQueueTag(queueTagNumber);

            ListenNodeController.Send(ln.Message);

            ResultState res;
            if ((res = await WaitForOK(timeout)).State == StateEnum.OK)
            {
                if (queueTagNumber > 0)
                    if ((res = await WaitForQueueTag()).State != StateEnum.OK)
                        return res;

                return new ResultState() { WaitingFor = WaitingForEnum.MotionComplete, State = StateEnum.OK };
            }

            WaitingForQueueTag = false;

            return res;
        }

        public void PreWaitForOK(string scriptID)
        {
            WaitingForOK = true;
            WaitingForOK_ScriptID = scriptID;
        }
        public async Task<ResultState> WaitForOK(int timeout = 5000)
        {
            await Task.Run(() =>
            {
                DateTime start = DateTime.Now;
                while (WaitingForOK)
                {
                    if ((DateTime.Now - start) > TimeSpan.FromMilliseconds(timeout))
                        break;

                    Thread.Sleep(1);
                }
            });

            var res = new ResultState() { WaitingFor = WaitingForEnum.OK };
            if (WaitingForOK)
            {
                //Logger.Debug($"WaitForOK({WaitingForOK_ScriptID}) Timeout! ({timeout})");
                res.State = StateEnum.Timeout;
            }
            else
            {
                if (WaitingForOK_ScriptID == null)
                {
                    //Logger.Debug($"ScriptExit detected when waiting for OK.");
                    res.State = StateEnum.Exit;
                }
                else
                {
                    res.State = StateEnum.OK;
                }
            }

            WaitingForOK = false;
            WaitingForOK_ScriptID = "";

            return res;
        }

        public void PreWaitForQueueTag(int queueTagNumber)
        {
            WaitingForQueueTag = true;
            WaitingForQueueTag_TagNumber = queueTagNumber.ToString("D2");
        }
        public async Task<ResultState> WaitForQueueTag()
        {
            await Task.Run(() =>
            {
                DateTime start = DateTime.Now;
                while (WaitingForQueueTag)
                {
                    if (!IsReady)
                        break;

                    Thread.Sleep(1);
                }
            });

            var res = new ResultState() { WaitingFor = WaitingForEnum.QueueTag };

            if (WaitingForQueueTag_TagNumber == null)
            {
                //Logger.Debug($"ScriptExit detected when waiting for QueueTag.");
                res.State = StateEnum.Exit;
            }
            else
            {
                if (WaitingForQueueTag)
                    res.State = StateEnum.Exit;
                else
                    res.State = StateEnum.OK;
            }

            WaitingForQueueTag = false;
            WaitingForQueueTag_TagNumber = "";

            return res;
        }

        public void PreWaitForListenSend(string scriptID)
        {
            WaitingForListenSend = true;
            WaitingForListenSend_ScriptID = scriptID;
        }
        public async Task<ResultState> WaitForListenSend(int timeout = 5000)
        {
            await Task.Run(() =>
            {
                DateTime start = DateTime.Now;
                while (WaitingForListenSend)
                {
                    if ((DateTime.Now - start) > TimeSpan.FromMilliseconds(timeout))
                        break;
                    Thread.Sleep(1);
                }
            });

            var res = new ResultState() { WaitingFor = WaitingForEnum.ListenSend };
            if (WaitingForListenSend)
            {
                //Logger.Debug($"ListenSend({WaitingForListenSend_ScriptID}) Timeout! ({timeout})");
                res.State = StateEnum.Timeout;
            }
            else
            {
                res.State = StateEnum.OK;
                res.Script = WaitingForListenSend_Result;
            }

            WaitingForListenSend_ScriptID = "";
            WaitingForListenSend = false;
            WaitingForListenSend_Result = "";

            return res;
        }
        public async Task<ResultState> WaitForStatus(string subCommand, int timeout = 5000)
        {
            WaitingForStatus = true;
            WaitingForStatus_SubCommand = subCommand;

            await Task.Run(() =>
            {
                DateTime start = DateTime.Now;
                while (WaitingForStatus)
                {
                    if ((DateTime.Now - start) > TimeSpan.FromMilliseconds(timeout))
                        break;
                    Thread.Sleep(1);
                }
            });

            var res = new ResultState() { WaitingFor = WaitingForEnum.Status };
            if (WaitingForStatus)
                res.State = StateEnum.Timeout;
            else
            {
                res.State = StateEnum.OK;
                res.Script = WaitingForStatus_Result;
            }

            WaitingForStatus_SubCommand = "";
            WaitingForStatus = false;
            WaitingForStatus_Result = "";

            return res;
        }
        public async Task<ResultState> WaitForListenNodeEntry(string nodeName = "Listen1", int timeout = 5000)
        {
            WaitingForListenNode = true;
            WaitingForListenNode_Name = nodeName;

            await Task.Run(() =>
            {
                DateTime start = DateTime.Now;
                while (WaitingForListenNode)
                {
                    if ((DateTime.Now - start) > TimeSpan.FromMilliseconds(timeout))
                        break;
                    Thread.Sleep(1);
                }
            });

            var res = new ResultState() { WaitingFor = WaitingForEnum.ListenNodeEntry };
            if (WaitingForListenNode)
                res.State = StateEnum.Timeout;
            else
            {
                res.State = StateEnum.OK;
                res.Script = WaitingForStatus_Result;
            }

            WaitingForListenNode = false;
            WaitingForListenNode_Name = "";

            return res;
        }


        private void ListenNodeController_MessageEvent(ListenNodeController.LnStates state, string message, ListenNode listenNode)
        {
            if (listenNode.Header == ListenNode.Headers.TMSCT)
            {
                if (listenNode.ScriptID == "exit")
                {
                    WaitingForQueueTag_TagNumber = null;
                    WaitingForQueueTag = false;

                    WaitingForOK_ScriptID = null;
                    WaitingForOK = false;

                    return;
                }

                if (WaitingForOK)
                    if (listenNode.ScriptID == WaitingForOK_ScriptID)
                    {
                        if (listenNode.Script.StartsWith("OK"))
                        {
                            WaitingForOK = false;
                            return;
                        }
                    }


                if (WaitingForListenNode)
                    if (listenNode.Script.StartsWith(WaitingForListenNode_Name))
                        WaitingForListenNode = false;
            }
            else if (listenNode.Header == ListenNode.Headers.TMSTA)
            {
                if (WaitingForStatus)
                {
                    if (listenNode.ScriptID == WaitingForStatus_SubCommand)
                    {
                        WaitingForStatus_Result = listenNode.Script;
                        WaitingForStatus = false;
                        return;
                    }

                }

                if (WaitingForListenSend)
                    if (listenNode.ScriptID == WaitingForListenSend_ScriptID)
                    {
                        WaitingForListenSend_Result = listenNode.Script;
                        WaitingForListenSend = false;
                        return;
                    }


                if (WaitingForQueueTag)
                {
                    if (listenNode.ScriptID == WaitingForQueueTag_TagNumber)
                    {
                        if (listenNode.Script.StartsWith($"{WaitingForQueueTag_TagNumber}"))
                        {
                            WaitingForQueueTag = false;
                            return;
                        }
                        else
                        {

                        }
                    }
                    else
                    {

                    }

                }

            }


            //if (state == Controllers.ListenNodeController.LnStates.Response)
            //{
            //    if (listenNode.ScriptID == "base")
            //    {
            //        if (listenNode.Script.StartsWith("OK"))
            //        {

            //        }
            //        else
            //        {

            //        }
            //    }

            //    if (listenNode.ScriptID == "land")
            //    {
            //        if (listenNode.Script.StartsWith("OK"))
            //        {

            //        }
            //        else
            //        {

            //        }
            //    }


            //    if (listenNode.ScriptID == "90")
            //    {
            //        if (listenNode.Script.StartsWith("true"))
            //        {
            //            App.RobotController.ListenNodeController.Send(new ListenNode("ChangeBase(\"vision_Landmark\")", ListenNode.Headers.TMSCT, "base").Message);
            //        }
            //        else
            //        {
            //            Status = "Landmark Not Found!";
            //        }
            //    }
            //}
        }

        private void EthernetSlaveController_SocketStateEvent(SocketStates state, string message)
        {
            switch (state)
            {
                case SocketStates.Closed:
                    Reset();
                    break;

                case SocketStates.Trying:
                    break;

                case SocketStates.Open:
                    break;

                case SocketStates.Exception:
                    Reset();
                    break;
            }
        }
        private void EthernetSlaveController_EsStateEvent(EthernetSlaveController.EsStates state, string message, EthernetSlave ethernetSlave)
        {
            if (state == EthernetSlaveController.EsStates.Normal)
            {
                lock (UpdateLock)
                {
                    CurrentBase = ethernetSlave.GetValue("Base_Name").Trim('\"');
                    CurrentTool = ethernetSlave.GetValue("TCP_Name").Trim('\"');
                    CurrentToolValue = new MotionScriptBuilder.Position(ethernetSlave.GetValue("TCP_Value"));
                    CurrentPosition = new MotionScriptBuilder.Cartesian(ethernetSlave.GetValue("Coord_Base_Tool"));
                    CurrentJointPosition = new MotionScriptBuilder.Joint(ethernetSlave.GetValue("Joint_Angle")) { Type = MotionScriptBuilder.PositionTypes.JOINT };

                    isEsReady = true;

                    Task.Run(() => RobotDataUpdated?.Invoke());
                }
            }
        }

        public void LoadToolsList()
        {
            Tools.Clear();

            Tools.Add("NOTOOL", new Tool() { Name = "NOTOOL" });

            if (string.IsNullOrEmpty(ToolsDirectory)) return;

            foreach (var file in Directory.EnumerateFiles(ToolsDirectory))
            {
                if (!file.EndsWith(".tool")) continue;

                var data = File.ReadAllText(file);
                var tool = JsonConvert.DeserializeObject<Tool>(data);
                if (tool != null)
                {
                    tool.FilePath = file;
                    if (!Tools.ContainsKey(Path.GetFileNameWithoutExtension(file)))
                        Tools.Add(Path.GetFileNameWithoutExtension(file), tool);
                }

            }
        }

        public void LoadBasesList()
        {
            Bases.Clear();

            Bases.Add("RobotBase", new Base() { Name = "RobotBase" });
            Bases.Add("vision_Landmark", new Base() { Name = "vision_Landmark" });

            if (string.IsNullOrEmpty(BasesDirectory))
                return;

            foreach (var file in Directory.EnumerateFiles(BasesDirectory))
            {
                if (!file.EndsWith(".base")) continue;

                var data = File.ReadAllText(file);
                var base1 = JsonConvert.DeserializeObject<Base>(data);

                base1.FilePath = file;

                Bases.Add(Path.GetFileNameWithoutExtension(file), base1);
            }
        }

        private void Reset()
        {
            lock (UpdateLock)
            {
                CurrentBase = "";
                CurrentTool = "";
                CurrentPosition = null;
                CurrentJointPosition = null;
                isEsReady = false;
            }

            Task.Run(() => RobotDataUpdated?.Invoke());
        }


    }
}
