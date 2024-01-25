using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Linq;
using System.Net;



namespace WindowsGSM.Plugins
{
    public class Palworld : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.Palworld", // WindowsGSM.XXXX
            author = "itkujo",
            description = "WindowsGSM plugin for supporting Palworld Dedicated Server",
            version = "1.0",
            url = "https://github.com/itkujo/WindowsGSM.Palworld-Updated", // Github repository link (Best practice)
            color = "#34c9eb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true; // As of 25.18, login is no longer needed. Source: https://discord.com/channels/729837326120910915/735188487615283232/1167603768091754537
        public override string AppId => "2394010"; // Game server app
		
        // - Standard Constructor and properties
        public Palworld(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;

        // - Game server Fixed variables
        public override string StartPath => @"Pal\Binaries\Win64\PalServer-Win64-Test-Cmd.exe"; // Game server start path 
        public string FullName = "Palworld Dedicated Server"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 3; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()

        // - Game server default values
        public string ServerName = "Palworld";
        public string Defaultmap = "MainWorld5"; // Original (MapName)
        public string Maxplayers = "32"; // WGSM reads this as string but originally it is number or int (MaxPlayers)
        public string Port = "8211"; // WGSM reads this as string but originally it is number or int
        public string QueryPort = "8212"; // WGSM reads this as string but originally it is number or int (SteamQueryPort)
        public string Additional = "EpicApp=PalServer -useperfthreads -NoAsyncLoadingThread -UseMultithreadForDS";


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
			 //No config file seems
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            var param = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(_serverData.ServerMap))
                param.Append($" {_serverData.ServerMap}");

            param.Append("?listen");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerName))
                param.Append($"?SessionName=\"\"\"{_serverData.ServerName}\"\"\"");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerIP))
                param.Append($"?MultiHome={_serverData.ServerIP}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerPort))
                param.Append($"?Port={_serverData.ServerPort}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerQueryPort))
                param.Append($"?QueryPort={_serverData.ServerQueryPort}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer))
                param.Append($"?MaxPlayers={_serverData.ServerMaxPlayer}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerParam))
                if(_serverData.ServerParam.StartsWith("?"))
                    param.Append($"{_serverData.ServerParam}");
                else if (_serverData.ServerParam.StartsWith("-"))
                    param.Append($" {_serverData.ServerParam}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer))
                param.Append($" -WinLiveMaxPlayers={_serverData.ServerMaxPlayer}");

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        FileName = shipExePath,
                        Arguments = param.ToString(),
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        FileName = shipExePath,
                        Arguments = param.ToString(),
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = false,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }


        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(async () =>
            {
                if (p.StartInfo.CreateNoWindow)
                {
                    p.CloseMainWindow();
                }
                else
                {
                    Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                    Functions.ServerConsole.SendWaitToMainWindow("quit");
                    Functions.ServerConsole.SendWaitToMainWindow("{ENTER}");
                    await Task.Delay(6000);
                }
            });
        }

        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId, true, loginAnonymous);
            Error = steamCMD.Error;

            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;
            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }

    }
}
