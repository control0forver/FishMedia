using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace FishMedia
{
    public class Node<T, TIndex>
    {
        public T Data { get; set; }
        public Dictionary<TIndex, Node<T, TIndex>> Next { get; set; }
        //public Node<T> Next { get; set; }

        public Node(T data)
        {
            Data = data;
            Next = null;
        }
        public Node()
        {
            Data = default(T);
            Next = null;
        }
    }
    public class FishMediaConfigItem : Dictionary<string, string>
    {
        public FishMediaConfigItem() { }
        public FishMediaConfigItem(string key, string value) { base[key] = value; }
    };
    public class FishMediaConfigNode : Node<FishMediaConfigItem, string> { };

    public class ServerConfigData
    {
        public string Id = "";

        public ServerConfigData() { }
    }

    public class WebServerConfigData : ServerConfigData
    {
        public string RootDir = "", Index = "", IpAddr = "", Port = "", IpAddr6 = "", Port6 = "", IpV6 = "";

        public WebServerConfigData() { }
    }

    public class RtmpServerConfigData : ServerConfigData
    {
        public string IpAddr = "", Port = "", IpAddr6 = "", Port6 = "", IpV6 = "";

        public RtmpServerConfigData() { }
    }

    public class FishMediaConfig
    {
        public readonly static string c_strDefaultConfig =
            "# Default Config" +Environment.NewLine+
            "[Config]"+Environment.NewLine+
            "  [Servers]"+Environment.NewLine+
            "    [Web]"+Environment.NewLine+
            "      Id = 'web1'"+Environment.NewLine+
            "      RootDir = www # Also for: RootDir = \"www\""+Environment.NewLine+
            "      Index = index.htm # Also for: Index = \'index.htm\'"+Environment.NewLine+
            ""+Environment.NewLine+
            "      # Any > IpAddress"+Environment.NewLine+
            "      # IpV6 > IpV4"+Environment.NewLine+
            "      IpV6 = true"+Environment.NewLine+
            "      #IpAddr = 127.0.0.1"+Environment.NewLine+
            "      IpAddr = Any"+Environment.NewLine+
            "      Port = 8080"+Environment.NewLine+
            "      #IpAddr6 = ::1"+Environment.NewLine+
            "      IpAddr6 = Any"+Environment.NewLine+
            "      Port6 = 8080"+Environment.NewLine+
            "    END"+Environment.NewLine+
            ""+Environment.NewLine+
            "    [Web] # Web Server No.2"+Environment.NewLine+
            "      Id = 'web2'"+Environment.NewLine+
            "      RootDir = www"+Environment.NewLine+
            "      Index = index.htm"+Environment.NewLine+
            ""+Environment.NewLine+
            "      IpAddr = Any"+Environment.NewLine+
            "      Port = 8088"+Environment.NewLine+
            "    END"+Environment.NewLine+
            ""+Environment.NewLine+
            "    [Rtmp]"+Environment.NewLine+
            "    Id = 'rtmp1'"+Environment.NewLine+
            ""+Environment.NewLine+
            "      IpAddr = Any"+Environment.NewLine+
            "      Port = 1935"+Environment.NewLine+
            "    END"+Environment.NewLine+
            "  END"+Environment.NewLine+
            "END"+Environment.NewLine+
            "";
        public readonly static string c_strDefaultConfigPath = "config.conf";
        public static Encoder c_encDefaultEncoder = Encoding.ASCII.GetEncoder();
        public static Decoder c_decDefaultDecoder = Encoding.ASCII.GetDecoder();

        public FishMediaConfigNode nodeConfigNodeTree { get; private set; } = new FishMediaConfigNode();
        public string strConfigData { get; private set; } = c_strDefaultConfig;
        public string strConfigPath { get; private set; } = c_strDefaultConfigPath;
        public Encoder encEncoder { get; private set; } = c_encDefaultEncoder;
        public Decoder decDecoder { get; private set; } = c_decDefaultDecoder;

        public FishMediaConfig(bool bResolveWithDefault = false)
        {
            if (bResolveWithDefault)
                ResolveConfig();
        }

        public static FishMediaConfig NewConfigByData(string strConfigData)
        {
            FishMediaConfig config = new FishMediaConfig();

            config.strConfigData = strConfigData;

            return config;
        }

        public static FishMediaConfig NewConfigByFile(string strConfigPath)
        {
            FishMediaConfig config = new FishMediaConfig();
            config.LoadConfigFromFile(strConfigPath);

            return config;
        }

        unsafe public bool LoadConfig(bool bResolveConfig = true)
        {
            if (!File.Exists(strConfigPath))
                return false;

            LoadConfigFromFile(strConfigPath, bResolveConfig);
            return true;
        }

        unsafe public void LoadConfigFromFile(string strConfigPath, bool bResolveConfig = true)
        {
            if (CanFileAccess(strConfigPath, true, true, true))
            {
                byte[] arr_byteBytes = File.ReadAllBytes(strConfigPath);
                int iCharCount = decDecoder.GetCharCount(arr_byteBytes, 0, arr_byteBytes.Length);

                if (iCharCount > 0)
                {
                    char[] strDecodedData = new char[iCharCount];
                    fixed (byte* p_arr_byteBytes = arr_byteBytes)
                    {
                        fixed (char* p_strDecodedData = strDecodedData)
                        {
                            decDecoder.GetChars(p_arr_byteBytes, arr_byteBytes.Length, p_strDecodedData, iCharCount, true);
                        }
                    }
                    decDecoder.GetChars(arr_byteBytes, strDecodedData, true);

                    strConfigData = new string(strDecodedData);
                }

                if (bResolveConfig)
                    ResolveConfig();
            }
        }

        public void LoadConfigFromMemory(string strConfigData, bool bResolveConfig = true)
        {
            this.strConfigData = strConfigData;

            if (bResolveConfig)
                ResolveConfig();
        }

        public void ResolveConfig()
        {
            string strConfigDataCopy = this.strConfigData;

            bool bNewLineHasEnter = false;
            bNewLineHasEnter = strConfigDataCopy.Contains('\r');
            string strNewLine = "";
            if (bNewLineHasEnter)
                strNewLine = "\r\n";
            else
                strNewLine = "\n";
            string[] arr_strConfigDataLines = strConfigDataCopy.Split(strNewLine);

            if (arr_strConfigDataLines.Length <= 0)
                return;

            int iLineIndex = 0;
            void MakeNode(ref FishMediaConfigNode ParentNood)
            {
                FishMediaConfigItem NodeTree = new FishMediaConfigItem();
                Dictionary<string, Node<FishMediaConfigItem, string>> NextNoods = new Dictionary<string, Node<FishMediaConfigItem, string>>();

                while (iLineIndex < arr_strConfigDataLines.Length)
                {
                    string strConfigDataLine = arr_strConfigDataLines[iLineIndex].Trim();

                    // Igrone comment
                    if (strConfigDataLine.Contains('#'))
                    {
                        strConfigDataLine = strConfigDataLine.Remove(strConfigDataLine.IndexOf('#')).Trim();
                    }

                    if (strConfigDataLine.Length > 0)
                    {
                        if (strConfigDataLine == "END")
                            break;

                        // Get into node
                        if (strConfigDataLine.StartsWith('[') && strConfigDataLine.EndsWith(']'))
                        {
                            string strNodeKey = strConfigDataLine.Substring(strConfigDataLine.IndexOf('[') + 1, strConfigDataLine.IndexOf(']') - 1 - strConfigDataLine.IndexOf('['));

                            if (NodeTree.ContainsKey(strNodeKey))
                            {
                                uint u_iExternId = 0;
                                while (u_iExternId != uint.MaxValue)
                                {
                                    string strMultiKey = strNodeKey + ' ' + u_iExternId;
                                    if (NodeTree.ContainsKey(strMultiKey))
                                        u_iExternId++;
                                    else
                                    {
                                        strNodeKey = strMultiKey;
                                        break;
                                    }
                                }
                            }
                            NodeTree[strNodeKey] = ""; // Add Node

                            FishMediaConfigNode NextNood = new FishMediaConfigNode();
                            ++iLineIndex;
                            MakeNode(ref NextNood);
                            NextNoods[strNodeKey] = NextNood;
                        }
                        else
                        {
                            string strNodeMemberKey = strConfigDataLine.Substring(0, strConfigDataLine.IndexOf('=')).Trim();
                            string strOriginNodeMemberValue = strConfigDataLine.Substring(strConfigDataLine.IndexOf('=') + 1).Trim();
                            if ((strOriginNodeMemberValue.StartsWith('\'') && strOriginNodeMemberValue.EndsWith('\'')) || (strOriginNodeMemberValue.StartsWith('\"') && strOriginNodeMemberValue.EndsWith('\"')))
                            {
                                strOriginNodeMemberValue = strOriginNodeMemberValue.Remove(0, 1);
                                strOriginNodeMemberValue = strOriginNodeMemberValue.Remove(strOriginNodeMemberValue.Length - 1, 1);
                            }
                            string strNodeMemberValue = strOriginNodeMemberValue;

                            NodeTree[strNodeMemberKey] = strNodeMemberValue;
                        }
                    }

                    ++iLineIndex;
                }

                ParentNood.Next = NextNoods;
                ParentNood.Data = NodeTree;
            }

            FishMediaConfigNode node = new FishMediaConfigNode();
            MakeNode(ref node);
            nodeConfigNodeTree = node;

        }

        public void SaveConfig()
        {
            int iByteCount = encEncoder.GetByteCount(strConfigData, true);
            byte[] arr_byteBytes = new byte[iByteCount];
            encEncoder.GetBytes(strConfigData, arr_byteBytes, true);

            string strDirectoryPath = Path.GetDirectoryName(strConfigPath);
            if (strDirectoryPath.Trim() != string.Empty && !Directory.Exists(strDirectoryPath))
                Directory.CreateDirectory(strDirectoryPath);
            File.WriteAllBytes(strConfigPath, arr_byteBytes);
        }

        public void SetPath(string strConfigPath)
        {
            this.strConfigPath = strConfigPath;
        }

        public void SetEncoder(Encoder encEncoder)
        {
            this.encEncoder = encEncoder;
        }

        public void SetDecoder(Decoder decDecoder)
        {
            this.decDecoder = decDecoder;
        }


        // Utils
        private static bool CanFileAccess(string strPath, bool bPermissionNeed = true, bool bTryCreate = false, bool bTryGetPermission = false)
        {
            if (!File.Exists(strPath))
            {
                if (!bTryCreate)
                    return false;

                try
                {
                    File.Create(strPath).Dispose();
                }
                catch (Exception eEx)
                {
                    Console.Error.WriteLine(eEx.Message);
                    return false;
                }
            }

            bool bPermissionAccess = false;
            try
            {
                File.Open(strPath, FileMode.Open, FileAccess.ReadWrite).Dispose();
                bPermissionAccess = true;
            }
            catch (Exception)
            {
                bPermissionAccess = false;
            }


            if (bPermissionNeed)
            {
                if (bPermissionAccess)
                    return true;

                if (bTryGetPermission)
                {
                    try
                    {
                        File.SetAttributes(strPath, FileAttributes.Normal | FileAttributes.ReadOnly);
                    }
                    catch (Exception eEx)
                    {
                        Console.Error.WriteLine(eEx.Message);
                        return false;
                    }

                    try
                    {
                        File.Open(strPath, FileMode.Open, FileAccess.ReadWrite).Dispose();
                        return true;
                    }
                    catch (IOException)
                    {
                        Console.Error.WriteLine("Get Permission Failed!");
                        return false;
                    }
                }
                else
                    return false;
            }
            else
                return true;

        }
    }
}
