using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ocuda.Ops.Data.ServiceFacade;
using Ocuda.Ops.Models;
using Ocuda.Ops.Models.Entities;
using Ocuda.Ops.Service.Filters;
using Ocuda.Ops.Service.Interfaces.Ops.Repositories;
using Ocuda.PolarisHelper;
using Ocuda.Utility.Exceptions;
using Ocuda.Utility.Models;

namespace Ocuda.Ops.Data.Ops
{
    public class CustomerLookupRepository :
        OpsRepository<OpsContext, BooksByMailCustomer, int>,
        ICustomerRepository
    {
        private const int BooksByMailPatronCodeId = 5;
        private readonly PolarisContext _polarisContext;

        public CustomerLookupRepository(Repository<OpsContext> repositoryFacade,
            PolarisContext polarisContext,
            ILogger<CustomerLookupRepository> logger) : base(repositoryFacade, logger)
        {
            ArgumentNullException.ThrowIfNull(polarisContext);
            _polarisContext = polarisContext;
        }

        public async Task<DataWithCount<IList<CustomerLookup>>>
            GetPaginatedCustomerLookupListAsync(CustomerLookupFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);

            try
            {
                IQueryable<CustomerLookupRow> query = _polarisContext.Database
                    .SqlQuery<CustomerLookupRow>($@"SELECT PT.PatronID AS CustomerLookupID,
                        PT.Barcode,
                        PT.LastActivityDate,
                        PTR.NameFirst,
                        PTR.NameLast
                    FROM Polaris.Patrons AS PT WITH (NOLOCK)
                    INNER JOIN Polaris.PatronRegistration AS PTR WITH (NOLOCK)
                        ON PT.PatronID = PTR.PatronID
                    WHERE PT.PatronCodeID = {BooksByMailPatronCodeId}");

                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    query = query.Where(_ =>
                        (((_.NameFirst ?? string.Empty) + " " + (_.NameLast ?? string.Empty))
                            .Contains(filter.Search))
                        || (_.Barcode ?? string.Empty).Contains(filter.Search));
                }

                var count = await query.CountAsync();
                var orderedQuery = OrderCustomers(query, filter.OrderBy, filter.OrderDesc);
                var rows = await orderedQuery
                    .Skip(filter.Skip.GetValueOrDefault())
                    .Take(filter.Take.GetValueOrDefault(15))
                    .ToListAsync();

                return new DataWithCount<IList<CustomerLookup>>
                {
                    Count = count,
                    Data = rows.Select(_ => new CustomerLookup
                    {
                        Barcode = _.Barcode,
                        CustomerLookupID = _.CustomerLookupID,
                        LastActivityDate = _.LastActivityDate,
                        NameFirst = _.NameFirst,
                        NameLast = _.NameLast
                    }).ToList()
                };
            }
            catch (Exception ex) when (ex is DbException || ex is InvalidOperationException)
            {
                _logger.LogError(ex, "Error querying Books By Mail patrons in Polaris");
                throw new OcudaException("Error retrieving Books By Mail patrons", ex);
            }
        }

