namespace Cosmos.DataTransfer.Interfaces
{
    public interface IExtensionWithSettings
    {
        IEnumerable<IDataExtensionSettings> GetSettings();
    }
}
