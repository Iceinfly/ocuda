using System.Collections.Generic;
using System.Net;
using Ocuda.HappyFoxHelper.Models;
using Ocuda.Utility.Exceptions;

namespace Ocuda.HappyFoxHelper
{
    public class HappyFoxException : OcudaException
    {
        public HappyFoxException(string message) : base(message) { }

        public HappyFoxException(string message, System.Exception innerException)
            : base(message, innerException)
        { }

        public IReadOnlyCollection<ValidationError> Errors { get; init; }
            = new List<ValidationError>();

        public HttpStatusCode? StatusCode { get; init; }
    }
}
