using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FishMedia.Servers.RTMP
{
    public class RtmpServer
    {
        public TcpListener tcplstnerListener = null;
        public IPAddress ipaddrIp = IPAddress.IPv6Any;
        public ushort iPort = 1935;
        public bool bIsRunning = false;

        public RtmpServer()
        {
            Init();
        }

        public RtmpServer(IPAddress ipaddrIp, ushort iPort)
        {
            this.ipaddrIp = ipaddrIp;
            this.iPort = iPort;

            Init();
        }

        public void Init()
        {
            tcplstnerListener = new TcpListener(ipaddrIp, iPort);
        }

        public void SetIPAddress(IPAddress ipaddrIp)
        {
            this.ipaddrIp = ipaddrIp;
        }

        public void SetPort(ushort iPort)
        {
            this.iPort = iPort;
        }

        public void Start()
        {
            if (bIsRunning && tcplstnerListener == null)
                return;

            tcplstnerListener.Start();
            bIsRunning = true;

            try
            {
                while (bIsRunning)
                {
                    TcpClient tcpcliClient = tcplstnerListener.AcceptTcpClient();
                    new Thread(() => { ClientHandler(tcpcliClient); }).Start();
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
            }
        }

        public void Stop()
        {
            tcplstnerListener.Stop();
        }

        private void ClientHandler(TcpClient tcpcliClient)
        {

        }
    }
}
