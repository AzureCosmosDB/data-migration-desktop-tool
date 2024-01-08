using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cosmos.DataTransfer.PostgresqlExtension
{
    public class PostgreDataCol
    {
        public string ColumnName { get; set; }
        public Type ColumnType { get; set; }

        public NpgsqlTypes.NpgsqlDbType PostgreType { get; set; }

        public PostgreDataCol(string colname, Type coltype)
        {
            ColumnName = colname;
            ColumnType = coltype;
            PostgreType = Convert(coltype);
        }

        public Dictionary<long, object> SparseColumnData { get; } = new Dictionary<long, object>();

        
        

        public void AddColumnValue(long row, object? value)
        {
            if (value == null)
            {
                return;
            }

            SparseColumnData[row] = value;
        }

        public NpgsqlTypes.NpgsqlDbType Convert(Type coltype)
        {
            if (coltype.Name == "Missing")
            {
                return NpgsqlTypes.NpgsqlDbType.Unknown;
            }
            return coltype switch
            {
                var type when type == typeof(string) => NpgsqlTypes.NpgsqlDbType.Varchar,
                var type when type == typeof(int) => NpgsqlTypes.NpgsqlDbType.Integer,
                var type when type == typeof(long) => NpgsqlTypes.NpgsqlDbType.Bigint,
                var type when type == typeof(bool) => NpgsqlTypes.NpgsqlDbType.Boolean,
                var type when type == typeof(DateTime) => NpgsqlTypes.NpgsqlDbType.Timestamp,
                var type when type == typeof(double) => NpgsqlTypes.NpgsqlDbType.Double,
                var type when type == typeof(float) => NpgsqlTypes.NpgsqlDbType.Real,
                var type when type == typeof(decimal) => NpgsqlTypes.NpgsqlDbType.Numeric,
                var type when type == typeof(byte[]) => NpgsqlTypes.NpgsqlDbType.Bytea,
                var type when type == typeof(Guid) => NpgsqlTypes.NpgsqlDbType.Uuid,
                var type when type == typeof(char) => NpgsqlTypes.NpgsqlDbType.Char,
                var type when type == typeof(TimeSpan) => NpgsqlTypes.NpgsqlDbType.Interval,
                var type when type == typeof(DateTimeOffset) => NpgsqlTypes.NpgsqlDbType.TimestampTz,
                var type when type == typeof(short) => NpgsqlTypes.NpgsqlDbType.Smallint,
                var type when type == typeof(uint) => NpgsqlTypes.NpgsqlDbType.Integer,
                var type when type == typeof(ushort) => NpgsqlTypes.NpgsqlDbType.Smallint,
                var type when type == typeof(ulong) => NpgsqlTypes.NpgsqlDbType.Bigint,
                var type when type == typeof(sbyte) => NpgsqlTypes.NpgsqlDbType.Smallint,
                var type when type == typeof(byte) => NpgsqlTypes.NpgsqlDbType.Smallint,
                var type when type == typeof(char[]) => NpgsqlTypes.NpgsqlDbType.Text,
                var type when type == typeof(char?) => NpgsqlTypes.NpgsqlDbType.Char,                
                _ => NpgsqlTypes.NpgsqlDbType.Unknown,
            };
        }
    }
}
