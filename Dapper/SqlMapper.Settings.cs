﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;

namespace Dapper
{
    public static partial class SqlMapper
    {
        /// <summary>
        /// Permits specifying certain SqlMapper values globally.
        /// </summary>
        public static class Settings
        {
            // disable single result by default; prevents errors AFTER the select being detected properly
            private const CommandBehavior DefaultAllowedCommandBehaviors = ~CommandBehavior.SingleResult;
            internal static CommandBehavior AllowedCommandBehaviors { get; private set; } = DefaultAllowedCommandBehaviors;
            private static void SetAllowedCommandBehaviors(CommandBehavior behavior, bool enabled)
            {
                if (enabled) AllowedCommandBehaviors |= behavior;
                else AllowedCommandBehaviors &= ~behavior;
            }
            /// <summary>
            /// Gets or sets whether Dapper should use the CommandBehavior.SingleResult optimization
            /// </summary>
            /// <remarks>Note that a consequence of enabling this option is that errors that happen <b>after</b> the first select may not be reported</remarks>
            public static bool UseSingleResultOptimization
            {
                get { return (AllowedCommandBehaviors & CommandBehavior.SingleResult) != 0; }
                set { SetAllowedCommandBehaviors(CommandBehavior.SingleResult, value); }
            }
            /// <summary>
            /// Gets or sets whether Dapper should use the CommandBehavior.SingleRow optimization
            /// </summary>
            /// <remarks>Note that on some DB providers this optimization can have adverse performance impact</remarks>
            public static bool UseSingleRowOptimization
            {
                get { return (AllowedCommandBehaviors & CommandBehavior.SingleRow) != 0; }
                set { SetAllowedCommandBehaviors(CommandBehavior.SingleRow, value); }
            }

            internal static bool DisableCommandBehaviorOptimizations(CommandBehavior behavior, Exception ex)
            {
                if (AllowedCommandBehaviors == DefaultAllowedCommandBehaviors
                    && (behavior & (CommandBehavior.SingleResult | CommandBehavior.SingleRow)) != 0)
                {
                    if (ex.Message.Contains(nameof(CommandBehavior.SingleResult))
                        || ex.Message.Contains(nameof(CommandBehavior.SingleRow)))
                    { // some providers just allow these, so: try again without them and stop issuing them
                        SetAllowedCommandBehaviors(CommandBehavior.SingleResult | CommandBehavior.SingleRow, false);
                        return true;
                    }
                }
                return false;
            }

            static Settings()
            {
                SetDefaults();
            }

            /// <summary>
            /// Resets all Settings to their default values
            /// </summary>
            public static void SetDefaults()
            {
                CommandTimeout = null;
                ApplyNullValues = PadListExpansions = UseIncrementalPseudoPositionalParameterNames = false;
                AllowedCommandBehaviors = DefaultAllowedCommandBehaviors;
                FetchSize = InListStringSplitCount = -1;
            }

            /// <summary>
            /// Specifies the default Command Timeout for all Queries
            /// </summary>
            public static int? CommandTimeout { get; set; }

            /// <summary>
            /// Indicates whether nulls in data are silently ignored (default) vs actively applied and assigned to members
            /// </summary>
            public static bool ApplyNullValues { get; set; }

            /// <summary>
            /// Should list expansions be padded with null-valued parameters, to prevent query-plan saturation? For example,
            /// an 'in @foo' expansion with 7, 8 or 9 values will be sent as a list of 10 values, with 3, 2 or 1 of them null.
            /// The padding size is relative to the size of the list; "next 10" under 150, "next 50" under 500,
            /// "next 100" under 1500, etc.
            /// </summary>
            /// <remarks>
            /// Caution: this should be treated with care if your DB provider (or the specific configuration) allows for null
            /// equality (aka "ansi nulls off"), as this may change the intent of your query; as such, this is disabled by 
            /// default and must be enabled.
            /// </remarks>
            public static bool PadListExpansions { get; set; }
            /// <summary>
            /// If set (non-negative), when performing in-list expansions of integer types ("where id in @ids", etc), switch to a string_split based
            /// operation if there are this many elements or more. Note that this feature requires SQL Server 2016 / compatibility level 130 (or above).
            /// </summary>
            public static int InListStringSplitCount { get; set; } = -1;

            /// <summary>
            /// If set (non-negative), when performing in-list expansions ("where id in @ids", etc), switch to TVP based
            /// operation if there are this many elements or more.
            /// If both InListStringSplitCount and InListTVPCount is set (non-negative), StringSplit will be attempted first and TVP will be attempted next.
            /// </summary>
            public static int InListTVPCount { get; set; } = -1;

