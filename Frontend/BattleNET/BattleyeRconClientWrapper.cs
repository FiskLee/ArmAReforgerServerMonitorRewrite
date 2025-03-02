using System.Threading.Tasks;
using BattleNET;
using BattleNET.Models;

namespace ArmaReforgerServerMonitor.Frontend.Rcon
{
    /// <summary>
    /// A higher-level wrapper for the BattleyeRconClient.
    /// </summary>
    public class BattleyeRconClientWrapper
    {
        private BattleyeRconClient? _client;

        public async Task<BattlEyeConnectionResult> ConnectAsync(string host, int port, string password)
        {
            _client = new BattleyeRconClient(host, port, password);
            return await _client.ConnectAsync();
        }

        public Task DisconnectAsync()
        {
            return _client?.DisconnectAsync() ?? Task.CompletedTask;
        }

        public Task<string> ExecuteCommandAsync(string command)
        {
            return _client?.ExecuteCommandAsync(command) ?? Task.FromResult(string.Empty);
        }

        public bool IsConnected => _client?.IsConnected ?? false;
    }
}
