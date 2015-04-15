using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Sqlfy.Example
{
    public class Example
    {
        public static void Main(string[] args)
        {
            Stopwatch watch = Stopwatch.StartNew();
            var authorSql = SqlBuilder<Author>.Instance;
            var bookSql = SqlBuilder<Book>.Instance;
            var filters = new[] 
            { 
                new Filter(authorSql.GetColumn("LastName"), FilterType.EqualTo, "TWAIN"),
                new Filter(bookSql.GetColumn("Title"), FilterType.Like, "%FINN%"),
            };
            var joins = new[] 
                        {
                            new Join(authorSql, 
                            bookSql, 
                            JoinType.Left, 
                            new [] {new On{ LeftColumn = "productName", RightColumn = "ProductID"}})
                        };
            var sql = SqlBuilder<Author>.Instance.Select(filters: filters, joins: joins);
            Console.WriteLine(sql);
            Console.ReadLine();
        }
    }

    [SqlObject(DbName = "DBAUTH")]
    public class Author
    {
        [SqlObject(DbName = "ID")]
        public string AuthorID { get; set; }
        [SqlObject(DbName = "FNAM")]
        public string FirstName { get; set; }
        [SqlObject(DbName = "LNAM")]
        public string LastName { get; set; }
    }

    [SqlObject(DbName = "DBBKS")]
    public class Book
    {
        [SqlObject(DbName = "ID")]
        public string BookID { get; set; }
        [SqlObject(DbName = "AUTHID")]
        public string AuthorFK { get; set; }
        [SqlObject(DbName = "PGCNT")]
        public string PageCount { get; set; }
        [SqlObject(DbName = "PUBL")]
        public string Publisher { get; set; }
        public string ReleaseDate { get; set; }
        public string Title { get; set; }
    }
}
