using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace TM_Comms
{
    public partial class MotionScriptBuilder
    {
        public const double MAX_BLEND_MM = 10.0;
        public const double MIN_DISTANCE_MM = 0.05;
        public const double MAX_VELOCITY_MMS = 2000.0;
        public const int MAX_ACCEL_MS = 200;

        public List<MoveStep> Moves;
        public MotionScriptBuilder()
        {
            Moves = new List<MoveStep>();
        }

        public MotionScriptBuilder(List<MoveStep> moves)
        {
            Moves = moves;
        }

        public VerifyResult VerifyBlend()
        {
            VerifyResult vr = VerifyResult.OK;

            int i = 0;
            MoveStep prev = null;
            foreach (MoveStep ms in Moves)
            {
                if (i == 0)
                {
                    ms.Blent_pct = 0;
                    prev = ms;
                    i++;
                    continue;
                }

                if (prev.Move_type != ms.Move_type)
                {
                    if (prev.Blent_pct > 0)
                    {
                        prev.Blent_pct = 0;
                        vr = VerifyResult.UPDATED;
                    }
                }

                if (prev.Where.Type == Space.MType.EPOSE & ms.Where.Type == Space.MType.EPOSE)
                {
                    double dist = Distance(prev.Where, ms.Where);
                    if (dist < MAX_BLEND_MM)
                    {
                        if (dist <= MIN_DISTANCE_MM)
                        {
                            vr = VerifyResult.FAILED;
                            break;
                        }

                        int blend = (int)(dist / MAX_BLEND_MM) * 100;
                        if (ms.Blent_pct > blend)
                        {
                            ms.Blent_pct = blend;
                            vr = VerifyResult.UPDATED;
                        }

                    }
                }

                prev = ms;
            }
            return vr;
        }

        public MotionResult Move(List<MoveStep> blends)
        {
            return MotionResult.FAILURE;
        }

        public ListenNode BuildScriptData(bool addScriptExit, bool initVariables)
        {
            StringBuilder sb = new StringBuilder();
            int step = 1;
            foreach (MoveStep ms in Moves)
            {
                if (ms.Move_type == MotionType.LINEAR)
                {
                    if (ms.Where.Type == Space.MType.EPOSE)
                        sb.Append(initVariables ? $"float[] {GetPLineCart(ms, step++)}" : GetPLineCart(ms, step++));

                    if (ms.Where.Type == Space.MType.JOINTS)
                        sb.Append(initVariables ? $"float[] {GetPLineJoint(ms, step++)}" : GetPLineJoint(ms, step++));
                }

                if (ms.Move_type == MotionType.JOINT)
                {
                    if (ms.Where.Type == Space.MType.EPOSE)
                        sb.Append(initVariables ? $"float[] {GetPTPCart(ms, step++)}" : GetPTPCart(ms, step++));

                    if (ms.Where.Type == Space.MType.JOINTS)
                        sb.Append(initVariables ? $"float[] {GetPTPJoint(ms, step++)}" : GetPTPJoint(ms, step++));
                }
            }

            sb.Append(GetQueueTag(1));

            if(addScriptExit)
                sb.Append(GetScriptExit());

            return new ListenNode(sb.ToString(), ListenNode.Headers.TMSCT);
        }

        private string GetPLineCart(MoveStep ms, int pos) => $"targetP{pos}={{{ms.Where[0]},{ms.Where[1]},{ms.Where[2]},{ms.Where[3]},{ms.Where[4]},{ms.Where[5]}}}\r\n" +
                                                             $"Line(\"CPP\",targetP{pos},{ms.Velocity_pct},{ms.Acceleration_ms},{ms.Blent_pct},true)\r\n";
        private string GetPLineJoint(MoveStep ms, int pos) => $"targetP{pos}={{{ms.Where[0]},{ms.Where[1]},{ms.Where[2]},{ms.Where[3]},{ms.Where[4]},{ms.Where[5]}}}\r\n" +
                                                              $"Line(\"JPP\",targetP{pos},{ms.Velocity_pct},{ms.Acceleration_ms},{ms.Blent_pct},true)\r\n";
        private string GetPTPCart(MoveStep ms, int pos) => $"targetP{pos}={{{ms.Where[0]},{ms.Where[1]},{ms.Where[2]},{ms.Where[3]},{ms.Where[4]},{ms.Where[5]}}}\r\n" +
                                                           $"PTP(\"CPP\",targetP{pos},{ms.Velocity_pct},{ms.Acceleration_ms},{ms.Blent_pct},true)\r\n";
        private string GetPTPJoint(MoveStep ms, int pos) => $"targetP{pos}={{{ms.Where[0]},{ms.Where[1]},{ms.Where[2]},{ms.Where[3]},{ms.Where[4]},{ms.Where[5]}}}\r\n" +
                                                            $"PTP(\"JPP\",targetP{pos},{ms.Velocity_pct},{ms.Acceleration_ms},{ms.Blent_pct},true)\r\n";
        private string GetQueueTag(int num) => $"QueueTag({num})\r\n";
        private string GetScriptExit() => $"ScriptExit()\r\n";
        public virtual AbortCondResult Abort()
        {
            return AbortCondResult.CONTINUE;
        }

        private double Distance(Space p1, Space p2) => Math.Pow((Math.Pow(p2[0] - p1[0], 2) + Math.Pow(p2[1] - p1[1], 2) + Math.Pow(p2[3] - p1[3], 2)) * 1.0, 0.5);

    }

    public partial class MotionScriptBuilder
    {
        public enum MotionResult
        {
            SUCCESS = 0,        //The motion reached its destination.
            FAILURE = -1,       //The motion did not reach its destination, but no indication to cause.
            ABORT_COND = -2,    //The motion did not reach its destination, due to the abort callback returning `AbortCondResult::ABORT`
            JOINT_LIMIT = -3,   //The motion did not reach its destination, due to one or more joints reaching the limit in their range of motion.
            TIMEOUT = -4        //The motion did not reach its destination, due to robot not making a timely reply.
        }
        public enum AbortCondResult
        {
            PAUSE = -1,     //Motion of the robot shall cease. However the function initiating the motion shall not return. Upon the next `CONTINUE` the motion shall resume.
            CONTINUE = 0,   //do not interrupt the motion of the robot
            ABORT = 1       //Motion of the robot shall cease. The function initiating the motion shall return `MotionResult::ABORT_COND`.
        }

        public enum MotionType
        {
            JOINT = 1,
            LINEAR = 2,
            CIRCULAR = 3
        }

        public enum VerifyResult
        {
            OK = 0,         //Validated w/o changes
            UPDATED = 1,    //Values updated (specific to blend, velocity?)
            FAILED = -1    //Didn't validate, couldn't update (i.e unsupported move type passed)
        }

        public class Space : List<double>
        {
            public enum MType
            {
                POSE = 0,
                JOINTS = 1,
                EPOSE = 2
            }

            public MType Type { get; set; }
        }

        public class CPose : Space
        {
            public double X { get { return base[0]; } set { base[0] = value; } }
            public double Y { get { return base[1]; } set { base[1] = value; } }
            public double Z { get { return base[2]; } set { base[2] = value; } }
            public double Yaw { get { return base[3]; } set { base[3] = value; } }
            public double Pitch { get { return base[4]; } set { base[4] = value; } }
            public double Roll { get { return base[5]; } set { base[5] = value; } }
            public double Config { get { return base[6]; } set { base[6] = value; } }

            public CPose() { Clear(); }
            public CPose(double x_POS_m, double y_POS_m, double z_POS_m, double yaw, double pitch, double roll, double config)
            {
                Clear();
                Add(x_POS_m);
                Add(y_POS_m);
                Add(z_POS_m);
                Add(yaw);
                Add(pitch);
                Add(roll);
                Add(config);
                base.Type = MType.EPOSE;
            }
            public CPose(CPose pose)
            {
                Clear();
                AddRange(pose);
                Type = MType.EPOSE;
            }
        }

        public class Pose : Space
        {
            public double X => base[0];
            public double Y => base[1];
            public double Z => base[2];
            public double Qx => base[3];
            public double Qy => base[4];
            public double Qz => base[5];
            public double Qw => base[6];
            public double Config => base[7];

            public Pose(double x_POS_m, double y_POS_m, double z_POS_m, double x_QUAT, double y_QUAT, double z_QUAT, double w_QUAT, double config)
            {
                Clear();
                Add(x_POS_m);
                Add(y_POS_m);
                Add(z_POS_m);
                Add(x_QUAT);
                Add(y_QUAT);
                Add(z_QUAT);
                Add(w_QUAT);
                Add(config);
                base.Type = MType.POSE;
            }
            public Pose(Pose pose)
            {
                Clear();
                AddRange(pose);
                Type = MType.POSE;
            }
        }

        public class Joint : Space
        {
            public Joint(double j1, double j2, double j3, double j4, double j5, double j6, double config)
            {
                Clear();
                Add(j1);
                Add(j2);
                Add(j3);
                Add(j4);
                Add(j5);
                Add(j6);
                Add(config);
                Type = MType.JOINTS;
            }
            public Joint(Joint joint)
            {
                Clear();
                AddRange(joint);
                Type = MType.JOINTS;
            }
        }

        public class MoveStep
        {
            public Space Where { get; set; }
            public MotionType Move_type { get; set; }
            public int Acceleration_ms { get; set; }
            public int Velocity_pct { get; set; }
            public int Blent_pct { get; set; }

            public MoveStep(Space where, MotionType motionType, int velocity_pct, int accel_ms, int blend_pct)
            {
                Where = where;
                Move_type = motionType;
                Acceleration_ms = accel_ms;
                Velocity_pct = velocity_pct;
                Blent_pct = blend_pct;
            }
        }
    }


}
