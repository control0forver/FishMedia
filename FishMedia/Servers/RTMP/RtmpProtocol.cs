﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using static FishMedia.Servers.RTMP.RtmpProtocol;
using static FishMedia.Servers.RTMP.AMF;
using static FishMedia.Servers.RTMP.AMF.AVal;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using FishMedia.Utils;

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

            public static readonly AVal _avCheckbw = AVC("_checkbw");
            public static readonly AVal _avError = AVC("_error");
            public static readonly AVal _avOnbwcheck = AVC("_onbwcheck");
            public static readonly AVal _avOnbwdone = AVC("_onbwdone");
            public static readonly AVal _avResult = AVC("_result");
            public static readonly AVal av0 = AVC("0");
            public static readonly AVal avApp = AVC("app");
            public static readonly AVal avAudio = AVC("audio");
            public static readonly AVal avAudioCodecs = AVC("audioCodecs");
            public static readonly AVal avCapabilities = AVC("capabilities");
            public static readonly AVal avClose = AVC("close");
            public static readonly AVal avCode = AVC("code");
            public static readonly AVal avConnect = AVC("connect");
            public static readonly AVal avCreateStream = AVC("createStream");
            public static readonly AVal avDeleteStream = AVC("deleteStream");
            public static readonly AVal avDescription = AVC("description");
            public static readonly AVal avDuration = AVC("duration");
            public static readonly AVal avFCSubscribe = AVC("FCSubscribe");
            public static readonly AVal avFCUnpublish = AVC("FCUnpublish");
            public static readonly AVal avFlashVer = AVC("flashVer");
            public static readonly AVal avFpad = AVC("fpad");
            public static readonly AVal avLevel = AVC("level");
            public static readonly AVal avLive = AVC("live");
            public static readonly AVal avNonprivate = AVC("nonprivate");
            public static readonly AVal avObjectEncoding = AVC("objectEncoding");
            public static readonly AVal avOnBWDone = AVC("onBWDone");
            public static readonly AVal avOnFCSubscribe = AVC("onFCSubscribe");
            public static readonly AVal avOnFCUnsubscribe = AVC("onFCUnsubscribe");
            public static readonly AVal avOnMetaData = AVC("onMetaData");
            public static readonly AVal avOnStatus = AVC("onStatus");
            public static readonly AVal avPageUrl = AVC("pageUrl");
            public static readonly AVal avPause = AVC("pause");
            public static readonly AVal avPing = AVC("ping");
            public static readonly AVal avPlay = AVC("play");
            public static readonly AVal avPlaylist_ready = AVC("playlist_ready");
            public static readonly AVal avPong = AVC("pong");
            public static readonly AVal avPublish = AVC("publish");
            public static readonly AVal avRecord = AVC("record");
            public static readonly AVal avReleaseStream = AVC("releaseStream");
            public static readonly AVal avSecureToken = AVC("secureToken");
            public static readonly AVal avSecureTokenResponse = AVC("secureTokenResponse");
            public static readonly AVal avSeek = AVC("seek");
            public static readonly AVal avSet_playlist = AVC("set_playlist");
            public static readonly AVal avSwfUrl = AVC("swfUrl");
            public static readonly AVal avTcUrl = AVC("tcUrl");
            public static readonly AVal avType = AVC("type");
            public static readonly AVal avVideo = AVC("video");
            public static readonly AVal avVideoCodecs = AVC("videoCodecs");
            public static readonly AVal avVideoFunction = AVC("videoFunction");
        }

        public enum RtmpFeature
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
            RTMPE = RtmpFeature.ENC,
            RTMPT = RtmpFeature.HTTP,
            RTMPS = RtmpFeature.SSL,
            RTMPTE = (RtmpFeature.HTTP | RtmpFeature.ENC),
            RTMPTS = (RtmpFeature.HTTP | RtmpFeature.SSL),
            RTMFP = RtmpFeature.MFP
        }

        public enum RtmpPacketType
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

        public enum RtmpHeaderType
        {
            Large = 0,  // 11 bytes
            Medium,     // 7  bytes
            Small,      // 3  bytes
            Minimum     // 0  bytes
        }
        public static short RtmpHeaderTypeLength(RtmpHeaderType headertypeHeaderType)
        {
            switch (headertypeHeaderType)
            {
                case RtmpHeaderType.Large:
                    return 11;
                case RtmpHeaderType.Medium:
                    return 7;
                case RtmpHeaderType.Small:
                    return 3;
                case RtmpHeaderType.Minimum:
                    return 0;
            }

            return 3;
        }

        #endregion

        public struct RTMPChunk
        {
            public byte[] arr_dChunk;
            public byte[] arr_dHeader;
        }

        public struct RTMPPacket
        {
            public byte u_iHeaderType;
            public uint u_iTimeStamp;
            public uint u_iMessageLength;
            public byte u_iPacketType;
            public int iChannelId;
            public uint u_iMessageStreamId;
            public RTMPChunk[] p_chkChunk;
            public byte u_iHasAbsTimestamp;

            public RTMPPacket()
            {
                u_iHeaderType = (byte)0;
                u_iTimeStamp = 0;
                u_iMessageLength = 0;
                u_iPacketType = (byte)0;
                iChannelId = 0;
                u_iMessageStreamId = 0;
                this.p_chkChunk = new RTMPChunk[1] { new RTMPChunk() };
                u_iHasAbsTimestamp = (byte)0;
            }

            public RTMPPacket(byte[] arr_byteRtmpPacketData)
            {
                BinaryReader brBytes = new BinaryReader(new MemoryStream(arr_byteRtmpPacketData));
                {
                    u_iHeaderType = brBytes.ReadByte();
                    u_iTimeStamp = Utils.Utils.BinaryConverter.ReadReverseUInt24(brBytes.ReadBytes(RtmpHeaderTypeLength((RtmpHeaderType)u_iHeaderType)));
                    u_iMessageLength = Utils.Utils.BinaryConverter.ReadReverseUInt24(brBytes.ReadBytes(3));
                    u_iPacketType = brBytes.ReadByte();
                    iChannelId = brBytes.ReadByte();
                    u_iMessageStreamId = Utils.Utils.BinaryConverter.ReadReverseUInt32(brBytes.ReadBytes(4));
                    p_chkChunk = new RTMPChunk[1]{ new RTMPChunk() };
                    u_iHasAbsTimestamp = 0;
                };
            }
        }
        /*

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
         */

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

