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
            ColumnType = coltype;
            if (coltype != System.Type.Missing.GetType())
            {
                ParquetDataType = MapDataType(name, coltype);
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

            SparseColumnData[row] = value;
        }
    }
}
