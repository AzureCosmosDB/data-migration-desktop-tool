using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cosmos.DataTransfer.App.Windows.Framework;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Cosmos.DataTransfer.Ui.Common;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Input;
using Cosmos.DataTransfer.App.Windows.Actions;

namespace Cosmos.DataTransfer.App.Windows;

public class MainViewModel : ViewModelBase
{
    public MainViewModel()
    {
        GenerateCmdLineCommand = new AsyncRelayCommand(new GenerateCommandLineAction(this).Execute, () => !IsExecuting);
        ExportSettingsCommand = new AsyncRelayCommand(new ExportSettingsAction(this).Execute, () => !IsExecuting);
        RunJobCommand = new AsyncRelayCommand(new RunJobAction(this).Execute, () => !IsExecuting);
        CancelCommand = new AsyncRelayCommand(Cancel, () => IsExecuting);

        var appSettings = App.Current.Settings;
        if (appSettings == null)
        {
            throw new InvalidOperationException();
        }

        DataService = new WpfAppDataService(appSettings);

        Initialize();

        if (File.Exists(appSettings.CoreAppPath))
            Messenger.Log(new LogMessage($"Using DMT application at path '{appSettings.CoreAppPath}'."));
        else
            Messenger.Log(LogMessage.Error($"DMT application not found. Attempted to use path '{appSettings.CoreAppPath}'."));
    }

    private async void Initialize()
    {
        try
        {
            var extensions = await DataService.GetExtensionsAsync();
            Sources.AddRange(extensions.Sources);
            Sinks.AddRange(extensions.Sinks);
        }
        catch (Exception ex)
        {
            Messenger.Log(LogMessage.Error($"Failed to load extensions"));
        }
    }

    public IRelayCommand ExportSettingsCommand { get; }
    public IRelayCommand RunJobCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IRelayCommand GenerateCmdLineCommand { get; }

    public ObservableCollection<ExtensionDefinition> Sources { get; } = new();
    public ObservableCollection<ExtensionDefinition> Sinks { get; } = new();

    private ExtensionDefinition? _selectedSource;
    public ExtensionDefinition? SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
            {
                if (_selectedSource == null)
                {
                    SourceSettings = null;
                }
                else
                {
                    SourceSettings = DataService.GetSettingsAsync(_selectedSource.DisplayName, ExtensionDirection.Source)
                        .GetAwaiter().GetResult();
                }
            }
        }
    }

    private ExtensionDefinition? _selectedSink;
    public ExtensionDefinition? SelectedSink
    {
        get => _selectedSink;
        set
        {
            if (SetProperty(ref _selectedSink, value))
            {
                if (_selectedSink == null)
                {
                    SinkSettings = null;
                }
                else
                {
                    SinkSettings = DataService.GetSettingsAsync(_selectedSink.DisplayName, ExtensionDirection.Sink)
                        .GetAwaiter().GetResult();
                }
            }
        }
    }

    private ExtensionSettings? _sinkSettings;

    public ExtensionSettings? SinkSettings
    {
        get => _sinkSettings;
        set => SetProperty(ref _sinkSettings, value);
    }

    private ExtensionSettings? _sourceSettings;

    public ExtensionSettings? SourceSettings
    {
        get => _sourceSettings;
        set => SetProperty(ref _sourceSettings, value);
    }

    public CancellationTokenSource? CurrentExecutionAction { get; set; }
    private bool _isExecuting;

    public bool IsExecuting
    {
        get => _isExecuting;
        set
        {
            if (SetProperty(ref _isExecuting, value))
            {
                GenerateCmdLineCommand.NotifyCanExecuteChanged();
                ExportSettingsCommand.NotifyCanExecuteChanged();
                RunJobCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public IAppDataService DataService { get; }

    public IMessenger GetMessenger() => Messenger;

    private async Task Cancel()
    {
        CancelExecution(false);
    }

    public void CancelExecution(bool completed)
    {
        if (!completed)
        {
            CurrentExecutionAction?.Cancel();
        }
        CurrentExecutionAction = null;
        IsExecuting = false;
    }
}