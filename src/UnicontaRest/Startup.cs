using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
                        options.SerializerSettings.Converters.Add(new TableFieldDataRowConverter());
                    });
        }

        public void Configure(IApplicationBuilder app)
        {
            app
                .UseDeveloperExceptionPage()
                .UseRouting()
                .UseEndpoints(endpoints => endpoints.MapControllers());
        }

        public class TableFieldDataRowConverter : JsonConverter<TableFieldDataRow>
        {
            public override TableFieldDataRow ReadJson(JsonReader reader, Type objectType, TableFieldDataRow existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                throw new NotImplementedException();
            }

            public override void WriteJson(JsonWriter writer, TableFieldDataRow value, JsonSerializer serializer)
            {
                writer.WriteStartArray();

                for (var i = 0; i < value.Count; i++)
                {
                    writer.WriteValue(value[i]);
                }

                writer.WriteEndArray();
            }
        }
    }
}
