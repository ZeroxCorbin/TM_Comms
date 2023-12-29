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

        private AsyncSocket.ASocketManager Socket { get; }

        public enum LnStates
        {
            Response,
            Exception
        }

        public SocketStates SocketState { get; set; }
        public delegate void SocketStateEventDelegate(SocketStates state, string message);
        public event SocketStateEventDelegate SocketStateEvent;

        public LnStates LnState { get; set; }
        public delegate void MessageEventDelegate(LnStates state, string message, ListenNode listenNode);
        public event MessageEventDelegate MessageEvent;

        public bool IsConnected => Socket.IsConnected;
        public bool Retry { get; set; }
        public string IPAddress { get; set; }
        public ListenNodeController()
        {
            Socket = new AsyncSocket.ASocketManager();
            Socket.CloseEvent += Socket_CloseEvent;
            Socket.ConnectEvent += Socket_ConnectEvent;
            Socket.ExceptionEvent += Socket_ExceptionEvent;
            Socket.MessageEvent += Socket_MessageEvent;
        }

        public void Connect(string iPAddress, bool retry = true)
        {
            IPAddress = iPAddress;
            Retry = retry;

            if (Socket.IsConnected)
            {
                Retry = false;
                Socket.Close();
            }
            else
            {
                SocketStateEvent?.Invoke(SocketStates.Trying, "Trying");
                Task.Run(() =>
                {
                    if (!Socket.Connect(IPAddress, 5890))
                        SocketStateEvent?.Invoke(SocketStates.Exception, "Unable to connect!");
                });
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

        private void Socket_ExceptionEvent(object sender, EventArgs e)
        {
            //Logger.Error((Exception)sender);
            SocketStateEvent?.Invoke(SocketStates.Exception, ((Exception)sender).Message);
        }
        private void Socket_ConnectEvent()
        {
            //Logger.Info($"Socket Open");

            SocketState = SocketStates.Open;
            SocketStateEvent?.Invoke(SocketStates.Open, "Open");

            Socket.StartReceiveMessages("\r\n");
        }
        private void Socket_CloseEvent()
        {
            //Logger.Info($"Socket Closed");

            SocketState = SocketStates.Closed;
            SocketStateEvent?.Invoke(SocketStates.Closed, "Close");

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
