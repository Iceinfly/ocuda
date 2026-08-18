using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ocuda.Ops.Models;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.Ops.Service.Interfaces.Ops.Services;
using Ocuda.PolarisHelper;
using Ocuda.Utility.Exceptions;
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

        public Task<CustomerLookup> GetCustomerLookupInfoAsync(int customerLookupID, string barcode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(barcode);

            var customer = _polarisHelper.GetCustomerDataOverride(barcode);
            if (customer == null || customer.Id != customerLookupID)
            {
                return Task.FromResult<CustomerLookup>(null);
            }

            _patronBarcodes[customerLookupID] = barcode;

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
            var checkouts = _polarisHelper.GetPatronItemsOut(GetBarcode(customerLookupID));
            var categories = checkouts
                .Where(_ => _.BibId > 0)
                .Select(_ => _.BibId)
                .Distinct()
                .ToDictionary(_ => _, _ => _polarisHelper.GetBibGenre(_));

            IList<Material> materials = checkouts.Select(_ => new Material
            {
                Author = _.Author,
                Category = _.BibId > 0 && categories.TryGetValue(_.BibId, out var category)
                    ? category
                    : null,
                DueDate = _.DueDate,
                Title = _.Title
            }).ToList();

            return Task.FromResult(materials);
        }

        public Task<int> GetCustomerLookupHistoryCountAsync(int customerLookupID)
        {
            return Task.FromResult(_polarisHelper
                .GetPatronReadingHistoryCount(GetBarcode(customerLookupID)));
        }

        public Task<DataWithCount<IList<Material>>> GetPaginatedCustomerLookupHistoryAsync(
            MaterialFilter filter)
        {
            return _customerRepository.GetPaginatedCustomerLookupHistoryAsync(filter);
        }

        public Task<IList<Material>> GetCustomerLookupHoldsAsync(int customerLookupID)
        {
            IList<Material> materials = _polarisHelper
                .GetPatronHolds(GetBarcode(customerLookupID))
                .Select(_ => new Material
                {
                    Author = _.Author,
                    HoldStatus = _.HoldStatus,
                    Title = _.Title
                })
                .ToList();

            return Task.FromResult(materials);
        }

        private string GetBarcode(int patronId)
        {
            if (_patronBarcodes.TryGetValue(patronId, out var barcode))
            {
                return barcode;
            }

            throw new OcudaException($"No barcode is available for Polaris patron {patronId}");
        }
    }
}