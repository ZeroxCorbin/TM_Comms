using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TM_Comms;

namespace TM_Comms.Controllers
{
    public class ListenNodeController
    {
       // private Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        public AsyncSocket.ASocketManager Socket { get; }

        public enum LnStates
        {
            Response,
            Exception
        }


        public LnStates LnState { get; set; }
        public delegate void MessageEventDelegate(LnStates state, string message, ListenNode listenNode);
        public event MessageEventDelegate MessageEvent;

        public bool Retry { get; set; }
        public string IPAddress { get; set; }
        public ListenNodeController()
        {
            Socket = new AsyncSocket.ASocketManager();
            Socket.MessageEvent += Socket_MessageEvent;
        }

        public void Connect(string iPAddress, bool retry = true)
        {
            IPAddress = iPAddress;
            Retry = retry;

            if (Socket.State == AsyncSocket.ASocketStates.Open)
            {
                Retry = false;
                Socket.Close();
            }
            else
            {
                Socket.Connect(IPAddress, 5890);
                Socket.StartReceiveMessages("\r\n");
            }
        }

        public void Disconnect()
        {
            Retry = false;
            Socket.Close();
        }

        public void Send(string message)
        {
            //Logger.Debug($">{message.Trim('\r', '\n')}");
            Socket.Send(message);
        }

        private void Socket_CloseEvent()
        {

            if (Retry)
                Connect(IPAddress, Retry);
        }

        private void Socket_MessageEvent(object sender, EventArgs e)
        {
            var ln = new ListenNode();
            if (ln.ParseMessage((string)sender))
            {
                //Logger.Debug($"<{ln.Message.Trim('\r', '\n')}");
                Task.Run(() => MessageEvent?.Invoke(LnStates.Response, (string)sender, ln));
            }
            //else
            //    Logger.Error($"<(BAD) {(string)sender}");
        }
    }
}
