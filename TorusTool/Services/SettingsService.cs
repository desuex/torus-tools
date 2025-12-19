using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace TorusTool.Services;

public class AppSettings
{
    public List<string> RecentFiles { get; set; } = new();
    public string LastSelectedGameId { get; set; } = string.Empty;
}

public class SettingsService
{
    private readonly string _settingsPath;
    private AppSettings _currentSettings;

    public SettingsService()
    {
        _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        _currentSettings = new AppSettings();
        LoadSettings();
    }

    public List<string> RunTimeRecentFiles => _currentSettings.RecentFiles;
    public string LastSelectedGameId
    {
        get => _currentSettings.LastSelectedGameId;
        set
        {
            if (_currentSettings.LastSelectedGameId != value)
            {
                _currentSettings.LastSelectedGameId = value;
                SaveSettings();
            }
        }
    }

    public void AddRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        // Remove if exists to move to top
        _currentSettings.RecentFiles.RemoveAll(x => x.Equals(path, StringComparison.OrdinalIgnoreCase));

        // Insert at top
        _currentSettings.RecentFiles.Insert(0, path);

        // Keep max 10
        if (_currentSettings.RecentFiles.Count > 10)
        {
            _currentSettings.RecentFiles = _currentSettings.RecentFiles.Take(10).ToList();
        }

        SaveSettings();
    }

    private void LoadSettings()
    {
        if (File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                _currentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
                _currentSettings = new AppSettings();
            }
        }
    }

    private void SaveSettings()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_currentSettings, options);
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
}
