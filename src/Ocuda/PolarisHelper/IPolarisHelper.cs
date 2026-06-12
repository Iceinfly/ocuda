using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Models;
using Ocuda.PolarisHelper.Models;

namespace Ocuda.PolarisHelper
{
    public interface IPolarisHelper
    {
        bool IsConfigured { get; }

        bool AuthenticateCustomer(string barcode, string password);

        CreateRegistrationResult CreateCustomerRegistration(Customer customer);

        Task<List<CustomerBlock>> GetCustomerBlocksAsync(int customerId);

        Task<string> GetCustomerCodeNameAsync(int customerCodeId);

        Customer GetCustomerData(string barcode, string password);

        Customer GetCustomerDataOverride(string barcode);

        /// <summary>
        /// Provided with a barcode, map it as a "former" id and, if it is found as such, return the
        /// organization of the current user.
        /// </summary>
        /// <remarks>This method uses direct database access via SQL query.</remarks>
        /// <param name="formerBarcode">A customer's "former" barcode as recorded in Polaris</param>
        /// <returns>The organization id associated with the user if one is found, otherwise it
        /// returns null.
        /// </returns>
        Task<int?> GetOrganizationIdFormerDirect(string formerBarcode);

        /// <summary>
        /// Look up associated organization ids for a provided list of enumerable barcodes in a
        /// batch. If the barcode is not found then it will be omitted from the return dictionary
        /// so it is necessary to verify the submitted barcodes are all returned.
        /// </summary>
        /// <remarks>This method uses direct database access via SQL query.</remarks>
        /// <param name="barcodes">An enumeration of barcodes to look up</param>
        /// <returns>A dictionary keyed by the barcode with the value being the organization id.
        /// </returns>
        Task<IDictionary<string, int>> GetOrganizationIdsBatchDirect(IEnumerable<string> barcodes);

        /// <summary>
        /// Return a list of Polaris organizations.
        /// </summary>
        /// <remarks>This method uses public Polaris API access.</remarks>
        /// <returns>An enumerable of populated Organization objects</returns>
        IEnumerable<Organization> GetOrganizations();

        RenewRegistrationResult RenewCustomerRegistration(string barcode, string email);
    }
}