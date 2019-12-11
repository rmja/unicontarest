using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Reflection;
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
            if (!Request.TryGetCredentials(out var credentials))
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
