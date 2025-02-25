using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ArmaReforgerServerMonitor.Backend.Setup
{
    /// <summary>
    /// POCO for storing configuration.
    /// </summary>
    public class Configuration
    {
        public string MasterLogsDirectory { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool FullScanOption { get; set; }
    }

    /// <summary>
    /// Handles the initial configuration setup.
    /// Prompts the user for a master logs directory (with example based on OS), username, and password.
    /// Saves the configuration to a JSON file for future startups.
    /// </summary>
    public static class ConfigurationSetup
    {
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

        public static string MasterLogsDirectory { get; private set; } = string.Empty;
        public static string Username { get; private set; } = string.Empty;
        public static string Password { get; private set; } = string.Empty;
        public static bool FullScanOption { get; private set; } = false;

        public static void RunSetup()
        {
            // Attempt to load existing configuration.
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var config = JsonSerializer.Deserialize<Configuration>(json);
                    if (config != null &&
                        !string.IsNullOrWhiteSpace(config.MasterLogsDirectory) &&
                        Directory.Exists(config.MasterLogsDirectory) &&
                        !string.IsNullOrWhiteSpace(config.Username) &&
                        !string.IsNullOrWhiteSpace(config.Password))
                    {
                        MasterLogsDirectory = config.MasterLogsDirectory;
                        Username = config.Username;
                        Password = config.Password;
                        FullScanOption = config.FullScanOption;
                        return;
                    }
                    else
                    {
                        Console.WriteLine("Existing configuration is invalid or incomplete.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error loading configuration: " + ex.Message);
                }
            }

            // If no valid configuration exists, prompt the user.
            Console.WriteLine("Enter the master logs directory.");
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("Example: C:\\Program Files (x86)\\Steam\\steamapps\\common\\Arma Reforger");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Console.WriteLine("Example: /home/admin/arma-reforger/1874900/AReforgerMaster/logs/");
            }

            string input;
            do
            {
                Console.Write("Master Logs Directory: ");
                input = Console.ReadLine()?.Trim() ?? "";
                if (!Directory.Exists(input))
                {
                    Console.WriteLine("Directory does not exist. Please enter a valid directory.");
                }
            } while (!Directory.Exists(input));
            MasterLogsDirectory = input;

            // Prompt for username.
            do
            {
                Console.Write("Enter username for frontend access: ");
                Username = Console.ReadLine()?.Trim() ?? "";
                if (string.IsNullOrEmpty(Username))
                    Console.WriteLine("Username cannot be empty.");
            } while (string.IsNullOrEmpty(Username));

            // Prompt for password.
            do
            {
                Console.Write("Enter password for frontend access: ");
                Password = Console.ReadLine()?.Trim() ?? "";
                if (string.IsNullOrEmpty(Password))
                    Console.WriteLine("Password cannot be empty.");
            } while (string.IsNullOrEmpty(Password));

            // Ask for full scan option.
            Console.Write("Perform a full scan of logs? (y/n): ");
            string option = Console.ReadLine()?.Trim().ToLower() ?? "n";
            FullScanOption = option == "y" || option == "yes";

            // Save the configuration.
            var newConfig = new Configuration
            {
                MasterLogsDirectory = MasterLogsDirectory,
                Username = Username,
                Password = Password,
                FullScanOption = FullScanOption
            };

            try
            {
                string jsonToSave = JsonSerializer.Serialize(newConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, jsonToSave);
                Console.WriteLine("Configuration saved successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving configuration: " + ex.Message);
            }
        }
    }
}
