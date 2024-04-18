using Microsoft.VisualBasic;
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

        public PostgreDataCol(string colname, NpgsqlTypes.NpgsqlDbType postgreType)
        {
            ColumnName = colname;            
            PostgreType = postgreType;
            ColumnType = Convert(postgreType);
        }

        public PostgreDataCol(string colname, string postgredatatye)
        {
            ColumnName = colname;            
            PostgreType = Convert(postgredatatye);
            ColumnType = Convert(PostgreType);
        }

        public PostgreDataCol()
        {
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

        public Type Convert(NpgsqlTypes.NpgsqlDbType coltype)
        {
            return coltype switch
            {
                NpgsqlTypes.NpgsqlDbType.Varchar => typeof(string),
                NpgsqlTypes.NpgsqlDbType.Integer => typeof(int),
                NpgsqlTypes.NpgsqlDbType.Bigint => typeof(long),
                NpgsqlTypes.NpgsqlDbType.Boolean => typeof(bool),
                NpgsqlTypes.NpgsqlDbType.Timestamp => typeof(DateTime),
                NpgsqlTypes.NpgsqlDbType.Double => typeof(double),
                NpgsqlTypes.NpgsqlDbType.Real => typeof(float),
                NpgsqlTypes.NpgsqlDbType.Numeric => typeof(decimal),
                NpgsqlTypes.NpgsqlDbType.Bytea => typeof(byte[]),
                NpgsqlTypes.NpgsqlDbType.Uuid => typeof(Guid),
                NpgsqlTypes.NpgsqlDbType.Char => typeof(char),
                NpgsqlTypes.NpgsqlDbType.Interval => typeof(TimeSpan),
                NpgsqlTypes.NpgsqlDbType.TimestampTz => typeof(DateTimeOffset),
                NpgsqlTypes.NpgsqlDbType.Smallint => typeof(short),
                NpgsqlTypes.NpgsqlDbType.Unknown => typeof(DBNull),
                _ => typeof(DBNull),
            };
        }

        public NpgsqlTypes.NpgsqlDbType Convert(string postgredattype)
        {
            return postgredattype.ToLower() switch
            {
                "varchar" =>NpgsqlTypes.NpgsqlDbType.Varchar,
                "int8" => NpgsqlTypes.NpgsqlDbType.Bigint,
                "int4" => NpgsqlTypes.NpgsqlDbType.Integer,
                "int2" => NpgsqlTypes.NpgsqlDbType.Smallint,
                "bool" => NpgsqlTypes.NpgsqlDbType.Boolean,
                "timestamp" => NpgsqlTypes.NpgsqlDbType.Timestamp,
                "timestamptz" => NpgsqlTypes.NpgsqlDbType.TimestampTz,
                "float8" => NpgsqlTypes.NpgsqlDbType.Double,
                "float4" => NpgsqlTypes.NpgsqlDbType.Real,
                "numeric" => NpgsqlTypes.NpgsqlDbType.Numeric,
                "bytea" => NpgsqlTypes.NpgsqlDbType.Bytea,
                "char" => NpgsqlTypes.NpgsqlDbType.Char,
                "interval" => NpgsqlTypes.NpgsqlDbType.Interval,
                "int2vector"=> NpgsqlTypes.NpgsqlDbType.Array,
                "jsonb" => NpgsqlTypes.NpgsqlDbType.Jsonb,
                "name" => NpgsqlTypes.NpgsqlDbType.Name,
                "oid" => NpgsqlTypes.NpgsqlDbType.Oid,
                "text" => NpgsqlTypes.NpgsqlDbType.Text,
                "unknown" =>NpgsqlTypes.NpgsqlDbType.Unknown,
                _ => NpgsqlTypes.NpgsqlDbType.Unknown,
            };
        }

        public NpgsqlTypes.NpgsqlDbType Convert(Type coltype)
        {
            if (coltype.Name == "Missing")
            {
                return //NpgsqlTypes.NpgsqlDbType.Varchar;
                    NpgsqlTypes.NpgsqlDbType.Unknown;
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
