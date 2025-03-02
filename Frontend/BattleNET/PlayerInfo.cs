namespace BattleNET.Models
{
    /// <summary>
    /// Represents a player returned by the RCON "players" command.
    /// Adjust these properties according to the actual data provided by your server.
    /// </summary>
    public class PlayerInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Score { get; set; }
        public int Ping { get; set; }
        public int Id { get; set; }
        // Add any additional properties as required.
    }
}
