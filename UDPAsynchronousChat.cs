using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UDPAsynchronousChat
{
    public class TextUpdateEventArgs : EventArgs
    {
        public string Text { get; set; }
        public TextUpdateEventArgs(string _Text)
        {
            Text = _Text;
        }
    }

    public class UDPAsynchronousChatServer
    {
        Socket mBroadcastReceiver;
        IPEndPoint mIPEPLocal;
        private int RetryCount = 0;
        public EventHandler<TextUpdateEventArgs> RaiseTextUpdateEvent;

        List<EndPoint> ClientList;

        public UDPAsynchronousChatServer()
        {
            mBroadcastReceiver = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp);
            mIPEPLocal = new IPEndPoint(IPAddress.Any, 23000);
            mBroadcastReceiver.EnableBroadcast = true;
            ClientList = new List<EndPoint>();
        }

        protected virtual void OnRaiseTextUpdateEvent(TextUpdateEventArgs e)
        {
            RaiseTextUpdateEvent?.Invoke(this, e);
        }

        public void StartReceivingData()
        {
            try
            {
                SocketAsyncEventArgs saeArgs = new SocketAsyncEventArgs();
                saeArgs.SetBuffer(new byte[1024], 0, 1024);
                saeArgs.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

                if (!mBroadcastReceiver.IsBound)
                {
                    mBroadcastReceiver.Bind(mIPEPLocal);
                }
                saeArgs.Completed += ReceiveCompetedCallback;
                if (!mBroadcastReceiver.ReceiveFromAsync(saeArgs))
                {
                    OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                        $"Failed to receive data -- socket error {saeArgs.SocketError}"));
                    //Console.WriteLine($"Failed to receive data -- socket error {saeArgs.SocketError}");
                    if (RetryCount++ < 10)
                    {
                        StartReceivingData();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private void ReceiveCompetedCallback(object sender, SocketAsyncEventArgs e)
        {
            string textReceived = Encoding.ASCII.GetString(e.Buffer, 0, e.BytesTransferred);
            OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                $"Message ({e.BytesTransferred} bytes) Received from {e.RemoteEndPoint} -- " +
                textReceived));
            /*Console.WriteLine(
                $"Message ({e.BytesTransferred} bytes) Received from {e.RemoteEndPoint} -- " +
                textReceived);*/

            if(textReceived == "<discover>" && !ClientList.Contains(e.RemoteEndPoint))
            {
                ClientList.Add(e.RemoteEndPoint);
                OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                    $"Client {e.RemoteEndPoint} added to client list. {ClientList.Count} clients in list."));
                SendTextToEndPoint("<confirm>", e.RemoteEndPoint);

            }
            StartReceivingData();
        }

        private void SendTextToEndPoint(string textToSend, EndPoint remoteEndPoint)
        {
            SocketAsyncEventArgs saeSendArgs = new SocketAsyncEventArgs();
            saeSendArgs.RemoteEndPoint = remoteEndPoint;

            byte[] sendBytes = Encoding.ASCII.GetBytes(textToSend);
            saeSendArgs.SetBuffer(sendBytes, 0, sendBytes.Length);
            saeSendArgs.Completed += SendTextToEndPointCompleted;
            mBroadcastReceiver.SendToAsync(saeSendArgs);
        }

        private void SendTextToEndPointCompleted(object sender, SocketAsyncEventArgs e)
        {
            string sentMessage = Encoding.ASCII.GetString(e.Buffer, 0, e.BytesTransferred);
            OnRaiseTextUpdateEvent(new TextUpdateEventArgs(
                    $"Message {sentMessage} sent to Client {e.RemoteEndPoint}."));
        }

    }
}
