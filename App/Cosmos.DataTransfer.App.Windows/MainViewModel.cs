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
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System.Windows.Threading;

namespace Cosmos.DataTransfer.App.Windows;

public class MainViewModel : ViewModelBase
{
    private readonly IAppDataService _appDataService;

    public MainViewModel()
    {
        var appSettings = App.Current.Settings;
        if (appSettings == null)
        {
            throw new InvalidOperationException();
        }

        _appDataService = new WpfAppDataService(appSettings);
        
        Initialize();

        if (File.Exists(appSettings.CoreAppPath))
            Messenger.Log(new LogMessage($"Using DMT application at path '{appSettings.CoreAppPath}'."));
        else
            Messenger.Log(LogMessage.Error($"DMT application not found. Attempted to use path '{appSettings.CoreAppPath}'."));
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
                    SourceSettings = _appDataService.GetSettingsAsync(_selectedSource.DisplayName, ExtensionDirection.Source)
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
                    SinkSettings = _appDataService.GetSettingsAsync(_selectedSink.DisplayName, ExtensionDirection.Sink)
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

    private async void Initialize()
    {
        var extensions = await _appDataService.GetExtensionsAsync();
        Sources.AddRange(extensions.Sources);
        Sinks.AddRange(extensions.Sinks);
    }
}
