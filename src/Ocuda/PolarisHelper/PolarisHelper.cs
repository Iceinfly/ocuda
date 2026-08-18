using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Clc.Polaris.Api;
using Clc.Polaris.Api.Configuration;
using Clc.Polaris.Api.Models;
using Clc.Polaris.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ocuda.Models;
using Ocuda.PolarisHelper.Models;
using Ocuda.Utility.Exceptions;
using Ocuda.Utility.Keys;
using Ocuda.Utility.Services.Interfaces;

namespace Ocuda.PolarisHelper
{
    public class PolarisHelper : IPolarisHelper
    {
        private const int BibGenreElementId = 27;
        private const int CacheCodesHours = 1;
        private const int PAPIInvalidEmailErrorCode = -3518;
        private readonly IOcudaCache _cache;
        private readonly IConfiguration _config;
        private readonly PolarisContext _context;
        private readonly ILogger<PolarisHelper> _logger;
        private readonly IPapiClient _papiClient;

        public PolarisHelper(IOcudaCache cache,
            IConfiguration config,
            ILogger<PolarisHelper> logger)
        {
            ArgumentNullException.ThrowIfNull(cache);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(logger);

            _cache = cache;
            _config = config;
            _logger = logger;

            var settings = new PapiSettings();
            _config.GetSection(PapiSettings.SECTION_NAME).Bind(settings);
            _papiClient = new PapiClient(settings)
            {
                AllowStaffOverrideRequests = false
            };

            IsConfigured = ValidateConfiguration();
        }

        public PolarisHelper(IOcudaCache cache,
            IConfiguration config,
            PolarisContext context,
            ILogger<PolarisHelper> logger)
        {
            ArgumentNullException.ThrowIfNull(cache);
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);

            _cache = cache;
            _config = config;
            _context = context;
            _logger = logger;

            var settings = new PapiSettings();
            _config.GetSection(PapiSettings.SECTION_NAME).Bind(settings);
            _papiClient = new PapiClient(settings);

            IsConfigured = ValidateConfiguration();
        }

        public bool IsConfigured { get; }

        public bool AuthenticateCustomer(string barcode, string password)
        {
            var validateResult = _papiClient.PatronValidate(barcode, password);

            if (validateResult.Exception != null)
            {
                _logger.LogError(validateResult.Exception,
                    "Error authenticating Polaris account through PAPI");
                throw new OcudaException("Error authenticating customer", validateResult.Exception);
            }

            return validateResult?.Data != null;
        }

        public CreateRegistrationResult CreateCustomerRegistration(Customer customer)
        {
            ArgumentNullException.ThrowIfNull(customer);

            // Set password to last 4 digits of phone number
            var password = customer.PhoneNumber[^4..];

            var registrationData = new PatronRegistrationData
            {
                AddrCheckDate = customer.AddressVerificationDate,
                Barcode = customer.CustomerIdNumber,
                Birthdate = customer.BirthDate,
                // Set delivery to email
                DeliveryOptionID = 2,
                EmailAddress = customer.EmailAddress,
                ExpirationDate = customer.ExpirationDate,
                LogonBranchID = _papiClient.OrganizationId,
                LogonUserID = _papiClient.UserId,
                LogonWorkstationID = _papiClient.WorkstationId,
                NameFirst = customer.NameFirst,
                NameLast = customer.NameLast,
                Password = password,
                Password2 = password,
                PatronBranchID = _papiClient.OrganizationId,
                PatronCode = customer.CustomerCodeId,
                PhoneVoice1 = customer.PhoneNumber,
                User1 = customer.UserDefinedField1,
                User4 = customer.UserDefinedField4,
                User5 = customer.UserDefinedField5
            };

            foreach (var address in customer.Addresses)
            {
                registrationData.Addresses.Add(
                    new PatronRegistrationData.PatronRegistrationAddressData
                    {
                        City = address.City,
                        CountryID = address.CountryId,
                        County = address.County,
                        PostalCode = address.PostalCode,
                        State = address.State,
                        StreetOne = address.StreetAddressOne
                    });
            }

            var createResult = new CreateRegistrationResult();

            var registrationResults = _papiClient.PatronRegistrationCreateV2(registrationData);

            if (registrationResults.Exception != null)
            {
                _logger.LogError("PatronRegistrationCreate PAPI call was not successful: {ErrorMessage}",
                    registrationResults.Exception.Message);
            }
            else if (!registrationResults.Response.IsSuccessStatusCode)
            {
                _logger.LogError("PatronRegistrationCreate PAPI call was not successful after {Elapsed} ms: {StatusCode}",
                    registrationResults.ResponseTime,
                    registrationResults.Response.StatusCode);
            }
            else
            {
                if (registrationResults.Data.PAPIErrorCode != 0)
                {
                    _logger.LogError("PAPI error after {Elapsed} ms: {PAPIErrorCode} {PAPIErrorMessage}",
                        registrationResults.ResponseTime,
                        registrationResults.Data.PAPIErrorCode,
                        registrationResults.Data.ErrorMessage);

                    createResult.ErrorMessage = registrationResults.Data.ErrorMessage;
                }
                else if (registrationResults.Data.PAPIErrorCode == 0)
                {
                    createResult.Success = true;
                }
            }

            return createResult;
        }

