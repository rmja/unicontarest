using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Uniconta.API.System;
using Uniconta.Common;

namespace UnicontaRest.Controllers
{
    [Route("Companies/{companyId:int}/Query/{typeName}")]
    [ApiController]
    public class QueryController : UnicontaControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<object>> Get([FromQuery] Dictionary<string, string> filter)
        {
            var predicates = filter.Select(x => PropValuePairEx.GenereteWhereElements(Type, x.Key, x.Value)).ToList();

            var api = new QueryAPI(Session, Company);
            var queryMethod = api.GetType().GetMethods().First(x => x.Name == nameof(QueryAPI.QueryReader) && x.IsGenericMethod && x.GetParameters().FirstOrDefault()?.ParameterType == typeof(IEnumerable<PropValuePair>));
            var genericQueryReaderMethod = queryMethod.MakeGenericMethod(Type);
            var resultTask = (Task)genericQueryReaderMethod.Invoke(api, new object[] { predicates.AsEnumerable() });

            await resultTask;

            var result = resultTask.GetType().GetProperty("Result").GetValue(resultTask);
            return result;
        }
    }
}
