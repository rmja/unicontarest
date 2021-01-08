﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Uniconta.API.Service;
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

            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<UnicontaControllerBase>>();
            var connectionProvider = context.HttpContext.RequestServices.GetRequiredService<UnicontaConnectionProvider>();
            var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();

            try
            {
                var details = await connectionProvider.GetConnectionAsync(credentials, HttpContext.RequestAborted);

                Session = details.Session;
                Companies = details.Companies;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Unable to get uniconta connection for provided credentials");

                context.Result = Forbid();
                return;
            }

            if (Session is null || Companies is null)
            {
                logger.LogCritical("The uniconta connection is invalid {Session}, {Companies}", Session, Companies);
                context.Result = StatusCode(StatusCodes.Status502BadGateway);
                return;
            }

            if (!Session.LoggedIn)
            {
                logger.LogCritical("The session is not logged in");
                context.Result = StatusCode(StatusCodes.Status502BadGateway);
                return;
            }

            var routeValues = context.RouteData.Values;

            if (routeValues.TryGetValue<string>("typeName", out var typeName))
            {
                Type = _unicontaAssembly.GetType($"Uniconta.ClientTools.DataModel.{typeName}Client", throwOnError: false, ignoreCase: true);

                if (Type is null)
                {
                    Type = _unicontaAssembly.GetType($"Uniconta.DataModel.{typeName}", throwOnError: false, ignoreCase: true);
                }

                if (Type is null)
                {
                    context.Result = BadRequest($"The type {Type} was not found");
                    return;
                }
            }

            if (routeValues.TryGetValue<int>("companyId", out var companyId))
            {
                Company = Companies.FirstOrDefault(x => x.CompanyId == companyId);

                if (Company.GetUserFields() is null)
                {
                    var fields = await cache.GetOrCreateAsync($"Companines:{Company}:UserFields", async entry =>
                    {
                        entry.SetAbsoluteExpiration(TimeSpan.FromDays(1));
                        
                        // Make sure that the "Fields" field is loaded for the company
                        // This is neede to make user defined fields work
                        var company = await Session.GetCompany(companyId, Company);

                        var fields = company.GetUserFields();
                        return fields;
                    });

                    Company.SetUserFields(fields);
                }

                if (Company is null)
                {
                    context.Result = NotFound();
                    return;
                }
            }

            await next();
        }
    }

    public static class CompanyExtensions
    {
        public static object[] GetUserFields(this Company company)
        {
            var fields = typeof(Company).GetField("Fields", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(company);

            if (fields is null)
            {
                return null;
            }

            return ((IEnumerable)fields).Cast<object>().ToArray();
        }
    }
}