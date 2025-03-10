﻿using System;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Ocuda.i18n.RouteConstraint
{
    public class CultureRouteConstraint : IRouteConstraint
    {
        private readonly IOptions<RequestLocalizationOptions> _l10nOptions;

        public CultureRouteConstraint(IOptions<RequestLocalizationOptions> l10nOptions)
        {
            ArgumentNullException.ThrowIfNull(l10nOptions);

            _l10nOptions = l10nOptions;
        }

        public bool Match(HttpContext httpContext,
            IRouter route,
            string routeKey,
            RouteValueDictionary values,
            RouteDirection routeDirection)
        {
            string culture = values?[routeKey] as string;
            if (string.IsNullOrEmpty(culture))
            {
                return false;
            }
            try
            {
                return _l10nOptions.Value.SupportedCultures.Contains(new CultureInfo(culture));
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }
    }
}