using Microsoft.AspNetCore.Routing;
using System.ComponentModel;

namespace UnicontaRest
{
    public static class RouteValueDictionaryExtensions
    {
        public static bool TryGetValue<T>(this RouteValueDictionary values, string key, out T value)
        {
            if (values.TryGetValue(key, out var @objectValue))
            {
                if (objectValue is null)
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

        public static T GetValueOrDefault<T>(this RouteValueDictionary values, string key, T defaultValue = default)
        {
            return TryGetValue<T>(values, key, out var value) ? value : defaultValue;
        }
    }
}
