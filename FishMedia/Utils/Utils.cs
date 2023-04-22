using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FishMedia.Utils
{
    public static class Utils
    {

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
