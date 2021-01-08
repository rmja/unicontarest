using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Uniconta.API.System;
using Uniconta.ClientTools.DataModel;
using Uniconta.Common;
using Uniconta.DataModel;

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
            using var reader = new HttpRequestStreamReader(Request.Body, Encoding.UTF8);
            using var jsonReader = new JsonTextReader(reader);

            if (jsonReader.TokenType == JsonToken.None)
            {
                await jsonReader.ReadAsync(); // Read None
            }

            var models = new List<UnicontaBaseEntity>();

            if (jsonReader.TokenType == JsonToken.StartObject)
            {
                var jsonObject = await JObject.LoadAsync(jsonReader, HttpContext.RequestAborted);

                models.Add(ToBaseEntity(jsonObject));
            }
            else if (jsonReader.TokenType == JsonToken.StartArray)
            {
                await jsonReader.ReadAsync(); // Read StartArray

                while (jsonReader.TokenType != JsonToken.EndArray)
                {
                    var jsonObject = await JObject.LoadAsync(jsonReader, HttpContext.RequestAborted);

                    models.Add(ToBaseEntity(jsonObject));

                    await jsonReader.ReadAsync(); // Read EndObject
                }
            }
            else
            {
                return BadRequest("Object or array is expected");
            }

            try
            {
                var api = new CrudAPI(Session, Company);

                // Special handling of debtor orders where it may be needed to set a debtor as master
                if (Type == typeof(DebtorOrderClient))
                {
                    foreach (var orders in models.OfType<DebtorOrderClient>().GroupBy(x => x.Account))
                    {
                        var account = orders.Key;
                        var debtors = await api.Query<DebtorClient>(new[] {
                            PropValuePair.GenereteWhereElements(typeof(DebtorClient).GetProperty(nameof(DebtorClient.Account)), account)
                        });

                        if (debtors.Length != 1)
                        {
                            return BadRequest($"Unable to find debtor with account number {account}");
                        }

                        foreach (var order in orders)
                        {
                            order.SetMaster(debtors[0]);
                        }
                    }
                }

                var status = await api.Insert(models);

                if (status == ErrorCodes.Succes)
                {
                    return Ok(models);
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

        [HttpPatch]
        public async Task<ActionResult> Update([FromQuery] Dictionary<string, string> filter, int limit, JsonPatchDocument patch)
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
        public async Task<ActionResult> Delete([FromQuery] Dictionary<string, string> filter, int limit)
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

        private UnicontaBaseEntity ToBaseEntity(JObject jsonObject)
        {
            var entity = (UnicontaBaseEntity)jsonObject.ToObject(Type, _serializer);

            foreach (var jsonProperty in jsonObject.Properties())
            {
                var modelProperty = Type.GetProperty(jsonProperty.Name);
                if (modelProperty != null)
                {
                    TrySetBackingField(entity, modelProperty, jsonProperty.First.ToObject<object>());
                }
            }

            return entity;
        }

        private void TrySetBackingField(object instance, PropertyInfo property, object value)
        {
            var field =
                instance.GetType().GetField($"{property.Name}k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic) ??
                instance.GetType().GetField($"_{property.Name}", BindingFlags.Instance | BindingFlags.NonPublic);

            if (field is object)
            {
                field?.SetValue(instance, Convert.ChangeType(value, property.PropertyType));
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
