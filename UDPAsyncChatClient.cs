using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UDPAsynchronousChat
{
    public class UDPAsyncChatClient
    {
        Socket mBroadcastSender;
        IPEndPoint IPEPLocal;
        IPEndPoint IPEPServer;
        public EventHandler<TextUpdateEventArgs> RaiseTextUpdateEvent;
        private EndPoint mChatServerEP;

        public UDPAsyncChatClient(int _localPort = 0, int _remotePort = 0)
        {
            IPEPLocal = new IPEndPoint(IPAddress.Any, _localPort);
            IPEPServer = new IPEndPoint(IPAddress.Broadcast, _remotePort);

            mBroadcastSender = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);
            mBroadcastSender.EnableBroadcast = true;
        }
        protected virtual void OnRaiseTextUpdateEvent(TextUpdateEventArgs e)
        {
            EventHandler<TextUpdateEventArgs> handler = RaiseTextUpdateEvent;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public void SendBroadcast(string strDataforBroadcast)
        {
            if(String.IsNullOrEmpty(strDataforBroadcast))
            {
                Console.WriteLine("UDPAsyncChatClient.SendBroadcast was passed null or empty string!");
                return;
            }
            try
            {
                if(!mBroadcastSender.IsBound)
                {
                    mBroadcastSender.Bind(IPEPLocal);
                }

                byte[] databytes = Encoding.ASCII.GetBytes(strDataforBroadcast);

                SocketAsyncEventArgs saeArgs = new SocketAsyncEventArgs();
                saeArgs.SetBuffer(databytes, 0, databytes.Length);
                saeArgs.RemoteEndPoint = IPEPServer;

                saeArgs.Completed += SendCompletedCallback;
                mBroadcastSender.SendToAsync(saeArgs);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private void SendCompletedCallback(object sender, SocketAsyncEventArgs e)
        {
            OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                $"Data({e.Count} bytes) sent to {e.RemoteEndPoint.ToString()}"));
            if(Encoding.ASCII.GetString(e.Buffer, 0, e.BytesTransferred).Equals("<confirm>"))
                ReceiveTextFromServer(expectedValue: "<confirm>", IPEPReceiverLocal: IPEPLocal);

        }

        private void ReceiveTextFromServer(string expectedValue, IPEndPoint IPEPReceiverLocal)
        {
            if(IPEPReceiverLocal == null)
            {
                return;
            }
            SocketAsyncEventArgs saeConfirmArgs = new SocketAsyncEventArgs();
            saeConfirmArgs.SetBuffer(new byte[1024], 0, 1024);
            saeConfirmArgs.RemoteEndPoint = IPEPReceiverLocal;

            saeConfirmArgs.UserToken = expectedValue;
            saeConfirmArgs.Completed += ReceiveConfirmationCompleted;

            mBroadcastSender.ReceiveFromAsync(saeConfirmArgs);
        }

        private void ReceiveConfirmationCompleted(object sender, SocketAsyncEventArgs e)
        {
            if(e.BytesTransferred == 0) return;
            string receivedText = Encoding.ASCII.GetString(e.Buffer, 0, e.BytesTransferred);

            if(receivedText.Equals(Convert.ToString(e.UserToken)))
            {
                OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                    $"Received confirmation from server"));
                mChatServerEP = e.RemoteEndPoint;
                ReceiveTextFromServer(string.Empty, mChatServerEP as IPEndPoint);
            }
            else
            {
                OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                    $"Received unexpected usertoken: {receivedText}"));
            }
        }
    }
}
