using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FishMedia.Servers.RTMP
{
    public static class RtmpDefs
    {
        public static class SimpleHandShakePackets
        {
            public class C0
            {
                public const short iSize = 1;
                public const short iSize1_Version = 1;
                public const byte byteDefaultData = 0x03;

                class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize] { byteDefaultData };

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }

                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteVersion, iSize1_Version);
                    }
                }
            }

            public class S0
            {
                public const short iSize = 1;
                public const short iSize1_Version = 1;
                public const byte byteDefaultData = 0x03;

                class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize] { byteDefaultData };

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }

                    public void SetVersion(byte[] arr_byteVersion)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteVersion, iSize1_Version);
                    }
                }
            }

            public class C1
            {
                public const short iSize = 1536;
                public const short iSize1_Time = 4;
                public const short iSize2_Zero = 4;
                public const short iSize3_Random = iSize - iSize1_Time - iSize2_Zero;

                public class Data
                {
                    public byte[] data { get; private set; } = new byte[iSize];

                    public void Set(byte[] arr_byteArray)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Zero, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_Random, iSize1_Time + iSize2_Zero);
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
                        data = Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Time2, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_RandomEcho, iSize1_Time + iSize2_Time2);
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
                        data = Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize);
                    }
                    public void SetTime(byte[] arr_byteTime)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteTime, iSize1_Time);
                    }
                    public void SetZero(byte[] arr_byteArray)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteArray, iSize2_Time2, iSize1_Time);
                    }
                    public void SetRandom(byte[] arr_byteRandom)
                    {
                        data = Utils.CopyArrInRange<byte>(data, arr_byteRandom, iSize3_RandomEcho, iSize1_Time + iSize2_Time2);
                    }
                }
            }
        }

        public static class ComplexHandShakePackets { }


        private static class Utils
        {
            public static T[] CopyArrInRange<T>(T[] arr_SrcArray, T[] arr_CopyArray, int iIndexCount = 1, int iSrcStartIndex = 0, int iCopyStartIndex = 0)
            {
                T[] arr_ResultArray = (T[])arr_SrcArray.Clone();

                for (int i = 0; i < iIndexCount; i++)
                {
                    arr_ResultArray[iSrcStartIndex + i] = arr_CopyArray[iCopyStartIndex + i];
                }

                return arr_ResultArray;
            }
        }
    }
}
