using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sqlfy.Example
{
    public class Example
    {
        public static void Main(string[] args)
        {
            var sql = SqlBuilder<Product>.SelectAll();
            Console.WriteLine(sql);
            Console.ReadLine();
        }
    }

    [SqlObject(DbName = "ForgeRock")]
    public class Product
    {
        [SqlObject(DbName = "productName")]
        public string Name { get; set; }
        [SqlObject(DbName = "description")]
        public string Description { get; set; }
    }
}
