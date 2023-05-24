using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.CognitiveSearchExtension.Settings
{
    public abstract class CognitiveSearchSettingsBase : IDataExtensionSettings
    {
        /// <summary>
        /// The Endpoint String.
        /// </summary>
        [Required]
        public string? Endpoint { get; set; }

        /// <summary>
        /// The API key String.
        /// for Sink admin key
        /// for Source admin key or query key
        /// </summary>
        [Required]
        [SensitiveValue]
        public string? ApiKey { get; set; }

        /// <summary>
        /// The Index name.
        /// </summary>
        [Required]
        public string? Index { get; set; }
    }
}