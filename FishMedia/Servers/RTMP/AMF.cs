using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Xml.Linq;
using FishMedia.Utils;
using static FishMedia.Servers.RTMP.AMF;
using static FishMedia.Utils.Utils;

namespace FishMedia.Servers.RTMP
{
    public static class AMF
    {
        #region Types
        private const int ATRUE = 1;
        private const int AFALSE = 0;

        public const int AMF3_INTEGER_MAX = 268435455;
        public const int AMF3_INTEGER_MIN = -268435456;

        public enum AMFDataType
        {
            Number = 0, Boolean, String, Object,
            MovieClip,      /* reserved, not used */
            Null, Undefined, Reference, Ecma_Array, Object_End,
            Strict_Array, Date, Long_String, Unsupported,
            Recordset,      /* reserved, not used */
            Xml_Doc, Typed_Object,
            Avmplus,        /* switch to AMF3 */
            Invalid = 0xff
        }

        public enum AMF3DataType
        {
            Undefined = 0, Null, False, True,
            Integer, Double, String, Xml_Doc, Date,
            Array, Object, Xml, Byte_Array
        }

        public struct AVal
        {
            public byte[] arr_chValue;
            public int iLength;

            public static AVal AVC(char[] str)
            {
                return new AVal { arr_chValue = Encoding.UTF8.GetBytes(str), iLength = str.Length - 1 };
            }
            public static AVal AVC(string str) { return AVC(str.ToCharArray()); }
            public static bool AVMATCH(AVal a1, AVal a2)
            {
                return a1.iLength == a2.iLength && Enumerable.SequenceEqual(a1.arr_chValue, a2.arr_chValue);
            }
        }

        public struct AMFObject
        {
            public int iObjectNum;
            public AMFObjectProperty[] arr_amfobjpropObjProps;
        }

        public struct AMFPropertyValue
        {
            public double iPropertyNumber;
            public AVal avValue;
            public AMFObject objPropertyObject;
        }

        public struct AMFObjectProperty
        {
            public AVal PropertyName;
            public AMFDataType PropertyType;
            public AMFPropertyValue PropertyValue;
            public short iPropertyUTCOffset;
        }

        public struct AMF3ClassDef
        {
            public AVal ClassDefName;
            public byte ClassDefExternalizable;
            public byte ClassDefDynamic;
            public int ClassDefNum;
            public AVal[] ClassDefProps;
        }

        public static readonly AVal c_avEmpty = new AVal { arr_chValue = ArraySegment<byte>.Empty.ToArray(), iLength = 0 };
        public static readonly AMFObjectProperty c_amfobjpropInvalid = new AMFObjectProperty { PropertyName = c_avEmpty, PropertyType = AMFDataType.Invalid };
        public static readonly AMFObject c_amfobjInvalid = new AMFObject { iObjectNum = 0, arr_amfobjpropObjProps = ArraySegment<AMFObjectProperty>.Empty.ToArray() };
        #endregion

        #region Static Methods

        private static int AMFProp_Size(AMFObjectProperty prop)
        {
            int size = 0;

            size += AMF_EncodeInt16((short)prop.PropertyName.arr_chValue.Length).Length;
            size += prop.PropertyName.arr_chValue.Length;

            switch (prop.PropertyType)
            {
                case AMFDataType.String:
                    size += AMF_EncodeInt16((short)prop.PropertyValue.avValue.arr_chValue.Length).Length;
                    size += prop.PropertyValue.avValue.arr_chValue.Length;
                    size += 1; // for type byte
                    break;

                case AMFDataType.Boolean:
                    size += 2; // for type byte and boolean value
                    break;

                case AMFDataType.Null:
                case AMFDataType.Undefined:
                    size += 1; // for type byte
                    break;

                case AMFDataType.Number:
                    size += 9; // for type byte and double value
                    break;

                case AMFDataType.Object:
                    size += AMF_Encode(prop.PropertyValue.objPropertyObject).Length;
                    break;

                case AMFDataType.MovieClip:
                    size += 1; // for type byte
                    break;

                case AMFDataType.Object_End:
                    size += 1; // for type byte
                    break;

                case AMFDataType.Reference:
                    size += AMF_EncodeInt16((short)prop.PropertyValue.avValue.iLength).Length;
                    size += 1; // for type byte
                    break;

                case AMFDataType.Ecma_Array:
                    size += 4; // for type byte, count, and empty 24-bit size field
                    size += AMF_Encode(prop.PropertyValue.objPropertyObject).Length;
                    break;

                case AMFDataType.Strict_Array:
                    size += AMF_EncodeInt32(prop.PropertyValue.avValue.arr_chValue.Length).Length;
                    size += 1; // for type byte
                    size += AMF_Encode(prop.PropertyValue.objPropertyObject).Length;
                    break;

                case AMFDataType.Date:
                    size += 1; // for type byte
                    size += 8; // for date value
                    size += 2; // for time zone
                    break;

                case AMFDataType.Long_String:
                    size += AMF_EncodeInt32(prop.PropertyValue.avValue.arr_chValue.Length).Length;
                    size += prop.PropertyValue.avValue.arr_chValue.Length;
                    size += 1; // for type byte
                    break;

                default:
                    break;
            }

            return size;
        }

        #region Encode
        private static char[] ByteCastChar(byte[] arr_byteByteArray)
        {
            return Encoding.UTF8.GetString(arr_byteByteArray).ToCharArray();
        }

        public static class Byted
        {
            public static byte[] AMF_EncodeString(AVal avString)
            {
                int length = avString.iLength;
                int headerLength = 1 + ((length < 65536) ? 2 : 4);
                int totalLength = headerLength + length;

                byte[] output = new byte[totalLength];
                int index = 0;

                if (length < 65536)
                {
                    output[index++] = (byte)AMFDataType.String;

                    output[index++] = (byte)(length >> 8);
                    output[index++] = (byte)(length & 0xFF);
                }
                else
                {
                    output[index++] = (byte)AMFDataType.Long_String;

                    output[index++] = (byte)(length >> 24);
                    output[index++] = (byte)((length >> 16) & 0xFF);
                    output[index++] = (byte)((length >> 8) & 0xFF);
                    output[index++] = (byte)(length & 0xFF);
                }

                for (int i = 0; i <= length; i++)
                {
                    output[index++] = avString.arr_chValue[i];
                }

                return output;
            }
            public static byte[] AMF_EncodeNumber(double iValue)
            {
                byte[] output = new byte[9];
                int index = 0;

                if (index + 1 + 8 > output.Length)
                    return null;

                output[index++] = (byte)AMFDataType.Number;

                byte[] bytes = BitConverter.GetBytes(iValue);

                if (BitConverter.IsLittleEndian)
                    Array.Reverse(bytes);

                for (int i = 0; i < 8; i++)
                    output[index++] = (byte)bytes[i];

                return output;
            }
            public static byte[] AMF_EncodeInt16(short iValue)
            {
                byte[] output = new byte[2];
                int index = 0;

                if (index + 2 > output.Length)
                    return null;

                output[index++] = (byte)(iValue >> 8);
                output[index++] = (byte)(iValue & 0xff);

                return output;
            }
            public static byte[] AMF_EncodeInt24(int iValue)
            {
                byte[] output = new byte[3];
                int index = 0;

                if (index + 3 > output.Length)
                    return null;

                output[index++] = (byte)(iValue >> 16);
                output[index++] = (byte)(iValue >> 8);
                output[index++] = (byte)(iValue & 0xff);

                return output;
            }
            public static byte[] AMF_EncodeInt32(int iValue)
            {
                byte[] output = new byte[4];
                int index = 0;

                if (index + 4 > output.Length)
                    return null;

                output[index++] = (byte)(iValue >> 24);
                output[index++] = (byte)(iValue >> 16);
                output[index++] = (byte)(iValue >> 8);
                output[index++] = (byte)(iValue & 0xff);

                return output;
            }
            public static byte[] AMF_EncodeBoolean(int bValue)
            {
                byte[] output = new byte[2];
                int index = 0;

                if (index + 2 > output.Length)
                    return null;

                output[index++] = (byte)AMFDataType.Boolean;

                output[index++] = (byte)(bValue != 0 ? 0x01 : 0x00);

                return output;
            }

