using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace UnicontaRest
{
    public static class RouteValueDictionaryExtensions
    {
        public static bool TryGetValue<T>(this RouteValueDictionary values, string key, out T value)
        {
            if (values.TryGetValue(key, out var @objectValue))
            {
                if (objectValue == default)
                {
                    value = default;
                    return true;
                }

                var converter = TypeDescriptor.GetConverter(typeof(T));

                if (converter.CanConvertFrom(objectValue.GetType()))
                {
                    value = (T)converter.ConvertFrom(objectValue);
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
