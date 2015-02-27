using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DavidNews.Common;

namespace TestApp
{
    class Program
    {
        static void Main(string[] args)
        {
           //var items =  Redis.GetTopItems();

            Redis.ExpirePoints();

        }
    }
}
