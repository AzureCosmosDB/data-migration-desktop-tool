using System.ComponentModel.DataAnnotations;

namespace Cosmos.DataTransfer.Interfaces;

public static class ValidationExtensions
{
    public static IEnumerable<string?> GetValidationErrors<T>(this T? settings)
        where T : class, IDataExtensionSettings, new()
    {
        if (settings == null)
        {
            yield return $"Missing settings of type {typeof(T).Name}";
            settings = new T();
        }

        var context = new ValidationContext(settings, serviceProvider: null, items: null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(settings, context, results, true);
        foreach (var validationResult in results)
        {
            yield return validationResult.ErrorMessage;
        }
    }

    public static void Validate<T>(this T? settings) 
        where T : class, IDataExtensionSettings, new()
    {
        var validationErrors = settings.GetValidationErrors().ToList();
        if (validationErrors.Any())
        {
            throw new AggregateException($"Configuration for {typeof(T).Name} is invalid", validationErrors.Select(s => new Exception(s)));
        }
    }
}
