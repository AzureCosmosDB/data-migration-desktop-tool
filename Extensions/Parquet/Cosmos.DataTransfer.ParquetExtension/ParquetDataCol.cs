using Parquet.Schema;

namespace Cosmos.DataTransfer.ParquetExtension
{
    public class ParquetDataCol
    {
        public string ColumnName { get; set; }
        public Type ColumnType { get; set; }
        public IEnumerable<object?> ColumnData
        {
            get
            {
                var maxRow = SparseColumnData.Keys.Max();
                for (long i = 0; i <= maxRow; i++)
                {
                    yield return SparseColumnData.TryGetValue(i, out var value) ? value : null;
                }
            }
        }

        public Dictionary<long, object> SparseColumnData { get; } = new Dictionary<long, object>();

        public DataField ParquetDataType { get; set; }

        public Parquet.Data.DataColumn ParquetDataColumn { get; set; }

        public ParquetDataCol()
        {
            ColumnType = Type.Missing.GetType();
        }

        public ParquetDataCol(string name, Type coltype)
        {
            ColumnName = name;
            
            // Convert DateTimeOffset to DateTime for Parquet compatibility
            if (coltype == typeof(DateTimeOffset) || coltype == typeof(DateTimeOffset?))
            {
                ColumnType = typeof(DateTime);
                ParquetDataType = MapDataType(name, typeof(DateTime));
            }
            else
            {
                ColumnType = coltype;
                if (coltype != System.Type.Missing.GetType())
                {
                    ParquetDataType = MapDataType(name, coltype);
                }
            }
        }

        private static DataField MapDataType(string colname, Type coltype)
        {
            return new DataField(colname, coltype, true);
        }

        public void AddColumnValue(long row, object? value)
        {
            if (value == null)
            {
                return;
            }

            // Convert DateTimeOffset to DateTime for Parquet compatibility
            if (value is DateTimeOffset dateTimeOffset)
            {
                SparseColumnData[row] = dateTimeOffset.DateTime;
            }
            else
            {
                SparseColumnData[row] = value;
            }
        }
    }
}
