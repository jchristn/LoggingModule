namespace SyslogLogging.Tests.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    using SyslogLogging.Tests.Shared;

    [TestFixture]
    public sealed class LoggingModuleNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(LoggingModuleSuites.All);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
