using System;
using NUnit.Framework;

namespace Quantumart.IntegrationTests.Infrastructure
{
    internal static class TestEnvironmentHelpers
    {
        private const string CiDbNameParamPrefix = "qp8_test_ci_";

        private static readonly string CiDbNameParam = $"{CiDbNameParamPrefix}dbname";

        private static readonly string CiLocalDbName = $"{CiDbNameParamPrefix}{Environment.MachineName.ToLowerInvariant()}";

        internal static string GetSqlDbNameToRunTests => TestContext.Parameters.Get(CiDbNameParam, CiLocalDbName);
    }
}
