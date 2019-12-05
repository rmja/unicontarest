using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uniconta.API.Service;
using Uniconta.Common;
using Uniconta.Common.User;
using Uniconta.DataModel;

namespace UnicontaRest.Controllers
{
    public abstract class UnicontaControllerBase : Controller
    {
        private static readonly Assembly _unicontaAssembly = typeof(Debtor).Assembly;

        public Session Session { get; set; }
        public Company[] Companies { get; set; }
        public Company Company { get; set; }
        public Type Type { get; set; }

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!TryGetCredentials(out var credentials))
            { 
                context.Result = Unauthorized();
                return;
            }

            if (!await EnsureInitialized(context.HttpContext, credentials))
            {
                return;
            }

            var routeValues = context.RouteData.Values;

            if (routeValues.TryGetValue<string>("typeName", out var typeName))
            {
                Type = _unicontaAssembly.GetType($"Uniconta.DataModel.{typeName}", throwOnError: false, ignoreCase: true);

                if (Type is null)
                {
                    context.Result = BadRequest($"The type {Type} was not found");
                    return;
                }
            }

            if (routeValues.TryGetValue<int>("companyId", out var companyId))
            {
                Company = Companies.FirstOrDefault(x => x.CompanyId == companyId);

                if (Company is null)
                {
                    context.Result = NotFound();
                    return;
                }
            }

            await next();
        }

        private async Task<bool> EnsureInitialized(HttpContext httpContext, Credentials credentials)
        {
            var cache = httpContext.RequestServices.GetRequiredService<IMemoryCache>();

            var item = cache.GetOrCreate(credentials, entry =>
            {
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(60));
                return new SessionCacheItem();
            });

            if (item.IsInitialized)
            {
                Session = item.Session;
                Companies = item.Companies;
                return true;
            }

            if (item.WaitingForInitializationLockCount > 20)
            {
                httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return false;
            }

            Interlocked.Increment(ref item.WaitingForInitializationLockCount);

            await item.InitializationLock.WaitAsync();

            Interlocked.Decrement(ref item.WaitingForInitializationLockCount);

            try
            {
                if (item.IsInitialized)
                {
                    Session = item.Session;
                    Companies = item.Companies;
                    return true;
                }

                var options = httpContext.RequestServices.GetRequiredService<IOptions<UnicontaRestOptions>>().Value;
                var connection = new UnicontaConnection(APITarget.Live);
                Session = new Session(connection);

                var loggedIn = await Session.LoginAsync(credentials.Username, credentials.Password, LoginType.API, options.AffiliateKey);

                if (loggedIn != ErrorCodes.Succes)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return false;
                }

                Companies = await Session.GetCompanies();

                item.SetValues(Session, Companies);

                return true;
            }
            finally
            {
                item.InitializationLock.Release();
            }
        }

        private const string BasicSpace = "Basic ";

        private bool TryGetCredentials(out Credentials credentials)
        {
            var header = Request.Headers[HeaderNames.Authorization].FirstOrDefault();

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

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                credentials = default;
                return false;
            }

            credentials = new Credentials(username, password);
            return true;
        }

        private readonly struct Credentials
        {
            public string Username { get; }
            public string Password { get; }

            public Credentials(string username, string password) => (Username, Password) = (username, password);
        }

        private class SessionCacheItem
        {
            public Session Session { get; private set; }
            public Company[] Companies { get; private set; }
            public int WaitingForInitializationLockCount;
            public SemaphoreSlim InitializationLock { get; } = new SemaphoreSlim(1);
            public bool IsInitialized => Session is object && Companies is object;

            public void SetValues(Session session, Company[] companies)
            {
                Session = session;
                Companies = companies;
            }
        }
    }
}
