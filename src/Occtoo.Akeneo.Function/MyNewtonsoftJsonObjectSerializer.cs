using Azure.Messaging.EventGrid;
using CSharpFunctionalExtensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Occtoo.Functional.Extensions;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function;

public class MyNewtonsoftJsonObjectSerializer : Azure.Core.Serialization.ObjectSerializer
{
    private static readonly JsonSerializerSettings Settings = new() { Converters = { new MaybeJsonConverter() } };

    private static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

    public override object Deserialize(Stream stream, Type returnType, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        return Serializer.Deserialize(reader, returnType);
    }

    public override async ValueTask<object> DeserializeAsync(Stream stream, Type returnType, CancellationToken cancellationToken)
    {
        //This is needed for function to deserialize EventGridEvent messages correctly
        if (returnType == typeof(EventGridEvent))
        {
            object? deserialized = await System.Text.Json.JsonSerializer.DeserializeAsync(stream, returnType, cancellationToken: cancellationToken);
            return (EventGridEvent)deserialized;
        }

        using var reader = new StreamReader(stream);
        return Serializer.Deserialize(reader, returnType);
    }

    public override void Serialize(Stream stream, object value, Type inputType, CancellationToken cancellationToken)
    {
        using var streamWriter = new StreamWriter(stream);
        Serializer.Serialize(streamWriter, value);
    }

    public override BinaryData Serialize(object value, Type inputType = null, CancellationToken cancellationToken = default)
    {
        return BinaryData.FromString(JsonConvert.SerializeObject(value, Settings));
    }

    public override async ValueTask SerializeAsync(Stream stream, object value, Type inputType, CancellationToken cancellationToken)
    {
        await using var streamWriter = new StreamWriter(stream);
        Serializer.Serialize(streamWriter, value);
    }

    public override ValueTask<BinaryData> SerializeAsync(object value, Type inputType = null, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(BinaryData.FromString(JsonConvert.SerializeObject(value, Settings)));
    }
}

public class MaybeJsonConverter : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value == null)
            return;
        var dynamicValue = ((dynamic)value);
        if (dynamicValue.HasNoValue)
            serializer.Serialize(writer, null);
        else
        {
            serializer.Serialize(writer, dynamicValue.Value);
        }
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        JToken obj = JToken.Load(reader);
        object value = obj.Type switch
        {
            JTokenType.Boolean => Maybe<bool>.From(obj.Value<bool>()),
            JTokenType.Float => Maybe<double>.From(obj.Value<double>()),
            JTokenType.Integer => MapInteger(obj, objectType),
            JTokenType.String => obj.Value<string>().Empty() ? Maybe<string>.None : Maybe<string>.From(obj.Value<string>()),
            JTokenType.TimeSpan => Maybe<TimeSpan>.From(obj.Value<TimeSpan>()),
            JTokenType.Date => Maybe<DateTimeOffset>.From(obj.Value<DateTimeOffset>()),
            JTokenType.Guid => Maybe<Guid>.From(obj.Value<Guid>()),
            JTokenType.Object => CreateMaybeOfObject(objectType, obj),
            JTokenType.Null => CreateMaybeNone(objectType)
        };
        return value;
    }

    public override bool CanConvert(Type objectType) =>
        objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Maybe<>);

    private object CreateMaybeOfObject(Type targetType, JToken token)
    {
        var from = targetType.GetMethod(nameof(Maybe<object>.From));
        return from.Invoke(null, new[] { token.ToObject(targetType.GetGenericArguments().First()) });
    }
    private object CreateMaybeNone(Type valueType) => Activator.CreateInstance(valueType);

    private object MapInteger(JToken token, Type targetType)
        => targetType switch
        {
            { } i when i == typeof(Maybe<int>) => Maybe<int>.From(token.Value<int>()),
            { } l when l == typeof(Maybe<long>) => Maybe<long>.From(token.Value<long>()),
            { } b when b == typeof(Maybe<byte>) => Maybe<byte>.From(token.Value<byte>())
        };
}
