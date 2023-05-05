using FishMedia.Servers.HTTP;
using FishMedia.Servers.RTMP;
using System;
using System.Net;
using System.Text;

namespace FishMedia
{
    public class WebServer : HttpServer
    {
        public string strIndex = "index.htm";

        public WebServer(IPAddress ipaddrIp, int iPort, string strRoot, string strIndex)
            : base(ipaddrIp, iPort, strRoot)
        {
            this.strIndex = strIndex;
        }

        public override void OnPost(HttpRequest request, HttpResponse response)
        {
            string data = request.Params == null ? "" : string.Join(";", request.Params.Select(x => x.Key + "=" + x.Value).ToArray());

            string content = string.Format("Data:{0}", data);

            response.SetContent(content);
            response.Content_Encoding = "utf-8";
            response.StatusCode = "200";
            response.Content_Type = "text/html; charset=UTF-8";
            response.Headers["Server"] = "HttpServer";

            response.Send();
        }

        public override void OnGet(HttpRequest request, HttpResponse response)
        {
            string requestURL = request.URL;
            requestURL = requestURL.Replace("/", @"\").Replace("\\..", "").TrimStart('\\');
            string requestFile = Path.Combine(ServerRoot, requestURL);

            string extension = Path.GetExtension(requestFile);

            if (extension != "")
            {
                response = response.FromFile(requestFile);
            }
            else
            {
                if (Directory.Exists(requestFile) && !File.Exists(requestFile + "\\" + strIndex))
                {
                    //requestFile = Path.Combine(ServerRoot, Path.GetDirectoryName(requestFile));
                    var content = ListDirectory(requestFile, requestURL);
                    response = response.SetContent(content, Encoding.UTF8);
                    response.Content_Type = "text/html; charset=UTF-8";
                    response.StatusCode = "200";
                }
                else
                {
                    requestFile = Path.Combine(requestFile, strIndex);
                    response = response.FromFile(requestFile);
                    response.Content_Type = "text/html; charset=UTF-8";
                }
            }

            response.Send();
        }

        public override void OnDefault(HttpRequest request, HttpResponse response)
        {

        }

        private string ConvertPath(string[] urls)
        {
            string html = string.Empty;
            int length = ServerRoot.Length;
            foreach (var url in urls)
            {
                var s = url.StartsWith("..") ? url : url.Substring(length).TrimEnd('\\');
                html += String.Format("<li><a href=\"{0}\">{0}</a></li>", s);
            }

            return html;
        }

        private string ListDirectory(string requestDirectory, string requestURL)
        {
            var folders = requestURL.Length > 1 ? new string[] { "../" } : new string[] { };
            folders = folders.Concat(Directory.GetDirectories(requestDirectory)).ToArray();
            var foldersList = ConvertPath(folders);

            var files = Directory.GetFiles(requestDirectory);
            var filesList = ConvertPath(files);

            StringBuilder builder = new StringBuilder();
            builder.Append(string.Format("<html><head><title>/{0}/</title></head>", requestDirectory));
            builder.Append(string.Format("<body><h1>Index of /{0}/</h1><br/><ul>{1}{2}</ul></body></html>",
                 requestURL, filesList, foldersList));

            string str = builder.ToString();
            return builder.ToString();
        }
    }
    public class ConsoleLogger : ILogger
    {
        public void Log(object message)
        {
            Console.WriteLine("[WebServer Log] " + message);
        }
    }

    internal class Program
    {
        static List<Thread> threads= new List<Thread>();
        static Dictionary<string, RtmpServer> rtmpServers = new Dictionary<string, RtmpServer>();
        static Dictionary<string, WebServer> webServers = new Dictionary<string, WebServer>();

        static void webServerThreadHandler(object iServerId)
        {
            WebServer webServer = webServers[(string)iServerId];
            webServer.Start();
        }
        static void webServerThreadHandlerV6(object iServerId)
        {
            WebServer webServer = webServers[(string)iServerId];
            webServer.StartV6();
        }
        static void rtmpServerThreadHandler(object iServerId)
        {
            RtmpServer rtmpServer = rtmpServers[(string)iServerId];
            rtmpServer.Start();
        }

