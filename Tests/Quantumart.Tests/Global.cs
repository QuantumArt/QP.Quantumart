using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quantumart.QPublishing.Info;

namespace Quantumart.Tests
{
    internal class Global
    {
        public static string ConnectionString => $"Initial Catalog=mts_rf_qp;Data Source=mscsql01;Integrated Security=True;Application Name=UnitTest";

        public static int SiteId = 52;

    }
}

