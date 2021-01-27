using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Uniconta.API.Service;
using Uniconta.API.System;
using Uniconta.ClientTools.DataModel;
using Uniconta.Common;
using Uniconta.Common.User;

namespace UnicontaRest.Controllers
{
    [Route("Companies/{companyId:int}/Query/{typeName}")]
    [ApiController]
    public class QueryController : UnicontaControllerBase
    {
        private readonly UnicontaRestOptions _options;

        public QueryController(IOptions<UnicontaRestOptions> options)
        {
            _options = options.Value;
        }

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

        [HttpGet("/Companies/{companyId:int}/QueryTest/DebtorOrders/{orderId:int}")]
        public async Task<ActionResult<DebtorOrderLineClient[]>> GetDebtorOrder(int companyId, int orderId)
        {
            if (!Request.TryGetCredentials(out var credentials))
            {
                return Unauthorized();
            }

            var connection = new UnicontaConnection(APITarget.Live);
            var session = new Session(connection);
            var loginResult = await session.LoginAsync(credentials.Username, credentials.Password, LoginType.API, _options.AffiliateKey);
            if (loginResult != ErrorCodes.Succes)
            {
                return StatusCode(StatusCodes.Status403Forbidden, loginResult);
            }
            
            var company = await session.GetCompany(companyId);
            var api = new QueryAPI(session, company);
            var queryResult = await api.Query<DebtorOrderLineClient>(new[]
            {
                PropValuePair.GenereteWhereElements(typeof(DebtorOrderClient).GetProperty("OrderNumber"), orderId.ToString())
            });

            return queryResult;
        }
    }
}
