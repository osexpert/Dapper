namespace Dapper
{
    public static partial class SqlMapper
    {
        /// <summary>
        /// Represent user defined table type used for InList
        /// </summary>
        public class InListTableType
        {
            /// <summary>
            /// Name of Id column of the table type
            /// </summary>
            public string IdColumn { get; set; }

            /// <summary>
            /// Table type name
            /// </summary>
            public string TypeName { get; set; }
        }
    }
}
