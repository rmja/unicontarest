using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using Uniconta.DataModel;

namespace UnicontaRest.Controllers
{
    [ApiController]
    public class TypesController
    {
        [HttpGet("/")]
        public Usage GetIndex()
        {
            return new Usage();
        }

        public class Usage
        {
            private const string DateModelTypePrefix = "Uniconta.DataModel.";

            public string QueryEndpoint { get; } = "/Companies/:companyId/Query/:type";
            public string CrudEndpoint { get; } = "/Companies/:companyId/Crud/:type";
            public string InvoiceOrderEndpoint { get; } = "/Companies/:companyId/Invoice/Order/:orderNumber";

            public Dictionary<string, string[]> Enums { get; } = 
                typeof(Debtor).Assembly.ExportedTypes
                    .Where(x => x.IsEnum)
                    .ToDictionary(x => x.Name, x => Enum.GetNames(x));
            public string[] Types { get; } =
                typeof(Debtor).Assembly.ExportedTypes
                    .Where(x => !x.IsAbstract)
                    .Where(x => x.FullName.StartsWith(DateModelTypePrefix))
                    .Select(x => x.FullName.Substring(DateModelTypePrefix.Length))
                    .ToArray();
        }
    }
}
