using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace TM_Comms
{
    public class Conversion
    {
        public const double Million = 1000000.0;
        public const double TenThousand = 10000.0;
        public const double Thousand = 1000.0;

        public static double ToDeg(double rad) => (180 / Math.PI) * rad;
        public static double ToRad(double rad) => (rad / 180) * Math.PI;

        public static double[] AxisAngle_To_EulerXYZ_DegToRad(double rx, double ry, double rz, double angle) =>
            AxisAngle_To_EulerXYZ_RadToRad(ToRad(rx), ToRad(ry), ToRad(rz), ToRad(angle));

        public static double[] AxisAngle_To_EulerXYZ_RadToDeg(double rx, double ry, double rz, double angle) 
        {
            var tmp = AxisAngle_To_EulerXYZ_RadToRad(rx, ry, rz, angle);

            tmp[0] = ToDeg(tmp[0]);
            tmp[1] = ToDeg(tmp[1]);
            tmp[2] = ToDeg(tmp[2]);

            return tmp;
        }

        public static double[] AxisAngle_To_EulerXYZ_RadToRad(double rx, double ry, double rz, double angle)
        {
            double axis_norm = Math.Sqrt(rx * rx + ry * ry + rz * rz);

            // Compute the quaternion
            double sin_angle = Math.Sin(0.5 * angle);
            double qw = Math.Cos(0.5 * angle);
            double qx = rx / axis_norm * sin_angle;
            double qy = ry / axis_norm * sin_angle;
            double qz = rz / axis_norm * sin_angle;

            double euler_x = Math.Atan2(2.0 * (qw * qx - qy * qz), 1.0 - 2.0 * (qx * qx + qy * qy));
            double res = 2.0 * (qx * qz + qw * qy);
            if (res > 1.0)
                res = 1.0;
            if (res < -1.0)
                res = -1.0;
            double euler_y = Math.Asin(res);
            double euler_z = Math.Atan2(2.0 * (qw * qz - qx * qy), 1.0 - 2.0 * (qy * qy + qz * qz));

            return new double[] { euler_x, euler_y, euler_z };
        }

        public static double[] EulerXYZ_To_AxisAngle_DegToRad(double euler_rx, double euler_ry, double euler_rz) =>
            EulerXYZ_To_AxisAngle_RadToRad(ToRad(euler_rx), ToRad(euler_ry), ToRad(euler_rz));

        public static double[] EulerXYZ_To_AxisAngle_DegToDeg(double euler_rx, double euler_ry, double euler_rz)
        {
            var tmp = EulerXYZ_To_AxisAngle_RadToRad(ToRad(euler_rx), ToRad(euler_ry), ToRad(euler_rz));

            tmp[0] = ToDeg(tmp[0]);
            tmp[1] = ToDeg(tmp[1]);
            tmp[2] = ToDeg(tmp[2]);

            return tmp;
        }
    
        public static double[] EulerXYZ_To_AxisAngle_RadToRad(double euler_rx, double euler_ry, double euler_rz)
        {
            double cx = Math.Cos(euler_rx / 2.0);
            double cy = Math.Cos(euler_ry / 2.0);
            double cz = Math.Cos(euler_rz / 2.0);
            double sx = Math.Sin(euler_rx / 2.0);
            double sy = Math.Sin(euler_ry / 2.0);
            double sz = Math.Sin(euler_rz / 2.0);

            // Compute the quaternion
            double qw = -(sx * sy * sz) + (cx * cy * cz);
            double qx = (sx * cy * cz) + (sy * sz * cx);
            double qy = -(sx * sz * cy) + (sy * cx * cz);
            double qz = (sx * sy * cz) + (sz * cx * cy);

            // Compute axis angle
            double rx = qx / Math.Sqrt(qx * qx + qy * qy + qz * qz);
            double ry = qy / Math.Sqrt(qx * qx + qy * qy + qz * qz);
            double rz = qz / Math.Sqrt(qx * qx + qy * qy + qz * qz);
            double angle = 2.0 * Math.Atan2(Math.Sqrt(qx * qx + qy * qy + qz * qz), qw);

            return new double[] { rx, ry, rz, angle };
        }

        public static Quaternion EulerXYZ_To_Quaternion_DegToRad(double euler_rx, double euler_ry, double euler_rz)
        {
            return EulerXYZ_To_Quaternion_RadToRad(ToRad(euler_rx), ToRad(euler_ry), ToRad(euler_rz));
        }
        public static Quaternion EulerXYZ_To_Quaternion_RadToRad(double euler_rx, double euler_ry, double euler_rz)
        {

            float cy = (float)Math.Cos(euler_rz * 0.5);
            float sy = (float)Math.Sin(euler_rz * 0.5);
            float cp = (float)Math.Cos(euler_ry * 0.5);
            float sp = (float)Math.Sin(euler_ry * 0.5);
            float cr = (float)Math.Cos(euler_rx * 0.5);
            float sr = (float)Math.Sin(euler_rx * 0.5);

            return new Quaternion
            {
                W = (cr * cp * cy + sr * sp * sy),
                X = (sr * cp * cy - cr * sp * sy),
                Y = (cr * sp * cy + sr * cp * sy),
                Z = (cr * cp * sy - sr * sp * cy)
            };

        }



#if NETCOREAPP3_0_OR_GREATER
        public static Vector3 ToEulerAngles(Quaternion q)
        {
            Vector3 angles = new Vector3();

            // roll / x
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            angles.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            // pitch / y
            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (Math.Abs(sinp) >= 1)
            {
                angles.Y = (float)Math.CopySign(Math.PI / 2, sinp);
            }
            else
            {
                angles.Y = (float)Math.Asin(sinp);
            }

            // yaw / z
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            angles.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

            return angles;
        }
#endif
    }
}
