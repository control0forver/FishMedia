using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace FishMedia.Servers.RTMP
{
    public static class AMF
    {
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
                ushort length = (ushort)(_length[1] + (_length[0] << 8));
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

    }
}
