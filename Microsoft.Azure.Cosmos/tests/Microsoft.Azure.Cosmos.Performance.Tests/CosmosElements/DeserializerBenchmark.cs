﻿namespace Microsoft.Azure.Cosmos.Performance.Tests.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using BenchmarkDotNet.Attributes;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
    using Microsoft.Azure.Cosmos.Performance.Tests.Models;
    using Newtonsoft.Json;

    [MemoryDiagnoser]
    public class DeserializerBenchmark
    {
        private readonly string json;
        private readonly ReadOnlyMemory<byte> textBuffer;
        private readonly ReadOnlyMemory<byte> binaryBuffer;

        public DeserializerBenchmark()
        {
            List<Person> people = new List<Person>();
            for (int i = 0; i < 1000; i++)
            {
                people.Add(Person.GetRandomPerson());
            }

            this.json = JsonConvert.SerializeObject(people);

            IJsonWriter jsonTextWriter = Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Text);
            CosmosElement.Parse(this.json).WriteTo(jsonTextWriter);
            this.textBuffer = jsonTextWriter.GetResult();

            IJsonWriter jsonBinaryWriter = Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary);
            CosmosElement.Parse(this.json).WriteTo(jsonBinaryWriter);
            this.binaryBuffer = jsonBinaryWriter.GetResult();
        }

        [Benchmark]
        public void CosmosElementDeserializer_TryDeserialize_Text()
        {
            IReadOnlyList<Person> deserializedPeople = CosmosElementDeserializer.Deserialize<IReadOnlyList<Person>>(this.textBuffer);
        }

        [Benchmark]
        public void CosmosElementDeserializer_TryDeserialize_Binary()
        {
            IReadOnlyList<Person> deserializedPeople = CosmosElementDeserializer.Deserialize<IReadOnlyList<Person>>(this.binaryBuffer);
        }

        [Benchmark]
        public void JsonConvert_DeserializeObject_Text()
        {
            IReadOnlyList<Person> deserializedPeople = JsonConvert.DeserializeObject<IReadOnlyList<Person>>(this.json);
        }

        [Benchmark]
        public void JsonConvert_DeserializeObject_Binary()
        {
            JsonSerializer jsonSerializer = JsonSerializer.CreateDefault();
            CosmosDBToNewtonsoftReader cosmosDBToNewtonsoftReader = new CosmosDBToNewtonsoftReader(this.binaryBuffer);
            IReadOnlyList<Person> deserializedPeople = jsonSerializer.Deserialize<IReadOnlyList<Person>>(cosmosDBToNewtonsoftReader);
        }
    }
}