            public static byte[] AMF_EncodeNamedString(AVal avName, AVal avValue)
            {
                int nameLen = avName.iLength;
                int valueLen = avValue.iLength;
                int headerLength = 2 + nameLen;
                int totalLength = headerLength + valueLen;

                byte[] output = new byte[totalLength];
                int index = 0;

                if (headerLength > output.Length)
                    return null;

                output[index++] = (byte)(nameLen >> 8);
                output[index++] = (byte)(nameLen & 0xFF);

                for (int i = 0; i < nameLen; i++)
                {
                    output[index++] = (byte)avName.arr_chValue[i];
                }

                byte[] encodedValue = AMF_EncodeString(avValue);
                Array.Copy(encodedValue, 0, output, index, encodedValue.Length);

                return output;
            }
            public static byte[] AMF_EncodeNamedNumber(AVal avName, double dValue)
            {
                int nameLen = avName.iLength;
                int headerLength = 2 + nameLen + 9;
                int totalLength = headerLength;

                byte[] output = new byte[totalLength];
                int index = 0;

                if (headerLength > output.Length)
                    return null;

                output[index++] = (byte)(nameLen >> 8);
                output[index++] = (byte)(nameLen & 0xFF);

                for (int i = 0; i < nameLen; i++)
                {
                    output[index++] = (byte)avName.arr_chValue[i];
                }

                byte[] encodedValue = AMF_EncodeNumber(dValue);
                Array.Copy(encodedValue, 0, output, index, encodedValue.Length);

                return output;
            }
            public static byte[] AMF_EncodeNamedBoolean(AVal avName, int bValue)
            {
                int nameLen = avName.iLength;
                int headerLength = 2 + nameLen + 1;
                int totalLength = headerLength;

                byte[] output = new byte[totalLength];
                int index = 0;

                if (headerLength > output.Length)
                    return null;

                output[index++] = (byte)(nameLen >> 8);
                output[index++] = (byte)(nameLen & 0xFF);

                for (int i = 0; i < nameLen; i++)
                {
                    output[index++] = (byte)avName.arr_chValue[i];
                }

                byte[] encodedValue = AMF_EncodeBoolean(bValue);
                Array.Copy(encodedValue, 0, output, index, encodedValue.Length);

                return output;
            }

            public static byte[] AMF_Encode(AMFObject amfobjObject)
            {
                int i;
                int totalLength = 1;

                for (i = 0; i < amfobjObject.iObjectNum; i++)
                {
                    AVal propName = amfobjObject.arr_amfobjpropObjProps[i].PropertyName;
                    totalLength += 3 + propName.iLength; // 3 for AMF_PROPERTY, plus length bytes of propName
                    totalLength += AMFProp_Size(amfobjObject.arr_amfobjpropObjProps[i]); // add length of property value
                }

                totalLength += 3; // add 3 for AMF_OBJECT_END

                byte[] output = new byte[totalLength];
                int index = 0;

                if (index + 4 > output.Length)
                    return null;

                output[index++] = (byte)AMFDataType.Object;

                for (i = 0; i < amfobjObject.iObjectNum; i++)
                {
                    AVal propName = amfobjObject.arr_amfobjpropObjProps[i].PropertyName;
                    int propLength = AMFProp_Size(amfobjObject.arr_amfobjpropObjProps[i]);

                    if (index + 3 + propName.iLength + propLength > output.Length)
                        return null;

                    output[index++] = (byte)AMFDataType.String;
                    output[index++] = (byte)(propName.iLength >> 8);
                    output[index++] = (byte)(propName.iLength & 0xFF);
                    for (int j = 0; j < propName.iLength; j++)
                        output[index++] = (byte)propName.arr_chValue[j];

                    byte[] propValueBytes = AMFProp_Encode(amfobjObject.arr_amfobjpropObjProps[i]);
                    if (propValueBytes == null)
                        return null;

                    Array.Copy(propValueBytes, 0, output, index, propLength);
                    index += propLength;
                }

                if (index + 3 > output.Length)
                    return null; // no room for the end marker

                output[index++] = (byte)0;
                output[index++] = (byte)0;
                output[index++] = (byte)AMFDataType.Object_End;

                return output;
            }
            public static byte[] AMF_EncodeEcmaArray(AMFObject amfobjObject)
            {
                List<byte> output = new List<byte>(new List<byte>().Skip(1));

                output[0] = (byte)AMFDataType.Ecma_Array;
                output = new List<byte>(AMF_EncodeInt32(amfobjObject.iObjectNum));

                for (int i = 0; i < amfobjObject.iObjectNum; i++)
                {
                    byte[] res = AMFProp_Encode(amfobjObject.arr_amfobjpropObjProps[i]);
                    if (res == null)
                    {
                        // failed to encode property
                        break;
                    }
                    else
                    {
                        output = new List<byte>(res);
                    }
                }

                output = new List<byte>(AMF_EncodeInt24((int)AMFDataType.Object_End));

                return output.ToArray();
            }
            public static byte[] AMF_EncodeArray(AMFObject amfobjObject)
            {
                List<byte> output = new List<byte>(new List<byte>().Skip(1));
                output[0] = (byte)AMFDataType.Strict_Array;

                output = new List<byte>(AMF_EncodeInt32(amfobjObject.iObjectNum));

                for (int i = 0; i < amfobjObject.iObjectNum; i++)
                {
                    byte[] res = AMFProp_Encode(amfobjObject.arr_amfobjpropObjProps[i]);
                    if (res == null)
                    {
                        // failed to encode property
                        break;
                    }
                    else
                    {
                        output = new List<byte>(res);
                    }
                }

                // output = new List<byte>(AMF_EncodeInt24((int)AMFDataType.Object_End));

                return output.ToArray();
            }

