using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Sqlfy
{
    public interface ISqlBuilder
    {
        ColumnDefinition[] Columns { get; }
        TableDefintition Table { get; }
    }

    public class SqlBuilder<T> : ISqlBuilder
    {
        public ColumnDefinition[] Columns { get; private set; }
        public TableDefintition Table { get; private set; }

        private static object _lock =  new object();

        private static SqlBuilder<T> _instance;
        public static SqlBuilder<T> Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SqlBuilder<T>();
                        }
                    }
                }
                return _instance;
            }
        }

        private Dictionary<int, string> Precompiled {get;set;} 
        
        private SqlBuilder()
        {
            Precompiled = new Dictionary<int, string>();
            var type = typeof(T);
            PopulateTable(type);
            PopulateColumns(type.GetProperties());
        }

        #region Public Methods
        public string Select(string[] columnNames = null, Filter[] filters = null, Join[] joins = null)
        {
            return Select(columnNames, new[] { filters }, joins);
        }

        public string Select(string[] columnNames = null, Filter[][] filters = null, Join[] joins = null)
        {
            var columns = GetSelectedColumns(columnNames, joins);
            var hash = GetSqlHash(columns, filters, joins);
            if (Precompiled.ContainsKey(hash)) return Precompiled[hash];
            var builder = new StringBuilder("SELECT");
            GetColumnsString(builder, columns);
            builder.AppendFormat(" FROM {0} AS {1}", Table.DbName, Table.PrettyName);
            GetJoinsString(builder, joins);
            GetWhereString(builder, filters);
            return Precompiled[hash] = builder.ToString();
        }

        public ColumnDefinition GetColumn(string name)
        {
            return Columns.First(x => x.PrettyName == name);
        }
        #endregion

        #region Select Helper Methods
        private ColumnDefinition[] GetSelectedColumns(string[] columnNames = null, Join[] joins = null)
        {
            var allColumns = Columns.ToList();
            if (joins != null)
            {
                foreach (var join in joins)
                {
                    foreach (var column in join._left.Columns)
                    {
                        if (!allColumns.Contains(column))
                            allColumns.Add(column);
                    }
                    foreach (var column in join._right.Columns)
                    {
                        if (!allColumns.Contains(column))
                            allColumns.Add(column);
                    }
                }
            }
            return ((columnNames != null && columnNames.Any()) ? allColumns.Where(x => columnNames.Contains(x.PrettyName)) : allColumns).ToArray();
        }

        private void GetColumnsString(StringBuilder builder, ColumnDefinition[] columns)
        {
            var column = columns[0];
            builder.AppendFormat(" {0}.{1} AS {2}", column.Parent.PrettyName, column.DbName, column.PrettyName);
            for (int i = 1; i < columns.Length; i++)
            {
                column = columns[i];
                builder.AppendFormat(", {0}.{1} AS {2}", column.Parent.PrettyName, column.DbName, column.PrettyName);
            }
        }
        #endregion

        #region Join Clauses
        private void GetJoinsString(StringBuilder builder, Join[] joins)
        {
            if (joins != null && joins.Length > 0)
            {
                foreach (var join in joins)
                {
                    builder.AppendFormat(" {0}", join.ToString());
                }
            }
        }
        #endregion

        #region Where Clauses
        private void GetWhereString(StringBuilder builder, Filter[][] filters)
        {
            if (filters != null && filters.Length > 0)
            {
                builder.Append(" WHERE");
                GetAndList(builder, filters[0]);
                for (int i = 1; i < filters.Length; i++)
                {
                    builder.Append(" OR ");
                    GetAndList(builder, filters[i]);
                }
            }
        }

        private void GetAndList(StringBuilder builder, Filter[] filters)
        {
            builder.Append("(");
            var filter = filters[0];
            AppendSingleClause(builder, filter);
            for (int i = 1; i < filters.Length; i++)
            {
                filter = filters[i];
                builder.Append(" AND ");
                AppendSingleClause(builder, filter);
            }
            builder.Append(")");
        }

        private void AppendSingleClause(StringBuilder builder, Filter filter)
        {
            builder.AppendFormat("{0}.{1} {2} @{3}", filter.Column.Parent.PrettyName, filter.Column.DbName, filter.GetFilterTypeString(), filter.Column.PrettyName);
        }
        #endregion

        #region General Helper Methods
        private int GetSqlHash(ColumnDefinition[] columns, Filter[][] filters = null, Join[] joins = null)
        {
            unchecked
            {
                int hash = (int)2166136261;
                hash = hash * 16777619 ^ (columns ?? new ColumnDefinition[0]).GetListHashCode();
                foreach (var filterList in filters)
                    hash = hash * 16777619 ^ (filterList ?? new Filter[0]).GetListHashCode();
                hash = hash * 16777619 ^ (joins ?? new Join[0]).GetListHashCode();
                return hash;
            }
        }
        #endregion

        #region Constructor Helper Methods
        private void PopulateTable(Type type)
        {
            var table = GetTable(type);
            var dbName = table != null ? table.DbName : type.Name;
            Table = new TableDefintition { DbName = dbName, PrettyName = type.Name };
        }

        private void PopulateColumns(_PropertyInfo[] properties)
        {
            Columns = new ColumnDefinition[properties.Length];
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                var column = GetColumn(property);
                var dbName = column != null ? column.DbName : property.Name;
                Columns[i] = new ColumnDefinition { DbName = dbName, PrettyName = property.Name, Parent = Table };
            }
        }

        private TableAttribute GetTable(_MemberInfo info)
        {
            return info.GetCustomAttributes(true).OfType<TableAttribute>().FirstOrDefault();
        }

        private ColumnAttribute GetColumn(_PropertyInfo info)
        {
            return info.GetCustomAttributes(true).OfType<ColumnAttribute>().FirstOrDefault();
        }
        #endregion
    }

    internal interface IAlias
    {
        string DbName { get; set; }
        string PrettyName { get; set; }
    }

    public class TableDefintition : IAlias
    {
        public string DbName { get; set; }
        public string PrettyName { get; set; }
    }

    public class ColumnDefinition : IAlias
    {
        public string DbName { get; set; }
        public string PrettyName { get; set; }
        public TableDefintition Parent { get; set; }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                hash = hash * 16777619 ^ (DbName ?? string.Empty).GetHashCode();
                hash = hash * 16777619 ^ (PrettyName ?? string.Empty).GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            var other = obj as ColumnDefinition;
            if (other == null) return false;
            return DbName == other.DbName && PrettyName == other.PrettyName;
        }
    }

    public class ColumnAttribute : Attribute
    {
        public string DbName { get; set; }
        public AggregateType AggregateType { get; set; }
    }

    public class TableAttribute : Attribute
    {
        public string DbName { get; set; }
    }
    public static class Extensions
    {
        public static bool Equals<T>(this T[] obj, T[] other)
        {
            if (obj.Length != other.Length) return false;
            for (int i = 0; i < obj.Length; i++)
            {
                if (!obj[i].Equals(other[i]))
                    return false;
            }
            return true;
        }

        public static int GetListHashCode<T>(this T[] list)
        {
            unchecked
            {
                int hash = (int)2166136261;
                if (list == null) return hash;
                foreach (var val in list)
                {
                    hash = hash * 16777619 ^ val.GetHashCode();
                }
                return hash;
            }
        }
    }

    public class Filter
    {
        public ColumnDefinition Column { get; set; }
        public object Value { get; set; }
        public FilterType FilterType { get; set; }

        public Filter(ColumnDefinition column, FilterType filter, object value)
        {
            if (column == null) throw new Exception();
            Column = column;
            Value = value;
            FilterType = filter;
        }

        public string GetFilterTypeString()
        {
            switch(FilterType)
            {
                case FilterType.EqualTo:
                    return "=";
                case FilterType.GreaterThan:
                    return ">";
                case FilterType.GreaterThanOrEqualTo:
                    return ">=";
                case FilterType.LessThan:
                    return "<";
                case FilterType.LessThanOrEqualTo:
                    return "<=";
                case FilterType.Like:
                    return "LIKE";
                default:
                    throw new Exception();

            }
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                hash = hash * 16777619 ^ Column.GetHashCode();
                if(Value != null)
                    hash = hash * 16777619 ^ Value.GetHashCode();
                hash = hash * 16777619 ^ ((int)FilterType).GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            var filter = obj as Filter;
            if (filter == null) return false;
            if (!Column.Equals(Column)) return false;
            if (!Value.Equals(filter.Value)) return false;
            if (!FilterType.Equals(filter.FilterType)) return false;
            return true;
        }
        
    }

    public class On
    {
        public string LeftColumn {get;set;}
        public string RightColumn { get; set; }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                hash = hash * 16777619 ^ (LeftColumn ?? string.Empty).GetHashCode();
                hash = hash * 16777619 ^ (RightColumn ?? string.Empty).GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            var on = obj as On;
            if (obj == null) return false;
            if (!LeftColumn.Equals(on.LeftColumn)) return false;
            if (!RightColumn.Equals(on.RightColumn)) return false;
            return true;
        }
    }

    public class Join
    {
        public JoinType _joinType { get; private set; }
        public ISqlBuilder _left { get; private set; }
        public ISqlBuilder _right { get; private set; }
        public On[] _clauses { get; private set; }

        public Join(ISqlBuilder left, ISqlBuilder right, JoinType joinType, On[] clauses)
        {
            _joinType = joinType;
            _left = left;
            _right = right;
            _clauses = clauses;
        }

        public override string ToString()
        {
 	        return string.Format("{0} JOIN {1} AS {2}{3}", _joinType.ToString().ToUpper(), _right.Table.DbName, _right.Table.PrettyName, OnClause());
        }

        private string OnClause()
        {
            StringBuilder builder = new StringBuilder();
            var clause = _clauses[0];
            builder.AppendFormat(" ON {0}.{1} = {2}.{3}", _left.Table.PrettyName, clause.LeftColumn, _right.Table.PrettyName, clause.RightColumn);
            for(int i = 1; i < _clauses.Length; i++)
            {
                builder.AppendFormat(" AND {0}.{1} = {2}.{3}", _left.Table.PrettyName, clause.LeftColumn, _right.Table.PrettyName, clause.RightColumn);
            }
            return builder.ToString();
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                hash = hash * 16777619 ^ (_clauses ?? new On[0]).GetListHashCode();
                hash = hash * 16777619 ^ _right.GetHashCode();
                hash = hash * 16777619 ^ _left.GetHashCode();
                hash = hash * 16777619 ^ ((int)_joinType).GetHashCode();
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            var join = obj as Join;
            if (obj == null) return false;
            if (!_left.Equals(join._left)) return false;
            if (!_right.Equals(join._right)) return false;
            if (!_clauses.Equals(join._clauses)) return false;
            if (!_joinType.Equals(join._joinType)) return false;
            return true;
        }
    }

    #region Enums
    public enum JoinType
    {
        Left,
        Right,
        Inner,
        Full
    }

    public enum FilterType
    {
        EqualTo,
        LessThan,
        LessThanOrEqualTo,
        GreaterThan,
        GreaterThanOrEqualTo,
        Like,
    }

    public enum AggregateType
    {
        None,
        Average,
        Max,
        Min,
        Count
    }
    #endregion
}
