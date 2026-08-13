using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ocuda.Ops.Models;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.PolarisHelper;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Service
{
    public class CustomerLookupService : ICustomerLookupService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly Dictionary<int, string> _patronBarcodes = new();
        private readonly IPolarisHelper _polarisHelper;

        public CustomerLookupService(ICustomerRepository customerRepository,
            IPolarisHelper polarisHelper)
        {
            _customerRepository = customerRepository
                ?? throw new ArgumentNullException(nameof(customerRepository));
            _polarisHelper = polarisHelper
                ?? throw new ArgumentNullException(nameof(polarisHelper));
        }

        public Task<DataWithCount<IList<CustomerLookup>>> GetPaginatedCustomerLookupListAsync(
            CustomerLookupFilter filter)
        {
            return _customerRepository.GetPaginatedCustomerLookupListAsync(filter);
        }

        public Task<CustomerLookup> GetCustomerLookupInfoAsync(int customerLookupID)
        {
            var customer = _polarisHelper.GetCustomerDataOverride(GetBarcode(customerLookupID));
            if (customer == null || customer.Id != customerLookupID)
            {
                return Task.FromResult<CustomerLookup>(null);
            }

            return Task.FromResult(new CustomerLookup
            {
                Barcode = customer.CustomerIdNumber,
                CustomerLookupID = customer.Id,
                LastActivityDate = customer.LastActivityDate,
                NameFirst = customer.NameFirst,
                NameLast = customer.NameLast
            });
        }

        public Task<IList<Material>> GetCustomerLookupCheckoutsAsync(int customerLookupID)
        {
            return _customerRepository.GetCustomerLookupCheckoutsAsync(customerLookupID);
        }

        public Task<int> GetCustomerLookupHistoryCountAsync(int customerLookupID)
        {
            return _customerRepository.GetCustomerLookupHistoryCountAsync(customerLookupID);
        }

        public Task<DataWithCount<IList<Material>>> GetPaginatedCustomerLookupHistoryAsync(
            MaterialFilter filter)
        {
            return _customerRepository.GetPaginatedCustomerLookupHistoryAsync(filter);
        }

        public Task<IList<Material>> GetCustomerLookupHoldsAsync(int customerLookupID)
        {
            return _customerRepository.GetCustomerLookupHoldsAsync(customerLookupID);
        }

        private string GetBarcode(int patronId)
        {
            if (_patronBarcodes.TryGetValue(patronId, out var barcode))
            {
                return barcode;
            }

            barcode = _polarisHelper.GetPatronBarcode(patronId);
            _patronBarcodes[patronId] = barcode;
            return barcode;
        }
    }
}