        public async Task<DataWithCount<IList<Material>>>
            GetPaginatedCustomerLookupHistoryAsync(MaterialFilter filter)
        {
            ArgumentNullException.ThrowIfNull(filter);

            try
            {
                IQueryable<PatronHistoryRow> query = _polarisContext.Database
                    .SqlQuery<PatronHistoryRow>($@"SELECT BR.BrowseAuthor AS Author,
                        BR.BrowseTitle AS Title,
                        COALESCE(IRD.ClassificationNumber, IRD.CutterNumber) AS Category,
                        PRH.CheckOutDate AS CheckoutDate
                    FROM Polaris.PatronReadingHistory AS PRH WITH (NOLOCK)
                    INNER JOIN Polaris.ItemRecordDetails AS IRD WITH (NOLOCK)
                        ON PRH.ItemRecordID = IRD.ItemRecordID
                    INNER JOIN Polaris.CircItemRecords AS CIR WITH (NOLOCK)
                        ON PRH.ItemRecordID = CIR.ItemRecordID
                    INNER JOIN Polaris.BibliographicRecords AS BR WITH (NOLOCK)
                        ON CIR.AssociatedBibRecordID = BR.BibliographicRecordID
                    WHERE PRH.PatronID = {filter.CustomerLookupID}");

                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    query = query.Where(_ =>
                        (_.Title ?? string.Empty).Contains(filter.Search)
                        || (_.Author ?? string.Empty).Contains(filter.Search));
                }

                var count = await query.CountAsync();
                var orderedQuery = OrderHistory(query, filter.OrderBy, filter.OrderDesc);
                var rows = await orderedQuery
                    .Skip(filter.Skip.GetValueOrDefault())
                    .Take(filter.Take.GetValueOrDefault(10))
                    .ToListAsync();

                return new DataWithCount<IList<Material>>
                {
                    Count = count,
                    Data = rows.Select(_ => new Material
                    {
                        Author = _.Author,
                        Category = _.Category,
                        CheckoutDate = _.CheckoutDate,
                        Title = _.Title
                    }).ToList()
                };
            }
            catch (Exception ex) when (ex is DbException || ex is InvalidOperationException)
            {
                _logger.LogError(ex,
                    "Error querying Polaris reading history for patron {PatronId}",
                    filter.CustomerLookupID);
                throw new OcudaException("Error retrieving patron reading history", ex);
            }
        }

        private static IOrderedQueryable<CustomerLookupRow> OrderCustomers(
            IQueryable<CustomerLookupRow> query,
            CustomerLookupFilter.OrderType orderBy,
            bool orderDesc)
        {
            return orderBy switch
            {
                CustomerLookupFilter.OrderType.NameFirst => orderDesc
                    ? query.OrderByDescending(_ => _.NameFirst)
                        .ThenByDescending(_ => _.NameLast)
                        .ThenByDescending(_ => _.CustomerLookupID)
                    : query.OrderBy(_ => _.NameFirst)
                        .ThenBy(_ => _.NameLast)
                        .ThenBy(_ => _.CustomerLookupID),
                CustomerLookupFilter.OrderType.LastActivityDate => orderDesc
                    ? query.OrderByDescending(_ => _.LastActivityDate)
                        .ThenByDescending(_ => _.CustomerLookupID)
                    : query.OrderBy(_ => _.LastActivityDate)
                        .ThenBy(_ => _.CustomerLookupID),
                _ => orderDesc
                    ? query.OrderByDescending(_ => _.NameLast)
                        .ThenByDescending(_ => _.NameFirst)
                        .ThenByDescending(_ => _.CustomerLookupID)
                    : query.OrderBy(_ => _.NameLast)
                        .ThenBy(_ => _.NameFirst)
                        .ThenBy(_ => _.CustomerLookupID)
            };
        }

        private static IOrderedQueryable<PatronHistoryRow> OrderHistory(
            IQueryable<PatronHistoryRow> query,
            MaterialFilter.OrderType orderBy,
            bool orderDesc)
        {
            return orderBy switch
            {
                MaterialFilter.OrderType.Author => orderDesc
                    ? query.OrderByDescending(_ => _.Author).ThenByDescending(_ => _.Title)
                    : query.OrderBy(_ => _.Author).ThenBy(_ => _.Title),
                MaterialFilter.OrderType.CheckoutDate => orderDesc
                    ? query.OrderByDescending(_ => _.CheckoutDate).ThenByDescending(_ => _.Title)
                    : query.OrderBy(_ => _.CheckoutDate).ThenBy(_ => _.Title),
                _ => orderDesc
                    ? query.OrderByDescending(_ => _.Title).ThenByDescending(_ => _.Author)
                    : query.OrderBy(_ => _.Title).ThenBy(_ => _.Author)
            };
        }

        private class CustomerLookupRow
        {
            public string Barcode { get; set; }
            public int CustomerLookupID { get; set; }
            public DateTime? LastActivityDate { get; set; }
            public string NameFirst { get; set; }
            public string NameLast { get; set; }
        }

        private class PatronHistoryRow
        {
            public string Author { get; set; }
            public string Category { get; set; }
            public DateTime CheckoutDate { get; set; }
            public string Title { get; set; }
        }
    }
}
