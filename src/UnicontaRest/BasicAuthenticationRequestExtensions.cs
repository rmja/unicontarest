using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System;
using System.Linq;
using System.Text;

namespace UnicontaRest
{
    public static class BasicAuthenticationRequestExtensions
    {
        private const string BasicSpace = "Basic ";

        public static bool TryGetCredentials(this HttpRequest request, out Credentials credentials)
        {
            var header = request.Headers[HeaderNames.Authorization].FirstOrDefault();

            if (header?.StartsWith(BasicSpace, StringComparison.InvariantCultureIgnoreCase) != true)
            {
                credentials = default;
                return false;
            }

            var value = Encoding.UTF8.GetString(Convert.FromBase64String(header.AsSpan(BasicSpace.Length).ToString())).AsSpan();
            var indexOfSeparator = value.IndexOf(':');

            if (indexOfSeparator == -1)
            {
                credentials = default;
                return false;
            }

            var username = value.Slice(0, indexOfSeparator).ToString();
            var password = value.Slice(indexOfSeparator + 1).ToString();

            credentials = new Credentials(username, password);
            return true;
        }
    }

    public readonly struct Credentials
    {
        public string Username { get; }
        public string Password { get; }

        public Credentials(string username, string password) => (Username, Password) = (username, password);
    }
}