            public static byte[] AMFProp_Encode(AMFObjectProperty amfobjpropObjProps)
            {
                if (amfobjpropObjProps.PropertyType == AMFDataType.Invalid)
                    return null;

                int totalLength = 0;

                if (amfobjpropObjProps.PropertyType != AMFDataType.Null)
                    totalLength = amfobjpropObjProps.PropertyName.iLength + 2 + 1;

                switch (amfobjpropObjProps.PropertyType)
                {
                    case AMFDataType.Number:
                        totalLength += 9;
                        break;

                    case AMFDataType.Boolean:
                        totalLength += 2;
                        break;

                    case AMFDataType.String:
                    case AMFDataType.Long_String:
                        totalLength += amfobjpropObjProps.PropertyValue.avValue.iLength;
                        break;

                    case AMFDataType.Object:
                    case AMFDataType.Ecma_Array:
                    case AMFDataType.Strict_Array:
                        totalLength += AMF_Encode(amfobjpropObjProps.PropertyValue.objPropertyObject).Length;
                        break;

                    case AMFDataType.Null:
                        totalLength += 1;
                        break;

                    default:
                        return null;
                }

                byte[] output = new byte[totalLength];
                int index = 0;

                if (amfobjpropObjProps.PropertyType != AMFDataType.Null && amfobjpropObjProps.PropertyName.iLength > 0)
                {
                    if (index + 2 + amfobjpropObjProps.PropertyName.iLength >= output.Length)
                        return null;

                    output[index++] = (byte)(amfobjpropObjProps.PropertyName.iLength >> 8);
                    output[index++] = (byte)(amfobjpropObjProps.PropertyName.iLength & 0xff);
                    for (int i = 0; i < amfobjpropObjProps.PropertyName.iLength; i++)
                        output[index++] = (byte)amfobjpropObjProps.PropertyName.arr_chValue[i];
                }

                switch (amfobjpropObjProps.PropertyType)
                {
                    case AMFDataType.Number:
                        byte[] numberEncoded = AMF_EncodeNumber(amfobjpropObjProps.PropertyValue.iPropertyNumber);
                        if (numberEncoded == null || index + numberEncoded.Length > output.Length)
                            return null;
                        Array.Copy(ByteCastChar(numberEncoded), 0, output, index, numberEncoded.Length);
                        index += numberEncoded.Length;
                        break;

                    case AMFDataType.Boolean:
                        byte[] booleanEncoded = AMF_EncodeBoolean((int)amfobjpropObjProps.PropertyValue.iPropertyNumber);
                        if (booleanEncoded == null || index + booleanEncoded.Length > output.Length)
                            return null;
                        Array.Copy(ByteCastChar(booleanEncoded), 0, output, index, booleanEncoded.Length);
                        index += booleanEncoded.Length;
                        break;

                    case AMFDataType.String:
                    case AMFDataType.Long_String:
                        byte[] stringEncoded = AMF_EncodeString(amfobjpropObjProps.PropertyValue.avValue);
                        if (stringEncoded == null || index + stringEncoded.Length > output.Length)
                            return null;
                        Array.Copy(ByteCastChar(stringEncoded), 0, output, index, stringEncoded.Length);
                        index += stringEncoded.Length;
                        break;

                    case AMFDataType.Null:
                        if (index + 1 >= output.Length)
                            return null;
                        output[index++] = (byte)AMFDataType.Null;
                        break;

                    case AMFDataType.Object:
                    case AMFDataType.Ecma_Array:
                    case AMFDataType.Strict_Array:
                        byte[] objectEncoded = AMF_Encode(amfobjpropObjProps.PropertyValue.objPropertyObject);
                        if (objectEncoded == null || index + objectEncoded.Length > output.Length)
                            return null;
                        Array.Copy(ByteCastChar(objectEncoded), 0, output, index, objectEncoded.Length);
                        index += objectEncoded.Length;
                        break;

                    default:
                        return null;
                }

                return output;
            }

        }

        #region Chars
        public static char[] AMF_EncodeString(AVal avString)
        {
            return ByteCastChar(Byted.AMF_EncodeString(avString));
        }
        public static char[] AMF_EncodeNumber(double iValue)
        {
            return ByteCastChar(Byted.AMF_EncodeNumber(iValue));
        }
        public static char[] AMF_EncodeInt16(short iValue)
        {
            return ByteCastChar(Byted.AMF_EncodeInt16(iValue));
        }
        public static char[] AMF_EncodeInt24(int iValue)
        {
            return ByteCastChar(Byted.AMF_EncodeInt24(iValue));
        }
        public static char[] AMF_EncodeInt32(int iValue)
        {
            return ByteCastChar(Byted.AMF_EncodeInt32(iValue));
        }
        public static char[] AMF_EncodeBoolean(int bValue)
        {
            return ByteCastChar(Byted.AMF_EncodeBoolean(bValue));
        }

        public static char[] AMF_EncodeNamedString(AVal avName, AVal avValue)
        {
            return ByteCastChar(Byted.AMF_EncodeNamedString(avName, avValue));
        }
        public static char[] AMF_EncodeNamedNumber(AVal avName, double dValue)
        {
            return ByteCastChar(Byted.AMF_EncodeNamedNumber(avName, dValue));
        }
        public static char[] AMF_EncodeNamedBoolean(AVal avName, int bValue)
        {
            return ByteCastChar(Byted.AMF_EncodeNamedBoolean(avName, bValue));
        }

        public static char[] AMF_Encode(AMFObject amfobjObject)
        {
            return ByteCastChar(Byted.AMF_Encode(amfobjObject));
        }
        public static char[] AMF_EncodeEcmaArray(AMFObject amfobjObject)
        {
            return ByteCastChar(Byted.AMF_EncodeEcmaArray(amfobjObject));
        }
        public static char[] AMF_EncodeArray(AMFObject amfobjObject)
        {
            return ByteCastChar(Byted.AMF_EncodeArray(amfobjObject));
        }

        public static char[] AMFProp_Encode(AMFObjectProperty amfobjpropObjProps)
        {
            return ByteCastChar(Byted.AMFProp_Encode(amfobjpropObjProps));
        }
        #endregion

        #endregion

        #region Decode
        public static AVal AMF_DecodeString(byte[] arr_charData)
        {
            AVal avVal = new AVal { iLength = AMF_DecodeInt16(arr_charData) };
            avVal.arr_chValue = (avVal.iLength > 0) ? arr_charData.Skip(2).ToArray() : c_avEmpty.arr_chValue;

            return avVal;
        }
        public static AVal AMF_DecodeLongString(byte[] arr_charData)
        {
            AVal avVal = new AVal();
            avVal.iLength = (int)AMF_DecodeInt32(arr_charData);
            avVal.arr_chValue = (avVal.iLength > 0) ? arr_charData.Skip(4).ToArray() : c_avEmpty.arr_chValue;

            return avVal;
        }
        public static double AMF_DecodeNumber(byte[] arr_charData)
        {
            byte[] bytes = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                bytes[i] = arr_charData[i + 1];
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToDouble(bytes, 0);
        }
        public static ushort AMF_DecodeInt16(byte[] arr_charData)
        {
            return (ushort)((ushort)(arr_charData[0] << 8) | (ushort)(arr_charData[1]));
        }
        public static uint AMF_DecodeInt24(byte[] arr_charData)
        {
            return (uint)((arr_charData[0] << 16) | (arr_charData[1] << 8) | arr_charData[2]);
        }
        public static uint AMF_DecodeInt32(byte[] arr_charData)
        {
            return (uint)((arr_charData[0] << 24) | (arr_charData[1] << 16) | (arr_charData[2] << 8) | arr_charData[3]);
        }
        public static int AMF_DecodeBoolean(byte[] arr_charData)
        {
            return arr_charData[1];
        }

        public struct AMF_Decode_Return_t
        {
            public int iSize;
            public AMFObject amfobjObject;
        }
        public static AMF_Decode_Return_t AMF_Decode(byte[] arr_byteBuffer, int nSize, int bDecodeName)
        {
            AMF_Decode_Return_t return_T = new AMF_Decode_Return_t { iSize = 0, amfobjObject = c_amfobjInvalid };
            int nOriginalSize = nSize;
            int bError = AFALSE;     /* if there is an error while decoding - try to at least find the end mark AMF_OBJECT_END */

            while (nSize > 0)
            {
                AMFObjectProperty prop;
                int nRes;

                if (nSize >= 3 && AMF_DecodeInt24(arr_byteBuffer.ToArray()) == (uint)AMFDataType.Object_End)
                {
                    nSize -= 3;
                    bError = AFALSE;
                    break;
                }

                if (ToBoolean(bError))
                {
                    // RTMP_Log(RTMP_LOGERROR, "DECODING ERROR, IGNORING BYTES UNTIL NEXT KNOWN PATTERN!");
                    nSize--;
                    arr_byteBuffer = arr_byteBuffer.Skip(1).ToArray();
                    continue;
                }

                AMFProp_Decode_Return_t depropretRet_t = AMFProp_Decode(arr_byteBuffer.ToArray(), nSize, bDecodeName);
                nRes = depropretRet_t.iSize;
                prop = depropretRet_t.amfobjpropObject;

                if (nRes == -1)
                {
                    bError = ATRUE;
                    break;
                }
                else
                {
                    nSize -= nRes;
                    if (nSize < 0)
                    {
                        bError = ATRUE;
                        break;
                    }
                    arr_byteBuffer = arr_byteBuffer.Skip(nRes).ToArray();
                    return_T.amfobjObject = AMF_AddProp(return_T.amfobjObject, prop);
                }
            }

            if (ToBoolean(bError))
            {
                return_T.iSize = -1;
                return return_T;
            }

            return_T.iSize = nOriginalSize - nSize;
            return return_T;
        }

