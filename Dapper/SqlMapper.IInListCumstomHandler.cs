using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace Dapper
{
    public static partial class SqlMapper
    {
        /// <summary>
        /// TODO
        /// </summary>
        public interface IInListCumstomHandler
        {
            /// <summary>
            /// TODO
            /// </summary>
            /// <param name="command"></param>
            /// <param name="namePrefix"></param>
            /// <param name="byPosition"></param>
            /// <param name="list"></param> 
            /// <returns></returns>
            bool TryHandle(IDbCommand command, string namePrefix, bool byPosition, ref IEnumerable list);
        }
    }
}
