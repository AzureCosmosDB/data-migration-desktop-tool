using Azure;
using Azure.Search.Documents.Indexes;
using Cosmos.DataTransfer.CognitiveSearchExtension.Settings;
using Cosmos.DataTransfer.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Cosmos.DataTransfer.CognitiveSearchExtension
{
    [Export(typeof(IDataSourceExtension))]
    public class CognitiveSearchDataSourceExtension : IDataSourceExtensionWithSettings
    {
        public string DisplayName => "CognitiveSearch";

        public async IAsyncEnumerable<IDataItem> ReadAsync(IConfiguration config, ILogger logger, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = config.Get<CognitiveSearchDataSourceSettings>();
            settings.Validate();

            var indexClient = new SearchIndexClient(new Uri(settings.Endpoint!), new AzureKeyCredential(settings.ApiKey!));
            var searchClient = indexClient.GetSearchClient(settings.Index);

            var response = await searchClient.SearchAsync<JsonElement>("*"
                , new Azure.Search.Documents.SearchOptions()
                {
                    Filter = settings.ODataFilter
                },
                cancellationToken: cancellationToken);

            await foreach (var searchResult in response.Value.GetResultsAsync())
            {
                yield return new CognitiveSearchDataItem(searchResult.Document);
            }
        }

        public IEnumerable<IDataExtensionSettings> GetSettings()
        {
            yield return new CognitiveSearchDataSourceSettings();
        }
    }
}