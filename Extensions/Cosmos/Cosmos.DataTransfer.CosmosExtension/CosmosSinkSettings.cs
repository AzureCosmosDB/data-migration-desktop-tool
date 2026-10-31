using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;

namespace Cosmos.DataTransfer.CosmosExtension
{
    public class CosmosSinkSettings : CosmosSettingsBase, IDataExtensionSettings
    {
        public string? PartitionKeyPath { get; set; }
        public bool RecreateContainer { get; set; }
        public int BatchSize { get; set; } = 100;
        [Range(0, int.MaxValue)]
        public int MaxRetryCount { get; set; } = 5;
        [Range(1, int.MaxValue)]
        public int InitialRetryDurationMs { get; set; } = 200;
        public int? CreatedContainerMaxThroughput { get; set; }

        /// <summary>
        /// If true, the database will be created with autoscale enabled otherwise it'll be manual.
        /// </summary>
        public bool UseAutoscaleForDatabase { get; set; } = false;

        /// <summary>
        /// If true, the container will be created with autoscale enabled otherwise it'll be manual.
        /// </summary>
        /// <remarks>
        /// Conversion from Manual to Autoscale is not supported by the SDK. In order to convert an
        /// existing container from manual to auto you will need to recreate the container or convert
        /// the container from the Azure Portal.
        /// </remarks>
        public bool UseAutoscaleForCreatedContainer { get; set; } = true;

        /// <summary>
        /// If true, the database will be created with serverless mode enabled.
        /// Autoscaling and throughput cannot be set when serverless mode is enabled.
        /// </summary>
        public bool IsServerlessAccount { get; set; } = false;

        /// <summary>
        /// If true, the container will be created with shared throughput enabled.
        /// </summary>
        public bool UseSharedThroughput { get; set; } = false;

        public bool PreserveMixedCaseIds { get; set; } = false;
        public DataWriteMode WriteMode { get; set; } = DataWriteMode.Insert;
        public bool IgnoreNullValues { get; set; } = false;
        public List<string>? PartitionKeyPaths { get; set; }
        public Dictionary<string, DataItemTransformation>? Transformations { get; set; }

        public override IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var item in base.Validate(validationContext))
            {
                yield return item;
            }

            if (RecreateContainer)
            {
                if (UseRbacAuth)
                {
                    yield return new ValidationResult("RBAC auth does not support Container creation", new[] { nameof(UseRbacAuth) });
                }

                if (MissingPartitionKeys())
                {
                    yield return new ValidationResult("PartitionKeyPath must be specified when RecreateContainer is true", new[] { nameof(PartitionKeyPath), nameof(PartitionKeyPaths) });
                }
            }

            if (IsServerlessAccount && (CreatedContainerMaxThroughput.HasValue || UseSharedThroughput))
            {
                yield return new ValidationResult("Serverless accounts cannot have shared throughput", new[] { nameof(CreatedContainerMaxThroughput), nameof(UseSharedThroughput) });
            }

            if (PartitionKeyPaths != null)
            {
                if (PartitionKeyPaths.Count is < 1 or > 3)
                {
                    yield return new ValidationResult("PartitionKeyPaths must contain between 1 and 3 paths", new[] { nameof(PartitionKeyPaths) });
                }

                if (PartitionKeyPaths.Any(p => string.IsNullOrWhiteSpace(p) || !p.StartsWith("/")))
                {
                    yield return new ValidationResult("PartitionKeyPaths values must start with /", new[] { nameof(PartitionKeyPaths) });
                }
            }
            else if (!string.IsNullOrWhiteSpace(PartitionKeyPath))
            {
                if (!PartitionKeyPath.StartsWith("/"))
                {
                    yield return new ValidationResult("PartitionKeyPath must start with /", new[] { nameof(PartitionKeyPath) });
                }
            }

            if (MissingPartitionKeys() && WriteMode is DataWriteMode.InsertStream or DataWriteMode.UpsertStream or DataWriteMode.DeleteStream or DataWriteMode.Delete)
            {
                yield return new ValidationResult("PartitionKeyPath or PartitionKeyPaths must be specified when WriteMode is set to InsertStream, UpsertStream, DeleteStream, or Delete", new[] { nameof(PartitionKeyPath), nameof(PartitionKeyPaths), nameof(WriteMode) });
            }

            if (HasInvalidTransformations())
            {
                yield return new ValidationResult("Transformations must always specify SourceFieldName and either DestinationFieldName or DestinationFieldType", new[] { nameof(Transformations) });
            }
        }

        private bool MissingPartitionKeys()
        {
            return PartitionKeyPaths != null
                ? PartitionKeyPaths.Count == 0
                : string.IsNullOrWhiteSpace(PartitionKeyPath);
        }

        private bool HasInvalidTransformations()
        {
            return Transformations != null && 
                Transformations.Any(t => string.IsNullOrEmpty(t.Value.DestinationFieldName) && string.IsNullOrEmpty(t.Value.DestinationFieldTypeCode));
        }
    }
}