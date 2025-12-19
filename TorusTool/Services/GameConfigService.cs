using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TorusTool.Models;

namespace TorusTool.Services;

public class GameConfigService
{
    public List<GameConfig> AvailableGames { get; private set; } = new();
    public GameConfig CurrentGame { get; set; }

    public GameConfigService()
    {
        LoadGames();
        CurrentGame = AvailableGames.FirstOrDefault() ?? new GameConfig { Name = "Default", IsBigEndian = false };
    }

    private void LoadGames()
    {
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "games.json");
        if (File.Exists(configPath))
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    Converters = { new JsonStringEnumConverter() },
                    PropertyNameCaseInsensitive = true
                };
                var json = File.ReadAllText(configPath);
                AvailableGames = JsonSerializer.Deserialize<List<GameConfig>>(json, options) ?? new List<GameConfig>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading games.json: {ex.Message}");
            }
        }

        if (!AvailableGames.Any())
        {
            AvailableGames.Add(new GameConfig { Id = "Default", Name = "Default (Little Endian)", IsBigEndian = false });
        }
    }
}
