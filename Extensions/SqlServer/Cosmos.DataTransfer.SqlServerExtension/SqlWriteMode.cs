namespace Cosmos.DataTransfer.SqlServerExtension
{
    /// <summary>
    /// Defines the behavior when writing data to SQL Server.
    /// </summary>
    public enum SqlWriteMode
    {
        /// <summary>
        /// Inserts new records only using bulk insert. This is the default behavior.
        /// </summary>
        Insert,
        
        /// <summary>
        /// Uses SQL MERGE to insert new records or update existing ones based on primary key columns.
        /// When matched: updates all non-key columns with source values.
        /// When not matched: inserts new records.
        /// Requires PrimaryKeyColumns to be specified.
        /// </summary>
        Upsert
    }
}
