namespace Ocuda.Models
{
    /// <summary>
    /// This class represents a Polaris Organization allowing data to be passed to different
    /// modules without requiring them to reference the Polaris project.
    /// </summary>
    public class Organization
    {
        /// <summary>
        /// Abbreviation
        /// </summary>
		public string Abbreviation { get; set; }

        /// <summary>
        /// Display name
        /// </summary>
		public string DisplayName { get; set; }

        /// <summary>
        /// Name
        /// </summary>
		public string Name { get; set; }

        /// <summary>
        /// Type of organization
        /// </summary>
		public int OrganizationCodeID { get; set; }

        /// <summary>
        /// OrganizationID
        /// </summary>
        public int OrganizationID { get; set; }

        /// <summary>
        /// Parent OrganizationID, if any
        /// </summary>
		public int? ParentOrganizationID { get; set; }

        public override string ToString()
        {
            return $"{OrganizationID} - {Abbreviation} - {Name}";
        }
    }
}