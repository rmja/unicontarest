using System;
using System.Reflection;
using Uniconta.Common;

namespace UnicontaRest
{
    public static class PropValuePairEx
    {
        public static PropValuePair GenereteWhereElements(Type modelType, string propertyName, string value)
        {
            if (propertyName.StartsWith("_"))
            {
                var fieldType = modelType.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.FieldType;
                return PropValuePair.GenereteWhereElements(propertyName, fieldType, value);
            }
            else
            {
                var propertyInfo = modelType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                return PropValuePair.GenereteWhereElements(propertyInfo, value);
            }
        }
    }
}
