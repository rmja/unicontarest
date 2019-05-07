using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
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
            var credentials = GetCredentials();

            if (credentials == null)
            {
                context.Result = Unauthorized();
                return;
            }

            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<UnicontaRestOptions>>().Value;
            var connection = new UnicontaConnection(APITarget.Live);
            Session = new Session(connection);

            var loggedIn = await Session.LoginAsync(credentials?.Username, credentials?.Password, LoginType.API, options.AffiliateKey);

            if (loggedIn != ErrorCodes.Succes)
            {
                context.Result = Forbid();
                return;
            }

            Companies = await Session.GetCompanies();

            if (context.RouteData.Values.TryGetValue<int>("companyId", out var companyId))
            {
                Company = Companies.FirstOrDefault(x => x.CompanyId == companyId);

                if (Company == null)
                {
                    context.Result = NotFound();
                    return;
                }
            }

            if (context.RouteData.Values.TryGetValue<string>("typeName", out var typeName) )
            {
                Type = _unicontaAssembly.GetType($"Uniconta.DataModel.{typeName}", throwOnError: false, ignoreCase: true);

                if (Type == null)
                {
                    context.Result = BadRequest($"The type {Type} was not found");
                    return;
                }
            }

            await next();
        }

        private const string BasicSpace = "Basic ";

        private (string Username, string Password)? GetCredentials()
        {
            var header = Request.Headers[HeaderNames.Authorization].FirstOrDefault();

            if (header?.StartsWith(BasicSpace, StringComparison.InvariantCultureIgnoreCase) != true)
            {
                return null;
            }

            var value = Encoding.UTF8.GetString(Convert.FromBase64String(header.AsSpan(BasicSpace.Length).ToString())).AsSpan();
            var indexOfSeparator = value.IndexOf(':');

            if (indexOfSeparator == -1)
            {
                return null;
            }

            var username = value.Slice(0, indexOfSeparator).ToString();
            var password = value.Slice(indexOfSeparator + 1).ToString();

            return (username, password);
        }
    }
}