        public struct AMF3_Decode_Return_t
        {
            public int iSize;
            public AMFObject amfobjObject;
        }
        public static AMF3_Decode_Return_t AMF3_Decode(byte[] pBuffer, int nSize, int bDecodeName)
        {
            AMF3_Decode_Return_t return_T = new AMF3_Decode_Return_t { iSize = 0, amfobjObject = c_amfobjInvalid };

            int nOriginalSize = nSize;
            int nRef;
            int len;

            if (ToBoolean(bDecodeName))
            {
                if (pBuffer[0] != (byte)AMF3DataType.Object)
                    // RTMP_Log(RTMP_LOGERROR, "AMF3 Object encapsulated in AMF stream does not start with AMF3_OBJECT!");
                    pBuffer = pBuffer.Skip(1).ToArray();
                nSize--;
            }

            AMF3ReadIntegerReturn_t amf3riReturn_t = AMF3ReadInteger(pBuffer);
            nRef = amf3riReturn_t.iValue;
            len = amf3riReturn_t.iLength;

            pBuffer = pBuffer.Skip(len).ToArray();
            nSize -= len;

            if ((nRef & 1) == 0)
            {               /* object reference, 0xxx */
                uint objectIndex = ((uint)(nRef >> 1));

                // RTMP_Log(RTMP_LOGDEBUG, "Object reference, index: %d", objectIndex);
            }
            else                /* object instance */
            {
                int classRef = (nRef >> 1);

                AMF3ClassDef cd = new AMF3ClassDef { ClassDefName = c_avEmpty };

                AMFObjectProperty prop;

                if ((classRef & 0x1) == 0)
                {           /* class reference */
                    uint classIndex = ((uint)(classRef >> 1));
                    // RTMP_Log(RTMP_LOGDEBUG, "Class reference: %d", classIndex);
                }
                else
                {
                    int classExtRef = (classRef >> 1);
                    int i, cdnum;

                    cd.ClassDefExternalizable = (byte)((classExtRef & 0x1) == ATRUE ? 0x01 : 0x00);
                    cd.ClassDefDynamic = (byte)(((classExtRef >> 1) & 0x1) == ATRUE ? 0x01 : 0x00);

                    cdnum = classExtRef >> 2;

                    /* class name */

                    AMF3ReadStringReturn_t amf3rsReturn_t = AMF3ReadString(pBuffer);
                    cd.ClassDefName = amf3rsReturn_t.avStr;
                    len = amf3rsReturn_t.iLength;

                    nSize -= len;
                    pBuffer = pBuffer.Skip(len).ToArray();

                    /*std::string str = className; */

                    //RTMP_Log(RTMP_LOGDEBUG, "Class name: %s, externalizable: %d, dynamic: %d, classMembers: %d", cd.cd_name.av_val, cd.cd_externalizable, cd.cd_dynamic, cd.cd_num);

                    for (i = 0; i < cdnum; i++)
                    {
                        AVal memberName;
                        if (nSize <= 0)
                        {
                            goto invalid;
                        }

                        AMF3ReadStringReturn_t amf3rsReturn_t1 = AMF3ReadString(pBuffer);
                        memberName = amf3rsReturn_t1.avStr;
                        len = amf3rsReturn_t1.iLength;
                        // RTMP_Log(RTMP_LOGDEBUG, "Member: %s", memberName.av_val);

                        cd = AMF3CD_AddProp(cd, memberName);

                        nSize -= len;
                        pBuffer = pBuffer.Skip(len).ToArray();
                    }
                }

                /* add as referencable object */

                if (ToBoolean(cd.ClassDefExternalizable))
                {
                    int nRes;
                    AVal name = AVal.AVC("DEFAULT_ATTRIBUTE");

                    // RTMP_Log(RTMP_LOGDEBUG, "Externalizable, TODO check");

                    AMF3Prop_Decode_Return_t deamf3propReturn_t = AMF3Prop_Decode(pBuffer, nSize, AFALSE);
                    prop = deamf3propReturn_t.amfobjpropObjProps;
                    nRes = deamf3propReturn_t.iSize;

                    if (nRes == -1)
                    {
                        //RTMP_Log(RTMP_LOGDEBUG, "%s, failed to decode AMF3 property!", __FUNCTION__);
                    }
                    else
                    {
                        nSize -= nRes;
                        pBuffer = pBuffer.Skip(nRes).ToArray();
                    }

                    prop = AMFProp_SetName(prop, name);
                    return_T.amfobjObject = AMF_AddProp(return_T.amfobjObject, prop);
                }
                else
                {
                    int nRes, i;
                    for (i = 0; i < cd.ClassDefNum; i++) /* non-dynamic */
                    {
                        if (nSize <= 0)
                            goto invalid;

                        AMF3Prop_Decode_Return_t deamf3propReturn_t = AMF3Prop_Decode(pBuffer, nSize, AFALSE);
                        prop = deamf3propReturn_t.amfobjpropObjProps;
                        nRes = deamf3propReturn_t.iSize;

                        if (nRes == -1)
                        {
                            //RTMP_Log(RTMP_LOGDEBUG, "%s, failed to decode AMF3 property!", __FUNCTION__);
                        }

                        prop = AMFProp_SetName(prop, AMF3CD_GetProp(cd, i));
                        return_T.amfobjObject = AMF_AddProp(return_T.amfobjObject, prop);

                        pBuffer = pBuffer.Skip(nRes).ToArray();
                        nSize -= nRes;
                    }
                    if (ToBoolean(cd.ClassDefDynamic))
                    {
                        int len1 = 0;
                        do
                        {
                            if (nSize <= 0)
                                goto invalid;

                            AMF3Prop_Decode_Return_t deamf3propReturn_t = AMF3Prop_Decode(pBuffer, nSize, ATRUE);
                            prop = deamf3propReturn_t.amfobjpropObjProps;
                            nRes = deamf3propReturn_t.iSize;

                            AMF_AddProp(return_T.amfobjObject, prop);

                            pBuffer = pBuffer.Skip(nRes).ToArray();
                            nSize -= nRes;

                            len1 = prop.PropertyName.iLength;
                        } while (len1 > 0);
                    }
                }
                // RTMP_Log(RTMP_LOGDEBUG, "class object!");
            }

            return_T.iSize = nOriginalSize - nSize;
            return return_T;

        invalid:
            // RTMP_Log(RTMP_LOGDEBUG, "%s, invalid class encoding!", __FUNCTION__);
            return_T.iSize = nOriginalSize;
            return return_T;
        }

