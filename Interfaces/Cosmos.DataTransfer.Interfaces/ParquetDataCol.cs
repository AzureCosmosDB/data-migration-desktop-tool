using Parquet.Data;
using Parquet.Schema;

namespace Cosmos.DataTransfer.Interfaces
{
    public class ParquetDataCol
    {
        public string ColumnName { get; set; }
        public Type ColumnType { get; set; }
        public IList<object> ColumnData { get; set; }

        public DataField ParquetDataType { get; set; }

        public Parquet.Data.DataColumn ParquetDataColumn { get; set; }

        public ParquetDataCol()
        {
            ColumnType = Type.Missing.GetType();
            ColumnData = new List<object>();
        }

        public ParquetDataCol(string name, Type coltype)
        {
            ColumnName = name;
            ColumnType = coltype;
            ColumnData = new List<object>();
            if (coltype != System.Type.Missing.GetType())
            {
                ParquetDataType = MapDataType(name, coltype);
            }
        }

        private static DataField MapDataType(string colname, Type coltype)
        {
            return new DataField(colname, coltype, true);
        }
    }
}