        public async Task<List<CustomerBlock>> GetCustomerBlocksAsync(int customerId)
        {
            try
            {
                var blocks = await _context.Database
                    .SqlQuery<CustomerBlock>(@$"SELECT PS.PatronStopID AS BlockId, PSD.Description
                    FROM Polaris.PatronStops as PS (NOLOCK)
                    INNER JOIN Polaris.PatronStopDescriptions as PSD (NOLOCK)
                    on PS.PatronStopId = PSD.PatronStopId
                    WHERE PS.PatronID = {customerId}")
                    .ToListAsync();

                var freeTextBlocks = await _context.Database
                    .SqlQuery<CustomerBlock>(@$"SELECT NULL AS BlockId, FreeTextBlock AS Description
                    FROM Polaris.PatronFreeTextBlocks (NOLOCK)
                    WHERE PatronID = {customerId}")
                    .ToListAsync();

                blocks.AddRange(freeTextBlocks);

                return blocks;
            }
            catch (Exception ex) when (ex is DbException || ex is InvalidOperationException)
            {
                _logger.LogError(ex, "Error querying Polaris blocks for patron {PatronId}",
                    customerId);
                throw new OcudaException("Error retrieving customer blocks", ex);
            }
        }

        public async Task<string> GetCustomerCodeNameAsync(int customerCodeId)
        {
            var patronCodes = await _cache
                .GetObjectFromCacheAsync<List<PatronCodeRow>>(Cache.PolarisPatronCodes);

            if (patronCodes == null)
            {
                var patronCodesResult = _papiClient.PatronCodesGet();
                if (patronCodesResult?.Exception != null)
                {
                    _logger.LogError(patronCodesResult.Exception,
                        "Error getting Polaris patron codes through PAPI");
                    throw new OcudaException("Error resolving patron code name",
                        patronCodesResult.Exception);
                }

                patronCodes = patronCodesResult.Data.PatronCodesRows;
                await _cache.SaveToCacheAsync(Cache.PolarisPatronCodes,
                    patronCodes,
                    CacheCodesHours);
            }

            return patronCodes
                .Where(_ => _.PatronCodeID == customerCodeId)
                .Select(_ => _.Description)
                .SingleOrDefault();
        }

        public Customer GetCustomerData(string barcode, string password)
        {
            var patronDataResult = _papiClient
                .PatronBasicDataGet(barcode, password, addresses: true);

            if (patronDataResult?.Exception != null)
            {
                _logger.LogError(patronDataResult.Exception,
                    "Error getting Polaris account data through PAPI");
                throw new OcudaException("Error accessing Polaris records",
                    patronDataResult.Exception);
            }

            var patronData = patronDataResult?.Data?.PatronBasicData;

            return patronData == null ? null : GetCustomerInfo(patronData);
        }

        public Customer GetCustomerDataOverride(string barcode)
        {
            var patronDataResult = _papiClient
                .PatronBasicDataGet(barcode, addresses: true, notes: true);

            if (patronDataResult?.Exception != null)
            {
                _logger.LogError(patronDataResult.Exception,
                    "Error getting Polaris account data through PAPI override");
                throw new OcudaException("Error accessing Polaris records",
                    patronDataResult.Exception);
            }

            var patronData = patronDataResult?.Data?.PatronBasicData;

            return patronData == null ? null : GetCustomerInfo(patronData);
        }

        public string GetBibGenre(int bibId)
        {
            var data = ExecutePapiMethod(
                "BibGet",
                new[] { "BibGet" },
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["bibid"] = bibId,
                    ["organizationid"] = _papiClient.OrganizationId
                });

            return GetRows(data, "BibGetRows")
                .Where(_ => GetNullableInt(_, "ElementID") == BibGenreElementId)
                .Select(_ => GetString(_, "Value"))
                .FirstOrDefault(_ => !string.IsNullOrWhiteSpace(_));
        }

        public IList<PatronHold> GetPatronHolds(string barcode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(barcode);

            var data = ExecutePapiMethod(
                "PatronHoldRequestsGet",
                new[] { "PatronHoldRequestsGet", "PatronHoldRequestsGetOverride" },
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["barcode"] = barcode,
                    ["endpoint"] = "All",
                    ["requestid"] = 0,
                    ["status"] = "All"
                });

            return GetRows(data, "PatronHoldRequestsGetRows")
                .Select(_ => new PatronHold
                {
                    Author = GetString(_, "Author"),
                    HoldStatus = GetString(_, "StatusDescription"),
                    Title = GetString(_, "Title")
                })
                .ToList();
        }

