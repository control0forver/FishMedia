using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static FishMedia.Servers.RTMP.RtmpProtocol;

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

            if (Utils.Utils.IsV6Address(ipaddrIp))
                Console.WriteLine("Rtmp Server Running On http://[{0}]:{1}", ipaddrIp.ToString(), iPort.ToString());
            else
                Console.WriteLine("Rtmp Server Running On http://{0}:{1}", ipaddrIp.ToString(), iPort.ToString());

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
            bIsRunning = false;
            tcplstnerListener.Stop();
        }

        private void ClientHandler(TcpClient tcpcliClient)
        {
            Socket sockClientSocket = tcpcliClient.Client;

            #region Cient Start
            Console.WriteLine("New Rtmp Client Connection:");
            Console.WriteLine("  Remote IPAddress: {0}", ((IPEndPoint)sockClientSocket.RemoteEndPoint).Address.ToString());
            Console.WriteLine("  Remote Port: {0}", ((IPEndPoint)sockClientSocket.RemoteEndPoint).Port.ToString());

            if (!tcpcliClient.Connected)
                return;
            #endregion

            #region HandShaking
            SimpleHandShakePackets.C0 GetC0()
            {
                byte[] buffer = new byte[SimpleHandShakePackets.C0.iSize];
                sockClientSocket.Receive(buffer, SimpleHandShakePackets.C0.iSize, 0);

                SimpleHandShakePackets.C0 c0 = new SimpleHandShakePackets.C0();
                c0.data.Set(buffer);

                return c0;
            }
            SimpleHandShakePackets.C1 GetC1()
            {
                byte[] buffer = new byte[SimpleHandShakePackets.C1.iSize];
                sockClientSocket.Receive(buffer, SimpleHandShakePackets.C1.iSize, 0);

                SimpleHandShakePackets.C1 c1 = new SimpleHandShakePackets.C1();
                c1.data.Set(buffer);

                return c1;
            }
            SimpleHandShakePackets.C2 GetC2()
            {
                byte[] buffer = new byte[SimpleHandShakePackets.C2.iSize];
                sockClientSocket.Receive(buffer, SimpleHandShakePackets.C2.iSize, 0);

                SimpleHandShakePackets.C2 c2 = new SimpleHandShakePackets.C2();
                c2.data.Set(buffer);

                return c2;
            }

            void SendS0(SimpleHandShakePackets.S0 s0)
            {
                sockClientSocket.Send(s0.data.data);
            }
            void SendS1(SimpleHandShakePackets.S1 s1)
            {
                sockClientSocket.Send(s1.data.data);
            }
            void SendS2(SimpleHandShakePackets.S2 s2)
            {
                sockClientSocket.Send(s2.data.data);
            }

            try
            {
                SimpleHandShakePackets.C0 C0 = null;
                SimpleHandShakePackets.C1 C1 = null;
                SimpleHandShakePackets.C2 C2 = null;
                SimpleHandShakePackets.S0 S0 = null;
                SimpleHandShakePackets.S1 S1 = null;
                SimpleHandShakePackets.S2 S2 = null;

                #region HandShaking 1
                #region C0 Get
                {
                    C0 = GetC0();
                }
                #endregion
                #region C1 Get
                {
                    C1 = GetC1();
                }
                #endregion
                #endregion

                #region HandShaking 2
                #region Check C0 && Send S0
                {
                    byte[] C0_Version = C0.data.GetVersion();
                    S0 = new SimpleHandShakePackets.S0();

                    if (C0_Version[0] != 0x03)
                    {
                        // Unsupported Rtmp Protocol Version
                        S0.data.SetVersion(new byte[] { 0x03 });
                    }
                    else
                    {
                        S0.data.SetVersion(C0_Version);
                    }
                    SendS0(S0);
                }
                #endregion
                #region Check C1 && Send S1
                {
                    // Check C1
                    if (!Utils.Utils.CompareArr(C1.data.GetZero(), new byte[] { 0, 0, 0, 0 }))
                    {
                        // Client May Be Error, Close and Exit
                        goto ConnectionEnd;
                    }

                    // Make S1
                    S1 = new SimpleHandShakePackets.S1();
                    byte[] arr_byteS1TimeBytes = new byte[SimpleHandShakePackets.S1.iSize1_Time];
                    byte[] arr_byteS1ZeroBytes = new byte[SimpleHandShakePackets.S1.iSize2_Zero];
                    byte[] arr_byteS1RandomBytes = new byte[SimpleHandShakePackets.S1.iSize3_Random];

                    // Make Time Chunk
                    {
                        DateTime startTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        TimeSpan timeSpan = DateTime.UtcNow - startTime;
                        uint timestamp = (uint)timeSpan.TotalMilliseconds;
                        arr_byteS1TimeBytes[0] = (byte)(timestamp >> 24);
                        arr_byteS1TimeBytes[1] = (byte)(timestamp >> 16);
                        arr_byteS1TimeBytes[2] = (byte)(timestamp >> 8);
                        arr_byteS1TimeBytes[3] = (byte)timestamp;
                    }

                    // Make Zero Bytes
                    {
                        arr_byteS1ZeroBytes = new byte[] { 0, 0, 0, 0 };
                    }

                    // Make Random Bytes
                    {
                        using (RandomNumberGenerator rng = new RNGCryptoServiceProvider())
                        {
                            rng.GetBytes(arr_byteS1RandomBytes);
                        }
                    }

                    // Load
                    S1.data.SetTime(arr_byteS1TimeBytes);
                    S1.data.SetZero(arr_byteS1ZeroBytes);
                    S1.data.SetRandom(arr_byteS1RandomBytes);

                    SendS1(S1);
                }
                #endregion
                #endregion

                #region HandShaking 3
                #region C2 Get
                {
                    C2 = GetC2();
                }
                #endregion
                #region Check C2 && Send S2
                {
                    // Check C2
                    if (!Utils.Utils.CompareArr(C2.data.data, S1.data.data))
                    {
                        // Client Error: C2 not Copy of S1
                        goto ConnectionEnd;
                    }

                    // Make S2
                    S2 = new SimpleHandShakePackets.S2();
                    S2.data.Set(C1.data.data);
                    SendS2(S2);
                }
                #endregion
                #endregion

                Console.WriteLine("Rtmp Client HandShaked Successfully:");
                Console.WriteLine("  Remote IPAddress: {0}", ((IPEndPoint)sockClientSocket.RemoteEndPoint).Address.ToString());
                Console.WriteLine("  Remote Port: {0}", ((IPEndPoint)sockClientSocket.RemoteEndPoint).Port.ToString());

            }
            catch (Exception)
            {
                return;
            }
            #endregion

            #region Connection
            List<byte> arr_byteConnectionBytes = new List<byte>();

            #region Get Connect Packet
            try
            {
                while (true)
                {
                    byte[] bt = new byte[RtmpProtocol.iMaxNetRecvBufferSize];
                    tcpcliClient.GetStream().Read(bt);
                    bt = Utils.Utils.TrimByteArrayEnd(bt);
                    arr_byteConnectionBytes.AddRange(bt);

                    if (bt.Length < iMaxNetRecvBufferSize)
                        break;
                }
            }
            catch (Exception)
            {
                goto ConnectionEnd;
            }

            #endregion

            // TODO: Process Connnect Packet
            #region Process Connect Packet
            if (arr_byteConnectionBytes[0] == 0x02)
            {
                
            }
            #endregion

            // TODO: Streaming

            #endregion


ConnectionEnd:
            tcpcliClient.Close();
            return;
        }
    }
}