            /// <summary>
            /// Types in this dictionary will be handled if InListTVPCount is set and count is reached.
            /// IdColumn specify the column name of the id column in the table type and TableType specify the table type name.
            /// You can add, remove or change handlers as you wish.
            /// 
            /// Default handlers:
            /// - Type: byte: IdColumn: "Id", "TypeName": "Dapper_Byte"
            /// - Type: short: IdColumn: "Id", "TypeName": "Dapper_Int16"
            /// - Type: int: IdColumn: "Id", "TypeName": "Dapper_Int32"
            /// - Type: long: IdColumn: "Id", "TypeName": "Dapper_Int64"
            /// - Type: Guid: IdColumn: "Id", "TypeName": "Dapper_Guid"
            /// 
            /// The types are currently not created automatically in Sql Server by Dapper, you must create them themself.
            /// Example of a table type for int, without a primary key, for full compability,
            /// as it allows the same argument to be passed more than once, just like ("where id in (1, 1, 1)"):
            /// CREATE TYPE dbo.Dapper_Int32 AS TABLE (Id int NOT NULL)
            /// For better performance, but not full compability, it can be created with a primary key:
            /// CREATE TYPE dbo.Dapper_Int32 AS TABLE (Id int NOT NULL PRIMARY KEY CLUSTERED)
            /// But note that now, a query like ("where id in (1, 1, 1)") would fail with violation of primary key exception.
            /// 
            /// Example of how to create some commonly used types:
            /// CREATE TYPE dbo.Dapper_Byte AS TABLE (Id tinyint NOT NULL)
            /// CREATE TYPE dbo.Dapper_Int16 AS TABLE (Id short NOT NULL)
            /// CREATE TYPE dbo.Dapper_Int32 AS TABLE (Id int NOT NULL)
            /// CREATE TYPE dbo.Dapper_Int64 AS TABLE (Id bigint NOT NULL)
            /// CREATE TYPE dbo.Dapper_Guid AS TABLE (Id uniqueidentifier NOT NULL)
            /// CREATE TYPE dbo.Dapper_String AS TABLE (Id nvarchar(max) NOT NULL)
            /// DateTime can be stored several ways:
            /// CREATE TYPE dbo.Dapper_DateTime AS TABLE (Id datetime NOT NULL)
            /// CREATE TYPE dbo.Dapper_DateTime AS TABLE (Id datetime2 NOT NULL)
			/// Example of how to create only if not already exist:
			/// IF TYPE_ID('Dapper_Int32') IS NULL CREATE TYPE dbo.Dapper_Int32 AS TABLE (Id int NOT NULL)
            /// </summary>
            public static readonly Dictionary<Type, InListTableType> InListTVPHandlers = DefaultHandlers();

            private static Dictionary<Type, InListTableType> DefaultHandlers()
                => new()
                {
                { typeof(byte), new() { IdColumn = "Id", TypeName = "Dapper_Byte" } },
                { typeof(short), new() { IdColumn = "Id", TypeName = "Dapper_Int16" } },
                { typeof(int), new() { IdColumn = "Id", TypeName = "Dapper_Int32" } },
                { typeof(long), new() { IdColumn = "Id", TypeName = "Dapper_Int64" } },
                { typeof(Guid), new() { IdColumn = "Id", TypeName = "Dapper_Guid" } },
                };

            /// <summary>
            /// If set, pseudo-positional parameters (i.e. ?foo?) are passed using auto-generated incremental names, i.e. "1", "2", "3"
            /// instead of the original name; for most scenarios, this is ignored since the name is redundant, but "snowflake" requires this.
            /// </summary>
            public static bool UseIncrementalPseudoPositionalParameterNames { get; set; }

            /// <summary>
            /// If assigned a non-negative value, then that value is applied to any commands <c>FetchSize</c> property, if it exists;
            /// see https://docs.oracle.com/en/database/oracle/oracle-database/18/odpnt/CommandFetchSize.html; note that this value
            /// can only be set globally - it is not intended for frequent/contextual changing.
            /// </summary>
            public static long FetchSize
            {
                get => Volatile.Read(ref s_FetchSize);
                set
                {
                    if (Volatile.Read(ref s_FetchSize) != value)
                    {
                        Volatile.Write(ref s_FetchSize, value);
                        CommandDefinition.ResetCommandInitCache(); // if this setting is useful: we've invalidated things
                    }
                }
            }

            private static long s_FetchSize = -1;
        }
    }
}
