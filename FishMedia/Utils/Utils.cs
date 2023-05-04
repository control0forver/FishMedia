using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FishMedia.Utils
{
    public static class Utils
    {
        public static class BinaryConverter
        {
            /*public static int ReadReverseInt24(BinaryReader reader)
            {
                byte[] bytes = reader.ReadBytes(3);
                Array.Reverse(bytes);
                int value = BitConverter.ToInt32(bytes, 0);
                return value >> 8;
            }

            public static uint ReadReverseUInt24(BinaryReader reader)
            {
                byte[] bytes = reader.ReadBytes(3);
                Array.Reverse(bytes);
                uint value = BitConverter.ToUInt32(bytes, 0);
                return value >> 8;
            }

            public static uint ReadReverseUInt32(BinaryReader reader)
            {
                byte[] bytes = reader.ReadBytes(4);
                Array.Reverse(bytes);
                uint value = BitConverter.ToUInt32(bytes, 0);
                return value;
            }*/

            public static int ReadReverseInt24(byte[] arr_byteData, int iOffset = 0)
            {
                int value = (arr_byteData[iOffset] << 16) | (arr_byteData[iOffset + 1] << 8) | arr_byteData[iOffset + 2];
                if ((value & 0x00800000) != 0)
                {
                    value = (int)(0xff000000 | value);
                }
                return value;
            }

            public static uint ReadReverseUInt24(byte[] arr_byteData, int iOffset = 0)
            {
                return (uint)((arr_byteData[iOffset] << 16) | (arr_byteData[iOffset + 1] << 8) | arr_byteData[iOffset + 2]);
            }

            public static uint ReadReverseUInt32(byte[] arr_byteData, int iOffset = 0)
            {
                return (uint)((arr_byteData[iOffset] << 24) | (arr_byteData[iOffset + 1] << 16) | (arr_byteData[iOffset + 2] << 8) | arr_byteData[iOffset + 3]);
            }

        }

        public static byte[] CharArrayToByteArray(char[] arr_chCharArray)
        {
            int iLength = arr_chCharArray.Length;
            byte[] byteArray = new byte[iLength];

            for (int i = 0; i < iLength; ++i)
            {
                byteArray[i] = (byte)arr_chCharArray[i];
            }

            return byteArray;
        }

        public static char[] ByteArrayToCharArray(byte[] arr_byteBytesArray)
        {
            int iLength = arr_byteBytesArray.Length;
            char[] charArray = new char[iLength];

            for (int i = 0; i < iLength; ++i)
            {
                charArray[i] = (char)arr_byteBytesArray[i];
            }

            return charArray;
        }

        public static bool ToBoolean(int iValue)
        {
            return 0 == iValue ? false : true;
        }

        public static int ToInteger(bool bValue)
        {
            return false == bValue ? 0 : 1;
        }

        public static bool IsV6Address(AddressFamily addressFamily)
        {
            bool result = false;
            if (addressFamily == AddressFamily.InterNetwork)
                result = false;
            if (addressFamily == AddressFamily.InterNetworkV6)
                result = true;

            return result;
        }
        public static bool IsV6Address(IPAddress iPAddress)
        {
            return IsV6Address(iPAddress.AddressFamily);
        }

        public static byte[] TrimByteArrayEnd(byte[] arr_byteBytes)
        {
            List<byte> list = arr_byteBytes.ToList();
            for (int i = arr_byteBytes.Length - 1; i >= 0; i--)
            {
                if (arr_byteBytes[i] == 0x00)
                {
                    list.RemoveAt(i);
                }
                else
                {
                    break;
                }
            }
            return list.ToArray();
        }

        public static bool _CompareArr<T>(T[] arr1, T[] arr2)
        {
            var q = from a in arr1 join b in arr2 on a equals b select a;
            bool flag = arr1.Length == arr2.Length && q.Count() == arr1.Length;

            return flag;

        }
        public static bool CompareArr<T>(T[] arr1, T[] arr2)
        {
            if (arr1.Length != arr2.Length)
                return false;

            for (int i = 0; i < arr1.Length; ++i)
            {
                if (!arr1[i].Equals(arr2[i]))
                    return false;
            }

            return true;

        }

        public static T[] SubArr<T>(T[] arr_SrcArray, int iStartIndex = 0)
        {
            return arr_SrcArray.Skip(iStartIndex).ToArray();
        }
        public static T[] SubArr<T>(T[] arr_SrcArray, int iStartIndex, int iIndexCount)
        {
            return arr_SrcArray.Skip(iStartIndex).Take(iIndexCount).ToArray();
        }

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
