using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LibEthernetIPStack.ObjectsLibrary
{
    public class CIPAttributeIdSerializer : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // find all properties with type 'int'
            var properties = value.GetType().GetProperties();

            writer.WriteStartObject();

            foreach (var property in properties)
            {
                if (property.CustomAttributes.Any())
                {
                    var frst = property.CustomAttributes.First();

                    if (frst.AttributeType == typeof(CIPAttributId))
                    {
                        if (frst.ConstructorArguments.Count == 2)
                        {
                            var attId = (string)frst.ConstructorArguments[1].Value;
                            if (string.IsNullOrEmpty(attId))
                                continue;

                            writer.WritePropertyName(attId);

                            var propertyValue = property.GetValue(value);
                            if (propertyValue != null && !propertyValue.GetType().IsPrimitive && !(propertyValue is string))
                            {
                                serializer.Serialize(writer, propertyValue, propertyValue.GetType());
                            }
                            else
                                writer.WriteValue(propertyValue);

                            // let the serializer serialize the value itself
                            // (so this converter will work with any other type, not just int)
                            //serializer.Serialize(writer, property.GetValue(value, null));
                        }
                    }
                }
            }

            writer.WriteEndObject();
        }

        public override bool CanRead
        {
            get { return false; }
        } 
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }
    }
}
