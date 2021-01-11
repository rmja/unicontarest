using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
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

namespace UnicontaRest.Controllers
{
    [Route("Companies/{companyId:int}/Crud/{typeName}")]
    [ApiController]
    public class CrudController : UnicontaControllerBase
    {
        private static readonly JsonSerializer _serializer = JsonSerializer.CreateDefault();
        private readonly IMemoryCache _cache;

        public CrudController(IMemoryCache cache)
        {
            _cache = cache;
        }

        [HttpPost]
        public async Task<ActionResult> Create()
        {
            using var reader = new HttpRequestStreamReader(Request.Body, Encoding.UTF8);
            using var jsonReader = new JsonTextReader(reader);

            if (jsonReader.TokenType == JsonToken.None)
            {
                await jsonReader.ReadAsync(); // Read None
            }

            var models = new List<(UnicontaBaseEntity, JObject)>();

            if (jsonReader.TokenType == JsonToken.StartObject)
            {
                var jsonObject = await JObject.LoadAsync(jsonReader, HttpContext.RequestAborted);
                var model = ToBaseEntity(jsonObject);
                models.Add((model, jsonObject));
            }
            else if (jsonReader.TokenType == JsonToken.StartArray)
            {
                await jsonReader.ReadAsync(); // Read StartArray

                while (jsonReader.TokenType != JsonToken.EndArray)
                {
                    var jsonObject = await JObject.LoadAsync(jsonReader, HttpContext.RequestAborted);
                    var model = ToBaseEntity(jsonObject);
                    models.Add((model, jsonObject));

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
                    foreach (var (model, jsonObject) in models)
                    {
                        await AugmentDebtorOrderMasterAsync(api, (DebtorOrderClient)model, jsonObject);
                    }
                }

                var entities = models.Select(x => x.Item1).ToArray();
                
                var status = await api.Insert(entities);

                if (status == ErrorCodes.Succes)
                {
                    return Ok(entities);
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

        private async Task AugmentDebtorOrderMasterAsync(CrudAPI api, DebtorOrderClient order, JObject jsonObject)
        {
            var account = order.Account;
            var dcAccount = await _cache.GetOrCreateAsync($"Debtors:{account}", async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromDays(1));

                var debtors = await api.Query<DebtorClient>(new[] {
                            PropValuePair.GenereteWhereElements(typeof(DebtorClient).GetProperty(nameof(DebtorClient.Account)), account)
                        });

                if (debtors.Length != 1)
                {
                    throw new Exception($"Unable to find debtor with account number {account}");
                }

                return debtors[0];
            });

            var assignedProperties = new HashSet<string>(
                jsonObject.Properties().Select(x => x.Name).Concat(jsonObject.Properties().Select(x => $"_{x.Name}")), StringComparer.OrdinalIgnoreCase);
            // This is copied from the disassembly of Uniconta.DataModel.DCOrder.SetMaster()
            order._DCAccount ??= dcAccount._Account;
            order._Currency = assignedProperties.Contains(nameof(DebtorOrderClient._Currency)) ? order._Currency : dcAccount._Currency;
            order._EndDiscountPct = assignedProperties.Contains(nameof(DebtorOrderClient._EndDiscountPct)) ? order._EndDiscountPct : dcAccount._EndDiscountPct;
            order._LayoutGroup ??= dcAccount._LayoutGroup;
            order._PriceList ??= dcAccount._PriceList;
            order._Employee ??= dcAccount._Employee;
            order._Dim1 ??= dcAccount._Dim1;
            order._Dim2 ??= dcAccount._Dim2;
            order._Dim3 ??= dcAccount._Dim3;
            order._Dim4 ??= dcAccount._Dim4;
            order._Dim5 ??= dcAccount._Dim5;
            //order._RowId = 0; // Not assignable, but 0 should be the default value
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
