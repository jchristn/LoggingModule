namespace SyslogLogging.Tests.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;
    using Touchstone.XunitAdapter;

    using SyslogLogging.Tests.Shared;

    public sealed class LoggingModuleTheoryTests
    {
        public static TouchstoneTheoryData TestCases()
        {
            return new TouchstoneTheoryData(LoggingModuleSuites.All);
        }

        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