        static string strConfigPath = "FishMedia.conf";

        static void TestCode()
        {
            char[] str = "adwadadawASFESFEado109423834()\0".ToArray();
            byte[] amfStrData = AMF.Byted.AMF_EncodeString(AMF.AVal.AVC(str));

            AMF.AVal deVal = AMF.AMF_DecodeString(amfStrData);
            byte[] amfDecoedStr = deVal.arr_chValue;

            char[] decoder = Utils.Utils.ByteArrayToCharArray(amfDecoedStr);

            Console.WriteLine(decoder);


        }

        static int Main(string[] args)
        {
            Console.ResetColor();
            Console.WriteLine("Fish Media   Ver. 1.0\n");

            TestCode();

            Console.WriteLine("(Press Any Key to Continue)");
            Console.ReadKey(true);

            #region Read Config
            FishMediaConfig config = new FishMediaConfig();
            config.SetPath(strConfigPath);
            if (!File.Exists(strConfigPath))
            {
                Console.WriteLine("Config({0}) Not Found, use default.", strConfigPath);
                config.SaveConfig();
            }
            config.LoadConfig();
            #endregion


            #region Web Server
            Console.BackgroundColor = ConsoleColor.Magenta;
            Console.WriteLine();
            Console.WriteLine("Loading Web Server");
            Console.WriteLine();
            Console.ResetColor();
            {
                FishMediaConfigNode nodeIndex = config.nodeConfigNodeTree;
                string ServerId = "Web";

                // Read Default Config
                //nodeIndex = new FishMediaConfig(true).nodeConfigNodeTree;

                // Read Current Config
                nodeIndex = config.nodeConfigNodeTree;

                if (nodeIndex.Data.ContainsKey("Config"))
                {
                    nodeIndex = (FishMediaConfigNode)nodeIndex.Next["Config"];
                    if (nodeIndex.Data.ContainsKey("Servers"))
                    {
                        nodeIndex = (FishMediaConfigNode)nodeIndex.Next["Servers"];
                        foreach (var key in nodeIndex.Data.Keys)
                        {
                            if (key.Length >= ServerId.Length && key.Substring(0, ServerId.Length) == ServerId)
                            {
                                // Read Config
                                FishMediaConfigNode node = (FishMediaConfigNode)nodeIndex.Next[key];

                                WebServerConfigData webServerConfigData = new WebServerConfigData { };
                                foreach (var item in node.Data)
                                {
                                    Console.WriteLine($"{item.Key}: {item.Value}");
                                    switch (item.Key)
                                    {
                                        default:
                                            break;

                                        case "Id":
                                            {
                                                webServerConfigData.Id = item.Value;
                                                break;
                                            }

                                        case "RootDir":
                                            {
                                                webServerConfigData.RootDir = item.Value;
                                                break;
                                            }
                                        case "Index":
                                            {
                                                webServerConfigData.Index = item.Value;
                                                break;
                                            }
                                        case "IpAddr":
                                            {
                                                webServerConfigData.IpAddr = item.Value;
                                                break;
                                            }
                                        case "Port":
                                            {
                                                webServerConfigData.Port = item.Value;
                                                break;
                                            }
                                        case "IpAddr6":
                                            {
                                                webServerConfigData.IpAddr6 = item.Value;
                                                break;
                                            }
                                        case "Port6":
                                            {
                                                webServerConfigData.Port6 = item.Value;
                                                break;
                                            }
                                        case "IpV6":
                                            {
                                                webServerConfigData.IpV6 = item.Value;
                                                break;
                                            }
                                    }
                                }

                                if (!Directory.Exists(webServerConfigData.RootDir))
                                {
                                    Directory.CreateDirectory(webServerConfigData.RootDir);
                                    Directory.CreateDirectory(Path.Combine(webServerConfigData.RootDir, "images"));
                                    File.WriteAllText(Path.Combine(webServerConfigData.RootDir, webServerConfigData.Index), "<h>This is an image test.</h>\n<br/>\n<img src=\"images/Image1.png\"/>");
                                    File.WriteAllBytes(Path.Combine(webServerConfigData.RootDir, "images", "Image1.png"), Resource1.Image1);
                                }

                                // Start Server
                                WebServer webServer = null;
                                if (webServerConfigData.IpV6 == "true")
                                {
                                    IPAddress ipaddrIp = IPAddress.None;
                                    if (webServerConfigData.IpAddr6 == "Any")
                                        ipaddrIp = IPAddress.IPv6Any;
                                    else
                                        ipaddrIp = IPAddress.Parse(webServerConfigData.IpAddr6);
                                    webServer = new WebServer(ipaddrIp, int.Parse(webServerConfigData.Port6), webServerConfigData.RootDir, webServerConfigData.Index);
                                    webServer.SetRoot(webServerConfigData.RootDir);
                                    webServer.Logger = new ConsoleLogger();
                                    webServers[webServerConfigData.Id] = webServer;

                                    Thread thread = new Thread(webServerThreadHandlerV6) { IsBackground = true };
                                    thread.Start(webServerConfigData.Id);
                                    threads.Add(thread);
                                }
                                else
                                {
                                    IPAddress ipaddrIp = IPAddress.None;
                                    if (webServerConfigData.IpAddr == "Any")
                                        ipaddrIp = IPAddress.Any;
                                    else
                                        ipaddrIp = IPAddress.Parse(webServerConfigData.IpAddr);
                                    webServer = new WebServer(ipaddrIp, int.Parse(webServerConfigData.Port), webServerConfigData.RootDir, webServerConfigData.Index);
                                    webServer.Logger = new ConsoleLogger();
                                    webServers[webServerConfigData.Id] = webServer;
                                    
                                    Thread thread = new Thread(webServerThreadHandler) { IsBackground = true };
                                    thread.Start(webServerConfigData.Id);
                                    threads.Add(thread);
                                }

                                Console.WriteLine("Web Server: {0} Running\n", webServerConfigData.Id);
                            }
                        }
                    }
                }

            }
            Console.BackgroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine();
            Console.WriteLine("Web Server All Started");
            Console.WriteLine();
            Console.ResetColor();
            #endregion

            // TODO: 
            // Rtmp Server IpV6 Support
            #region Rtmp Server
            ConsoleColor consoleColor = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.Magenta;
            Console.WriteLine();
            Console.WriteLine("=== Loading Rtmp Server ===");
            Console.WriteLine();
            Console.ResetColor();
            {
                FishMediaConfigNode nodeIndex = config.nodeConfigNodeTree;
                string ServerId = "Rtmp";

                // Read Default Config
                //nodeIndex = new FishMediaConfig(true).nodeConfigNodeTree;

                // Read Current Config
                nodeIndex = config.nodeConfigNodeTree;

                if (nodeIndex.Data.ContainsKey("Config"))
                {
                    nodeIndex = (FishMediaConfigNode)nodeIndex.Next["Config"];
                    if (nodeIndex.Data.ContainsKey("Servers"))
                    {
                        nodeIndex = (FishMediaConfigNode)nodeIndex.Next["Servers"];
                        foreach (var key in nodeIndex.Data.Keys)
                        {
                            if (key.Length >= ServerId.Length && key.Substring(0, ServerId.Length) == ServerId)
                            {
                                // Read Config
                                FishMediaConfigNode node = (FishMediaConfigNode)nodeIndex.Next[key];

                                RtmpServerConfigData rtmpServerConfigData = new RtmpServerConfigData { };
                                foreach (var item in node.Data)
                                {
                                    Console.WriteLine($"{item.Key}: {item.Value}");
                                    switch (item.Key)
                                    {
                                        default:
                                            break;

                                        case "Id":
                                            {
                                                rtmpServerConfigData.Id = item.Value;
                                                break;
                                            }
                                        case "IpAddr":
                                            {
                                                rtmpServerConfigData.IpAddr = item.Value;
                                                break;
                                            }
                                        case "Port":
                                            {
                                                rtmpServerConfigData.Port = item.Value;
                                                break;
                                            }
                                        case "IpAddr6":
                                            {
                                                rtmpServerConfigData.IpAddr6 = item.Value;
                                                break;
                                            }
                                        case "Port6":
                                            {
                                                rtmpServerConfigData.Port6 = item.Value;
                                                break;
                                            }
                                        case "IpV6":
                                            {
                                                rtmpServerConfigData.IpV6 = item.Value;
                                                break;
                                            }
                                    }
                                }

                                // Start Server
                                RtmpServer rtmpServer = null;
                                IPAddress ipaddrIp = IPAddress.None;
                                if (rtmpServerConfigData.IpAddr == "Any")
                                    ipaddrIp = IPAddress.Any;
                                else
                                    ipaddrIp = IPAddress.Parse(rtmpServerConfigData.IpAddr);
                                rtmpServer = new RtmpServer(ipaddrIp, (ushort)int.Parse(rtmpServerConfigData.Port));
                                rtmpServers[rtmpServerConfigData.Id] = rtmpServer;

                                Thread thread = new Thread(rtmpServerThreadHandler) { IsBackground = true };
                                thread.Start(rtmpServerConfigData.Id);
                                threads.Add(thread);

                                Console.WriteLine("Rtmp Server: {0} Running\n", rtmpServerConfigData.Id);
                            }
                        }
                    }
                }

            }
            Console.BackgroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine();
            Console.Write("=== Rtmp Server All Started ===");
            Console.WriteLine();
            Console.WriteLine();
            Console.ResetColor();
            #endregion

            #region Shell
            while (true)
            {
                string strCmdLineData = "";
                string strCmdLine = "";
                string strCmdExec = "";
                string strCmdArgString = null;
                string[] strCmdArgs = null;

                Console.Write(">");
                strCmdLineData = Console.ReadLine();
                strCmdLine = strCmdLineData.Trim();

                if (strCmdLine == string.Empty)
                    continue;

                strCmdLine += ' ';
                strCmdExec = strCmdLine.Remove(strCmdLine.IndexOf(' '));
                strCmdArgString = strCmdLine.Remove(0, strCmdLine.IndexOf(' ')).Trim();
                strCmdArgs = strCmdArgString.Split(' ').Where(s => !string.IsNullOrEmpty(s)).ToArray();

                {
                    bool bCommandFound = false;
                    if (strCmdExec == "help")
                    {
                        bCommandFound = true;

                        Console.WriteLine("Commands:");
                        Console.WriteLine(" help - Show all commands");
                        Console.WriteLine(" show - Show current config");
                        Console.WriteLine(" list - List servers");
                        Console.WriteLine(" exit - Exit all servers");
                    }

                    if (strCmdExec == "show")
                    {
                        bCommandFound = true;

                        Console.WriteLine(config.strConfigData);
                    }

                    if (strCmdExec == "list")
                    {
                        bCommandFound = true;

                        Console.WriteLine("Servers:");

                        Console.WriteLine(" [Web]");
                        foreach (var item in webServers)
                        {
                            Console.WriteLine($"  Id: {item.Key}");
                        }
                        Console.WriteLine(" [Rtmp]");
                        foreach (var item in rtmpServers)
                        {
                            Console.WriteLine($"  Id: {item.Key}");
                        }
                    }

                    if (strCmdExec == "exit")
                    {
                        bCommandFound = true;

                        // Stop Servers
                        foreach (var item in webServers)
                        {
                            item.Value.Stop();
                        }
                        foreach (var item in rtmpServers)
                        {
                            item.Value.Stop();
                        }

                        // Wait Threads
                        foreach (var thread in threads)
                        {
                            thread.Join();
                        }

                        break;
                    }

                    if (!bCommandFound)
                    {
                        Console.WriteLine("Unknown Command: {0}", strCmdExec);
                    }
                }
            }
            #endregion

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Debug]Program Finished (Press Any Key)");
            Console.ResetColor();
            Console.ReadKey(true);
            return 0;
        }
    }
}