        public struct AMF_DecodeArray_Return_t
        {
            public int iSize;
            public AMFObject amfobjObject;
        }
        public static AMF_DecodeArray_Return_t AMF_DecodeArray(byte[] pBuffer, int nSize, int nArrayLen, int bDecodeName)
        {
            AMF_DecodeArray_Return_t return_T = new AMF_DecodeArray_Return_t { iSize = 0, amfobjObject = c_amfobjInvalid };

            int nOriginalSize = nSize;
            int bError = AFALSE;

            while (nArrayLen > 0)
            {
                AMFObjectProperty prop;
                int nRes;
                nArrayLen--;

                if (nSize <= 0)
                {
                    bError = ATRUE;
                    break;
                }
                AMFProp_Decode_Return_t deamfpropReturn_t = AMFProp_Decode(pBuffer, nSize, bDecodeName);
                prop = deamfpropReturn_t.amfobjpropObject;
                nRes = deamfpropReturn_t.iSize;
                if (nRes == -1)
                {
                    bError = ATRUE;
                    break;
                }
                else
                {
                    nSize -= nRes;
                    pBuffer = pBuffer.Skip(nRes).ToArray();
                    AMF_AddProp(return_T.amfobjObject, prop);
                }
            }
            if (ToBoolean(bError))
            {
                return_T.iSize = -1;

                return return_T;
            }

            return_T.iSize = nOriginalSize - nSize;
            return return_T;
        }

        public struct AMFProp_Decode_Return_t
        {
            public int iSize;
            public AMFObjectProperty amfobjpropObject;
        }
        public static AMFProp_Decode_Return_t AMFProp_Decode(byte[] arr_byteBuffer, int nSize, int bDecodeName)
        {
            AMFProp_Decode_Return_t return_T = new AMFProp_Decode_Return_t { iSize = 0, amfobjpropObject = c_amfobjpropInvalid };

            int nOriginalSize = nSize;
            int nRes;

            if (ToBoolean(bDecodeName) && nSize < 4)
            {               /* at least name (length + at least 1 byte) and 1 byte of data */
                // RTMP_Log(RTMP_LOGDEBUG, "%s: Not enough data for decoding with name, less than 4 bytes!", __FUNCTION__);
                goto return_n1;
            }

            if (ToBoolean(bDecodeName))
            {
                ushort nNameSize = AMF_DecodeInt16(arr_byteBuffer);
                if (nNameSize > nSize - 2)
                {
                    // RTMP_Log(RTMP_LOGDEBUG, "%s: Name size out of range: namesize (%d) > len (%d) - 2", __FUNCTION__, nNameSize, nSize);
                    goto return_n1;
                }

                return_T.amfobjpropObject.PropertyName = AMF_DecodeString(arr_byteBuffer);
                nSize -= 2 + nNameSize;
                arr_byteBuffer = arr_byteBuffer.Skip(2 + nNameSize).ToArray();
            }

            if (nSize == 0)
            {
                goto return_n1;
            }

            nSize--;

            return_T.amfobjpropObject.PropertyType = (AMFDataType)arr_byteBuffer[0];
            arr_byteBuffer = arr_byteBuffer.Skip(1).ToArray();

            switch (return_T.amfobjpropObject.PropertyType)
            {
                case AMFDataType.Number:
                    if (nSize < 8)
                        goto return_n1;
                    return_T.amfobjpropObject.PropertyValue.iPropertyNumber = AMF_DecodeNumber(arr_byteBuffer);
                    nSize -= 8;
                    break;
                case AMFDataType.Boolean:
                    if (nSize < 1)
                        goto return_n1;
                    return_T.amfobjpropObject.PropertyValue.iPropertyNumber = (double)AMF_DecodeBoolean(arr_byteBuffer);
                    nSize--;
                    break;
                case AMFDataType.String:
                    {
                        ushort nStringSize = AMF_DecodeInt16(arr_byteBuffer);

                        if (nSize < (long)nStringSize + 2)
                            goto return_n1;
                        return_T.amfobjpropObject.PropertyValue.avValue = AMF_DecodeString(arr_byteBuffer);
                        nSize -= (2 + nStringSize);
                        break;
                    }
                case AMFDataType.Object:
                    {
                        AMF_Decode_Return_t deamfRet_t = AMF_Decode(arr_byteBuffer, nSize, ATRUE);
                        nRes = deamfRet_t.iSize;
                        return_T.amfobjpropObject.PropertyValue.objPropertyObject = deamfRet_t.amfobjObject;


                        if (nRes == -1)
                            goto return_n1;
                        nSize -= nRes;
                        break;
                    }
                case AMFDataType.MovieClip:
                    {
                        // RTMP_Log(RTMP_LOGERROR, "AMF_MOVIECLIP reserved!");
                        goto return_n1;
                    }
                case AMFDataType.Null:
                case AMFDataType.Undefined:
                case AMFDataType.Unsupported:
                    return_T.amfobjpropObject.PropertyType = AMFDataType.Null;
                    break;
                case AMFDataType.Reference:
                    {
                        // RTMP_Log(RTMP_LOGERROR, "AMF_REFERENCE not supported!");
                        goto return_n1;
                    }
                case AMFDataType.Ecma_Array:
                    {
                        nSize -= 4;

                        /* next comes the rest, mixed array has a final 0x000009 mark and names, so its an object */
                        AMF_Decode_Return_t deamfRet_t = AMF_Decode(arr_byteBuffer.Skip(4).ToArray(), nSize, ATRUE);
                        nRes = deamfRet_t.iSize;
                        return_T.amfobjpropObject.PropertyValue.objPropertyObject = deamfRet_t.amfobjObject;

                        if (nRes == -1)
                            goto return_n1;
                        nSize -= nRes;
                        break;
                    }
                case AMFDataType.Object_End:
                    {
                        goto return_n1;
                    }
                case AMFDataType.Strict_Array:
                    {
                        uint nArrayLen = AMF_DecodeInt32(arr_byteBuffer);
                        nSize -= 4;

                        AMF_DecodeArray_Return_t deamfarrRet_t = AMF_DecodeArray(arr_byteBuffer.Skip(4).ToArray(), nSize, (int)nArrayLen, AFALSE);
                        nRes = deamfarrRet_t.iSize;
                        return_T.amfobjpropObject.PropertyValue.objPropertyObject = deamfarrRet_t.amfobjObject;

                        if (nRes == -1)
                            goto return_n1;
                        nSize -= nRes;
                        break;
                    }
                case AMFDataType.Date:
                    {
                        // RTMP_Log(RTMP_LOGDEBUG, "AMF_DATE");

                        if (nSize < 10)
                            goto return_n1;

                        return_T.amfobjpropObject.PropertyValue.iPropertyNumber = AMF_DecodeNumber(arr_byteBuffer);
                        return_T.amfobjpropObject.iPropertyUTCOffset = (short)AMF_DecodeInt16(arr_byteBuffer.Skip(8).ToArray());

                        nSize -= 10;
                        break;
                    }
                case AMFDataType.Long_String:
                case AMFDataType.Xml_Doc:
                    {
                        uint nStringSize = AMF_DecodeInt32(arr_byteBuffer);
                        if (nSize < (long)nStringSize + 4)
                            goto return_n1;
                        return_T.amfobjpropObject.PropertyValue.avValue = AMF_DecodeLongString(arr_byteBuffer);
                        nSize -= (4 + (int)nStringSize);
                        if (return_T.amfobjpropObject.PropertyType == AMFDataType.Long_String)
                            return_T.amfobjpropObject.PropertyType = AMFDataType.String;
                        break;
                    }
                case AMFDataType.Recordset:
                    {
                        // RTMP_Log(RTMP_LOGERROR, "AMF_RECORDSET reserved!");
                        goto return_n1;
                    }
                case AMFDataType.Typed_Object:
                    {
                        // RTMP_Log(RTMP_LOGERROR, "AMF_TYPED_OBJECT not supported!");
                        goto return_n1;
                    }
                case AMFDataType.Avmplus:
                    {
                        AMF3_Decode_Return_t deamf3_Return_t = AMF3_Decode(arr_byteBuffer, nSize, ATRUE);
                        nRes = deamf3_Return_t.iSize;
                        return_T.amfobjpropObject.PropertyValue.objPropertyObject = deamf3_Return_t.amfobjObject;


                        if (nRes == -1)
                            goto return_n1;
                        nSize -= nRes;
                        return_T.amfobjpropObject.PropertyType = AMFDataType.Object;
                        break;
                    }
                default:
                    // RTMP_Log(RTMP_LOGDEBUG, "%s - unknown datatype 0x%02x, @%p", __FUNCTION__, prop->p_type, arr_byteBuffer - 1);
                    goto return_n1;
            }

            return_T.iSize = nOriginalSize - nSize;
            return return_T;

        return_n1:
            return_T.iSize = -1;
            return return_T;

        }

