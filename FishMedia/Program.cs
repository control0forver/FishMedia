using Servers.HTTP;
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
                    requestFile = Path.Combine(ServerRoot, requestFile);
                    var content = ListDirectory(requestFile, requestURL);
                    response = response.SetContent(content, Encoding.UTF8);
                    response.Content_Type = "text/html; charset=UTF-8";
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
            builder.Append(string.Format("<html><head><title>{0}</title></head>", requestDirectory));
            builder.Append(string.Format("<body><h1>{0}</h1><br/><ul>{1}{2}</ul></body></html>",
                 requestURL, filesList, foldersList));

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
        static WebServer webServer = null;
        static Thread webServerThread = new Thread(webServerThreadHandler) { IsBackground = true };
        static void webServerThreadHandler()
        {
            webServer.Start();
        }
        static WebServer webServer6 = null;
        static Thread webServer6Thread = new Thread(webServer6ThreadHandler) { IsBackground = true };
        static void webServer6ThreadHandler()
        {
            webServer6.Start();
        }

        static string strConfigPath = "FishMedia.conf";

        static int Main(string[] args)
        {
            Console.WriteLine("Fish Media   Ver. 1.0\n");

            Console.WriteLine("(Press Any Key to Continue)");
            Console.ReadKey(true);

            FishMediaConfig config = new FishMediaConfig();
            config.SetPath(strConfigPath);
            if (!File.Exists(strConfigPath))
            {
                Console.WriteLine("Config({0}) Not Found, use default.", strConfigPath);
                config.SaveConfig();
            }
            config.LoadConfig();


            Console.WriteLine("Starting Web Server");
            {
                FishMediaConfigNode nodeIndex = config.nodeConfigNodeTree;
                string RootDir = "", Index = "", IpAddr = "", Port = "", IpAddr6 = "", Port6 = "", IpV6 = "";

                void ReadConfig()
                {
                    if (nodeIndex.Data.ContainsKey("Config"))
                    {
                        nodeIndex = (FishMediaConfigNode)nodeIndex.Next["Config"];
                        if (nodeIndex.Data.ContainsKey("Servers"))
                        {
                            nodeIndex = (FishMediaConfigNode)nodeIndex.Next["Servers"];
                            if (nodeIndex.Data.ContainsKey("Web"))
                            {
                                nodeIndex = (FishMediaConfigNode)nodeIndex.Next["Web"];

                                foreach (var item in nodeIndex.Data)
                                {
                                    switch (item.Key)
                                    {
                                        default:
                                            break;

                                        case "RootDir":
                                            {
                                                RootDir = item.Value;
                                                break;
                                            }
                                        case "Index":
                                            {
                                                Index = item.Value;
                                                break;
                                            }
                                        case "IpAddr":
                                            {
                                                IpAddr = item.Value;
                                                break;
                                            }
                                        case "Port":
                                            {
                                                Port = item.Value;
                                                break;
                                            }
                                        case "IpAddr6":
                                            {
                                                IpAddr6 = item.Value;
                                                break;
                                            }
                                        case "Port6":
                                            {
                                                Port6 = item.Value;
                                                break;
                                            }
                                        case "IpV6":
                                            {
                                                IpV6 = item.Value;
                                                break;
                                            }
                                    }
                                }
                            }
                        }
                    }
                }

                // Read Default Config
                //nodeIndex = new FishMediaConfig(true).nodeConfigNodeTree;
                //ReadConfig();

                // Read Current Config
                nodeIndex = config.nodeConfigNodeTree;
                ReadConfig();

                IPAddress ipaddrIp = IPAddress.None;
                if (IpAddr == "Any")
                    ipaddrIp = IPAddress.Any;
                else
                    ipaddrIp = IPAddress.Parse(IpAddr);
                webServer = new WebServer(ipaddrIp, int.Parse(Port), RootDir, Index);
                webServer.Logger = new ConsoleLogger();
                webServerThread.Start();

                if (IpV6 == "true")
                {
                    IPAddress ipaddrIpV6 = IPAddress.None;
                    if (IpAddr6 == "Any")
                        ipaddrIpV6 = IPAddress.IPv6Any;
                    else
                        ipaddrIpV6 = IPAddress.Parse(IpAddr6);
                    webServer6 = new WebServer(ipaddrIpV6, int.Parse(Port6), RootDir, Index);
                    webServer6.Logger = new ConsoleLogger6();
                    webServer6Thread.Start();
                }
                else
                {

                }
            }

            while (true)
            {
                string strCmdLine = "";

                Console.Write(">");
                strCmdLine = Console.ReadLine().Trim();

                if (strCmdLine == string.Empty)
                    continue;

                {
                    if (strCmdLine == "help")
                    {
                        Console.WriteLine("Commands:");
                        Console.WriteLine(" help - Show all commands");
                        Console.WriteLine(" show - Show current config");
                        Console.WriteLine(" exit - Exit all servers");
                    }

                    if (strCmdLine == "show")
                    {
                        Console.WriteLine(config.strConfigData);
                    }

                    if (strCmdLine == "exit")
                    {
                        webServer.Stop();
                        if (webServerThread.IsAlive)
                            webServerThread.Join();

                        break;
                    }
                }
            }

            Console.WriteLine("[Debug]Program Finished (Press Any Key)");
            Console.ReadKey(true);
            return 0;
        }
    }
}