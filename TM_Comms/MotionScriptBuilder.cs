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
        public static Dictionary<string, List<string>> MoveTypes_DataFormats = new Dictionary<string, List<string>>()
        {
            { "PTP", new List<string>() { "CPP", "JPP" } },
            { "Line", new List<string>() { "CPP", "CPR", "CAP", "CAR" } },
            { "PLine", new List<string>() { "CAP", "JAP" } },
        };



        public class MoveStep
        {  
            public string MoveType { get; set; }
            public string DataFormat { get; set; } //CPP, JPP, etc...

            public Position Position { get; set; }
            public string Velocity { get; set; } 
            public string Accel { get; set; }
            public string Blend { get; set; }
            public bool Precision { get; set; }

            public string MoveCommand() => $"{MoveType}(\"{DataFormat}\",{Position.ToCSV},{Velocity},{Accel},{Blend},{(!Precision ? "true" : "false")})\r\n";
            public string MoveCommand(int posNum, bool initFloat = true) =>  $"{(initFloat ? "float[]" : "")} targetP{posNum}={{{Position.ToCSV}}}\r\n" +
                                                                            $"{MoveType}(\"{DataFormat}\",targetP{posNum},{Velocity},{Accel},{Blend},{(!Precision ? "true" : "false")})\r\n";

            public MoveStep(string moveType, string dataFormat, Position position, string velocity, string accel, string blend)
            {
                MoveType = moveType;

                DataFormat = dataFormat; 
                Position = position;
                Accel = accel;
                Velocity = velocity;
                Blend = blend;
            }
        }


        public List<MoveStep> Moves = new List<MoveStep>();

        public ListenNode BuildScriptData(bool addScriptExit, bool initVariables)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("\r\n");

            int step = 1;
            foreach (MoveStep ms in Moves)
            { 
                if(!initVariables)
                    sb.Append(ms.MoveCommand());
                else
                    sb.Append(ms.MoveCommand(step++, initVariables));
            }

            sb.Append(GetQueueTag(1));

            if(addScriptExit)
                sb.Append(GetScriptExit());

            return new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT);
        }

        private string GetQueueTag(int num) => $"QueueTag({num})\r\n";
        private string GetScriptExit() => $"ScriptExit()\r\n";

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
        public class Position : List<string>
        {
            public string V1 { get { return base[0]; } set { base[0] = value; } }
            public string V2 { get { return base[1]; } set { base[1] = value; } }
            public string V3 { get { return base[2]; } set { base[2] = value; } }
            public string V4 { get { return base[3]; } set { base[3] = value; } }
            public string V5 { get { return base[4]; } set { base[4] = value; } }
            public string V6 { get { return base[5]; } set { base[5] = value; } }

            public string ToCSV => $"{base[0]},{base[1]},{base[2]},{base[3]},{base[4]},{base[5]}"; 
            public enum MType
            {
                CARTESIAN = 0,
                JOINT = 1,
            }

            public MType Type { get; set; }

            public Position()
            {

            }
            public Position(string pos)
            {
                string[] spl = pos.Split(',');
                Clear();
                Add(spl[0]);
                Add(spl[1]);
                Add(spl[2]);
                Add(spl[3]);
                Add(spl[4]);
                Add(spl[5]);
            }
        }

        public class Cartesian : Position
        {
            public string X { get { return base[0]; } set { base[0] = value; } }
            public string Y { get { return base[1]; } set { base[1] = value; } }
            public string Z { get { return base[2]; } set { base[2] = value; } }
            public string RX { get { return base[3]; } set { base[3] = value; } }
            public string RY { get { return base[4]; } set { base[4] = value; } }
            public string RZ { get { return base[5]; } set { base[5] = value; } }

            public Cartesian() { Clear(); }
            public Cartesian(string x, string y, string z, string rX, string rY, string rZ)
            {
                Clear();
                Add(x);
                Add(y);
                Add(z);
                Add(rX);
                Add(rY);
                Add(rZ);
                base.Type = MType.CARTESIAN;
            }
            public Cartesian(string pos)
            {
                string[] spl = pos.Split(',');
                Clear();
                Add(spl[0]);
                Add(spl[1]);
                Add(spl[2]);
                Add(spl[3]);
                Add(spl[4]);
                Add(spl[5]);
                base.Type = MType.CARTESIAN;
            }
            public Cartesian(Position Position)
            {
                Clear();
                AddRange(Position);
                Type = MType.CARTESIAN;
            }
        }

        public class Joint : Position
        {
            public string J1 { get { return base[0]; } set { base[0] = value; } }
            public string J2 { get { return base[1]; } set { base[1] = value; } }
            public string J3 { get { return base[2]; } set { base[2] = value; } }
            public string J4 { get { return base[3]; } set { base[3] = value; } }
            public string J5 { get { return base[4]; } set { base[4] = value; } }
            public string J6 { get { return base[5]; } set { base[5] = value; } }

            public Joint(string j1, string j2, string j3, string j4, string j5, string j6)
            {
                Clear();
                Add(j1);
                Add(j2);
                Add(j3);
                Add(j4);
                Add(j5);
                Add(j6);
                Type = MType.JOINT;
            }
            public Joint(string pos)
            {
                string[] spl = pos.Split(',');
                Clear();
                Add(spl[0]);
                Add(spl[1]);
                Add(spl[2]);
                Add(spl[3]);
                Add(spl[4]);
                Add(spl[5]);
                Type = MType.JOINT;
            }
            public Joint(Position Position)
            {
                Clear();
                AddRange(Position);
                Type = MType.JOINT;
            }
        }


    }


}
