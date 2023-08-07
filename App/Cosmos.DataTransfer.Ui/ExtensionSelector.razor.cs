using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using Cosmos.DataTransfer.Ui.Common;
using Microsoft.AspNetCore.Components;

namespace Cosmos.DataTransfer.Ui
{
    public partial class ExtensionSelector
    {
        private string? _selectedExtension;

        [Parameter]
        public ExtensionDirection Direction { get; set; }

        [Parameter]
        public IEnumerable<ExtensionDefinition>? AvailableExtensions { get; set; }
        
        [Parameter]
        public EventCallback<string> OnExtensionSelected { get; set; }

        public string? SelectedExtension
        {
            get => _selectedExtension;
            set
            {
                if (string.Equals(_selectedExtension, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _selectedExtension = value;

                OnExtensionSelected.InvokeAsync(value); 
            }
        }
    }
}