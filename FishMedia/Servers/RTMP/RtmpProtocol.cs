using System;
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
            Abort = 0x02,
            MessageRead = 0x03,
            UserControl = 0x04,
            WindowSize = 0x05,
            SetPeerBandwidth = 0x06,
            /*  ... 0x07 */
            Audio = 0x08,
            Video = 0x09,
            /*  ... 0x0A */
            /*  ... 0x0B */
            /*  ... 0x0C */
            /*  ... 0x0D */
            /*  ... 0x0E */
            DataAMF3 = 0x0F,
            SharedObjectAMF3 = 0x10,
            CommandAMF3 = 0x11,
            DataAMF0 = 0x12,
            SharedObjectAMF0 = 0x13,
            CommandAMF0 = 0x14,
            /*  ... 0x15 */
            Aggregate = 0x16
        }

        public enum RtmpHeaderFormat
        {
            Large = 0,  // 11 bytes
            Medium,     // 7  bytes
            Small,      // 3  bytes
            Minimum     // 0  bytes
        }
        public static short RtmpHeaderTypeLength(RtmpHeaderFormat headertypeHeaderType)
        {
            switch (headertypeHeaderType)
            {
                case RtmpHeaderFormat.Large:
                    return 11;
                case RtmpHeaderFormat.Medium:
                    return 7;
                case RtmpHeaderFormat.Small:
                    return 3;
                case RtmpHeaderFormat.Minimum:
                    return 0;
            }

            return 3;
        }

        #endregion

        public enum TimestampType
        {
            ABSOLUTE = 0,
            RELATIVE = 1,
            NONE = 3 // Packet Has No TimeStamp
        };


        public struct RTMPBasicHeader
        {
            public byte[] arr_byteBasicHeader;

            public byte u_iFormat
            { get { return (byte)((arr_byteBasicHeader[0] >> 6) & 0b11); } }
            public uint u_iChunkStreamIdByte1
            {
                get
                {
                    return (uint)(arr_byteBasicHeader[0] & 0b00111111);
                }
            }
            public uint u_iChunkStreamId
            {
                get
                {
                    uint _u_iChunkStreamIdByte1 = u_iChunkStreamIdByte1;

                    if (_u_iChunkStreamIdByte1 == 0)
                    {
                        return (uint)(64 + arr_byteBasicHeader[1]);
                    }
                    if (_u_iChunkStreamIdByte1 == 1)
                    {
                        return (uint)(64 + arr_byteBasicHeader[1] + arr_byteBasicHeader[2] * 256);
                    }

                    return _u_iChunkStreamIdByte1;
                }
            }
            public TimestampType tsTimeStampSet
            {
                get
                {
                    uint _u_iChunkStreamId = u_iChunkStreamId;

                    if (_u_iChunkStreamId == 2)
                        return TimestampType.RELATIVE;

                    if (_u_iChunkStreamId == 0 || _u_iChunkStreamId == 1 || _u_iChunkStreamId == 3)
                        return (TimestampType)_u_iChunkStreamId;

                    return TimestampType.ABSOLUTE;
                }
            }

            #region Static Utils
            public static uint BasicHeaderSize(byte byteByte1)
            {
                return (uint)(byteByte1 & 0b00111111);
            }
            #endregion

            public RTMPBasicHeader(byte[] arr_byteBasicHeader)
            {
                this.arr_byteBasicHeader = Utils.Utils.CopyArrOut(arr_byteBasicHeader);
            }

            public void Reset()
            {
                arr_byteBasicHeader = null;
            }
        }

        public struct RTMPChunkHeader
        {
            public RTMPBasicHeader _bschdrBasicHeader;
            public RTMPBasicHeader bschdrBasicHeader
            {
                get
                { return _bschdrBasicHeader; }
                set
                { _bschdrBasicHeader = value; DataReset(); }
            }
            public byte[] arr_byteChunkMessageHeader;
            public uint u_iExtendedTimeStamp;

            public RtmpHeaderFormat hdrFormat;
            public RtmpPacketType pktmsgChunkStreamId;

            private void DataReset()
            {
                hdrFormat = (RtmpHeaderFormat)bschdrBasicHeader.u_iFormat;
                pktmsgChunkStreamId = (RtmpPacketType)bschdrBasicHeader.u_iChunkStreamId;

                arr_byteChunkMessageHeader = new byte[(int)RtmpHeaderTypeLength(hdrFormat)];
            }

            public RTMPChunkHeader(byte[] byteData)
            {
                hdrFormat = 0;
                pktmsgChunkStreamId = 0;
                arr_byteChunkMessageHeader = null;
                u_iExtendedTimeStamp = 0;
                _bschdrBasicHeader = new RTMPBasicHeader(byteData);
            }

            public void Reset()
            {
                hdrFormat = 0;
                pktmsgChunkStreamId = 0;
                u_iExtendedTimeStamp = 0;
                arr_byteChunkMessageHeader = null;
                _bschdrBasicHeader.Reset();
            }
        }

        /*
        public struct RTMPChunk
        {
            public RTMPChunkHeader hdrHeader;
            public List<byte> arr_dChunkData;

            public RTMPChunk() { hdrHeader = new RTMPChunkHeader(); arr_dChunkData = new List<byte>(); }

            public RTMPChunk(RTMPChunkHeader hdrHeader, byte[] arr_byteData)
            {
                this.hdrHeader = hdrHeader;
                arr_dChunkData = new List<byte>(arr_byteData);
            }

            public void Reset()
            {
                hdrHeader.Reset();
                arr_dChunkData.Clear();
            }
        }*/

        public struct RTMPChunk
        {
            public RTMPChunkHeader hdrHeader;
            public byte[] arr_dChunkData;

            public RTMPChunk(int iChunkSize = 512) { hdrHeader = new RTMPChunkHeader(); arr_dChunkData = new byte[iChunkSize]; }

            public RTMPChunk(RTMPChunkHeader hdrHeader, int iChunkSize = 512)
            {
                this.hdrHeader = hdrHeader;
                arr_dChunkData = new byte[iChunkSize];
            }

            // Returns overflowed bytes count
            public int SetChunkData(byte[] arr_byteData)
            {
                Utils.Utils.CopyArr(arr_byteData, arr_dChunkData, arr_dChunkData.Length);
                return arr_byteData.Length - arr_dChunkData.Length;
            }

            public void ChunkResize(uint iChunkSize, bool bTryKeepData = false)
            {
                if (bTryKeepData)
                {
                    Array.Resize(ref arr_dChunkData, (int)iChunkSize);
                    return;
                }

                arr_dChunkData = new byte[iChunkSize];
            }

            public void Reset()
            {
                hdrHeader.Reset();
                Array.Clear(arr_dChunkData, 0, arr_dChunkData.Length);
            }
        }

        public struct RTMPPacket
        {
            public bool bHasAbsTimestamp;
            public uint u_iBodySize;
            public uint u_iTimeStamp;
            public byte u_iPacketType;
            public uint u_iStreamId;
            public RTMPChunk[] p_chkBody;

            public RTMPPacket()
            {
                u_iTimeStamp = 0;
                u_iBodySize = 0;
                u_iPacketType = (byte)0;
                u_iStreamId = 0;
                this.p_chkBody = new RTMPChunk[1] { new RTMPChunk() };
                bHasAbsTimestamp = false;
            }

            public void Reset()
            {
                u_iTimeStamp = 0;
                u_iBodySize = 0;
                u_iPacketType = (byte)0;
                u_iStreamId = 0;
                bHasAbsTimestamp = false;
                p_chkBody[0].Reset();
            }

            public int Load(byte[] arr_byteRtmpPacketData)
            {
                using (BinaryReader brBytes = new BinaryReader(new MemoryStream(arr_byteRtmpPacketData)))
                {
                    long iLastBRPos = brBytes.BaseStream.Position;

                    RTMPBasicHeader bschdrBasicHeader;

                    #region Header

                    #region 1 byte

                    #region BasicHeader
                    {
                        iLastBRPos = brBytes.BaseStream.Position; // Record Pos

                        bschdrBasicHeader = new RTMPBasicHeader(new byte[1] { brBytes.ReadByte() });
                        switch (bschdrBasicHeader.u_iChunkStreamIdByte1)
                        {
                            default:
                                break;

                            case 2:
                                break;

                            case 0:
                                brBytes.BaseStream.Position = iLastBRPos; // Resume Pos
                                bschdrBasicHeader = new RTMPBasicHeader(brBytes.ReadBytes(2));
                                break;
                            case 1:
                                brBytes.BaseStream.Position = iLastBRPos; // Resume Pos
                                bschdrBasicHeader = new RTMPBasicHeader(brBytes.ReadBytes(3));
                                break;
                        }

                        p_chkBody[0].hdrHeader.bschdrBasicHeader = bschdrBasicHeader;
                    }
                    #endregion

                    #region HasAbsTimeStamp
                    {
                        switch (bschdrBasicHeader.tsTimeStampSet)
                        {
                            default: break; // Set As Default: false

                            case TimestampType.ABSOLUTE:
                                bHasAbsTimestamp = true;
                                break;

                            case TimestampType.RELATIVE:
                                bHasAbsTimestamp = false;
                                break;

                            case TimestampType.NONE:
                                bHasAbsTimestamp = false;
                                break;
                        }
                    }
                    #endregion

                    #endregion

                    #region 3-4 bytes

                    #region TimeStamp
                    {
                        iLastBRPos = brBytes.BaseStream.Position; // Record Pos
                        u_iTimeStamp = Utils.Utils.ByteConverter.ReadReverseUInt24(brBytes.ReadBytes(3)); // 3 Bytes
                        if (u_iTimeStamp > 0xFFFFFF)
                        {
                            brBytes.BaseStream.Position = iLastBRPos; // Resume Pos
                            u_iTimeStamp = Utils.Utils.ByteConverter.ReadReverseUInt32(brBytes.ReadBytes(4)); // 4 Bytes if > 0xFFFFFF
                        }
                    }
                    #endregion

                    #endregion

                    #region 3 bytes

                    #region BodySize
                    {
                        u_iBodySize = Utils.Utils.ByteConverter.ReadReverseUInt24(brBytes.ReadBytes(3));
                    }
                    #endregion

                    #endregion

                    #region 1 byte

                    #region PacketType
                    {
                        u_iPacketType = brBytes.ReadByte();
                    }
                    #endregion

                    #endregion

                    #region 4 bytes

                    #region StreamId
                    {
                        u_iStreamId = Utils.Utils.ByteConverter.ReadReverseUInt32(brBytes.ReadBytes(4));
                    }
                    #endregion

                    #endregion

                    #endregion

                    #region Body
                    {
                        byte[] arr_byteBody = brBytes.ReadBytes((int)u_iBodySize);
                        Utils.Utils.CopyArr(arr_byteBody, p_chkBody[0].arr_dChunkData, arr_byteBody.Length);
                    }
                    #endregion

                    return (int)brBytes.BaseStream.Position;
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
                        Utils.Utils.CopyArr(arr_byteArray, data,iSize);
                    }
                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        Utils.Utils.CopyArr(arr_byteVersion, data, iSize1_Version);
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
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize);
                    }
                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        Utils.Utils.CopyArr(arr_byteVersion, data, iSize1_Version);
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
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        Utils.Utils.CopyArr(arr_byteTime, data, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize2_Zero, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        Utils.Utils.CopyArr(arr_byteRandom, data, iSize3_Random, iSize1_Time + iSize2_Zero);
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
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        Utils.Utils.CopyArr(arr_byteTime, data, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize2_Zero, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        Utils.Utils.CopyArr(arr_byteRandom, data, iSize3_Random, iSize1_Time + iSize2_Zero);
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
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        Utils.Utils.CopyArr(arr_byteTime, data, iSize1_Time);
                    }
                    public void SetTime2(byte[] arr_byteArray)
                    {
                        Utils.Utils.CopyArr(arr_byteArray,data, iSize2_Time2,iSize1_Time);
                    }
                    public void SetRandomEcho(byte[] arr_byteRandom)
                    {
                        Utils.Utils.CopyArr(arr_byteRandom, data, iSize3_RandomEcho, iSize1_Time + iSize2_Time2);
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
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        Utils.Utils.CopyArr(arr_byteTime, data, iSize1_Time);
                    }
                    public void SetTime2(byte[] arr_byteArray)
                    {
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize2_Time2, iSize1_Time);
                    }
                    public void SetRandomEcho(byte[] arr_byteRandom)
                    {
                        Utils.Utils.CopyArr(arr_byteRandom, data, iSize3_RandomEcho, iSize1_Time + iSize2_Time2);
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
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize);
                    }
                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        Utils.Utils.CopyArr(arr_byteVersion, data, iSize1_Version);
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
                        Utils.Utils.CopyArr(arr_byteArray, data, iSize);
                    }
                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        Utils.Utils.CopyArr(arr_byteVersion, data, iSize1_Version);
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