        public IList<PatronCheckout> GetPatronItemsOut(string barcode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(barcode);

            var data = ExecutePapiMethod(
                "PatronItemsOutGet",
                new[] { "PatronItemsOutGet", "PatronItemsOutGetOverride" },
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["barcode"] = barcode,
                    ["excludeecontent"] = false,
                    ["id"] = "All",
                    ["status"] = "All"
                });

            return GetRows(data, "PatronItemsOutGetRows")
                .Select(_ => new PatronCheckout
                {
                    Author = GetString(_, "Author"),
                    BibId = GetNullableInt(_, "BibID") ?? 0,
                    DueDate = GetNullableDateTime(_, "DueDate") ?? default,
                    Title = GetString(_, "Title")
                })
                .ToList();
        }

        public int GetPatronReadingHistoryCount(string barcode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(barcode);

            var data = ExecutePapiMethod(
                "PatronReadingHistoryGet",
                new[] { "PatronReadingHistoryGet", "PatronReadingHistoryGetOverride" },
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["barcode"] = barcode,
                    ["page"] = -1,
                    ["rowsperpage"] = 1
                });

            return GetNullableInt(data, "PAPIErrorCode") ?? 0;
        }

        public async Task<int?> GetOrganizationIdFormerDirect(string formerBarcode)
        {
            try
            {
                var formerBarcodeQuery = await _context.Database.SqlQuery<BarcodeOrgId>(
                    @$"SELECT pr.FormerID [Barcode], p.OrganizationID
                    FROM Polaris.PatronRegistration pr (NOLOCK)
                    INNER JOIN Polaris.Patrons p (NOLOCK) ON pr.PatronID = p.PatronID
                    WHERE pr.FormerID = {formerBarcode}")
                    .ToListAsync();

                return formerBarcodeQuery.FirstOrDefault()?.OrganizationID;
            }
            catch (Exception ex) when (ex is DbException || ex is InvalidOperationException)
            {
                _logger.LogError(ex, "Error querying Polaris organization for former barcode {FormerBarcode}",
                    formerBarcode);
                throw new OcudaException("Error retrieving based on former barcode", ex);
            }
        }

        public async Task<IDictionary<string, int>> GetOrganizationIdsBatchDirect(IEnumerable<string> barcodes)
        {
            ArgumentNullException.ThrowIfNull(barcodes);
            try
            {
                return await _context.Database
                    .SqlQuery<BarcodeOrgId>($"SELECT Barcode, OrganizationID FROM Polaris.Patrons (NOLOCK)")
                    .Where(_ => barcodes.Contains(_.Barcode))
                    .ToDictionaryAsync(k => k.Barcode, v => v.OrganizationID);
            }
            catch (Exception ex) when (ex is DbException || ex is InvalidOperationException)
            {
                _logger.LogError(ex, "Error querying Polaris organization id for barcodes {Barcodes}",
                    barcodes);
                throw new OcudaException("Error retrieving customer organization id", ex);
            }
        }

        public IEnumerable<Organization> GetOrganizations()
        {
            var orgGetResult = _papiClient.OrganizationsGet();
            if (orgGetResult.Exception != null)
            {
                _logger.LogError(orgGetResult.Exception,
                    "Error getting Polaris organizations through PAPI");
                throw new OcudaException("Error accessing Polaris records",
                    orgGetResult.Exception);
            }

            return orgGetResult.Data.OrganizationsGetRows.Select(_ => new Organization
            {
                Abbreviation = _.Abbreviation,
                DisplayName = _.DisplayName,
                Name = _.Name,
                OrganizationCodeID = _.OrganizationCodeID,
                OrganizationID = _.OrganizationID,
                ParentOrganizationID = _.ParentOrganizationID
            });
        }

        public RenewRegistrationResult RenewCustomerRegistration(string barcode, string email)
        {
            var date = DateTime.Now.AddYears(1);
            var updateParams = new PatronUpdateParams
            {
                BranchId = _papiClient.OrganizationId,
                UserId = _papiClient.UserId,
                LogonWorkstationId = _papiClient.WorkstationId,
                ExpirationDate = date,
                AddrCheckDate = date,
                EmailAddress = email
            };

            var renewResult = new RenewRegistrationResult();

            var updateResults = _papiClient.PatronUpdate(barcode, updateParams);

            if (updateResults.Exception != null)
            {
                _logger.LogError("PAPI call was not successful: {ErrorMessage}",
                    updateResults.Exception.Message);
            }
            else if (!updateResults.Response.IsSuccessStatusCode)
            {
                _logger.LogError("PAPI call was not successful after {Elapsed} ms: {StatusCode}",
                    updateResults.ResponseTime,
                    updateResults.Response.StatusCode);
            }
            else
            {
                if (updateResults.Data.PAPIErrorCode == PAPIInvalidEmailErrorCode)
                {
                    renewResult.EmailNotUpdated = true;
                    updateParams.EmailAddress = null;

                    updateResults = _papiClient.PatronUpdate(barcode, updateParams);
                }

                if (updateResults.Data.PAPIErrorCode != 0)
                {
                    _logger.LogError("PAPI error after {Elapsed} ms: {PAPIErrorCode}",
                        updateResults.ResponseTime,
                        updateResults.Data.PAPIErrorCode);
                }
                else if (updateResults.Data.PAPIErrorCode == 0)
                {
                    renewResult.Success = true;
                    if (renewResult.EmailNotUpdated)
                    {
                        _logger.LogWarning("Unable to update email to {EmailAddress} for barcode {Barcode}",
                            email,
                            barcode);
                    }
                }
            }

            return renewResult;
        }

        private object ExecutePapiMethod(string operation,
            IEnumerable<string> methodNames,
            IReadOnlyDictionary<string, object> values)
        {
            object response;
            try
            {
                response = InvokePapiMethod(methodNames, values);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException,
                    "Error executing Polaris {Operation} through PAPI",
                    operation);
                throw new OcudaException($"Error executing Polaris {operation}", ex.InnerException);
            }
            catch (Exception ex) when (ex is ArgumentException
                || ex is InvalidOperationException
                || ex is MissingMethodException)
            {
                _logger.LogError(ex,
                    "Error executing Polaris {Operation} through PAPI",
                    operation);
                throw new OcudaException($"Error executing Polaris {operation}", ex);
            }

            if (response == null)
            {
                throw new OcudaException($"Polaris {operation} returned no response");
            }

            if (GetPropertyValue(response, "Exception") is Exception responseException)
            {
                _logger.LogError(responseException,
                    "Polaris {Operation} PAPI call failed",
                    operation);
                throw new OcudaException($"Error executing Polaris {operation}", responseException);
            }

            var httpResponse = GetPropertyValue(response, "Response");
            if (GetPropertyValue(httpResponse, "IsSuccessStatusCode") is bool isSuccess
                && !isSuccess)
            {
                throw new OcudaException(
                    $"Polaris {operation} returned an unsuccessful HTTP response");
            }

            var data = GetPropertyValue(response, "Data");
            if (data == null)
            {
                throw new OcudaException($"Polaris {operation} returned no data");
            }

            var papiErrorCode = GetNullableInt(data, "PAPIErrorCode");
            if (papiErrorCode < 0)
            {
                var errorMessage = GetString(data, "ErrorMessage");
                throw new OcudaException(string.IsNullOrWhiteSpace(errorMessage)
                    ? $"Polaris {operation} returned PAPI error {papiErrorCode}"
                    : $"Polaris {operation} returned PAPI error {papiErrorCode}: {errorMessage}");
            }

            return data;
        }

        private object InvokePapiMethod(IEnumerable<string> methodNames,
            IReadOnlyDictionary<string, object> values)
        {
            foreach (var methodName in methodNames)
            {
                var methods = _papiClient.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(_ => string.Equals(_.Name, methodName, StringComparison.Ordinal))
                    .OrderBy(_ => _.GetParameters().Length);

                foreach (var method in methods)
                {
                    if (TryBuildArguments(method.GetParameters(), values, out var arguments))
                    {
                        return method.Invoke(_papiClient, arguments);
                    }
                }
            }

            throw new MissingMethodException(
                $"No compatible PAPI method was found for {string.Join(" or ", methodNames)}");
        }

        private static bool TryBuildArguments(ParameterInfo[] parameters,
            IReadOnlyDictionary<string, object> values,
            out object[] arguments)
        {
            arguments = new object[parameters.Length];
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (TryGetArgumentValue(parameter.Name, values, out var value))
                {
                    if (!TryConvertValue(value, parameter.ParameterType, out var convertedValue))
                    {
                        return false;
                    }

                    arguments[index] = convertedValue;
                }
                else if (parameter.HasDefaultValue)
                {
                    arguments[index] = parameter.DefaultValue;
                }
                else if (parameter.IsOptional)
                {
                    arguments[index] = Type.Missing;
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetArgumentValue(string parameterName,
            IReadOnlyDictionary<string, object> values,
            out object value)
        {
            if (values.TryGetValue(parameterName, out value))
            {
                return true;
            }

            var normalizedName = NormalizeName(parameterName);
            foreach (var item in values)
            {
                if (NormalizeName(item.Key) == normalizedName)
                {
                    value = item.Value;
                    return true;
                }
            }

            if (normalizedName is "patronbarcode" or "customerbarcode")
            {
                return values.TryGetValue("barcode", out value);
            }

            if (normalizedName is "pagesize" or "rows" or "rowcount")
            {
                return values.TryGetValue("rowsperpage", out value);
            }

            value = null;
            return false;
        }

        private static bool TryConvertValue(object value, Type targetType, out object convertedValue)
        {
            if (value == null)
            {
                convertedValue = null;
                return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
            }

            var conversionType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (conversionType.IsInstanceOfType(value))
            {
                convertedValue = value;
                return true;
            }

            if (conversionType.IsEnum && value is string enumValue)
            {
                if (Enum.TryParse(conversionType, enumValue, true, out var parsedEnum))
                {
                    convertedValue = parsedEnum;
                    return true;
                }

                convertedValue = null;
                return false;
            }

            try
            {
                convertedValue = Convert.ChangeType(value,
                    conversionType,
                    CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex) when (ex is FormatException
                || ex is InvalidCastException
                || ex is OverflowException)
            {
                convertedValue = null;
                return false;
            }
        }

        private static DateTime? GetNullableDateTime(object source, string propertyName)
        {
            var value = GetPropertyValue(source, propertyName);
            if (value is DateTime dateTime)
            {
                return dateTime;
            }

            if (value != null
                && DateTime.TryParse(value.ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDateTime))
            {
                return parsedDateTime;
            }

            return null;
        }

        private static int? GetNullableInt(object source, string propertyName)
        {
            var value = GetPropertyValue(source, propertyName);
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException
                || ex is InvalidCastException
                || ex is OverflowException)
            {
                return null;
            }
        }

        private static object GetPropertyValue(object source, string propertyName)
        {
            return source?.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(source);
        }

        private static IEnumerable<object> GetRows(object data, string propertyName)
        {
            return GetPropertyValue(data, propertyName) is IEnumerable rows
                ? rows.Cast<object>()
                : Enumerable.Empty<object>();
        }

        private static string GetString(object source, string propertyName)
        {
            return GetPropertyValue(source, propertyName)?.ToString();
        }

        private static string NormalizeName(string value)
        {
            return string.Concat((value ?? string.Empty)
                .Where(char.IsLetterOrDigit))
                .ToLowerInvariant();
        }

        private static Customer GetCustomerInfo(PatronData patronData)
        {
            var customer = new Customer
            {
                AddressVerificationDate = patronData.AddrCheckDate,
                BirthDate = patronData.BirthDate,
                BlockingNotes = patronData.PatronNotes?.BlockingStatusNotes,
                ChargeBalance = patronData.ChargeBalance,
                CustomerCodeId = patronData.PatronCodeID,
                CustomerIdNumber = patronData.Barcode,
                EmailAddress = patronData.EmailAddress,
                ExpirationDate = patronData.ExpirationDate,
                Id = patronData.PatronID,
                IsBlocked = patronData.PatronSystemBlocks.Length != 0,
                LastActivityDate = patronData.LastActivityDate,
                NameFirst = patronData.NameFirst,
                NameLast = patronData.NameLast,
                Notes = patronData.PatronNotes?.NonBlockingStatusNotes,
                UserDefinedField1 = patronData.User1,
                UserDefinedField2 = patronData.User2,
                UserDefinedField3 = patronData.User3,
                UserDefinedField4 = patronData.User4,
                UserDefinedField5 = patronData.User5
            };

            var addressList = new List<CustomerAddress>();
            foreach (var address in patronData.PatronAddresses)
            {
                addressList.Add(new CustomerAddress
                {
                    AddressType = address.FreeTextLabel,
                    AddressTypeId = address.AddressTypeID,
                    City = address.City,
                    Country = address.Country,
                    CountryId = address.CountryID,
                    County = address.County,
                    Id = address.AddressId,
                    PostalCode = address.PostalCode,
                    State = address.State,
                    StreetAddressOne = address.StreetOne,
                    StreetAddressTwo = address.StreetTwo,
                    ZipPlusFour = address.ZipPlusFour,
                });
            }

            customer.Addresses = addressList;

            return customer;
        }

        private bool ValidateConfiguration()
        {
            var validConfiguration = true;

            if (string.IsNullOrEmpty(_papiClient.AccessID))
            {
                _logger.LogError("Polaris Helper is not configured: PapiSetting 'AccessID' is missing");
                validConfiguration = false;
            }
            if (string.IsNullOrWhiteSpace(_papiClient.AccessKey))
            {
                _logger.LogError("Polaris Helper is not configured: PapiSetting 'AccessKey' is missing");
                validConfiguration = false;
            }
            if (string.IsNullOrWhiteSpace(_papiClient.Hostname))
            {
                _logger.LogError("Polaris Helper is not configured: PapiSetting 'Hostname' is missing");
                validConfiguration = false;
            }

            if (_papiClient.AllowStaffOverrideRequests)
            {
                if (_papiClient.OrganizationId == 0)
                {
                    _logger.LogError("Polaris Helper is not configured: PapiSetting 'OrganizationId' is missing");
                    validConfiguration = false;
                }
                if (_papiClient.UserId == 0)
                {
                    _logger.LogError("Polaris Helper is not configured: PapiSetting 'UserId' is missing");
                    validConfiguration = false;
                }
                if (_papiClient.WorkstationId == 0)
                {
                    _logger.LogError("Polaris Helper is not configured: PapiSetting 'WorkstationId' is missing");
                    validConfiguration = false;
                }
                if (string.IsNullOrWhiteSpace(_papiClient.StaffOverrideAccount?.Domain))
                {
                    _logger.LogError("Polaris Helper is not configured: PapiSetting 'StaffOverrideAccount.Domain' is missing");
                    validConfiguration = false;
                }
                if (string.IsNullOrWhiteSpace(_papiClient.StaffOverrideAccount?.Password))
                {
                    _logger.LogError("Polaris Helper is not configured: PapiSetting 'StaffOverrideAccount.Password' is missing");
                    validConfiguration = false;
                }
                if (string.IsNullOrWhiteSpace(_papiClient.StaffOverrideAccount?.Username))
                {
                    _logger.LogError("Polaris Helper is not configured: PapiSetting 'StaffOverrideAccount.Username' is missing");
                    validConfiguration = false;
                }
                if (string.IsNullOrWhiteSpace(_context.Database.GetConnectionString()))
                {
                    _logger.LogError("Polaris Helper is not configured: ConnectionString 'Polaris' is missing");
                    validConfiguration = false;
                }
            }

            return validConfiguration;
        }

        private class BarcodeOrgId
        {
            public string Barcode { get; set; }
            public int OrganizationID { get; set; }
        }
    }
}