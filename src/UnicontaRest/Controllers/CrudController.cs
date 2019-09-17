using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Uniconta.API.System;
using Uniconta.Common;

namespace UnicontaRest.Controllers
{
    [Route("Companies/{companyId:int}/Crud/{typeName}")]
    [ApiController]
    public class CrudController : UnicontaControllerBase
    {
        private static readonly JsonSerializer _serializer = JsonSerializer.CreateDefault();

        [HttpPost]
        public async Task<ActionResult> Create()
        {
            using (var reader = new StreamReader(Request.Body))
            using (var jsonReader = new JsonTextReader(reader))
            {
                var model = (UnicontaBaseEntity)_serializer.Deserialize(jsonReader, Type);

                try
                {
                    var api = new CrudAPI(Session, Company);
                    var status = await api.Insert(model);

                    if (status == ErrorCodes.Succes)
                    {
                        return Ok(model);
                    }
                    else
                    {
                        return StatusCode(500, status);
                    }
                }
                catch (Exception e)
                {
                    return BadRequest(e.Message);
                }
            }
        }

        [HttpPatch]
        public async Task<ActionResult> Update(Dictionary<string, string> filter, int limit, JsonPatchDocument patch)
        {
            var predicates = filter.Select(x => PropValuePairEx.GenereteWhereElements(Type, x.Key, x.Value)).ToList();

            var api = new CrudAPI(Session, Company);
            var values = await QueryAsync(api, predicates);

            if (values.Length > limit)
            {
                return Conflict($"There are more values ({values.Length}) matching filter then allowed by the limit ({limit})");
            }

            foreach (var value in values)
            {
                patch.ApplyTo(value);
            }

            var status = await api.Update(values);

            if (status == ErrorCodes.Succes)
            {
                return Ok(values);
            }
            else
            {
                return StatusCode(500, status);
            }
        }

        [HttpDelete]
        public async Task<ActionResult> Delete(Dictionary<string, string> filter, int limit)
        {
            var predicates = filter.Select(x => PropValuePairEx.GenereteWhereElements(Type, x.Key, x.Value)).ToList();

            var api = new CrudAPI(Session, Company);
            var values = await QueryAsync(api, predicates);

            if (values.Length > limit)
            {
                return Conflict($"There are more values ({values.Length}) matching filter then allowed by the limit ({limit})");
            }

            var status = await api.Delete(values);

            if (status == ErrorCodes.Succes)
            {
                return NoContent();
            }
            else
            {
                return StatusCode(500, status);
            }
        }

        private async Task<UnicontaBaseEntity[]> QueryAsync(CrudAPI api, IEnumerable<PropValuePair> propWhere)
        {
            var queryMethod = typeof(CrudAPI).GetMethods().First(x => x.Name == nameof(CrudAPI.Query) && x.IsGenericMethod && x.GetParameters().FirstOrDefault()?.ParameterType == typeof(IEnumerable<PropValuePair>));
            var genericQueryMethod = queryMethod.MakeGenericMethod(Type);

            var resultTask = (Task)genericQueryMethod.Invoke(api, new object[] { propWhere });

            await resultTask;

            var result = resultTask.GetType().GetProperty(nameof(Task<object>.Result)).GetValue(resultTask);
            return (UnicontaBaseEntity[])result;
        }
    }
}
