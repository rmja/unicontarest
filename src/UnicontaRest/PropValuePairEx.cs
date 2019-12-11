using System;
using System.Linq;
using System.Reflection;
using Uniconta.Common;

namespace UnicontaRest
{
    public static class PropValuePairEx
    {
        public static PropValuePair GenereteWhereElements(Type modelType, string propertyName, string value)
        {
            PropValuePair result;
            var values = value.Split(" OR ", StringSplitOptions.RemoveEmptyEntries);

            if (propertyName.StartsWith("_"))
            {
                var fieldType = modelType.GetField(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.FieldType;

                result = PropValuePair.GenereteWhereElements(propertyName, fieldType, values[0]);
            }
            else
            {
                var propertyInfo = modelType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                result = PropValuePair.GenereteWhereElements(propertyInfo, values[0]);
            }

            if (values.Length > 1)
            {
                foreach (var or in values.Skip(1))
                {
                    result.OrList.Add(new PropValueNode() { Value = or });
                }
            }

            return result;
        }
    }
}
