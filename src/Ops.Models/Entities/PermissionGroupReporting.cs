using Ocuda.Ops.Models.Abstract;

namespace Ocuda.Ops.Models.Entities
{
    public class PermissionGroupReporting : PermissionGroupMappingBase
    {
        public bool CanImport { get; set; }
        public int ReportId { get; set; }
    }
}