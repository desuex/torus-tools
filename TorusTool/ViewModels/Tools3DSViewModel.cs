using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Platform.Storage;
using TorusTool.Services;
using TorusTool.Models;
using TorusTool.IO;

namespace TorusTool.ViewModels;

public partial class Tools3DSViewModel : ViewModelBase
{
    private readonly GameConfigService _configService = new();

    [ObservableProperty]
    private string _statusLog = "Ready.";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressMax = 100;

    [ObservableProperty]
    private string _packfilePath = string.Empty;

    [ObservableProperty]
    private string _hunkFilesPath = string.Empty;

    [ObservableProperty]
    private bool _overwrite = false;

    public ObservableCollection<GameConfig> AvailableGames { get; } = new();

    [ObservableProperty]
    private GameConfig _selectedGame;

    public Tools3DSViewModel()
    {
        foreach (var game in _configService.AvailableGames)
        {
            if (game.Platform == PlatformType.Nintendo3DS) 
               AvailableGames.Add(game);
        }
        
        if (AvailableGames.Count > 0) 
            SelectedGame = AvailableGames[0];
        else 
            SelectedGame = new GameConfig { Name = "Generic 3DS", IsBigEndian = false, Platform = PlatformType.Nintendo3DS };
    }
    
    public void Log(string message)
    {
        StatusLog += $"\n{DateTime.Now:HH:mm:ss}: {message}";
    }

    [RelayCommand]
    public async Task BrowsePackfile(IStorageProvider storageProvider)
    {
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select packfile.dat",
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("Packfile") { Patterns = new[] { "packfile.dat", "*.dat" } } }
        });

        if (files.Count >= 1)
        {
            PackfilePath = files[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    public async Task BrowseHunkDir(IStorageProvider storageProvider)
    {
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select HUNKFILES directory",
            AllowMultiple = false
        });

        if (folders.Count >= 1)
        {
            HunkFilesPath = folders[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    public async Task Unpack()
    {
        if (string.IsNullOrEmpty(PackfilePath)) { Log("Error: Select packfile first."); return; }
        if (string.IsNullOrEmpty(HunkFilesPath)) { Log("Error: Select output directory first."); return; }

        if (Directory.Exists(HunkFilesPath) && Directory.GetFiles(HunkFilesPath).Length > 0 && !Overwrite)
        {
            Log("Error: Output directory not empty. Check Overwrite to continue.");
            return;
        }

        Log("Starting Unpack...");
        ProgressValue = 0;

        try
        {
            await Task.Run(() =>
            {
                PackfileWriterExtensions.UnpackAll(PackfilePath, HunkFilesPath, (name, current, total) =>
                {
                   // Dispatch to UI thread if needed? Avalonia bindings usually handle basic property updates?
                   // No, usually needs Dispatcher.UIThread.Invoke/Post.
                   // But CommunityToolkit.Mvvm properties trigger PropertyChanged.
                   // Updating frequently might freeze UI if not throttled or dispatched.
                   // Avalonia 11+ is usually fine with async updates but let's be safe.
                   Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                       ProgressMax = total;
                       ProgressValue = current;
                       // Only log every 10% or so to avoid spam
                       if (current % 50 == 0) Log($"Unpacking: {name} ({current}/{total})");
                   });
                });
            });
            Log("Unpack Complete!");
        }
        catch (Exception ex)
        {
            Log($"Error Unpacking: {ex.Message}");
        }
        finally
        {
            ProgressValue = 0;
        }
    }

    [RelayCommand]
    public async Task Repack()
    {
        if (string.IsNullOrEmpty(HunkFilesPath)) { Log("Error: Select input directory first."); return; }
        if (string.IsNullOrEmpty(PackfilePath)) { Log("Error: Select output packfile path."); return; }
        
        if (File.Exists(PackfilePath) && !Overwrite)
        {
             Log("Error: Output file exists. Check Overwrite to continue.");
             return;
        }
        
        Log("Starting Repack...");
        ProgressValue = 0; // Indeterminate?
        
        try
        {
            await Task.Run(() => {
                // Determine file count for progress?
                // PackfileWriter.Repack logic is synchronous.
                // We'd need to modify PackfileWriter to report progress or just wait.
                // It iterates files first.
                // Let's just run it.
                PackfileWriter.Repack(HunkFilesPath, PackfilePath);
            });
            Log("Repack Complete!");
        }
        catch (Exception ex)
        {
            Log($"Error Repacking: {ex.Message}");
        }
    }
    
    [RelayCommand]
    public void Close(Avalonia.Controls.Window window)
    {
        window.Close();
    }
}
