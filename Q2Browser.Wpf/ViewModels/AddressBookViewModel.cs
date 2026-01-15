using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Q2Connect.Core.Models;
using Q2Connect.Core.Services;
using Q2Connect.Wpf.Services;

namespace Q2Connect.Wpf.ViewModels;

public class AddressBookViewModel : INotifyPropertyChanged
{
    private readonly FavoritesService _favoritesService;
    private readonly Settings _settings;
    private LauncherService? _launcherService;
    private AddressBookEntry? _selectedEntry;
    private string _newAddress = string.Empty;
    private string _newLabel = string.Empty;

    public AddressBookViewModel(FavoritesService favoritesService, Settings settings)
    {
        _favoritesService = favoritesService;
        _settings = settings;
        Entries = new ObservableCollection<AddressBookEntry>();
        
        AddCommand = new RelayCommand(async _ => await AddEntryAsync(), _ => CanAddEntry());
        DeleteCommand = new RelayCommand(async _ => await DeleteSelectedEntryAsync(), _ => SelectedEntry != null);
        ConnectCommand = new RelayCommand(_ => ConnectToSelectedEntry(), _ => SelectedEntry != null);
        CopyDetailsCommand = new RelayCommand(_ => CopyDetails(), _ => SelectedEntry != null);
        
        _ = LoadEntriesAsync();
    }

    public void SetLauncherService(LauncherService launcherService)
    {
        _launcherService = launcherService;
    }

    public ObservableCollection<AddressBookEntry> Entries { get; }

    public AddressBookEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            _selectedEntry = value;
            OnPropertyChanged();
            ((RelayCommand)DeleteCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ConnectCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CopyDetailsCommand).RaiseCanExecuteChanged();
        }
    }

    public string NewAddress
    {
        get => _newAddress;
        set
        {
            _newAddress = value;
            OnPropertyChanged();
            ((RelayCommand)AddCommand).RaiseCanExecuteChanged();
        }
    }

    public string NewLabel
    {
        get => _newLabel;
        set
        {
            _newLabel = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand CopyDetailsCommand { get; }

    private bool CanAddEntry()
    {
        return !string.IsNullOrWhiteSpace(NewAddress);
    }

    private bool IsValidAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        var parts = address.Split(':');
        if (parts.Length != 2)
            return false;

        if (!IPAddress.TryParse(parts[0], out _))
            return false;

        if (!int.TryParse(parts[1], out var port) || port < 1 || port > 65535)
            return false;

        return true;
    }

    private async System.Threading.Tasks.Task AddEntryAsync()
    {
        if (!CanAddEntry())
            return;

        var trimmedAddress = NewAddress.Trim();
        
        // Check for duplicates
        if (Entries.Any(e => e.Address.Equals(trimmedAddress, StringComparison.OrdinalIgnoreCase)))
        {
            System.Windows.MessageBox.Show(
                $"An entry with address '{trimmedAddress}' already exists.",
                "Duplicate Entry",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var entry = new AddressBookEntry
        {
            Address = trimmedAddress,
            Label = string.IsNullOrWhiteSpace(NewLabel) ? trimmedAddress : NewLabel.Trim()
        };

        Entries.Add(entry);
        await SaveEntriesAsync();

        NewAddress = string.Empty;
        NewLabel = string.Empty;
    }

    private async System.Threading.Tasks.Task DeleteSelectedEntryAsync()
    {
        if (SelectedEntry == null)
            return;

        Entries.Remove(SelectedEntry);
        await SaveEntriesAsync();
    }

    private async System.Threading.Tasks.Task LoadEntriesAsync()
    {
        try
        {
            var entries = await _favoritesService.LoadAddressBookAsync();
            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.LogError($"Failed to load address book: {ex.Message}", ex.ToString());
        }
    }

    private async System.Threading.Tasks.Task SaveEntriesAsync()
    {
        try
        {
            await _favoritesService.SaveAddressBookAsync(Entries.ToList(), _settings.PortableMode);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.LogError($"Failed to save address book: {ex.Message}", ex.ToString());
        }
    }

    private void ConnectToSelectedEntry()
    {
        if (SelectedEntry == null || _launcherService == null)
            return;

        try
        {
            // Pass the address directly without validation - let Quake 2 handle parsing
            _launcherService.LaunchGameWithAddress(SelectedEntry.Address);
        }
        catch (InvalidOperationException ex)
        {
            System.Windows.MessageBox.Show(
                $"Cannot connect to server:\n\n{ex.Message}\n\nPlease configure the Quake 2 executable path in Settings.",
                "Configuration Required",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        catch (System.IO.FileNotFoundException ex)
        {
            System.Windows.MessageBox.Show(
                $"Cannot connect to server:\n\n{ex.Message}\n\nPlease check the Quake 2 executable path in Settings.",
                "File Not Found",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error launching game:\n\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            DiagnosticLogger.Instance.LogError($"Error launching game: {ex.Message}", ex.ToString());
        }
    }

    private void CopyDetails()
    {
        if (SelectedEntry == null)
            return;

        try
        {
            var details = $"Address: {SelectedEntry.Address}";
            if (!string.IsNullOrWhiteSpace(SelectedEntry.Label))
            {
                details += $"\nLabel: {SelectedEntry.Label}";
            }
            details += $"\nDisplay: {SelectedEntry.DisplayText}";

            Clipboard.SetText(details);
        }
        catch (Exception ex)
        {
            DiagnosticLogger.Instance.LogError($"Error copying address book entry details: {ex.Message}", ex.ToString());
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

