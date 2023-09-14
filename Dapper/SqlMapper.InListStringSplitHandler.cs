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

        private class InListStringSplitHandler : IInListCumstomHandler
        {
            public bool TryHandle(IDbCommand command, string namePrefix, bool byPosition, ref IEnumerable list)
            {
                int splitAt = SqlMapper.Settings.InListStringSplitCount;
                return splitAt >= 0 && TryStringSplit(ref list, splitAt, namePrefix, command, byPosition);
            }

            private static bool TryStringSplit(ref IEnumerable list, int splitAt, string namePrefix, IDbCommand command, bool byPosition)
            {
                if (list == null || splitAt < 0) return false;
                return list switch
                {
                    IEnumerable<int> l => TryStringSplit(ref l, splitAt, namePrefix, command, "int", byPosition, (sb, i) => sb.Append(i.ToString(CultureInfo.InvariantCulture))),
                    IEnumerable<long> l => TryStringSplit(ref l, splitAt, namePrefix, command, "bigint", byPosition, (sb, i) => sb.Append(i.ToString(CultureInfo.InvariantCulture))),
                    IEnumerable<short> l => TryStringSplit(ref l, splitAt, namePrefix, command, "smallint", byPosition, (sb, i) => sb.Append(i.ToString(CultureInfo.InvariantCulture))),
                    IEnumerable<byte> l => TryStringSplit(ref l, splitAt, namePrefix, command, "tinyint", byPosition, (sb, i) => sb.Append(i.ToString(CultureInfo.InvariantCulture))),
                    _ => false,
                };
            }

            private static bool TryStringSplit<T>(ref IEnumerable<T> list, int splitAt, string namePrefix, IDbCommand command, string colType, bool byPosition,
                Action<StringBuilder, T> append)
            {
                if (list is not ICollection<T> typed)
                {
                    typed = list.ToList();
                    list = typed; // because we still need to be able to iterate it, even if we fail here
                }
                if (typed.Count < splitAt) return false;

                string varName = null;
                var regexIncludingUnknown = GetInListRegex(namePrefix, byPosition);
                var sql = Regex.Replace(command.CommandText, regexIncludingUnknown, match =>
                {
                    var variableName = match.Groups[1].Value;
                    if (match.Groups[2].Success)
                    {
                        // looks like an optimize hint; leave it alone!
                        return match.Value;
                    }
                    else
                    {
                        varName = variableName;
                        return "(select cast([value] as " + colType + ") from string_split(" + variableName + ",','))";
                    }
                }, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);
                if (varName == null) return false; // couldn't resolve the var!

                command.CommandText = sql;
                var concatenatedParam = command.CreateParameter();
                concatenatedParam.ParameterName = namePrefix;
                concatenatedParam.DbType = DbType.AnsiString;
                concatenatedParam.Size = -1;
                string val;
                using (var iter = typed.GetEnumerator())
                {
                    if (iter.MoveNext())
                    {
                        var sb = GetStringBuilder();
                        append(sb, iter.Current);
                        while (iter.MoveNext())
                        {
                            append(sb.Append(','), iter.Current);
                        }
                        val = sb.ToString();
                    }
                    else
                    {
                        val = "";
                    }
                }
                concatenatedParam.Value = val;
                command.Parameters.Add(concatenatedParam);
                return true;
            }
        }
    }
}
