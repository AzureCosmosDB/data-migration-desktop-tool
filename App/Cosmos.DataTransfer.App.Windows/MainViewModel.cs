using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using Cosmos.DataTransfer.App.Windows.Framework;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Cosmos.DataTransfer.Ui.Common;
using System.Diagnostics.Metrics;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace Cosmos.DataTransfer.App.Windows;

public class MainViewModel : ViewModelBase
{
    private readonly IAppDataService _dataService;

    public MainViewModel()
    {
        GenerateCmdLineCommand = new RelayCommand(GenerateCmdLine);
        ExportSettingsCommand = new RelayCommand(ExportSettings);
        RunJobCommand = new RelayCommand(RunJob);
        CancelCommand = new RelayCommand(Cancel);

        var appSettings = App.Current.Settings;
        if (appSettings == null)
        {
            throw new InvalidOperationException();
        }

        _dataService = new WpfAppDataService(appSettings);

        Initialize();

        if (File.Exists(appSettings.CoreAppPath))
            Messenger.Log(new LogMessage($"Using DMT application at path '{appSettings.CoreAppPath}'."));
        else
            Messenger.Log(LogMessage.Error($"DMT application not found. Attempted to use path '{appSettings.CoreAppPath}'."));
    }

    private async void Initialize()
    {
        var extensions = await _dataService.GetExtensionsAsync();
        Sources.AddRange(extensions.Sources);
        Sinks.AddRange(extensions.Sinks);
    }

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
                    SourceSettings = _dataService.GetSettingsAsync(_selectedSource.DisplayName, ExtensionDirection.Source)
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
                    SinkSettings = _dataService.GetSettingsAsync(_selectedSink.DisplayName, ExtensionDirection.Sink)
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
    private bool _isExecuting;

    public ExtensionSettings? SourceSettings
    {
        get => _sourceSettings;
        set => SetProperty(ref _sourceSettings, value);
    }

    public ICommand ExportSettingsCommand { get; }

    private void ExportSettings()
    {
        CurrentExecutionAction = new CancellationTokenSource();
        IsExecuting = true;

        ThenReset(ExecuteExportSettings(CurrentExecutionAction.Token));
    }

    private async Task ExecuteExportSettings(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = await _dataService.BuildSettingsAsync(SelectedSource?.DisplayName ?? throw new InvalidOperationException("No Source selected"),
                SelectedSink?.DisplayName ?? throw new InvalidOperationException("No Sink selected"),
                SourceSettings?.Settings,
                SinkSettings?.Settings);

            Messenger.Log(LogMessage.Data(output));
        }
        catch (Exception ex)
        {
            Messenger.Log(LogMessage.Error(ex.Message));
        }
    }

    public ICommand RunJobCommand { get; }

    private void RunJob()
    {
        CurrentExecutionAction = new CancellationTokenSource();
        IsExecuting = true;

        ThenReset(ExecuteRunJob(CurrentExecutionAction.Token));
    }

    private async Task ExecuteRunJob(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool completed = await _dataService.ExecuteWithSettingsAsync(SelectedSource?.DisplayName ?? throw new InvalidOperationException("No Source selected"),
                SelectedSink?.DisplayName ?? throw new InvalidOperationException("No Sink selected"),
                SourceSettings?.Settings,
                SinkSettings?.Settings,
                async m => Messenger.Log(m),
                cancellationToken);
        }
        catch (Exception ex)
        {
            Messenger.Log(LogMessage.Error(ex.Message));
        }
    }

    public ICommand CancelCommand { get; }

    private async void Cancel()
    {
        await CancelExecution(false);
    }

    public ICommand GenerateCmdLineCommand { get; }

    private void GenerateCmdLine()
    {
        CurrentExecutionAction = new CancellationTokenSource();
        IsExecuting = true;

        ThenReset(ExecuteGenerateCmdLine(CurrentExecutionAction.Token));
    }

    public void ThenReset(Task task)
    {
        task.ContinueWith(t =>
        {
            CancelExecution(true);
        });
    }

    protected async Task CancelExecution(bool Completed)
    {
        if (!Completed)
        {
            CurrentExecutionAction?.Cancel();
        }
        CurrentExecutionAction = null;
        IsExecuting = false;
    }

    public CancellationTokenSource? CurrentExecutionAction { get; set; }

    public bool IsExecuting
    {
        get => _isExecuting;
        set => SetProperty(ref _isExecuting, value);
    }

    private async Task ExecuteGenerateCmdLine(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = await _dataService.BuildCommandAsync(SelectedSource?.DisplayName ?? throw new InvalidOperationException("No Source selected"),
                SelectedSink?.DisplayName ?? throw new InvalidOperationException("No Sink selected"),
                SourceSettings?.Settings,
                SinkSettings?.Settings);
            Messenger.Log(LogMessage.Data(output));
        }
        catch (Exception ex)
        {
            Messenger.Log(LogMessage.Error(ex.Message));
        }
    }
}
