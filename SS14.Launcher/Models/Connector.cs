using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DynamicData;
using Newtonsoft.Json;
using ReactiveUI;
using Serilog;
using SS14.Launcher.Models.Logins;

namespace SS14.Launcher.Models
{
    public class Connector : ReactiveObject
    {
        private readonly Updater _updater;
        private readonly DataManager _cfg;
        private readonly LoginManager _loginManager;

        private ConnectionStatus _status = ConnectionStatus.None;
        private bool _clientExitedBadly;

        public Connector(Updater updater, DataManager cfg, LoginManager loginManager)
        {
            _updater = updater;
            _cfg = cfg;
            _loginManager = loginManager;
        }

        public ConnectionStatus Status
        {
            get => _status;
            private set => this.RaiseAndSetIfChanged(ref _status, value);
        }

        public bool ClientExitedBadly
        {
            get => _clientExitedBadly;
            private set => this.RaiseAndSetIfChanged(ref _clientExitedBadly, value);
        }

        public async void Connect(string address)
        {
            Status = ConnectionStatus.Connecting;

            var parsedAddress = UriHelper.ParseSs14Uri(address);

            // Fetch server connect info.
            var infoAddr = UriHelper.GetServerInfoAddress(parsedAddress);
            ServerInfo info;

            try
            {
                var resp = await Global.GlobalHttpClient.GetStringAsync(infoAddr);
                info = JsonConvert.DeserializeObject<ServerInfo>(resp);
            }
            catch (Exception e) when (e is JsonException || e is HttpRequestException)
            {
                Log.Error(e, "Failed to connect");
                Status = ConnectionStatus.ConnectionFailed;
                return;
            }

            // Run update.
            Status = ConnectionStatus.Updating;
            var installation = await _updater.RunUpdateForLaunchAsync(info.BuildInformation);

            if (_updater.Status == Updater.UpdateStatus.Error)
            {
                Status = ConnectionStatus.UpdateError;
                return;
            }

            Debug.Assert(installation != null);

            Uri connectAddress;
            if (string.IsNullOrEmpty(info.ConnectAddress))
            {
                // No connect address specified, use same address/port as base address.
                connectAddress = new UriBuilder
                {
                    Scheme = "udp",
                    Host = infoAddr.Host,
                    Port = infoAddr.Port
                }.Uri;
            }
            else
            {
                try
                {
                    connectAddress = new Uri(info.ConnectAddress);
                }
                catch (FormatException e)
                {
                    Log.Error(e, "Failed to parse ConnectAddress");
                    Status = ConnectionStatus.ConnectionFailed;
                    return;
                }
            }

            Status = ConnectionStatus.StartingClient;

            var cVars = new List<(string, string)>();

            if (info.AuthInformation.Mode != AuthMode.Disabled && _loginManager.ActiveAccount != null)
            {
                var account = _loginManager.ActiveAccount;

                cVars.Add(("auth.token", account.LoginInfo.Token.Token));
                cVars.Add(("auth.userid", account.LoginInfo.UserId.ToString()));
                cVars.Add(("auth.serverpubkey", info.AuthInformation.PublicKey));
                cVars.Add(("auth.server", ConfigConstants.AuthUrl));
            }

            // Launch client.
            var proc = LaunchClient(installation, new[]
            {
                // We are using the launcher. Don't show main menu etc..
                "--launcher",

                // Pass username to launched client.
                // We don't load username from client_config.toml when launched via launcher.
                "--username", _loginManager.ActiveAccount?.Username ?? "JoeGenero",

                // Connection address
                "--connect-address", connectAddress.ToString(),

                // ss14(s):// address passed in. Only used for feedback in the client.
                "--ss14-address", parsedAddress.ToString(),

                // GLES2 forcing or using default fallback
                "--cvar", "display.renderer=" + (_cfg.ForceGLES2 ? "3" : "0"),
            }, cVars);

            // Wait 300ms, if the client exits with a bad error code before that it's probably fucked.
            var waitClient = proc.WaitForExitAsync();
            var waitDelay = Task.Delay(300);

            await Task.WhenAny(waitDelay, waitClient);

            if (!proc.HasExited)
            {
                Status = ConnectionStatus.ClientRunning;
                await waitClient;
                return;
            }

            ClientExitedBadly = proc.ExitCode != 0;
            Status = ConnectionStatus.ClientExited;
        }

        private static Process LaunchClient(Installation installation, IEnumerable<string> extraArgs, List<(string, string)> cVars)
        {
            var binPath = Path.Combine(UserDataDir.GetUserDataDir(), "installations",
                installation.DiskId.ToString(CultureInfo.InvariantCulture));
            ProcessStartInfo startInfo;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(binPath, "Robust.Client")
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(binPath, "Robust.Client.exe"),
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // TODO: does this cause macOS to make a security warning?
                // If it does we'll have to manually launch the contents, which is simple enough.
                startInfo = new ProcessStartInfo
                {
                    FileName = "open",
                    ArgumentList = {"Space Station 14.app", "--args"},
                };
            }
            else
            {
                throw new NotSupportedException("Unsupported platform.");
            }

            if (cVars.Count != 0)
            {
                var envVarValue = string.Join(';', cVars.Select(p => $"{p.Item1}={p.Item2}"));
                startInfo.EnvironmentVariables["ROBUST_CVARS"] = envVarValue;
            }

            startInfo.EnvironmentVariables["DOTNET_ROLL_FORWARD"] = "LatestMajor";

            startInfo.UseShellExecute = false;
            startInfo.WorkingDirectory = binPath;
            startInfo.ArgumentList.AddRange(extraArgs);
            return Process.Start(startInfo);
        }

        public enum ConnectionStatus
        {
            None,
            Updating,
            UpdateError,
            Connecting,
            ConnectionFailed,
            StartingClient,
            ClientRunning,
            ClientExited
        }
    }
}
