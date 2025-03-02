using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using BattleNET;
using BattleNET.Models;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using System.Text;

namespace ArmaReforgerServerMonitor.Frontend.Rcon
{
    /// <summary>
    /// Wraps the BattleNET BattlEyeClient to provide asynchronous RCON connection, disconnection, and command execution.
    /// </summary>
    public class BattleyeRconClient
    {
        private readonly BattlEyeClient _client;
        private readonly BattlEyeLoginCredentials _credentials;

        public bool IsConnected => _client.Connected;

        // Register the code pages provider so that Encoding.GetEncoding(1252) works.
        static BattleyeRconClient()
        {
            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        }

        /// <summary>
        /// Initializes a new instance using host, port, and password.
        /// </summary>
        public BattleyeRconClient(string host, int port, string password)
        {
            // Convert host to IPAddress.
            if (!IPAddress.TryParse(host, out IPAddress? ipAddress))
            {
                var entry = Dns.GetHostEntry(host);
                if (entry.AddressList.Length == 0)
                    throw new ArgumentException("Unable to resolve host.", nameof(host));
                ipAddress = entry.AddressList[0];
            }

            _credentials = new BattlEyeLoginCredentials
            {
                Host = ipAddress!, // Guaranteed non-null after check.
                Port = port,
                Password = password
            };

            _client = new BattlEyeClient(_credentials);
        }

        /// <summary>
        /// Connects to the RCON server asynchronously.
        /// </summary>
        public Task<BattlEyeConnectionResult> ConnectAsync()
        {
            return Task.Run(() => _client.Connect());
        }

        /// <summary>
        /// Disconnects from the RCON server asynchronously.
        /// </summary>
        public Task DisconnectAsync()
        {
            return Task.Run(() => _client.Disconnect());
        }

        /// <summary>
        /// Executes a command on the RCON server asynchronously.
        /// </summary>
        public Task<string> ExecuteCommandAsync(string command)
        {
            return Task.Run(() =>
            {
                int packetId = _client.SendCommand(command, true);
                // TODO: Wait for and return the actual response.
                return "";
            });
        }

        /// <summary>
        /// Retrieves active players asynchronously by executing the "players" command.
        /// </summary>
        public async Task<List<PlayerInfo>> GetActivePlayersAsync()
        {
            string response = await ExecuteCommandAsync("players");
            var players = JsonSerializer.Deserialize<List<PlayerInfo>>(
                response, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return players ?? new List<PlayerInfo>();
        }
    }
}
