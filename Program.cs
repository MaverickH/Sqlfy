using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Sqlfy
{
    public static class SqlBuilder<T>
    {
        private static ColumnDefinition[] Columns { get; set; }
        private static TableDefintition Table { get; set; }

        static SqlBuilder()
        {
            var type = typeof(T);
            var sqlObject = GetSqlObject(type);
            var dbName = sqlObject != null ? sqlObject.DbName : type.Name;
            Table = new TableDefintition { DbName = dbName, PrettyName = type.Name };
            var properties = type.GetProperties();
            Columns = new ColumnDefinition[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                sqlObject = GetSqlObject(property);
                dbName = sqlObject != null ? sqlObject.DbName : property.Name;
                Columns[i] = new ColumnDefinition { DbName = dbName, PrettyName = property.Name };
            }
        }

        private static SqlObjectAttribute GetSqlObject(_MemberInfo info)
        {
            return info.GetCustomAttributes(true).OfType<SqlObjectAttribute>().FirstOrDefault();
        }

        private static string _selectAllSql;

        public static string SelectAll()
        {
            return _selectAllSql ?? (_selectAllSql = string.Format("SELECT {0} FROM {1} AS {2}", GetColumnsString(), Table.DbName, Table.PrettyName));
        }

        private static string GetColumnsString()
        {
            var column = Columns[0];
            StringBuilder builder = new StringBuilder(string.Format("{0}.{1} AS {2}", Table.PrettyName, column.DbName, column.PrettyName));
            for (int i = 1; i < Columns.Length; i++)
            {
                column = Columns[i];
                builder.AppendFormat(", {0}.{1} AS {2}", Table.PrettyName, column.DbName, column.PrettyName);
            }
            return builder.ToString();
        }
    }

    internal interface IAlias
    {
        string DbName { get; set; }
        string PrettyName { get; set; }
    }

    internal class TableDefintition : IAlias
    {
        public string DbName { get; set; }
        public string PrettyName { get; set; }
    }

    internal class ColumnDefinition : IAlias
    {
        public string DbName { get; set; }
        public string PrettyName { get; set; }
    }

    internal class SqlObjectAttribute : Attribute
    {
        public string DbName { get; set; }
        public string PrettyName { get; set; }
    }
}
