using System;
using Npgsql;
using NUnit.Framework;

namespace Quantumart.IntegrationTests
{
    [SetUpFixture]
    public class Config
    {
        [OneTimeSetUp]
        public void SetUp()
        {
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            AppContext.SetSwitch("Npgsql.EnableStoredProcedureCompatMode", true);
        }
    }
}