        public struct AMF3Prop_Decode_Return_t
        {
            public int iSize;
            public AMFObjectProperty amfobjpropObjProps;
        }
        public static AMF3Prop_Decode_Return_t AMF3Prop_Decode(byte[] pBuffer, int nSize, int bDecodeName)
        {
            AMF3Prop_Decode_Return_t return_T = new AMF3Prop_Decode_Return_t { iSize = 0, amfobjpropObjProps = c_amfobjpropInvalid };

            int nOriginalSize = nSize;
            AMF3DataType type;

            /* decode name */
            if (ToBoolean(bDecodeName))
            {
                AMF3ReadStringReturn_t amf3rdReturn_t = AMF3ReadString(pBuffer);
                AVal name = amf3rdReturn_t.avStr;
                int nRes = amf3rdReturn_t.iLength;

                if (name.arr_chValue.Length <= 0)
                {
                    return_T.iSize = nRes;
                    return return_T;
                }

                nSize -= nRes;

                if (nSize <= 0)
                    goto return_n1;
                return_T.amfobjpropObjProps.PropertyName = name;
                pBuffer = pBuffer.Skip(nRes).ToArray();
            }

            /* decode */
            type = (AMF3DataType)pBuffer[0];
            pBuffer = pBuffer.Skip(1).ToArray();

            nSize--;

            switch (type)
            {
                case AMF3DataType.Undefined:
                case AMF3DataType.Null:
                    return_T.amfobjpropObjProps.PropertyType = AMFDataType.Null;
                    break;
                case AMF3DataType.False:
                    return_T.amfobjpropObjProps.PropertyType = AMFDataType.Boolean;
                    return_T.amfobjpropObjProps.PropertyValue.iPropertyNumber = 0.0;
                    break;
                case AMF3DataType.True:
                    return_T.amfobjpropObjProps.PropertyType = AMFDataType.Boolean;
                    return_T.amfobjpropObjProps.PropertyValue.iPropertyNumber = 1.0;
                    break;
                case AMF3DataType.Integer:
                    {
                        AMF3ReadIntegerReturn_t amf3riReturn_t = AMF3ReadInteger(pBuffer);
                        int res = amf3riReturn_t.iValue;
                        int len = amf3riReturn_t.iLength;
                        return_T.amfobjpropObjProps.PropertyValue.iPropertyNumber = (double)res;
                        return_T.amfobjpropObjProps.PropertyType = AMFDataType.Number;
                        nSize -= len;
                        break;
                    }
                case AMF3DataType.Double:
                    if (nSize < 8)
                        goto return_n1;
                    return_T.amfobjpropObjProps.PropertyValue.iPropertyNumber = AMF_DecodeNumber(pBuffer);
                    return_T.amfobjpropObjProps.PropertyType = AMFDataType.Number;
                    nSize -= 8;
                    break;
                case AMF3DataType.String:
                case AMF3DataType.Xml_Doc:
                case AMF3DataType.Xml:
                    {
                        AMF3ReadStringReturn_t amf3rsReturn_t = AMF3ReadString(pBuffer);
                        return_T.amfobjpropObjProps.PropertyValue.avValue = amf3rsReturn_t.avStr;
                        int len = amf3rsReturn_t.iLength;

                        return_T.amfobjpropObjProps.PropertyType = AMFDataType.String;
                        nSize -= len;
                        break;
                    }
                case AMF3DataType.Date:
                    {
                        AMF3ReadIntegerReturn_t amf3riReturn_t = AMF3ReadInteger(pBuffer);

                        int res = amf3riReturn_t.iValue;
                        int len = amf3riReturn_t.iLength;

                        nSize -= len;
                        pBuffer = pBuffer.Skip(len).ToArray();

                        if ((res & 0x1) == 0)
                        {           /* reference */
                            uint nIndex = ((uint)(res >> 1));
                            // RTMP_Log(RTMP_LOGDEBUG, "AMF3_DATE reference: %d, not supported!", nIndex);
                        }
                        else
                        {
                            if (nSize < 8)
                                goto return_n1;

                            return_T.amfobjpropObjProps.PropertyValue.iPropertyNumber = AMF_DecodeNumber(pBuffer);
                            nSize -= 8;
                            return_T.amfobjpropObjProps.PropertyType = AMFDataType.Number;
                        }
                        break;
                    }
                case AMF3DataType.Object:
                    {
                        AMF3_Decode_Return_t deamf3Return_t = AMF3_Decode(pBuffer, nSize, ATRUE);
                        return_T.amfobjpropObjProps.PropertyValue.objPropertyObject = deamf3Return_t.amfobjObject;
                        int nRes = deamf3Return_t.iSize;

                        if (nRes == -1)
                            goto return_n1;
                        nSize -= nRes;
                        return_T.amfobjpropObjProps.PropertyType = AMFDataType.Object;
                        break;
                    }
                case AMF3DataType.Array:
                case AMF3DataType.Byte_Array:
                default:
                    // RTMP_Log(RTMP_LOGDEBUG, "%s - AMF3 unknown/unsupported datatype 0x%02x, @%p", __FUNCTION__, (unsigned char)(*pBuffer), pBuffer);
                    goto return_n1;
            }
            if (nSize < 0)
                goto return_n1;

            return_T.iSize = nOriginalSize - nSize;
            return return_T;

        return_n1:
            return_T.iSize = -1;
            return return_T;
        }
        #endregion

        #region AMF3 Reader
        public struct AMF3ReadIntegerReturn_t
        {
            public int iLength;
            public int iValue;
        }
        public static AMF3ReadIntegerReturn_t AMF3ReadInteger(byte[] arr_byteData)
        {
            AMF3ReadIntegerReturn_t return_t = new AMF3ReadIntegerReturn_t { iLength = 0, iValue = 0 };

            int i = 0;
            int val = 0;

            while (i <= 2)
            {               /* handle first 3 bytes */
                if (ToBoolean(arr_byteData[i] & 0x80))
                {           /* byte used */
                    val <<= 7;      /* shift up */
                    val |= (arr_byteData[i] & 0x7f);    /* add bits */
                    i++;
                }
                else
                {
                    break;
                }
            }

            if (i > 2)
            {               /* use 4th byte, all 8bits */
                val <<= 8;
                val |= arr_byteData[3];

                /* range check */
                if (val > AMF3_INTEGER_MAX)
                    val -= (1 << 29);
            }
            else
            {               /* use 7bits of last unparsed byte (0xxxxxxx) */
                val <<= 7;
                val |= arr_byteData[i];
            }

            return_t.iValue = val;
            return_t.iLength = i > 2 ? 4 : i + 1;
            return return_t; ;
        }
        public struct AMF3ReadStringReturn_t
        {
            public int iLength;
            public AVal avStr;
        }
        public static AMF3ReadStringReturn_t AMF3ReadString(byte[] data)
        {
            AMF3ReadStringReturn_t return_t = new AMF3ReadStringReturn_t { iLength = 0, avStr = c_avEmpty };

            int nRef = 0;
            int len;

            AMF3ReadIntegerReturn_t amf3riReturn_t = AMF3ReadInteger(data);
            len = amf3riReturn_t.iLength;
            nRef = amf3riReturn_t.iValue;
            data = data.Skip(len).ToArray();

            if ((nRef & 0x1) == 0)
            {    /* reference: 0xxx */
                uint refIndex = ((uint)(nRef >> 1));
                // RTMP_Log(RTMP_LOGDEBUG, "%s, string reference, index: %d, not supported, ignoring!", __FUNCTION__, refIndex);
                return_t.avStr = c_avEmpty;
                return_t.iLength = len;
            }
            else
            {
                uint nSize = ((uint)(nRef >> 1));

                return_t.avStr.arr_chValue = data;
                return_t.avStr.iLength = (int)nSize;
                return_t.iLength = (int)(len + nSize);
            }

            return return_t;
        }
        #endregion

