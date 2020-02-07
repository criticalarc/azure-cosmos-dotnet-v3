﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json.Interop
{
    using System;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    /// Wrapper class that implements a Newtonsoft JsonReader,
    /// but forwards all the calls to a CosmosDB JSON reader.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    sealed class CosmosDBToNewtonsoftReader : Newtonsoft.Json.JsonReader
    {
        /// <summary>
        /// Singleton boxed value for null.
        /// </summary>
        private static readonly object Null = null;

        /// <summary>
        /// Singleton boxed value for false.
        /// </summary>
        private static readonly object False = false;

        /// <summary>
        /// Singleton boxed value for true.
        /// </summary>
        private static readonly object True = true;

        /// <summary>
        /// The CosmosDB JSON Reader that will be used for implementation.
        /// </summary>
        private readonly IJsonReader jsonReader;

        /// <summary>
        /// Initializes a new instance of the NewtonsoftReader class.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        public CosmosDBToNewtonsoftReader(ReadOnlyMemory<byte> buffer)
        {
            this.jsonReader = Microsoft.Azure.Cosmos.Json.JsonReader.Create(buffer);
        }

        /// <summary>
        /// Reads the next token from the reader.
        /// </summary>
        /// <returns>True if a token was read, else false.</returns>
        public override bool Read()
        {
            bool read = this.jsonReader.Read();
            if (!read)
            {
                this.SetToken(JsonToken.None);
                return false;
            }

            JsonTokenType jsonTokenType = this.jsonReader.CurrentTokenType;
            JsonToken newtonsoftToken;
            object value;
            switch (jsonTokenType)
            {
                case JsonTokenType.BeginArray:
                    newtonsoftToken = JsonToken.StartArray;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.EndArray:
                    newtonsoftToken = JsonToken.EndArray;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.BeginObject:
                    newtonsoftToken = JsonToken.StartObject;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.EndObject:
                    newtonsoftToken = JsonToken.EndObject;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.String:
                    newtonsoftToken = JsonToken.String;
                    value = this.jsonReader.GetStringValue();
                    break;

                case JsonTokenType.Number:
                    Number64 number64Value = this.jsonReader.GetNumberValue();
                    if (number64Value.IsInteger)
                    {
                        value = Number64.ToLong(number64Value);
                        newtonsoftToken = JsonToken.Integer;
                    }
                    else
                    {
                        value = Number64.ToDouble(number64Value);
                        newtonsoftToken = JsonToken.Float;
                    }
                    break;

                case JsonTokenType.True:
                    newtonsoftToken = JsonToken.Boolean;
                    value = CosmosDBToNewtonsoftReader.True;
                    break;

                case JsonTokenType.False:
                    newtonsoftToken = JsonToken.Boolean;
                    value = CosmosDBToNewtonsoftReader.False;
                    break;

                case JsonTokenType.Null:
                    newtonsoftToken = JsonToken.Null;
                    value = CosmosDBToNewtonsoftReader.Null;
                    break;

                case JsonTokenType.FieldName:
                    newtonsoftToken = JsonToken.PropertyName;
                    value = this.jsonReader.GetStringValue();
                    break;

                default:
                    throw new ArgumentException($"Unexpected jsonTokenType: {jsonTokenType}");
            }

            this.SetToken(newtonsoftToken, value);
            return read;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="Byte"/>[].
        /// </summary>
        /// <returns>A <see cref="Byte"/>[] or <c>null</c> if the next JSON token is null. This method will return <c>null</c> at the end of an array.</returns>
        public override byte[] ReadAsBytes()
        {
            this.Read();
            if (!this.jsonReader.TryGetBufferedRawJsonToken(out ReadOnlyMemory<byte> bufferedRawJsonToken))
            {
                throw new Exception("Failed to get the bytes.");
            }

            byte[] value = bufferedRawJsonToken.ToArray();
            this.SetToken(JsonToken.Bytes, value);
            return value;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="Nullable{T}"/> of <see cref="DateTime"/>.
        /// </summary>
        /// <returns>A <see cref="Nullable{T}"/> of <see cref="DateTime"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override DateTime? ReadAsDateTime()
        {
            DateTime? dateTime = (DateTime?)this.ReadAsTypeFromString<DateTime>(DateTime.Parse);
            if (dateTime != null)
            {
                this.SetToken(JsonToken.Date, dateTime);
            }

            return dateTime;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <returns>A <see cref="Nullable{T}"/> of <see cref="DateTimeOffset"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override DateTimeOffset? ReadAsDateTimeOffset()
        {
            DateTimeOffset? dateTimeOffset = (DateTimeOffset?)this.ReadAsTypeFromString<DateTimeOffset>(DateTimeOffset.Parse);
            if (dateTimeOffset != null)
            {
                this.SetToken(JsonToken.Date, dateTimeOffset);
            }

            return dateTimeOffset;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="Nullable{T}"/> of <see cref="Decimal"/>.
        /// </summary>
        /// <returns>A <see cref="Nullable{T}"/> of <see cref="Decimal"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override decimal? ReadAsDecimal()
        {
            decimal? value = (decimal?)this.ReadNumberValue();
            if (value != null)
            {
                this.SetToken(JsonToken.Float, value);
            }

            return value;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="Nullable{T}"/> of <see cref="Int32"/>.
        /// </summary>
        /// <returns>A <see cref="Nullable{T}"/> of <see cref="Int32"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override int? ReadAsInt32()
        {
            int? value = (int?)this.ReadNumberValue();
            if (value != null)
            {
                this.SetToken(JsonToken.Integer, value);
            }

            return value;
        }

        /// <summary>
        /// Reads the next JSON token from the source as a <see cref="String"/>.
        /// </summary>
        /// <returns>A <see cref="String"/>. This method will return <c>null</c> at the end of an array.</returns>
        public override string ReadAsString()
        {
            string value = (string)this.ReadAsTypeFromString<string>((x) => x);
            if (value != null)
            {
                this.SetToken(JsonToken.String, value);
            }

            return value;
        }

        /// <summary>
        /// Reads the next string token and deserializes it to a type.
        /// </summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="parse">The function that deserializes the token.</param>
        /// <returns>The next string token deserialized to a type or null if at the end of an array.</returns>
        private object ReadAsTypeFromString<T>(Func<string, T> parse)
        {
            this.Read();
            object value;
            if (this.jsonReader.CurrentTokenType == JsonTokenType.EndArray)
            {
                value = null;
            }
            else
            {
                string stringValue = this.jsonReader.GetStringValue();
                value = parse(stringValue);
            }

            return value;
        }

        /// <summary>
        /// Reads the next number token but returns null at the end of an array.
        /// </summary>
        /// <returns>The next number token but returns null at the end of an array.</returns>
        private double? ReadNumberValue()
        {
            this.Read();
            double? value;
            if (this.jsonReader.CurrentTokenType == JsonTokenType.EndArray)
            {
                value = null;
            }
            else
            {
                value = Number64.ToDouble(this.jsonReader.GetNumberValue());
            }

            return value;
        }
    }
}
