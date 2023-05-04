using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static FishMedia.Servers.RTMP.RtmpProtocol;

namespace FishMedia.Servers.RTMP
{
    public static class RtmpProtocol
    {
        private static readonly Encoding byteEncoder = Encoding.UTF8;

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
            Large = 0,  // 11 bytes
            Medium,     // 7  bytes
            Small,      // 3  bytes
            Minimum     // 0  bytes
        }

        #endregion

        public struct RTMPChunk
        {
            public int iHeaderSize;
            public int iChunkSize;
            public byte[] p_dChunk;
            public byte[] arr_dHeader;

            #region Public Member Methods
            public string GetChunkString()
            {
                return GetChunkString(p_dChunk);
            }
            public char[] GetChunk()
            {
                return GetChunk(p_dChunk);
            }
            public string GetHeaderString()
            {
                return GetHeaderString(arr_dHeader);
            }
            public char[] GetHeader()
            {
                return GetHeader(arr_dHeader);
            }
            #endregion 

            #region Static Methods
            public static string GetChunkString(byte[] data)
            {
                return byteEncoder.GetString(data);
            }
            public static char[] GetChunk(byte[] data)
            {
                return GetChunkString(data).ToCharArray();
            }
            public static string GetHeaderString(byte[] data)
            {
                return byteEncoder.GetString(data);
            }
            public static char[] GetHeader(byte[] data)
            {
                return GetHeaderString(data).ToCharArray();
            }
            public static int GetChunkHeaderSize(byte byteByte)
            {
                int iSize = 0;
                int fmt = (byteByte >> 6) & 0x03;
                switch (fmt)
                {
                    case (int)RtmpPacketSizes.Large:
                        iSize = 11;
                        break;
                    case (int)RtmpPacketSizes.Medium:
                        iSize = 7;
                        break;
                    case (int)RtmpPacketSizes.Small:
                        iSize = 3;
                        break;
                    case (int)RtmpPacketSizes.Minimum:
                        iSize = 0;
                        break;

                    default: return iSize;
                }
                return iSize;
            }
            #endregion
        }

        public struct RTMPPacket
        {
            public byte u_iHeaderType;
            public byte u_iPacketType;
            public byte u_iHasAbsTimestamp;
            public int iChannel;
            public uint u_iTimeStamp;
            public int u_iInfoField2;
            public uint u_iBodySize;
            public uint u_iBytesRead;
            public RTMPChunk[] p_chkChunk;
            public byte[] dBody;


            public static string GetBodyString(byte[] data)
            {
                return byteEncoder.GetString(data);
            }
            public static char[] GetBody(byte[] data)
            {
                return GetBodyString(data).ToCharArray();
            }
            public string GetBodyString()
            {
                return GetBodyString(dBody);
            }
            public char[] GetBody()
            {
                return GetBody(dBody);
            }

            public RTMPPacket(byte[] arr_byteBytes)
            {
                p_chkChunk = new RTMPChunk[1];
                p_chkChunk[0] = new RTMPChunk();
                dBody = null;

                MemoryStream memStream = new MemoryStream(arr_byteBytes);
                BinaryReader binReader = new BinaryReader(memStream);

                // Read RTMPPacket Header
                u_iHeaderType = binReader.ReadByte();
                iChannel = (int)Utils.Utils.BinaryConverter.ReadReverseInt24(binReader.ReadBytes(3));
                u_iTimeStamp = Utils.Utils.BinaryConverter.ReadReverseUInt24(binReader.ReadBytes(3));
                u_iTimeStamp |= (uint)binReader.ReadByte() << 24;
                u_iHasAbsTimestamp = (byte)(u_iTimeStamp >> 31);
                u_iTimeStamp &= 0x7fffffff;
                u_iPacketType = binReader.ReadByte();
                u_iBodySize = Utils.Utils.BinaryConverter.ReadReverseUInt32(binReader.ReadBytes(4));
                u_iBytesRead = 0;
                u_iInfoField2 = binReader.ReadInt32();

                // Read RTMPPacket Chunk
                int iChunkSize = 0;
                switch (iChannel)
                {
                    case 2:
                        iChunkSize = 128;
                        break;
                    case 3:
                        iChunkSize = 64;
                        break;
                    default:
                        iChunkSize = 256;
                        break;
                }
                p_chkChunk[0].iChunkSize = iChunkSize;
                p_chkChunk[0].iHeaderSize = RTMPChunk.GetChunkHeaderSize(u_iHeaderType);
                p_chkChunk[0].p_dChunk = new byte[u_iBodySize];
                p_chkChunk[0].arr_dHeader = new byte[MiscInfos.iRtmpMaxHeaderSize];
                binReader.Read(p_chkChunk[0].arr_dHeader, 0, p_chkChunk[0].iHeaderSize);

                // Read RTMPPacket Body
                dBody = new byte[u_iBodySize];
                int iBytesToRead = (int)(u_iBodySize - u_iBytesRead);
                if (iBytesToRead < iChunkSize)
                {
                    iChunkSize = iBytesToRead;
                }

                while (iBytesToRead > 0)
                {
                    int iBytesRead = binReader.Read(dBody, (int)u_iBytesRead, iBytesToRead);
                    if (iBytesRead == 0)
                        break;
                    u_iBytesRead += (uint)iBytesRead;
                    iBytesToRead -= iBytesRead;
                }
                p_chkChunk[0].p_dChunk = dBody;
            }
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