        #region AMF Object
        public static AMFObject AMF_Dump(AMFObject amfobjObject)
        {
            int n;
            // RTMP_Log(RTMP_LOGDEBUG, "(object begin)");
            for (n = 0; n < amfobjObject.iObjectNum; n++)
            {
                AMFProp_Dump(amfobjObject.arr_amfobjpropObjProps[n]);
            }
            // RTMP_Log(RTMP_LOGDEBUG, "(object end)");

            return amfobjObject;
        }
        public static AMFObject AMF_Reset(AMFObject amfobjObject)
        {
            int n;
            for (n = 0; n < amfobjObject.iObjectNum; n++)
            {
                AMFProp_Reset(amfobjObject.arr_amfobjpropObjProps[n]);
            }
            amfobjObject = c_amfobjInvalid;

            return amfobjObject;
        }

        public static AMFObject AMF_AddProp(AMFObject amfobjObject, AMFObjectProperty amfobjpropObjProps)
        {
            amfobjObject.arr_amfobjpropObjProps[amfobjObject.iObjectNum++] = amfobjpropObjProps;

            return amfobjObject;
        }
        public static int AMF_CountProp(AMFObject amfobjObject)
        {
            return amfobjObject.iObjectNum;
        }
        public static AMFObjectProperty AMF_GetProp(AMFObject amfobjObject, AVal name, int iIndex)
        {
            if (iIndex >= 0)
            {
                if (iIndex < amfobjObject.iObjectNum)
                    return amfobjObject.arr_amfobjpropObjProps[iIndex];
            }
            else
            {
                for (int n = 0; n < amfobjObject.iObjectNum; n++)
                {
                    if (AVal.AVMATCH(amfobjObject.arr_amfobjpropObjProps[n].PropertyName, name))
                        return amfobjObject.arr_amfobjpropObjProps[n];
                }
            }

            return c_amfobjpropInvalid;
        }
        #endregion


        #region Prop

        #region Prop Set
        public static AMFObjectProperty AMFProp_SetName(AMFObjectProperty amfobjpropObjProps, AVal avName)
        {
            amfobjpropObjProps.PropertyName = avName;

            return amfobjpropObjProps;
        }
        public static AMFObjectProperty AMFProp_SetNumber(AMFObjectProperty amfobjpropObjProps, double iValue)
        {
            amfobjpropObjProps.PropertyValue.iPropertyNumber = iValue;

            return amfobjpropObjProps;
        }
        public static AMFObjectProperty AMFProp_SetBoolean(AMFObjectProperty amfobjpropObjProps, int bFlag)
        {
            amfobjpropObjProps.PropertyValue.iPropertyNumber = bFlag;

            return amfobjpropObjProps;
        }
        public static AMFObjectProperty AMFProp_SetString(AMFObjectProperty amfobjpropObjProps, AVal avStr)
        {
            amfobjpropObjProps.PropertyType = AMFDataType.String;
            amfobjpropObjProps.PropertyValue.avValue = avStr;

            return amfobjpropObjProps;
        }
        public static AMFObjectProperty AMFProp_SetObject(AMFObjectProperty amfobjpropObjProps, AMFObject amfobjObject)
        {
            amfobjpropObjProps.PropertyType = AMFDataType.Object;
            amfobjpropObjProps.PropertyValue.objPropertyObject = amfobjObject;

            return amfobjpropObjProps;
        }

        public static AMFObjectProperty AMFProp_Dump(AMFObjectProperty amfobjpropObjProps)
        {
            byte[] strRes = new byte[256];
            byte[] str = new byte[256];
            AVal avName;

            if (amfobjpropObjProps.PropertyType == AMFDataType.Invalid)
            {
                // INVALID
                return amfobjpropObjProps;
            }

            if (amfobjpropObjProps.PropertyType == AMFDataType.Null)
            {
                // NULL
                return amfobjpropObjProps;
            }

            if (amfobjpropObjProps.PropertyName.iLength != 0)
            {
                avName = amfobjpropObjProps.PropertyName;
            }
            else
            {
                avName = AVal.AVC("no-name.");
            }
            if (avName.iLength > 18)
                avName.iLength = 18;

            byte[] spBuilder(int iCapacity, string strFormat, params object?[] args)
            {
                StringBuilder strBuilder = new StringBuilder(iCapacity);
                strBuilder.AppendFormat(strFormat, args);
                return Encoding.UTF8.GetBytes(strBuilder.ToString());
            }
            strRes = spBuilder(255, "Name: {0,18}, ", avName.arr_chValue.Take(Math.Min(avName.iLength, 255 - 18)));

            if (amfobjpropObjProps.PropertyType == AMFDataType.Object)
            {
                //RTMP_Log(RTMP_LOGDEBUG, "Property: <%sOBJECT>", strRes);
                AMF_Dump(amfobjpropObjProps.PropertyValue.objPropertyObject);
                return amfobjpropObjProps;
            }
            else if (amfobjpropObjProps.PropertyType == AMFDataType.Ecma_Array)
            {
                //RTMP_Log(RTMP_LOGDEBUG, "Property: <%sECMA_ARRAY>", strRes);
                AMF_Dump(amfobjpropObjProps.PropertyValue.objPropertyObject);
                return amfobjpropObjProps;
            }
            else if (amfobjpropObjProps.PropertyType == AMFDataType.Strict_Array)
            {
                //RTMP_Log(RTMP_LOGDEBUG, "Property: <%sSTRICT_ARRAY>", strRes);
                AMF_Dump(amfobjpropObjProps.PropertyValue.objPropertyObject);
                return amfobjpropObjProps;
            }

            switch (amfobjpropObjProps.PropertyType)
            {
                case AMFDataType.Number:
                    str = spBuilder(255, "NUMBER:\t{0:F2}", amfobjpropObjProps.PropertyValue.iPropertyNumber);
                    break;
                case AMFDataType.Boolean:
                    str = spBuilder(255, "BOOLEAN:\t{0}", amfobjpropObjProps.PropertyValue.iPropertyNumber != 0.0 ? "TRUE" : "FALSE");
                    break;
                case AMFDataType.String:
                    str = spBuilder(255, "STRING:\t{0}", Encoding.UTF8.GetString(amfobjpropObjProps.PropertyValue.avValue.arr_chValue, 0, Math.Min(amfobjpropObjProps.PropertyValue.avValue.iLength, 255)));
                    break;
                case AMFDataType.Date:
                    str = spBuilder(255, "DATE:\ttimestamp: {0:F2}, UTC offset: {1}", amfobjpropObjProps.PropertyValue.iPropertyNumber, amfobjpropObjProps.iPropertyUTCOffset);
                    break;
                default:
                    str = spBuilder(255, "INVALID TYPE 0x{0:X2}", (byte)amfobjpropObjProps.PropertyType);
                    break;
            }

            //RTMP_Log(RTMP_LOGDEBUG, "Property: <%s%s>", strRes, str);

            return amfobjpropObjProps;
        }
        public static AMFObjectProperty AMFProp_Reset(AMFObjectProperty amfobjpropObjProps)
        {
            if (amfobjpropObjProps.PropertyType == AMFDataType.Object || amfobjpropObjProps.PropertyType == AMFDataType.Ecma_Array || amfobjpropObjProps.PropertyType == AMFDataType.Strict_Array)
                AMF_Reset(amfobjpropObjProps.PropertyValue.objPropertyObject);
            else
            {
                amfobjpropObjProps.PropertyValue.avValue = c_avEmpty;
            }
            amfobjpropObjProps.PropertyType = AMFDataType.Invalid;

            return amfobjpropObjProps;
        }
        #endregion

