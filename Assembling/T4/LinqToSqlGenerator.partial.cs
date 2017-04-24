using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quantumart.QP8.Assembling.T4
{
    public partial class LinqToSqlGenerator
    {
        public LinqToSqlGenerator(string dbmlPath)
        {
            DbmlPath = dbmlPath;
        }

        public string DbmlPath { get; set; }

    }
}
