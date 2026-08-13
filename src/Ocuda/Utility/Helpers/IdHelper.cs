using System;
using System.IO.Hashing;
using System.Text;

namespace Ocuda.Utility.Helpers
{
    public static class IdHelper
    {
        /// <summary>
        /// Compute a hashed value for the provided string, using CRC32. Should be unique for each
        /// provided string. This is not cryptographically secure.
        /// </summary>
        /// <param name="value">Text string to provide a hash</param>
        /// <returns>An int hash of the string</returns>
        public static int ComputeId(string value)
        {
            return BitConverter.ToInt32(Crc32.Hash(Encoding.UTF8.GetBytes(value)));
        }
    }
}