using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using LimboServiceSoap;

class Program
{
    static void Main()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Starting SOAP listener...");
        Console.ResetColor();
        StartSoapListener(8000);
    }

    public static int FindOpenPort(int startPort, int endPort)
    {
        Console.WriteLine($"Searching for an open port between {startPort} and {endPort}...");
        for (int port = startPort; port <= endPort; port++)
        {
            try
            {
                using (var listener = new TcpListener(IPAddress.Loopback, port))
                {
                    listener.Start();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Found open port: {port}");
                    Console.ResetColor();
                    return port;
                }
            }
            catch (SocketException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Port {port} is in use. Trying next port...");
                Console.ResetColor();
                continue; // port is in use, try the next one
            }
        }
        throw new Exception("No open ports found.");
    }


    public static Process StartRCCService(int port, string version, string renderType, int placeId, string jobId, int universeId, int matchmaking, int maxPlayers, int udpPort, int creatorId, int placeVersion, string siteUrl, string placeFetchUrl, bool isRetry = false)
    {
        string executablePath = version switch
        {
            "2016" => "C:\\RCC\\2016\\RCC2016.exe",
            "2018" => "C:\\RCC\\2018\\RCC2018.exe",
            "2020" => "C:\\RCC\\Twenty20\\RCC2020.exe",
            "Renderer" => "C:\\RCC\\Twenty20\\Renderer2020.exe",
            _ => throw new ArgumentException($"Unknown version: {version}")
        };

        Console.WriteLine($"Starting RCCService for version {version} on port {port} with executable {executablePath}");

        if (!File.Exists(executablePath))
        {
            LogError("RCCService executable not found.");
            return null;
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/K \"{executablePath}\" -console -verbose {port}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = false,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var process = new Process
        {
            StartInfo = processStartInfo,
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += new DataReceivedEventHandler((sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                if (args.Data.Contains("Failed to finish initializing game:"))
                {
                    LogError("RCC failed to start.");
                    Console.WriteLine(args.Data);
                }
            }
        });
        process.ErrorDataReceived += new DataReceivedEventHandler((sender, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                if (args.Data.Contains("Failed to finish initializing game:"))
                {
                    LogError("RCC failed to start.");
                    Console.WriteLine(args.Data);
                }
            }
        });

        try
        {
            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                LogSuccess("RCCService started successfully.");

                return process;
            }
            else
            {
                LogError("Failed to start RCCService.");
                return null;
            }
        }
        catch (Exception ex)
        {
            LogError($"Error starting RCCService: {ex.Message}");
            return null;
        }
    }

    public static void SendSoapRequest(int port, string version, string renderType, int placeId, string jobId, int universeId, int matchmaking, int maxPlayers, int udpPort, int creatorId, int placeVersion, string siteUrl, string placeFetchUrl)
    {
        Console.WriteLine($"Sending SOAP request to port {port} with version {version} and render type {renderType}...");

        string soapData = version switch
        {
            "2016" => $"pcall(function() loadfile('https://assetgame.limbo.com/Game/gameserver.ashx?id={placeId}&gameid={jobId}&universeid={universeId}&matchmaking={matchmaking}&maxplayers={maxPlayers}&udp={udpPort}')() end)",
            "2018" => $"{{\"Mode\":\"GameServer\",\"GameId\":\"{jobId}\",\"Settings\":{{\"PlaceId\":{placeId},\"GameId\":\"{jobId}\",\"GsmInterval\":5,\"MaxPlayers\":{maxPlayers},\"MaxGameInstances\":{maxPlayers},\"MachineAddress\":\"127.0.0.1\",\"ApiKey\":\"21ee7df5-7d5b-4cef-83c9-36f92ea10aa5\",\"PreferredPlayerCapacity\":{maxPlayers},\"DataCenterId\":\"12345\",\"PlaceVisitAccessKey\":\"21ee7df5-7d5b-4cef-83c9-36f92ea10aa5\",\"UniverseId\":{universeId},\"MatchmakingContextId\":{matchmaking},\"CreatorId\":{creatorId},\"CreatorType\":\"User\",\"PlaceVersion\":{placeVersion},\"BaseUrl\":\"{siteUrl}\",\"JobId\":\"{jobId}\",\"PreferredPort\":{udpPort}}},\"Arguments\":{{}}}}",
            "2020" => $"{{\"Mode\":\"GameServer\",\"GameId\":\"{jobId}\",\"Settings\":{{\"PlaceId\":{placeId},\"GameId\":\"{jobId}\",\"GsmInterval\":5,\"MaxPlayers\":{maxPlayers},\"MaxGameInstances\":{maxPlayers},\"MachineAddress\":\"127.0.0.1\",\"ApiKey\":\"21ee7df5-7d5b-4cef-83c9-36f92ea10aa5\",\"PreferredPlayerCapacity\":{maxPlayers},\"DataCenterId\":\"12345\",\"PlaceVisitAccessKey\":\"21ee7df5-7d5b-4cef-83c9-36f92ea10aa5\",\"UniverseId\":{universeId},\"MatchmakingContextId\":{matchmaking},\"CreatorId\":{creatorId},\"CreatorType\":\"User\",\"PlaceVersion\":{placeVersion},\"BaseUrl\":\"{siteUrl}\",\"JobId\":\"{jobId}\",\"PreferredPort\":{udpPort},\"PlaceFetchUrl\":\"{placeFetchUrl}\"}},\"Arguments\":{{}}}}",
            _ => throw new ArgumentException($"Unknown version: {version}")
        };

        var binding = new BasicHttpBinding();
        var endpoint = new EndpointAddress($"http://localhost:{port}/RCCServiceSoap");
        var client = new RCCServiceSoapClient(binding, endpoint);

        try
        {
            var response = client.OpenJobExAsync(new Job
            {
                id = jobId,
                expirationInSeconds = 120
            }, new ScriptExecution
            {
                script = soapData
            });

            LogSuccess("Response:");
            Console.WriteLine(response);
        }
        catch (Exception ex)
        {
            LogError($"Error sending SOAP request: {ex.Message}");
        }
    }

    public static void StartSoapListener(int port)
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        LogSuccess($"SOAP listener started on port {port}");

        while (true)
        {
            try
            {
                var context = listener.GetContext();
                Task.Run(() => HandleRequestAsync(context));
            }
            catch (Exception ex)
            {
                LogError($"Error handling request: {ex.Message}");
            }
        }
    }

    public static async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var accessKey = request.Headers["Access-Key"];
            if (string.IsNullOrEmpty(accessKey) || accessKey != "testkey")
            {
                LogError("Invalid Access-Key.");

                response.StatusCode = (int)HttpStatusCode.Forbidden;
                response.Close();
                return;
            }

            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                string requestData = await reader.ReadToEndAsync();
                LogInfo("Received SOAP request!");

                var (version, renderType, placeId, maxPlayers, jobId, universeId, matchmaking, creatorId, placeVersion, siteUrl, udpPort, placeFetchUrl) = ParseSoapRequest(requestData);

                int openPort = FindOpenPort(31501, 33000);
                var process = StartRCCService(openPort, version, renderType, int.Parse(placeId), jobId, int.Parse(universeId), int.Parse(matchmaking), int.Parse(maxPlayers), int.Parse(udpPort), int.Parse(creatorId), int.Parse(placeVersion), siteUrl, placeFetchUrl);
                if (process != null)
                {
                    SendSoapRequest(openPort, version, renderType, int.Parse(placeId), jobId, int.Parse(universeId), int.Parse(matchmaking), int.Parse(maxPlayers), int.Parse(udpPort), int.Parse(creatorId), int.Parse(placeVersion), siteUrl, placeFetchUrl);
                    LogSuccess("Sent SOAP response to RCC.");
                }
                else
                {
                    LogError("Cancelling request because no RCC was found.");
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.Close();
                }
            }
        }
        catch (Exception ex)
        {
            LogError($"Error processing request: {ex.Message}");
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
            response.Close();
        }
    }

    public static (string version, string renderType, string placeId, string maxPlayers, string jobId, string universeId, string matchmaking, string creatorId, string placeVersion, string siteUrl, string udpPort, string placeFetchUrl) ParseSoapRequest(string soapRequest)
    {
        try
        {
            XDocument doc = XDocument.Parse(soapRequest);
            XNamespace soapNs = "http://schemas.xmlsoap.org/soap/envelope/";
            XNamespace webNs = "http://www.limbo.com/LimboServiceSoap";
            var body = doc.Root.Element(soapNs + "Body");

            string GetValue(string elementName) => body?.Element(webNs + elementName)?.Value ?? string.Empty;

            var version = GetValue("Version");
            var renderType = GetValue("RenderType");
            var placeId = GetValue("PlaceId");
            var maxPlayers = GetValue("MaxPlayers");
            var jobId = GetValue("JobId");
            var universeId = GetValue("UniverseId");
            var matchmaking = GetValue("Matchmaking");
            var creatorId = GetValue("CreatorId");
            var placeVersion = GetValue("PlaceVersion");
            var siteUrl = GetValue("SiteURL");
            var udpPort = GetValue("UDPPort");
            var placeFetchUrl = GetValue("PlaceFetchUrl");

            if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(renderType))
            {
                throw new ArgumentException("Version is required");
            }

            LogSuccess($"Parsed SOAP request. Version: {version}, RenderType: {renderType}");

            return (version, renderType, placeId, maxPlayers, jobId, universeId, matchmaking, creatorId, placeVersion, siteUrl, udpPort, placeFetchUrl);
        }
        catch (Exception ex)
        {
            LogError($"Error parsing SOAP request: {ex.Message}");
            throw;
        }
    }

    public static void LogInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void LogSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    public static void LogError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(message);
        Console.ResetColor();
    }
}
