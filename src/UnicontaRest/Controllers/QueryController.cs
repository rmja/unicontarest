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

            if (predicates.Any(x => x is null))
            {
                return BadRequest("Invalid filter");
            }

            if (predicates.Any(x => x.OrList.Count > 40))
            {
                return BadRequest("The maximum number of OR's in a filter is 40");
            }

            var api = new QueryAPI(Session, Company);
            var queryMethod = api.GetType().GetMethods().First(x => x.Name == nameof(QueryAPI.Query) && x.IsGenericMethod && x.GetParameters().FirstOrDefault()?.ParameterType == typeof(IEnumerable<PropValuePair>));
            var genericQueryMethod = queryMethod.MakeGenericMethod(Type);
            var resultTask = (Task)genericQueryMethod.Invoke(api, new object[] { predicates.AsEnumerable() });

            await resultTask;

            var result = resultTask.GetType().GetProperty("Result").GetValue(resultTask);
            return result;
        }
    }
}
