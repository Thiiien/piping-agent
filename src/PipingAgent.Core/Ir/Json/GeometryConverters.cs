using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using PipingAgent.Core.Geometry;

namespace PipingAgent.Core.Ir.Json
{
    /// <summary>Điểm và véc-tơ ghi thành mảng số, ví dụ <c>[21500, 12000, 4100]</c>, theo ví dụ ở mục 7.5.</summary>
    internal static class NumberArray
    {
        public static double[] Read(ref Utf8JsonReader reader, int expected)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Cần mảng {expected} số, gặp {reader.TokenType}.");
            }

            var values = new double[expected];
            for (var i = 0; i < expected; i++)
            {
                if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
                {
                    throw new JsonException($"Cần mảng {expected} số, phần tử thứ {i + 1} không phải số.");
                }

                values[i] = reader.GetDouble();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException($"Cần mảng đúng {expected} số, mảng dài hơn.");
            }

            return values;
        }

        public static void Write(Utf8JsonWriter writer, params double[] values)
        {
            writer.WriteStartArray();
            foreach (var v in values) writer.WriteNumberValue(v);
            writer.WriteEndArray();
        }
    }

    public sealed class Point2Converter : JsonConverter<Point2>
    {
        public override Point2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var v = NumberArray.Read(ref reader, 2);
            return new Point2(v[0], v[1]);
        }

        public override void Write(Utf8JsonWriter writer, Point2 value, JsonSerializerOptions options) =>
            NumberArray.Write(writer, value.X, value.Y);
    }

    public sealed class Point3Converter : JsonConverter<Point3>
    {
        public override Point3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var v = NumberArray.Read(ref reader, 3);
            return new Point3(v[0], v[1], v[2]);
        }

        public override void Write(Utf8JsonWriter writer, Point3 value, JsonSerializerOptions options) =>
            NumberArray.Write(writer, value.X, value.Y, value.Z);
    }

    public sealed class Vector2Converter : JsonConverter<Vector2>
    {
        public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var v = NumberArray.Read(ref reader, 2);
            return new Vector2(v[0], v[1]);
        }

        public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options) =>
            NumberArray.Write(writer, value.X, value.Y);
    }

    public sealed class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var v = NumberArray.Read(ref reader, 3);
            return new Vector3(v[0], v[1], v[2]);
        }

        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options) =>
            NumberArray.Write(writer, value.X, value.Y, value.Z);
    }

    /// <summary>Hộp bao ghi thành <c>{"min":[x,y],"max":[x,y]}</c>.</summary>
    public sealed class Box2Converter : JsonConverter<Box2>
    {
        public override Box2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Point2? min = null, max = null;
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Hộp bao phải là đối tượng có min và max.");
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var name = reader.GetString();
                reader.Read();
                var v = NumberArray.Read(ref reader, 2);
                if (string.Equals(name, "min", StringComparison.OrdinalIgnoreCase)) min = new Point2(v[0], v[1]);
                else if (string.Equals(name, "max", StringComparison.OrdinalIgnoreCase)) max = new Point2(v[0], v[1]);
                else throw new JsonException($"Hộp bao không có trường {name}.");
            }

            if (min == null || max == null) throw new JsonException("Hộp bao thiếu min hoặc max.");
            return new Box2(min.Value, max.Value);
        }

        public override void Write(Utf8JsonWriter writer, Box2 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("min");
            NumberArray.Write(writer, value.Min.X, value.Min.Y);
            writer.WritePropertyName("max");
            NumberArray.Write(writer, value.Max.X, value.Max.Y);
            writer.WriteEndObject();
        }
    }

    public sealed class Box3Converter : JsonConverter<Box3>
    {
        public override Box3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Point3? min = null, max = null;
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Hộp bao phải là đối tượng có min và max.");
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var name = reader.GetString();
                reader.Read();
                var v = NumberArray.Read(ref reader, 3);
                if (string.Equals(name, "min", StringComparison.OrdinalIgnoreCase)) min = new Point3(v[0], v[1], v[2]);
                else if (string.Equals(name, "max", StringComparison.OrdinalIgnoreCase)) max = new Point3(v[0], v[1], v[2]);
                else throw new JsonException($"Hộp bao không có trường {name}.");
            }

            if (min == null || max == null) throw new JsonException("Hộp bao thiếu min hoặc max.");
            return new Box3(min.Value, max.Value);
        }

        public override void Write(Utf8JsonWriter writer, Box3 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("min");
            NumberArray.Write(writer, value.Min.X, value.Min.Y, value.Min.Z);
            writer.WritePropertyName("max");
            NumberArray.Write(writer, value.Max.X, value.Max.Y, value.Max.Z);
            writer.WriteEndObject();
        }
    }
}
