using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishMedia.Servers.RTMP
{
    public static class RtmpProtocol
    {
        public const int iMaxNetRecvBufferSize = 2048;

        #region Const Defs
        public const int iRtmpReLibVersion = 0x020300;  /* 2.3 */

        public static class MiscInfos
        {
            public static readonly int iRtmpDefaultChunkSize = 128;
            public static readonly int iRtmpBufferCacheSize = (16 * iMaxNetRecvBufferSize);
            public static readonly int iRtmpChannels = 65600;
            public static readonly int iRtmpMaxHeaderSize = 18;
        }

        public enum RtmpFeatures
        {
            HTTP = 0x01,
            ENC = 0x02,
            SSL = 0x04,
            MFP = 0x08,      /* not yet supported */
            Write = 0x10,    /* publish, not play */
            HTTP2 = 0x20     /* server-side rtmpt */
        }

        public enum RtmpProtocols
        {
            Undefined = -1,
            RTMP = 0,
            RTMPE = RtmpFeatures.ENC,
            RTMPT = RtmpFeatures.HTTP,
            RTMPS = RtmpFeatures.SSL,
            RTMPTE = (RtmpFeatures.HTTP | RtmpFeatures.ENC),
            RTMPTS = (RtmpFeatures.HTTP | RtmpFeatures.SSL),
            RTMFP = RtmpFeatures.MFP
        }

        public enum RtmpPacketTypes
        {
            /*  ... 0x00 */
            ChunkSize = 0x01,
            /*  ... 0x02 */
            BytesReadReport = 0x03,
            Control = 0x04,
            ServerBW = 0x05,
            ClientBW = 0x06,
            /*  ... 0x07 */
            Audio = 0x08,
            Video = 0x09,
            /*  ... 0x0A */
            /*  ... 0x0B */
            /*  ... 0x0C */
            /*  ... 0x0D */
            /*  ... 0x0E */
            FlexStreamSend = 0x0F,
            FlexSharedObject = 0x10,
            FlexMessage = 0x11,
            Info = 0x12,
            SharedObject = 0x13,
            Invoke = 0x14,
            /*  ... 0x15 */
            FlashVideo = 0x16
        }

        public enum RtmpPacketSizes
        {
            Large = 0,
            Medium,
            Small,
            Minimum
        }

        #endregion

        public struct RTMPChunk
        {
            int iHeaderSize;
            int iChunkSize;
            char[] dChunk;
            char[] arr_dHeader;

            public RTMPChunk()
            {
                iHeaderSize = 0;
                iChunkSize = 0;
                dChunk = null;
                arr_dHeader = new char[MiscInfos.iRtmpMaxHeaderSize];
            }
        }

        public struct RTMPPacket
        {
            byte u_iHeaderType;
            byte u_iPacketType;
            byte u_iHasAbsTimestamp;
            int iChannel;
            uint u_iTimeStamp;
            int u_iInfoField2;
            uint u_iBodySize;
            uint u_iBytesRead;
            RTMPChunk[] rtmpchkChunk;
            char[] dBody;
        }

        public static class SimpleHandShakePackets
        {
            public class C0
            {
                public const short iSize = 1;
                public const short iSize1_Version = 1;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteVersion, iSize1_Version);
                    }

                    public byte[] GetVersion()
                    {
                        return Utils.Utils.SubArr(data, 0, iSize1_Version);
                    }
                }
            }

            public class S0
            {
                public const short iSize = 1;
                public const short iSize1_Version = 1;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteVersion, iSize1_Version);
                    }

                    public byte[] GetVersion()
                    {
                        return Utils.Utils.SubArr(data, 0, iSize1_Version);
                    }
                }
            }

            public class C1
            {
                public const short iSize = 1536;
                public const short iSize1_Time = 4;
                public const short iSize2_Zero = 4;
                public const short iSize3_Random = iSize - iSize1_Time - iSize2_Zero;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Zero, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_Random, iSize1_Time + iSize2_Zero);
                    }

                    public byte[] GetTime()
                    {
                        return Utils.Utils.SubArr(data, 0, iSize1_Time);
                    }
                    public byte[] GetZero()
                    {
                        return Utils.Utils.SubArr(data, iSize1_Time, iSize2_Zero);
                    }
                    public byte[] GetRandom()
                    {
                        return Utils.Utils.SubArr(data, iSize1_Time + iSize2_Zero, iSize3_Random);
                    }
                }
            }

            public class S1
            {
                public const short iSize = 1536;
                public const short iSize1_Time = 4;
                public const short iSize2_Zero = 4;
                public const short iSize3_Random = iSize - iSize1_Time - iSize2_Zero;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Zero, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_Random, iSize1_Time + iSize2_Zero);
                    }

                    public byte[] GetTime()
                    {
                        return Utils.Utils.SubArr(data, 0, iSize1_Time);
                    }
                    public byte[] GetZero()
                    {
                        return Utils.Utils.SubArr(data, iSize1_Time, iSize2_Zero);
                    }
                    public byte[] GetRandom()
                    {
                        return Utils.Utils.SubArr(data, iSize1_Time + iSize2_Zero, iSize3_Random);
                    }
                }
            }

            public class C2
            {
                public const short iSize = 1536;
                public const short iSize1_Time = 4;
                public const short iSize2_Time2 = 4;
                public const short iSize3_RandomEcho = iSize - iSize1_Time - iSize2_Time2;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetTime2(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Time2, iSize1_Time);
                    }
                    public void SetRandomEcho(byte[] arr_byteRandom)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_RandomEcho, iSize1_Time + iSize2_Time2);
                    }

                    public byte[] GetTime()
                    {
                        return Utils.Utils.SubArr(data, 0, iSize1_Time);
                    }
                    public byte[] GetTime2()
                    {
                        return Utils.Utils.SubArr(data, iSize1_Time, iSize2_Time2);
                    }
                    public byte[] GetRandomEcho()
                    {
                        return Utils.Utils.SubArr(data, iSize1_Time + iSize2_Time2, iSize3_RandomEcho);
                    }
                }
            }

            public class S2
            {
                public const short iSize = 1536;
                public const short iSize1_Time = 4;
                public const short iSize2_Time2 = 4;
                public const short iSize3_RandomEcho = iSize - iSize1_Time - iSize2_Time2;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetTime2(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Time2, iSize1_Time);
                    }
                    public void SetRandomEcho(byte[] arr_byteRandom)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_RandomEcho, iSize1_Time + iSize2_Time2);
                    }

                    public byte[] GetTime()
                    {
                        return Utils.Utils.SubArr(data, 0, iSize1_Time);
                    }
                    public byte[] GetTime2()
                    {
                        return Utils.Utils.SubArr(data, iSize1_Time, iSize2_Time2);
                    }
                    public byte[] GetRandomEcho()
                    {
                        return Utils.Utils.SubArr(data, iSize1_Time + iSize2_Time2, iSize3_RandomEcho);
                    }
                }
            }
        }

        // TODO:
        // C1, S1, C2, S2
        public static class ComplexHandShakePackets
        {
            public class C0
            {
                public const short iSize = 1;
                public const short iSize1_Version = 1;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteVersion, iSize1_Version);
                    }
                }
            }

            public class S0
            {
                public const short iSize = 1;
                public const short iSize1_Version = 1;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteVersion, iSize1_Version);
                    }
                }
            }

            /*public class C1
            {
                // Version
                //  客户端的C1一般是0x80000702
                //  服务端的S1一般是0x04050001、0x0d0e0a0d(livego中数值)
                //

                public const short iSize = 1536;
                public const short iSize1_Time = 4;
                public const short iSize2_Version = 4;
                public const short iSize3_Key = 764;
                public const short iSize4_Digest = 764;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Zero, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_Random, iSize1_Time + iSize2_Zero);
                    }
                }
            }

            public class S1
            {
                public const short iSize = 1536;
                public const short iSize1_Time = 4;
                public const short iSize2_Zero = 4;
                public const short iSize3_Random = iSize - iSize1_Time - iSize2_Zero;

                public Data data = new Data();

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Zero, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_Random, iSize1_Time + iSize2_Zero);
                    }
                }
            }

            public class C2
            {
                public const short iSize = 1536;
                public const short iSize1_Time = 4;
                public const short iSize2_Time2 = 4;
                public const short iSize3_RandomEcho = iSize - iSize1_Time - iSize2_Time2;

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Time2, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_RandomEcho, iSize1_Time + iSize2_Time2);
                    }
                }
            }

            public class S2
            {
                public const short iSize = 1536;
                public const short iSize1_Time = 4;
                public const short iSize2_Time2 = 4;
                public const short iSize3_RandomEcho = iSize - iSize1_Time - iSize2_Time2;

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Time2, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        data = Utils.Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_RandomEcho, iSize1_Time + iSize2_Time2);
                    }
                }
            }*/
        }
    }
}
