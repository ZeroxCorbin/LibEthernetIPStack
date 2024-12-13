using Newtonsoft.Json;
using System;
using System.Linq;

namespace LibEthernetIPStack.CIP;

public class CIPAttributeIdSerializer : JsonConverter
{
    public override bool CanConvert(Type objectType) => true;

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        // find all properties with type 'int'
        System.Reflection.PropertyInfo[] properties = value.GetType().GetProperties();

        writer.WriteStartObject();

        foreach (System.Reflection.PropertyInfo property in properties)
            if (property.CustomAttributes.Any())
            {
                System.Reflection.CustomAttributeData frst = property.CustomAttributes.First();

                if (frst.AttributeType == typeof(CIPAttributId))
                    if (frst.ConstructorArguments.Count == 2)
                    {
                        string attId = (string)frst.ConstructorArguments[1].Value;
                        if (string.IsNullOrEmpty(attId))
                            continue;

                        writer.WritePropertyName(attId);

                        object propertyValue = property.GetValue(value);
                        if (propertyValue != null && !propertyValue.GetType().IsPrimitive && propertyValue is not string)
                            serializer.Serialize(writer, propertyValue, propertyValue.GetType());
                        else
                            writer.WriteValue(propertyValue);

                        // let the serializer serialize the value itself
                        // (so this converter will work with any other type, not just int)
                        //serializer.Serialize(writer, property.GetValue(value, null));
                    }
            }

        writer.WriteEndObject();
    }

    public override bool CanRead => false;
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
}
