using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;
using Uniconta.DataModel;

namespace UnicontaRest
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<UnicontaConnectionProvider>();
            services.Configure<UnicontaRestOptions>(Configuration);

            services
                .AddMemoryCache()
                .AddControllers()
                    .AddNewtonsoftJson(options =>
                    {
                        options.SerializerSettings.ContractResolver = new DefaultContractResolver();
                        options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                        options.SerializerSettings.Converters.Add(new StringEnumConverter());
                        options.SerializerSettings.Converters.Add(new TableFieldDataConverter());
                    });
        }

        public void Configure(IApplicationBuilder app)
        {
            app
                .UseDeveloperExceptionPage()
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapControllers());
        }

        public class TableFieldDataConverter : JsonConverter<ITableFieldData>
        {
            public override bool CanRead => false;

            public override ITableFieldData ReadJson(JsonReader reader, Type objectType, ITableFieldData existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, ITableFieldData value, JsonSerializer serializer)
            {
                if (value == null)
                {
                    writer.WriteNull();

                    return;
                }

                var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(value.GetType());

                writer.WriteStartObject();

                foreach (var property in contract.Properties)
                {
                    if (!property.Ignored)
                    {
                        writer.WritePropertyName(property.PropertyName);

                        if (property.PropertyType == typeof(TableFieldDataRow))
                        {
                            writer.WriteStartArray();

                            foreach (var field in value.UserFieldDef())
                            {
                                var fieldValue = value.GetUserFieldIdx(field.Index);
                                writer.WriteValue(fieldValue);
                            }

                            writer.WriteEndArray();
                        }
                        else
                        {
                            var propertyValue = property.ValueProvider.GetValue(value);

                            if (Object.ReferenceEquals(value, propertyValue))
                            {
                                writer.WriteNull();
                            }
                            else
                            {
                                serializer.Serialize(writer, propertyValue);
                            }
                        }
                    }
                }

                writer.WriteEndObject();
            }
        }
    }
}
