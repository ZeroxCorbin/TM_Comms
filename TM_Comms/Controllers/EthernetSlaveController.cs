using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using TM_Comms;

namespace TM_Comms.Controllers
{
    public class EthernetSlaveController
    {
        //private Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        public AsyncSocket.ASocketManager Socket { get; } = new AsyncSocket.ASocketManager();

        public enum EsStates
        {
            Normal,
            Response,
            Exception
        }
        public EsStates EsState { get; set; }
        public delegate void EsStateEventDelegate(EsStates state, string message, EthernetSlave ethernetSlave);
        public event EsStateEventDelegate EsStateEvent;

        public EthernetSlaveController()
        {
            Socket.MessageEvent += Socket_MessageEvent;
        }

        public void Connect(string ipAddress)
        {
            if (Socket.State == AsyncSocket.ASocketStates.Open)
            {
                Socket.Close();
            }
            else
            {
                Socket.Connect(ipAddress, 5891);
                Socket.StartReceiveMessages(@"[$]", @"[*][A-Z0-9][A-Z0-9]");
            }
        }

        public void Disconnect()
        {
            Socket.Close();
        }

        public void Send(string message)
        {
            //Logger.Debug($">{message}");
            Socket.Send(message);
        }


        private void Socket_MessageEvent(string message)
        {
            EthernetSlave es = new EthernetSlave();

            if (!es.ParseMessage(message))
            {
                //Logger.Error($"<(BAD) {message}");

                EsStateEvent?.Invoke(EsStates.Exception, message, null);
                return;
            }

            if (es.Header == EthernetSlave.Headers.TMSVR &&
                es.TransactionID_Int >= 0 && es.TransactionID_Int <= 9)
            {
                //Only trigger every 5th packet. 5 x 10ms
                if(es.TransactionID_Int == 0 || es.TransactionID_Int == 5)
                    EsStateEvent?.Invoke(EsStates.Normal, message, es);

                //if (CaptureData)
                //    outputFile.WriteLine(Regex.Replace(message, @"^[$]TMSVR,\w*,[0-9],[0-2],", "").Replace("\r\n", ","));
              
            }
            else
            {
                //Logger.Debug($"<{message}");

                string mes;
                if (es.Message.EndsWith("\r\n"))
                    mes = es.Message;
                else
                    mes = es.Message + "\r\n";

                EsStateEvent?.Invoke(EsStates.Response, message, es);
            }
        }
    }
}
