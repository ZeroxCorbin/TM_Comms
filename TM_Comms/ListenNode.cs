 using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TM_Comms
{
    public partial class ListenNode
    {
        public static int Port { get; } = 5890;

        public enum Headers
        { 
            TMSCT, //External Script
            TMSTA, //Aquire status or properties
            CPERR  //Communication data error
        }
        public enum CPErrorCodes
        {
            OK = 0,
            PACKET_ERROR,
            CHECKSUM_ERROR,
            HEADER_ERROR,
            PACKET_DATA_ERROR,
            NOT_IN_LISTEN = 0xF1
        }
        public const string StartByte = "$";
        public const string Separator = ",";
        public const string ChecksumSign = "*";
        public const string EndBytes = "\r\n";

        public Headers Header { get; set; } = Headers.TMSCT;
        public string HeaderString => Header.ToString();

        public int Length => Data.Length;

        public CPErrorCodes CPErrorCode { get; private set; } = CPErrorCodes.OK;

        public string ScriptID { get; set; } = "local";
        public int ScriptID_Int
        {
            get
            {
                if (int.TryParse(ScriptID, out int res))
                    return res;
                return -1;
            }
        }

        public string Script { get; set; } = string.Empty;

        private string Data
        {
            get
            {
                if (Header == Headers.TMSCT)
                    return $"{ScriptID}{Separator}{Script}";
                else
                    return $"{Script}";
            }
        }

        public byte Checksum => CalCheckSum();
        public string ChecksumString => CalCheckSum().ToString("X2");

        public string Message => $"{StartByte}{HeaderString}{Separator}{Length}{Separator}{Data}{Separator}{ChecksumSign}{ChecksumString}{EndBytes}";

        public ListenNode() { }
        public ListenNode(string script, Headers header = Headers.TMSCT, string scriptID = "local")
        {
            Header = header;
            Script = script;
            ScriptID = scriptID;
        }
        public bool ParseMessage(string message)
        {
            //$TMSCT,8,local,OK,*0C
            //$TMSCT,9,0,Listen1,*4C
            //$CPERR,2,02,*4A
            //$TMSCT,8,local,OK,*0C
            //$TMSTA,10,01,01,true,*64
            //$TMSCT,9,0,Listen1,*4C
            //$TMSCT,12,diag,ERROR; 2,*04

            //Server Response
            if (!Regex.IsMatch(message, @"^[$].+\*[0-9A-F][0-9A-F]"))
                return false;

            Match match;
            if (Regex.IsMatch(message, @"^[$]CPERR"))
            {
                string content = Regex.Replace(message, @"^[$]\w*,\w*,", "");
                Script = Regex.Replace(content, @",[*][0-9A-Z][0-9A-Z]*", "");


                match = Regex.Match(message, @"^[$]\w*,\w*,\w*,");
            }
            else
            {
                string content = Regex.Replace(message, @"^[$]\w*,\w*,\w*,", "");
                Script = Regex.Replace(content, @",[*][0-9A-Z][0-9A-Z]*", "");

                match = Regex.Match(message, @"^[$]\w*,\w*,\w*,.+,");
            }

            if (match.Success)
            {
                string[] spl = match.Value.Split(',');

                if (Enum.TryParse(spl[0].TrimStart('$'), out Headers header))
                    Header = header;
                else
                    return false;

                if(Header == Headers.CPERR)
                    if (Enum.TryParse(spl[2].TrimStart('$'), out CPErrorCodes error))
                        CPErrorCode = error;
                    else
                        return false;
                else
                    ScriptID = spl[2];

                return true;
            }

            return false;

        }

        private byte CalCheckSum()
        {
            Byte _CheckSumByte = 0x00;
            Byte[] bData = Encoding.ASCII.GetBytes($"{HeaderString}{Separator}{Length}{Separator}{Data}{Separator}");
            for (int i = 0; i < bData.Length; i++)
                _CheckSumByte ^= bData[i];
            return _CheckSumByte;
        }
    }


}
