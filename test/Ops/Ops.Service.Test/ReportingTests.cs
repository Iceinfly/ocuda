using Ocuda.Ops.Models.Definitions;

namespace Ops.Service.Test
{
    public class ReportingTests
    {
        [Fact]
        public void VerifyUniqueIds()
        {
            foreach (var reportId in ReportDefinitions.Definitions.Select(_ => _.Id))
            {
                Assert.NotNull(ReportDefinitions
                    .Definitions
                    .SingleOrDefault(_ => _.Id == reportId));
            }
        }

        [Fact]
        public void VerifyUniqueInternalIds()
        {
            IList<int> internalIds = [];
            foreach (var report in ReportDefinitions.Definitions)
            {
                Assert.DoesNotContain(report.InternalId, internalIds);
                internalIds.Add(report.InternalId);
            }
        }
    }
}