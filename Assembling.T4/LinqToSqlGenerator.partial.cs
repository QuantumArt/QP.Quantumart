﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quantumart.QP8.Assembling.T4
{
    public partial class LinqToSqlGenerator
    {
        public LinqToSqlGenerator(string dbmlPath, string ns, bool generateDbAttributes )
        {
            DbmlPath = dbmlPath;
            Namespace = ns;
            GenerateDbAttributes = generateDbAttributes;
        }

        public string DbmlPath { get; set; }

        public string Namespace { get; set; }

        public bool GenerateDbAttributes { get; set; }

    }
}