        #region Prop Get
        public static AMFDataType AMFProp_GetType(AMFObjectProperty amfobjpropObjProps)
        {
            return amfobjpropObjProps.PropertyType;
        }
        public static AVal AMFProp_GetName(AMFObjectProperty amfobjpropObjProps)
        {
            return amfobjpropObjProps.PropertyName;
        }
        public static double AMFProp_GetNumber(AMFObjectProperty amfobjpropObjProps)
        {
            return amfobjpropObjProps.PropertyValue.iPropertyNumber;
        }
        public static int AMFProp_GetBoolean(AMFObjectProperty amfobjpropObjProps)
        {
            return ToInteger(amfobjpropObjProps.PropertyValue.iPropertyNumber != 0);
        }
        public static AVal AMFProp_GetString(AMFObjectProperty amfobjpropObjProps)
        {
            return amfobjpropObjProps.PropertyType == AMFDataType.String ? amfobjpropObjProps.PropertyValue.avValue : c_avEmpty;
        }
        public static AMFObject AMFProp_GetObject(AMFObjectProperty amfobjpropObjProps)
        {
            return amfobjpropObjProps.PropertyType == AMFDataType.Object ? amfobjpropObjProps.PropertyValue.objPropertyObject : c_amfobjInvalid;
        }

        public static int AMFProp_IsValid(AMFObjectProperty amfobjpropObjProps)
        {
            return ToInteger(amfobjpropObjProps.PropertyType != AMFDataType.Invalid);
        }
        #endregion

        #endregion



        #region AMF3CD
        public static AMF3ClassDef AMF3CD_AddProp(AMF3ClassDef amf3cdClassDef, AVal amfobjpropObjProps)
        {
            amf3cdClassDef.ClassDefProps[amf3cdClassDef.ClassDefNum++] = amfobjpropObjProps;
            return amf3cdClassDef;
        }
        public static AVal AMF3CD_GetProp(AMF3ClassDef amf3cdClassDef, int iIndex)
        {
            return iIndex >= amf3cdClassDef.ClassDefNum ? c_avEmpty : amf3cdClassDef.ClassDefProps[iIndex];
        }
        #endregion

        #endregion

        /*
        // Utils
        public enum DataType
        {
            // ======== Defs ======== | ====== Type (or Description) ======
            Number = 0x00,              // 0 double
            Boolean = 0x01,             // 1 bool
            String = 0x02,              // 2 string
            Object = 0x03,              // 3 object
            MovieClip = 0x04,           // 4 (Not available in Remoting)
            Null = 0x05,                // 5 null
            Undefined = 0x06,           // 6 (Undefined)
            ReferencedObject = 0x07,    // 7 (Object Refernced)
            EcmaArray = 0x08,          // 8 (Mixed Array)
            EndOfObject = 0x09,         // 9 
            Array = 0x0a,               // 10 (Strict Array)
            Date = 0x0b,                // 11
            LongString = 0x0c,          // 12
            Unsupportedd = 0x0d,        // 13 object
            Recordset = 0x0e,           // 14 (Remoting, server->client only)
            Xml = 0x0f,                 // 15
            TypedObject = 0x10,         // 16
            AMF3data = 0x11             // 17 (Sent by Flash player 9+)
        }
        public static Type GetType(DataType en_DataTDataType)
        {
            switch (en_DataTDataType)
            {
                default:
                    return null;

                case DataType.Number: return typeof(string);
                case DataType.Boolean: return typeof(bool);
                case DataType.String: return typeof(string);
                case DataType.Object: return typeof(object);
            }
        }

        public class Reader0
        {
            // Data
            private readonly BinaryReader reader;


            public Reader0(byte[] arr_byteBytes)
            {
                reader = new BinaryReader(new MemoryStream(arr_byteBytes));
            }

            public Reader0(Stream stream)
            {
                reader = new BinaryReader(stream);
            }

            public bool IsStreamReadable()
            {
                return reader.PeekChar() != '\0';
            }

            public object Read()
            {
                byte type = reader.ReadByte();
                switch (type)
                {
                    case (byte)DataType.Number:
                        return ReadAMFNumber();
                    case (byte)DataType.Boolean:
                        return ReadAMFBoolean();
                    case (byte)DataType.String:
                        return ReadAMFString();
                    case (byte)DataType.Object:
                        return ReadAMFObject();
                    case (byte)DataType.Null:
                        return ReadAMFNull();
                    case (byte)DataType.Undefined:
                        return ReadAMFUndefined();
                    case (byte)DataType.EcmaArray:
                        return ReadAMFEcmaArray();
                    case (byte)DataType.Array:
                        return ReadAMFStrictArray();
                    default:
                        throw new Exception("Unsupported AMF type: " + type);
                }
            }

            private double ReadAMFNumber()
            {
                byte[] bytes = reader.ReadBytes(8);
                Array.Reverse(bytes);
                return BitConverter.ToDouble(bytes, 0);
            }

            private bool ReadAMFBoolean()
            {
                return reader.ReadByte() != 0;
            }

            private string ReadAMFString()
            {
                byte[] _length = reader.ReadBytes(2);
                ushort length = 0;
                if (BitConverter.IsLittleEndian)
                {
                     length = (ushort)(_length[1] + (_length[0] << 8));
                }
                else
                {
                     length = (ushort)(_length[0] + (_length[1] << 8));
                }
                byte[] bytes = reader.ReadBytes(length);
                return Encoding.UTF8.GetString(bytes);
            }

            private Dictionary<string, object> ReadAMFObject()
            {
                var obj = new Dictionary<string, object>();
                while (true)
                {
                    string key = ReadAMFString();
                    if (key.Length == 0) // empty string marks end of object
                        break;
                    object value = Read();
                    obj.Add(key, value);
                }
                return obj;
            }

            private object ReadAMFNull()
            {
                return null;
            }

            private object ReadAMFUndefined()
            {
                return null;
            }

            private Dictionary<string, object> ReadAMFEcmaArray()
            {
                uint count = reader.ReadUInt32();
                var obj = new Dictionary<string, object>();
                for (int i = 0; i < count; i++)
                {
                    string key = ReadAMFString();
                    object value = Read();
                    obj.Add(key, value);
                }
                return obj;
            }

            private object[] ReadAMFStrictArray()
            {
                uint count = reader.ReadUInt32();
                var array = new object[count];
                for (int i = 0; i < count; i++)
                {
                    array[i] = Read();
                }
                return array;
            }
        }
        */
    }
}
