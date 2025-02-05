﻿using System;
using System.Text.Json;

namespace Telstra.Twins.Core
{
    public interface IDigitalTwinSerializer
    {
        object DeserializeTwin(string json, JsonSerializerOptions serializerOptions = null);
        T DeserializeTwin<T>(string json, JsonSerializerOptions serializerOptions = null);
        string SerializeModel(Type twinType, bool htmlEncode = false, JsonSerializerOptions serializerOptions = null);
        string SerializeModel<T>(bool htmlEncode = false, JsonSerializerOptions serializerOptions = null);
        string SerializeTwin(object twin, bool htmlEncode = false, JsonSerializerOptions serializerOptions = null);
        string SerializeTwin<T>(T twin, bool htmlEncode = false, JsonSerializerOptions serializerOptions = null);
    }
}
