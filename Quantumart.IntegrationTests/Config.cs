using System;
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
        }
    }
}
