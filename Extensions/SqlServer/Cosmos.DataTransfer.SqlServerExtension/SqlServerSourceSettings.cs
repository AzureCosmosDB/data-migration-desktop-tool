using System.ComponentModel.DataAnnotations;
using Cosmos.DataTransfer.Interfaces;
using Cosmos.DataTransfer.Interfaces.Manifest;
using System.Data;
using System.Data.Common;

namespace Cosmos.DataTransfer.SqlServerExtension
{
    public class SqlServerSourceSettings : IDataExtensionSettings, IValidatableObject
    {
        [SensitiveValue]
        public string? ConnectionString { get; set; }

        public string? QueryText { get; set; }

        public string? FilePath { get; set; }

        public IDictionary<string, object>? Parameters { get; set; }
        
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (String.IsNullOrWhiteSpace(this.ConnectionString)) {
                yield return new ValidationResult("The `ConnectionString` field is required.",
                    new string[] { "ConnectionString" });
            } 
            if (String.IsNullOrWhiteSpace(this.QueryText) &&
                String.IsNullOrWhiteSpace(this.FilePath)) {
                    yield return new ValidationResult(
                        "Either `QueryText` or `FilePath` are required!",
                        new string[] { "QueryText", "FilePath"});
            } else if (String.IsNullOrWhiteSpace(this.QueryText) == false &&
                String.IsNullOrWhiteSpace(this.FilePath) == false) {
                    yield return new ValidationResult(
                        "Both `QueryText` and `FilePath` are not allowed.",
                        new string[] { "QueryText", "FilePath"});
            }
            if (!String.IsNullOrWhiteSpace(this.FilePath)) {
                ValidationResult? res = null;
                try {
                    _ = File.ReadAllText(this.FilePath);
                } catch (Exception e) {
                    res = new ValidationResult("Could not read `FilePath`. Reason: \n" + e.Message,
                    new string[] { "FilePath" });
                }
                if (res is not null) yield return res;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbProviderFactory">
        ///     Use e.g., <code>Microsoft.Data.SqlClient.SqlClientFactory.Instance</code>
        ///     or <code>Microsoft.Data.Sqlite.SqliteFactory.Instance</code>.
        /// </param>
        /// <returns></returns>
        public DbParameter[] GetDbParameters(DbProviderFactory dbProviderFactory) {
            var result = new List<DbParameter>();

            if (this.Parameters is null || this.Parameters.Count == 0) {
                return Array.Empty<DbParameter>();
            }

            foreach (var param in this.Parameters) {
                var dbparam = dbProviderFactory.CreateParameter()!;
                dbparam.ParameterName = param.Key;
                if (param.Value is bool b) {
                    dbparam.DbType = DbType.Boolean;
                    dbparam.Value = b;
                } else if (param.Value is long l) {
                    dbparam.DbType = DbType.Int64;
                    dbparam.Value = l;
                 } else if (param.Value is int i) {
                    dbparam.DbType = DbType.Int32;
                    dbparam.Value = i;
                 } else if (param.Value is float f) {
                    dbparam.DbType = DbType.Single;
                    dbparam.Value = f;
                 } else if (param.Value is double d) {
                    dbparam.DbType = DbType.Double;
                    dbparam.Value = d;
                 } else {
                    dbparam.DbType = DbType.String;
                    dbparam.Value = param.Value;
                 }
                 result.Add(dbparam);
             }
             return result.ToArray();
        }

        public string GetQueryText() {
            if (!String.IsNullOrWhiteSpace(this.FilePath)) {
                return File.ReadAllText(this.FilePath);
            } 
            return this.QueryText!;
        }
    }
}
