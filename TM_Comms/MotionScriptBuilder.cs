using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TM_Comms;

namespace TM_Comms
{
    public partial class MotionScriptBuilder
    {
        public int Step { get; private set; } = 1;
        public StringBuilder MotionScript { get; private set; } = new StringBuilder();

        public List<MoveStep> Moves { get; set; }

        public MotionScriptBuilder() => Moves = new List<MoveStep>();
        public MotionScriptBuilder(List<MoveStep> moves) => Moves = moves;

        public ListenNode BuildMotionScript(bool addScriptExit, bool initVariables = true)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n");

            int step = 1;
            foreach (MoveStep ms in Moves)
            {
                if (!initVariables)
                    sb.Append(ms.MoveCommand());
                else
                    sb.Append(ms.MoveCommand(step++, initVariables));
            }

            //One (1) will always be the script complete queue tag. 
            sb.Append(GetQueueTag(1));

            if (addScriptExit)
                sb.Append(GetScriptExit());

            return new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT);
        }

        private string GetQueueTag(int num) => $"QueueTag({num})";
        private string GetWaitQueueTag(int num, int timeout) => $"WaitQueueTag({num:D2},{timeout})";
        private string GetScriptExit() => $"ScriptExit()";


        public void MS_Start()
        {
            MotionScript.Clear();
            Step = 1;
        }
        public void MS_AddBaseChange(string baseName) => MotionScript.AppendLine($"ChangeBase(\"{baseName}\")");
        public void MS_AddBaseChange(Position baseOffset) => MotionScript.AppendLine($"ChangeBase({baseOffset.ToCSV})");

        public void MS_AddToolChange(string toolName) => MotionScript.AppendLine($"ChangeTCP(\"{toolName}\")");
        public void MS_AddToolChange(Position toolOffset) => MotionScript.AppendLine($"ChangeTCP({toolOffset.ToCSV})");

        public void MS_AddMove(MoveStep ms, bool initVariables = true)
        {
            if (!initVariables)
                MotionScript.AppendLine(ms.MoveCommand());
            else
                MotionScript.AppendLine(ms.MoveCommand(Step++, initVariables));
        }
        public void MS_AddMoveWithOffset(MoveStep ms, Position offset, bool toolRelative, bool initVariables = true) => 
            MotionScript.AppendLine(ms.MoveWithOffsetCommand(offset, toolRelative, Step++, initVariables));

        //public string MS_AddTranform(Position start, Position offset, bool toolRelative, int posNum, bool initFloat = true) =>
        //    $"{(initFloat ? "float[]" : "")} s{posNum}={{{start.ToCSV}}}\r\n" +
        //    $"{(initFloat ? "float[]" : "")} o{posNum}={{{offset.ToCSV}}}\r\n" +
        //    $"{(initFloat ? "float[]" : "")} trans{posNum}=applytrans(s{posNum},o{posNum},{toolRelative})\r\n";

        public void MS_AddQueueTag(int num) => MotionScript.AppendLine(GetQueueTag(num));
        public void MS_AddWaitQueueTag(int num, int timeout) => MotionScript.AppendLine(GetWaitQueueTag(num, timeout));
        public void MS_AddWaitQueueTag(int timeout) => MotionScript.AppendLine(GetWaitQueueTag(0, timeout));
        public void MS_AddScriptExit() => MotionScript.AppendLine(GetScriptExit());

        //public const double MAX_BLEND_MM = 10.0;
        //public const double MIN_DISTANCE_MM = 0.05;
        //public const double MAX_VELOCITY_MMS = 2000.0;
        //public const int MAX_ACCEL_MS = 200;
        //public VerifyResult VerifyBlend()
        //{
        //    VerifyResult vr = VerifyResult.OK;

        //    //int i = 0;
        //    //MoveStep prev = null;
        //    //foreach (MoveStep ms in Moves)
        //    //{
        //    //    if (i == 0)
        //    //    {
        //    //        ms.Blent_pct = 0;
        //    //        prev = ms;
        //    //        i++;
        //    //        continue;
        //    //    }

        //    //    if (prev.Move_type != ms.Move_type)
        //    //    {
        //    //        if (prev.Blent_pct > 0)
        //    //        {
        //    //            prev.Blent_pct = 0;
        //    //            vr = VerifyResult.UPDATED;
        //    //        }
        //    //    }

        //    //    if (prev.Where.Type == Position.MType.EPOSE & ms.Where.Type == Position.MType.EPOSE)
        //    //    {
        //    //        double dist = Distance(prev.Where, ms.Where);
        //    //        if (dist < MAX_BLEND_MM)
        //    //        {
        //    //            if (dist <= MIN_DISTANCE_MM)
        //    //            {
        //    //                vr = VerifyResult.FAILED;
        //    //                break;
        //    //            }

        //    //            int blend = (int)(dist / MAX_BLEND_MM) * 100;
        //    //            if (ms.Blent_pct > blend)
        //    //            {
        //    //                ms.Blent_pct = blend;
        //    //                vr = VerifyResult.UPDATED;
        //    //            }

        //    //        }
        //    //    }

        //    //    prev = ms;
        //    //}
        //    return vr;
        //}





        //private double Distance(Position p1, Position p2) => Math.Pow((Math.Pow(p2[0] - p1[0], 2) + Math.Pow(p2[1] - p1[1], 2) + Math.Pow(p2[3] - p1[3], 2)) * 1.0, 0.5);

    }

    public partial class MotionScriptBuilder
    {
        public enum MoveTypes
        {
            PTP,
            Line,
            PLine

        }

        public enum DataFormats
        {
            CPP,
            JPP,
            CPR,
            CAP,
            CAR,
            JAP
        }

        public static Dictionary<MoveTypes, List<DataFormats>> MoveTypes_DataFormats = new Dictionary<MoveTypes, List<DataFormats>>()
        {
            { MoveTypes.PTP, new List<DataFormats>() { DataFormats.CPP, DataFormats.JPP } },
            { MoveTypes.Line, new List<DataFormats>() { DataFormats.CPP, DataFormats.CPR, DataFormats.CAP, DataFormats.CAR } },
            { MoveTypes.PLine, new List<DataFormats>() { DataFormats.CAP, DataFormats.JAP } },
        };

        public class MoveStep
        {
            public MoveTypes MoveType { get; set; }
            public DataFormats DataFormat { get; set; } //CPP, JPP, etc...

            public Position Position { get; set; }
            public int Velocity { get; set; }
            public int Accel { get; set; }
            public int Blend { get; set; }
            public bool Precision { get; set; }

            public string BaseName { get; set; }

            public string MoveCommand() => $"{MoveType}(\"{DataFormat}\",{Position.ToCSV},{Velocity},{Accel},{Blend},{(!Precision ? "true" : "false")})\r\n";
            public string MoveCommand(int posNum, bool initFloat = true) => $"{(initFloat ? "float[]" : "")} targetP{posNum}={{{Position.ToCSV}}}\r\n" +
                                                                            $"{MoveType}(\"{DataFormat}\",targetP{posNum},{Velocity},{Accel},{Blend},{(!Precision ? "true" : "false")})\r\n";

            public string MoveWithOffsetCommand(Position offset, bool toolRelative, int posNum, bool initFloat = true) =>
                $"{(initFloat ? "float[]" : "")} targetP{posNum}1={{{Position.ToCSV}}}\r\n" +
                $"{(initFloat ? "float[]" : "")} targetP{posNum}2={{{offset.ToCSV}}}\r\n" +
                $"{(initFloat ? "float[]" : "")} targetP{posNum}3=applytrans(targetP{posNum}1,targetP{posNum}2,{toolRelative})\r\n" +
                $"{MoveType}(\"{DataFormat}\",targetP{posNum}3,{Velocity},{Accel},{Blend},{(!Precision ? "true" : "false")})\r\n";

            public MoveStep(string moveType, string dataFormat, Position position, int velocity, int accel, int blend, string baseName)
            {
                if (Enum.TryParse(moveType, out MoveTypes res))
                    MoveType = res;
                else
                    MoveType = MoveTypes.PTP;

                if (Enum.TryParse(dataFormat, out DataFormats format))
                    DataFormat = format;
                else
                {
                    if (position.Type == PositionTypes.CARTESIAN)
                        DataFormat = DataFormats.CPP;
                    else
                        DataFormat = DataFormats.JPP;
                }

                Position = position;
                Accel = accel;
                Velocity = velocity;
                Blend = blend;
                BaseName = baseName;
            }
            public MoveStep(MoveTypes moveType, DataFormats dataFormat, Position position, int velocity, int accel, int blend, string baseName)
            {
                MoveType = moveType;
                DataFormat = dataFormat;

                Position = position;
                Accel = accel;
                Velocity = velocity;
                Blend = blend;
                BaseName = baseName;
            }

            public MoveStep(MoveTypes moveType, DataFormats dataFormat, Position position, string baseName)
            {
                MoveType = moveType;
                DataFormat = dataFormat;

                Position = position;
                Accel = 100;
                Velocity = 100;
                Blend = 25;
                BaseName = baseName;
            }
        }


        public enum PositionTypes
        {
            CARTESIAN = 0,
            JOINT = 1,
        }

        public class Position : List<double>
        {
            public double V1 { get { return base[0]; } set { base[0] = value; } }
            public double V2 { get { return base[1]; } set { base[1] = value; } }
            public double V3 { get { return base[2]; } set { base[2] = value; } }
            public double V4 { get { return base[3]; } set { base[3] = value; } }
            public double V5 { get { return base[4]; } set { base[4] = value; } }
            public double V6 { get { return base[5]; } set { base[5] = value; } }

            public string ToCSV => $"{base[0]},{base[1]},{base[2]},{base[3]},{base[4]},{base[5]}";


            public PositionTypes Type { get; set; }

            protected Position() { }

            public Position(PositionTypes type)
            {
                for (int i = 0; i < 6; i++)
                    Add(0);

                Type = type;
            }

            public Position(string pos)
            {
                pos = pos.Trim('\r', '\n');
                pos = pos.Trim('{', '}');

                string[] spl = pos.Split(',');

                int cnt = 0;
                foreach (string s in spl)
                {
                    if (!double.TryParse(s, out double res))
                        break;
                    Add(res);
                    cnt++;
                }

                for (; cnt <= 6; cnt++)
                    Add(0);
            }
        }

        public class Cartesian : Position
        {
            public double X { get { return base[0]; } set { base[0] = value; } }
            public double Y { get { return base[1]; } set { base[1] = value; } }
            public double Z { get { return base[2]; } set { base[2] = value; } }
            public double RX { get { return base[3]; } set { base[3] = value; } }
            public double RY { get { return base[4]; } set { base[4] = value; } }
            public double RZ { get { return base[5]; } set { base[5] = value; } }

            public Cartesian() : base() { }

            public Cartesian(double x, double y, double z, double rX, double rY, double rZ)
            {
                Add(x);
                Add(y);
                Add(z);
                Add(rX);
                Add(rY);
                Add(rZ);
                base.Type = PositionTypes.CARTESIAN;
            }

            public Cartesian(string x, string y, string z, string rX, string rY, string rZ)
            {
                double.TryParse(x, out double res);
                Add(res);

                double.TryParse(y, out res);
                Add(res);

                double.TryParse(z, out res);
                Add(res);

                double.TryParse(rX, out res);
                Add(res);

                double.TryParse(rY, out res);
                Add(res);

                double.TryParse(rZ, out res);
                Add(res);

                base.Type = PositionTypes.CARTESIAN;
            }
            public Cartesian(string pos)
            {
                string[] spl = pos.Split(',');

                int cnt = 0;
                foreach (string s in spl)
                {
                    double.TryParse(s, out double res);
                    Add(res);
                    cnt++;
                }

                for (; cnt <= 6; cnt++)
                    Add(0);

                base.Type = PositionTypes.CARTESIAN;
            }
            public Cartesian(Position Position)
            {
                AddRange(Position);
                base.Type = PositionTypes.CARTESIAN;
            }
        }

        public class Joint : Position
        {
            public double J1 { get { return base[0]; } set { base[0] = value; } }
            public double J2 { get { return base[1]; } set { base[1] = value; } }
            public double J3 { get { return base[2]; } set { base[2] = value; } }
            public double J4 { get { return base[3]; } set { base[3] = value; } }
            public double J5 { get { return base[4]; } set { base[4] = value; } }
            public double J6 { get { return base[5]; } set { base[5] = value; } }

            public Joint() : base() { }

            public Joint(string j1, string j2, string j3, string j4, string j5, string j6)
            {
                double.TryParse(j1, out double res);
                Add(res);

                double.TryParse(j2, out res);
                Add(res);

                double.TryParse(j3, out res);
                Add(res);

                double.TryParse(j4, out res);
                Add(res);

                double.TryParse(j5, out res);
                Add(res);

                double.TryParse(j6, out res);
                Add(res);

                Type = PositionTypes.JOINT;
            }
            public Joint(string pos)
            {
                string[] spl = pos.Split(',');

                int cnt = 0;
                foreach (string s in spl)
                {
                    double.TryParse(s, out double res);
                    Add(res);
                    cnt++;
                }

                for (; cnt <= 6; cnt++)
                    Add(0);

                Type = PositionTypes.JOINT;
            }
            public Joint(Position Position)
            {
                AddRange(Position);
                Type = PositionTypes.JOINT;
            }
        }


    }


}
