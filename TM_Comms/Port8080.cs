using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TM_Comms
{
    public class Port8080
    {
        public static Rootobject Parse(string json)
        {
            Rootobject root = new Rootobject();
            try
            {
                root = JsonConvert.DeserializeObject<Rootobject>(json);
                root._Data = JsonConvert.DeserializeObject<Data>(root.Data);
            }
            catch(Exception)
            {

            }
            return root;
        }
        
        public class Rootobject
        {
            public int DataType { get; set; }
           
            public string Data { get; set; }
            public Data _Data { get; set; }
            public int PoolID { get; set; }
            public string CS { get; set; }

        }

 [JsonObject("Data")]
        public class Data
        {
            public object RobotPoint0 { get; set; }
            public float[] RobotPoint1 { get; set; }
            public string[][] IOList { get; set; }
            public string[][] IOList2String { get; set; }
            public int RobotLight { get; set; }
            public int ControlBOXMode { get; set; }
            public float StickSpeed { get; set; }
            public bool fgStickKeyStatus_Start_Key_Pressed { get; set; }
            public bool fgStickKeyStatus_Stop_Key_Pressed { get; set; }
            public bool fgStickKeyStatus_Power_Key_Pressed { get; set; }
            public bool fgStickKeyStatus_Mode_Key_Pressed { get; set; }
            public bool fgStickKeyStatus_Minus_Key_Pressed { get; set; }
            public bool fgStickKeyStatus_plus_Key_Pressed { get; set; }
            public string CurrentBaseName { get; set; }
            public bool fgCameraLightOn { get; set; }
        }
    }



}
