﻿//-----------------------------------------------------------------------
// <copyright file="LinqAttributeContractTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Converters;
    using BaselineTest;
    using System.Linq.Dynamic;
    using System.Text;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using System.Threading.Tasks;

    [TestClass]
    public class LinqTranslationBaselineTests : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            var authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            var uri = new Uri(Utils.ConfigurationManager.AppSettings["GatewayEndpoint"]);
            var connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
                EnableEndpointDiscovery = true,
            };

            cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder) => {
                    cosmosClientBuilder.WithCustomSerializer(new CustomJsonSerializer(new JsonSerializerSettings()
                    {
                        ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                        // We want to simulate the property not exist so ignoring the null value
                        NullValueHandling = NullValueHandling.Ignore
                    })).WithConnectionModeGateway();
            });
            await cosmosClient.GetDatabase(id : nameof(LinqTranslationBaselineTests)).DeleteAsync();
            testDb = await cosmosClient.CreateDatabaseAsync(id: nameof(LinqTranslationBaselineTests));
        }

        [ClassCleanup]
        public async static Task CleanUp()
        {
            if (testDb != null)
            {
                await testDb.DeleteAsync();
            }
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            testContainer = await testDb.CreateContainerAsync(new ContainerProperties(id : Guid.NewGuid().ToString(),partitionKeyPath : "/Pk"));
        }

        [TestCleanup]
        public async Task TestCleanUp()
        {
            await testContainer.DeleteContainerAsync();
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum TestEnum
        {
            Zero,
            One,
            Two
        }

        public enum TestEnum2
        {
            Zero,
            One,
            Two
        }

        public static bool ObjectSequenceEquals<T>(IEnumerable<T> enumA, IEnumerable<T> enumB)
        {
            if (enumA == null || enumB == null) return enumA == enumB;
            return enumA.SequenceEqual(enumB);
        }

        public static bool ObjectEquals(object objA, object objB)
        {
            if (objA == null || objB == null) return objA == objB;
            return objA.Equals(objB);
        }

        internal class DataObject : LinqTestObject
        {
            public double NumericField;
            public decimal DecimalField;
            public double IntField;
            public string StringField;
            public string StringField2;
            public int[] ArrayField;
            public List<int> EnumerableField;
            public Point Point;
            public int? NullableField;

            [JsonConverter(typeof(StringEnumConverter))]
            public TestEnum EnumField1;

            [JsonConverter(typeof(StringEnumConverter))]
            public TestEnum? NullableEnum1;

            // These fields should also serialize as string
            // the attribute is specified on the type level
            public TestEnum EnumField2;
            public TestEnum? NullableEnum2;

            // This field should serialize as number
            // there is no converter applied on the property
            // of the enum definition
            public TestEnum2 EnumNumber;

            [JsonConverter(typeof(UnixDateTimeConverter))]
            public DateTime UnixTime;

            [JsonConverter(typeof(IsoDateTimeConverter))]
            public DateTime IsoTime;

            // This field should serialize as ISO Date
            // as this is the default DateTimeConverter
            // used by Newtonsoft
            public DateTime DefaultTime;

            [JsonProperty(PropertyName = "id")]
            public string Id;

            public string Pk;
        }

        [TestMethod]
        public void TestLiteralSerialization()
        {
            List<DataObject> testData = new List<DataObject>();
            var constantQuery = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution : true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? constantQuery : testData.AsQueryable();
            List<LinqTestInput> inputs = new List<LinqTestInput>();
            // Byte
            inputs.Add(new LinqTestInput("Byte 1", b => getQuery(b).Select(doc => new { value = 1 })));
            inputs.Add(new LinqTestInput("Byte MinValue", b => getQuery(b).Select(doc => new { value = Byte.MinValue})));
            inputs.Add(new LinqTestInput("Byte MaxValue", b => getQuery(b).Select(doc => new { value = Byte.MaxValue})));
            // SByte
            inputs.Add(new LinqTestInput("SByte 2", b => getQuery(b).Select(doc => new { value = 2})));
            inputs.Add(new LinqTestInput("SByte MinValue", b => getQuery(b).Select(doc => new { value = SByte.MinValue})));
            inputs.Add(new LinqTestInput("SByte MaxValue", b => getQuery(b).Select(doc => new { value = SByte.MaxValue})));
            // UInt16
            inputs.Add(new LinqTestInput("UInt16 3", b => getQuery(b).Select(doc => new { value = 3})));
            inputs.Add(new LinqTestInput("UInt16 MinValue", b => getQuery(b).Select(doc => new { value = UInt16.MinValue})));
            inputs.Add(new LinqTestInput("UInt16 MaxValue", b => getQuery(b).Select(doc => new { value = UInt16.MaxValue})));
            // UInt32
            inputs.Add(new LinqTestInput("UInt32 4", b => getQuery(b).Select(doc => new { value = 4})));
            inputs.Add(new LinqTestInput("UInt32 MinValue", b => getQuery(b).Select(doc => new { value = UInt32.MinValue})));
            inputs.Add(new LinqTestInput("UInt32 MaxValue", b => getQuery(b).Select(doc => new { value = UInt32.MaxValue})));
            // UInt64
            inputs.Add(new LinqTestInput("UInt64 5", b => getQuery(b).Select(doc => new { value = 5})));
            inputs.Add(new LinqTestInput("UInt64 MinValue", b => getQuery(b).Select(doc => new { value = UInt64.MinValue})));
            inputs.Add(new LinqTestInput("UInt64 MaxValue", b => getQuery(b).Select(doc => new { value = UInt64.MaxValue})));
            // Int16
            inputs.Add(new LinqTestInput("Int16 6", b => getQuery(b).Select(doc => new { value = 6})));
            inputs.Add(new LinqTestInput("Int16 MinValue", b => getQuery(b).Select(doc => new { value = Int16.MinValue})));
            inputs.Add(new LinqTestInput("Int16 MaxValue", b => getQuery(b).Select(doc => new { value = Int16.MaxValue})));
            // Int32
            inputs.Add(new LinqTestInput("Int32 7", b => getQuery(b).Select(doc => new { value = 7})));
            inputs.Add(new LinqTestInput("Int32 MinValue", b => getQuery(b).Select(doc => new { value = Int32.MinValue})));
            inputs.Add(new LinqTestInput("Int32 MaxValue", b => getQuery(b).Select(doc => new { value = Int32.MaxValue})));
            // Int64
            inputs.Add(new LinqTestInput("Int64 8", b => getQuery(b).Select(doc => new { value = 8})));
            inputs.Add(new LinqTestInput("Int64 MinValue", b => getQuery(b).Select(doc => new { value = Int64.MinValue})));
            inputs.Add(new LinqTestInput("Int64 MaxValue", b => getQuery(b).Select(doc => new { value = Int64.MaxValue})));
            // Decimal
            inputs.Add(new LinqTestInput("Decimal 9", b => getQuery(b).Select(doc => new { value = 9})));
            inputs.Add(new LinqTestInput("Decimal MinValue", b => getQuery(b).Select(doc => new { value = Decimal.MinValue})));
            inputs.Add(new LinqTestInput("Decimal MaxValue", b => getQuery(b).Select(doc => new { value = Decimal.MaxValue})));
            // Double
            inputs.Add(new LinqTestInput("Double 10", b => getQuery(b).Select(doc => new { value = 10})));
            inputs.Add(new LinqTestInput("Double MinValue", b => getQuery(b).Select(doc => new { value = Double.MinValue})));
            inputs.Add(new LinqTestInput("Double MaxValue", b => getQuery(b).Select(doc => new { value = Double.MaxValue})));
            // Single
            inputs.Add(new LinqTestInput("Single 11", b => getQuery(b).Select(doc => new { value = 11})));
            inputs.Add(new LinqTestInput("Single MinValue", b => getQuery(b).Select(doc => new { value = Single.MinValue})));
            inputs.Add(new LinqTestInput("Single MaxValue", b => getQuery(b).Select(doc => new { value = Single.MaxValue})));
            // Bool
            inputs.Add(new LinqTestInput("Bool true", b => getQuery(b).Select(doc => new { value = true})));
            inputs.Add(new LinqTestInput("Bool false", b => getQuery(b).Select(doc => new { value = false})));
            // String
            string nullStr = null;
            inputs.Add(new LinqTestInput("String empty", b => getQuery(b).Select(doc => new { value = String.Empty})));
            inputs.Add(new LinqTestInput("String str1", b => getQuery(b).Select(doc => new { value = "str1" })));
            inputs.Add(new LinqTestInput("String special", b => getQuery(b).Select(doc => new { value = "long string with speicial characters (*)(*)__)((*&*(&*&'*(&)()(*_)()(_(_)*!@#$%^ and numbers 132654890" })));
            inputs.Add(new LinqTestInput("String unicode", b => getQuery(b).Select(doc => new { value = "unicode 㐀㐁㨀㨁䶴䶵" })));
            inputs.Add(new LinqTestInput("null object", b => getQuery(b).Select(doc => new { value = nullStr })));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestTypeCheckFunctions()
        {
            // IsDefined, IsNull, and IsPrimitive are not supported on the client side.
            // Partly because IsPrimitive is not trivial to implement.
            // Therefore these methods are verified with baseline only.
            List<DataObject> data = new List<DataObject>();
            var query = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution : true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? query : data.AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("IsDefined array", b => getQuery(b).Select(doc => doc.ArrayField.IsDefined())));
            inputs.Add(new LinqTestInput("IsDefined string", b => getQuery(b).Where(doc => doc.StringField.IsDefined())));
            inputs.Add(new LinqTestInput("IsNull array", b => getQuery(b).Select(doc => doc.ArrayField.IsNull())));
            inputs.Add(new LinqTestInput("IsNull string", b => getQuery(b).Where(doc => doc.StringField.IsNull())));
            inputs.Add(new LinqTestInput("IsPrimitive array", b => getQuery(b).Select(doc => doc.ArrayField.IsPrimitive())));
            inputs.Add(new LinqTestInput("IsPrimitive string", b => getQuery(b).Where(doc => doc.StringField.IsPrimitive())));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializer()
        {
            const int Records = 100;
            const int NumAbsMax = 500;
            const int MaxStringLength = 100;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                DataObject obj = new DataObject();
                obj.NumericField = random.Next(NumAbsMax * 2) - NumAbsMax;
                obj.StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Select w/ DataObject initializer", b => getQuery(b).Select(doc => new DataObject() { NumericField = doc.NumericField, StringField = doc.StringField })));
            inputs.Add(new LinqTestInput("Filter w/ DataObject initializer", b => getQuery(b).Where(doc => doc == new DataObject() { NumericField = doc.NumericField, StringField = doc.StringField })));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestStringEnumJsonConverter()
        {
            const int Records = 100;
            int testEnumCount = Enum.GetNames(typeof(TestEnum)).Length;
            int testEnum2Count = Enum.GetNames(typeof(TestEnum2)).Length;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                var obj = new DataObject();
                obj.EnumField1 = (TestEnum)(random.Next(testEnumCount));
                obj.EnumField2 = (TestEnum)(random.Next(testEnumCount));
                if (random.NextDouble() < 0.5)
                {
                    obj.NullableEnum1 = (TestEnum)(random.Next(testEnumCount));
                }
                if (random.NextDouble() < 0.5)
                {
                    obj.NullableEnum2 = (TestEnum)(random.Next(testEnumCount));
                }
                obj.EnumNumber = (TestEnum2)(random.Next(testEnum2Count));
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Filter w/ enum field comparison", b => getQuery(b).Where(doc => doc.EnumField1 == TestEnum.One)));
            inputs.Add(new LinqTestInput("Filter w/ enum field comparison #2", b => getQuery(b).Where(doc => TestEnum.One == doc.EnumField1)));
            inputs.Add(new LinqTestInput("Filter w/ enum field comparison #3", b => getQuery(b).Where(doc => doc.EnumField2 == TestEnum.Two)));
            inputs.Add(new LinqTestInput("Filter w/ nullable enum field comparison", b => getQuery(b).Where(doc => doc.NullableEnum1 == TestEnum.One)));
            inputs.Add(new LinqTestInput("Filter w/ nullable enum field comparison #2", b => getQuery(b).Where(doc => doc.NullableEnum2 == TestEnum.Two)));
            inputs.Add(new LinqTestInput("Filter w/ enum field comparison #4", b => getQuery(b).Where(doc => doc.EnumNumber == TestEnum2.Zero)));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestDateTimeJsonConverter()
        {
            const int Records = 100;
            DateTime midDateTime = new DateTime(2016, 9, 13, 0, 0, 0);
            Func<Random, DataObject> createDataObj = (random) =>
            {
                var obj = new DataObject();
                obj.IsoTime = LinqTestsCommon.RandomDateTime(random, midDateTime);
                obj.UnixTime = LinqTestsCommon.RandomDateTime(random, midDateTime);
                obj.DefaultTime = LinqTestsCommon.RandomDateTime(random, midDateTime);
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("IsoDateTimeConverter = filter", b => getQuery(b).Where(doc => doc.IsoTime == new DateTime(2016, 9, 13, 0, 0, 0))));
            inputs.Add(new LinqTestInput("IsoDateTimeConverter > filter", b => getQuery(b).Where(doc => doc.IsoTime > new DateTime(2016, 9, 13, 0, 0, 0))));
            inputs.Add(new LinqTestInput("IsoDateTimeConverter < filter", b => getQuery(b).Where(doc => doc.IsoTime < new DateTime(2016, 9, 13, 0, 0, 0))));
            inputs.Add(new LinqTestInput("UnixDateTimeConverter = filter", b => getQuery(b).Where(doc => doc.UnixTime == new DateTime(2016, 9, 13, 0, 0, 0))));
            inputs.Add(new LinqTestInput("UnixDateTimeConverter > filter", b => getQuery(b).Where(doc => doc.UnixTime > new DateTime(2016, 9, 13, 0, 0, 0))));
            inputs.Add(new LinqTestInput("UnixDateTimeConverter < filter", b => getQuery(b).Where(doc => doc.UnixTime < new DateTime(2016, 9, 13, 0, 0, 0))));
            inputs.Add(new LinqTestInput("Default (ISO) = filter", b => getQuery(b).Where(doc => doc.DefaultTime == new DateTime(2016, 9, 13, 0, 0, 0))));
            inputs.Add(new LinqTestInput("Default (ISO) > filter", b => getQuery(b).Where(doc => doc.DefaultTime > new DateTime(2016, 9, 13, 0, 0, 0))));
            inputs.Add(new LinqTestInput("Default (ISO) < filter", b => getQuery(b).Where(doc => doc.DefaultTime < new DateTime(2016, 9, 13, 0, 0, 0))));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestNullableFields()
        {
            const int Records = 5;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                var obj = new DataObject();
                if (random.NextDouble() < 0.5)
                {
                    if (random.NextDouble() < 0.1)
                    {
                        obj.NullableField = 5;
                    }
                    else
                    {
                        obj.NullableField = random.Next();
                    }
                }
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Filter", b => getQuery(b).Where(doc => doc.NullableField == 5)));
            inputs.Add(new LinqTestInput("Filter w/ .Value", b => getQuery(b).Where(doc => doc.NullableField.HasValue && doc.NullableField.Value == 5)));
            inputs.Add(new LinqTestInput("Filter w/ .HasValue", b => getQuery(b).Where(doc => doc.NullableField.HasValue)));
            inputs.Add(new LinqTestInput("Filter w/ .HasValue comparison true", b => getQuery(b).Where(doc => doc.NullableField.HasValue == true)));
            inputs.Add(new LinqTestInput("Filter w/ .HasValue comparison false", b => getQuery(b).Where(doc => doc.NullableField.HasValue == false)));
            inputs.Add(new LinqTestInput("Filter w/ .HasValue not", b => getQuery(b).Where(doc => !doc.NullableField.HasValue)));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [Ignore]
        public void TestMathFunctionsIssues()
        {
            // These are issues in scenarios with integers casting
            // the Linq query returns double values which got casted to the integer type
            // the casting is a rounded behavior e.g. 3.567 would become 4
            // whereas the casting behavior for data results is truncate behavior

            const int Records = 20;
            Func<Random, DataObject> createDataObj = (random) => new DataObject()
            {
                NumericField = 1.0 * random.Next() - random.NextDouble() / 2
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Select", b => getQuery(b).Select(doc => (int)doc.NumericField)));
            inputs.Add(new LinqTestInput("Abs int", b => getQuery(b)
                .Where(doc => doc.NumericField >= int.MinValue && doc.NumericField <= int.MaxValue)
                .Select(doc => Math.Abs((int)doc.NumericField))));
            inputs.Add(new LinqTestInput("Abs long", b => getQuery(b)
                .Where(doc => doc.NumericField >= long.MinValue && doc.NumericField <= long.MaxValue)
                .Select(doc => Math.Abs((long)doc.NumericField))));
            inputs.Add(new LinqTestInput("Abs sbyte", b => getQuery(b)
                .Where(doc => doc.NumericField >= sbyte.MinValue && doc.NumericField <= sbyte.MaxValue)
                .Select(doc => Math.Abs((sbyte)doc.NumericField))));
            inputs.Add(new LinqTestInput("Abs short", b => getQuery(b)
                .Where(doc => doc.NumericField >= short.MinValue && doc.NumericField <= short.MaxValue)
                .Select(doc => Math.Abs((short)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign int", b => getQuery(b).Select(doc => Math.Sign((int)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign long", b => getQuery(b).Select(doc => Math.Sign((long)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign sbyte", b => getQuery(b)
                .Where(doc => doc.NumericField >= sbyte.MinValue && doc.NumericField <= sbyte.MaxValue)
                .Select(doc => Math.Sign((sbyte)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign short", b => getQuery(b)
                .Where(doc => doc.NumericField >= short.MinValue && doc.NumericField <= short.MaxValue)
                .Select(doc => Math.Sign((short)doc.NumericField))));

            inputs.Add(new LinqTestInput("Round decimal", b => getQuery(b).Select(doc => Math.Round((decimal)doc.NumericField))));

            inputs.Add(new LinqTestInput("Tan", b => getQuery(b).Select(doc => Math.Tan(doc.NumericField))));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMathFunctions()
        {
            const int Records = 20;
            // when doing verification between data and query results for integer type (int, long, short, sbyte, etc.)
            // the backend returns double values which got casted to the integer type
            // the casting is a rounded behavior e.g. 3.567 would become 4, whereas the casting behavior for data results is truncate
            // therefore, for test data, we just want to have real number with the decimal part < 0.5.
            DataObject createDataObj(Random random) => new DataObject()
            {
                NumericField = 1.0 * random.Next() + random.NextDouble() / 2,
                DecimalField = (decimal)(1.0 * random.Next() + random.NextDouble()) / 2,
                IntField = 1.0 * random.Next(),
                Id = Guid.NewGuid().ToString(),
                Pk = "Test"
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            // some scenarios below requires input to be within data type range in order to be correct
            // therefore, we filter the right inputs for them accordingly.
            // e.g. float has a precision up to 7 digits so the inputs needs to be within that range before being casted to float.
            List<LinqTestInput> inputs = new List<LinqTestInput>();
            // Abs
            inputs.Add(new LinqTestInput("Abs decimal", b => getQuery(b).Select(doc => Math.Abs(doc.DecimalField))));

            inputs.Add(new LinqTestInput("Abs double", b => getQuery(b).Select(doc => Math.Abs((double)doc.NumericField))));
            inputs.Add(new LinqTestInput("Abs float", b => getQuery(b)
                .Where(doc => doc.NumericField > -1000000 && doc.NumericField < 1000000)
                .Select(doc => Math.Abs((float)doc.NumericField))));
            //inputs.Add(new LinqTestInput("Select", b => getQuery(b)
            //    .Select(doc => (int)doc.NumericField)));
            inputs.Add(new LinqTestInput("Abs int", b => getQuery(b)
                .Where(doc => doc.IntField >= int.MinValue && doc.IntField <= int.MaxValue)
                .Select(doc => Math.Abs((int)doc.IntField))));
            inputs.Add(new LinqTestInput("Abs long", b => getQuery(b)
                .Where(doc => doc.NumericField >= long.MinValue && doc.NumericField <= long.MaxValue)
                .Select(doc => Math.Abs((long)doc.NumericField))));
            inputs.Add(new LinqTestInput("Abs sbyte", b => getQuery(b)
                .Where(doc => doc.NumericField >= sbyte.MinValue && doc.NumericField <= sbyte.MaxValue)
                .Select(doc => Math.Abs((sbyte)doc.NumericField))));
            inputs.Add(new LinqTestInput("Abs short", b => getQuery(b)
                .Where(doc => doc.NumericField >= short.MinValue && doc.NumericField <= short.MaxValue)
                .Select(doc => Math.Abs((short)doc.NumericField))));
            // Acos
            inputs.Add(new LinqTestInput("Acos", b => getQuery(b)
                .Where(doc => doc.NumericField >= -1 && doc.NumericField <= 1)
                .Select(doc => Math.Acos(doc.NumericField))));
            // Asin
            inputs.Add(new LinqTestInput("Asin", b => getQuery(b)
                .Where(doc => doc.NumericField >= -1 && doc.NumericField <= 1)
                .Select(doc => Math.Asin(doc.NumericField))));
            // Atan
            inputs.Add(new LinqTestInput("Atan", b => getQuery(b).Select(doc => Math.Atan2(doc.NumericField, 1))));
            // Ceiling
            inputs.Add(new LinqTestInput("Ceiling decimal", b => getQuery(b).Select(doc => Math.Ceiling((decimal)doc.NumericField))));
            inputs.Add(new LinqTestInput("Ceiling double", b => getQuery(b).Select(doc => Math.Ceiling((double)doc.NumericField))));
            inputs.Add(new LinqTestInput("Ceiling float", b => getQuery(b)
                .Where(doc => doc.NumericField > -1000000 && doc.NumericField < 1000000)
                .Select(doc => Math.Ceiling((float)doc.NumericField))));
            // Cos
            inputs.Add(new LinqTestInput("Cos", b => getQuery(b).Select(doc => Math.Cos(doc.NumericField))));
            // Exp
            inputs.Add(new LinqTestInput("Exp", b => getQuery(b)
                .Where(doc => doc.NumericField >= -3 && doc.NumericField <= 3)
                .Select(doc => Math.Exp(doc.NumericField))));
            // Floor
            inputs.Add(new LinqTestInput("Floor decimal", b => getQuery(b).Select(doc => Math.Floor((decimal)doc.NumericField))));
            inputs.Add(new LinqTestInput("Floor double", b => getQuery(b).Select(doc => Math.Floor((double)doc.NumericField))));
            inputs.Add(new LinqTestInput("Floor float", b => getQuery(b)
                .Where(doc => doc.NumericField > -1000000 && doc.NumericField < 1000000)
                .Select(doc => Math.Floor((float)doc.NumericField))));
            // Log
            inputs.Add(new LinqTestInput("Log", b => getQuery(b)
                .Where(doc => doc.NumericField != 0)
                .Select(doc => Math.Log(doc.NumericField))));
            inputs.Add(new LinqTestInput("Log 1", b => getQuery(b)
                .Where(doc => doc.NumericField != 0)
                .Select(doc => Math.Log(doc.NumericField, 2))));
            inputs.Add(new LinqTestInput("Log10", b => getQuery(b)
                .Where(doc => doc.NumericField != 0)
                .Select(doc => Math.Log10(doc.NumericField))));
            // Pow
            inputs.Add(new LinqTestInput("Pow", b => getQuery(b).Select(doc => Math.Pow(doc.NumericField, 1))));
            // Round
            inputs.Add(new LinqTestInput("Round double", b => getQuery(b).Select(doc => Math.Round((double)doc.NumericField))));
            // Sign
            inputs.Add(new LinqTestInput("Sign decimal", b => getQuery(b).Select(doc => Math.Sign((decimal)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign double", b => getQuery(b).Select(doc => Math.Sign((double)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign float", b => getQuery(b)
                .Where(doc => doc.NumericField > -1000000 && doc.NumericField < 1000000)
                .Select(doc => Math.Sign((float)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign int", b => getQuery(b).Select(doc => Math.Sign((int)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign long", b => getQuery(b).Select(doc => Math.Sign((long)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign sbyte", b => getQuery(b)
                .Where(doc => doc.NumericField >= sbyte.MinValue && doc.NumericField <= sbyte.MaxValue)
                .Select(doc => Math.Sign((sbyte)doc.NumericField))));
            inputs.Add(new LinqTestInput("Sign short", b => getQuery(b)
                .Where(doc => doc.NumericField >= short.MinValue && doc.NumericField <= short.MaxValue)
                .Select(doc => Math.Sign((short)doc.NumericField))));
            // Sin
            inputs.Add(new LinqTestInput("Sin", b => getQuery(b).Select(doc => Math.Sin(doc.NumericField))));
            // Sqrt
            inputs.Add(new LinqTestInput("Sqrt", b => getQuery(b).Select(doc => Math.Sqrt(doc.NumericField))));
            // Truncate
            inputs.Add(new LinqTestInput("Truncate decimal", b => getQuery(b).Select(doc => Math.Truncate((decimal)doc.NumericField))));
            inputs.Add(new LinqTestInput("Truncate double", b => getQuery(b).Select(doc => Math.Truncate((double)doc.NumericField))));
            this.ExecuteTestSuite(inputs);
        }

        private Func<bool, IQueryable<DataObject>> CreateDataTestStringFunctions()
        {
            const int Records = 100;
            const int MaxStrLength = 100;
            const int MinStrLength = 5;
            Func<Random, DataObject> createDataObj = (random) => {
                var sb = new StringBuilder(LinqTestsCommon.RandomString(random, random.Next(MaxStrLength - MinStrLength) + MinStrLength));
                if (random.NextDouble() < 0.5)
                {
                    // make a "str" substring for StartsWith, EndsWith, and IndexOf
                    var p = random.Next(sb.Length - 3);
                    sb[p] = 's';
                    sb[p] = 't';
                    sb[p] = 'r';
                }
                return new DataObject() {
                    StringField = sb.ToString(),
                    Id = Guid.NewGuid().ToString(),
                    Pk = "Test"
            };
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);
            return getQuery;
        }

        [TestMethod]
        [Ignore]
        public void TestStringFunctionsIssues()
        {
            // issue when doing string.Reverse()
            List<LinqTestInput> inputs = new List<LinqTestInput>();
            var getQuery = CreateDataTestStringFunctions();
            inputs.Add(new LinqTestInput("Reverse", b => getQuery(b).Select(doc => doc.StringField.Reverse())));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestStringFunctions()
        {
            List<string> emptyList = new List<string>();
            List<string> constantList = new List<string>() { "one", "two", "three" };
            string[] constantArray = new string[] { "one", "two", "three" };

            var getQuery = CreateDataTestStringFunctions();            

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            // Concat
            inputs.Add(new LinqTestInput("Concat 2", b => getQuery(b).Select(doc => string.Concat(doc.StringField, "str"))));
            inputs.Add(new LinqTestInput("Concat 3", b => getQuery(b).Select(doc => string.Concat(doc.StringField, "str1", "str2"))));
            inputs.Add(new LinqTestInput("Concat 4", b => getQuery(b).Select(doc => string.Concat(doc.StringField, "str1", "str2", "str3"))));
            inputs.Add(new LinqTestInput("Concat 5", b => getQuery(b).Select(doc => string.Concat(doc.StringField, "str1", "str2", "str3", "str4"))));
            inputs.Add(new LinqTestInput("Concat array", b => getQuery(b).Select(doc => string.Concat(new string[] { doc.StringField, "str1", "str2", "str3", "str4" }))));
            // Contains
            inputs.Add(new LinqTestInput("Contains w/ string", b => getQuery(b).Select(doc => doc.StringField.Contains("str"))));
            inputs.Add(new LinqTestInput("Contains w/ char", b => getQuery(b).Select(doc => doc.StringField.Contains('c'))));
            inputs.Add(new LinqTestInput("Contains in string constant", b => getQuery(b).Select(doc => "str".Contains(doc.StringField))));
            // Contains with constants should be IN
            inputs.Add(new LinqTestInput("Contains in constant list", b => getQuery(b).Select(doc => constantList.Contains(doc.StringField))));
            inputs.Add(new LinqTestInput("Contains in constant array", b => getQuery(b).Select(doc => constantArray.Contains(doc.StringField))));
            inputs.Add(new LinqTestInput("Contains in constant list in filter", b => getQuery(b).Select(doc => doc.StringField).Where(str => constantList.Contains(str))));
            inputs.Add(new LinqTestInput("Contains in constant array in filter", b => getQuery(b).Select(doc => doc.StringField).Where(str => constantArray.Contains(str))));
            // NOT IN
            inputs.Add(new LinqTestInput("Not in constant list", b => getQuery(b).Select(doc => !constantList.Contains(doc.StringField))));
            inputs.Add(new LinqTestInput("Not in constant array", b => getQuery(b).Select(doc => !constantArray.Contains(doc.StringField))));
            inputs.Add(new LinqTestInput("Filter not in constant list", b => getQuery(b).Select(doc => doc.StringField).Where(str => !constantList.Contains(str))));
            inputs.Add(new LinqTestInput("Filter not in constant array", b => getQuery(b).Select(doc => doc.StringField).Where(str => !constantArray.Contains(str))));
            // Empty list
            inputs.Add(new LinqTestInput("Empty list contains", b => getQuery(b).Select(doc => emptyList.Contains(doc.StringField))));
            inputs.Add(new LinqTestInput("Empty list not contains", b => getQuery(b).Select(doc => !emptyList.Contains(doc.StringField))));
            // EndsWith
            inputs.Add(new LinqTestInput("EndsWith", b => getQuery(b).Select(doc => doc.StringField.EndsWith("str"))));
            inputs.Add(new LinqTestInput("Constant string EndsWith", b => getQuery(b).Select(doc => "str".EndsWith(doc.StringField))));
            // IndexOf
            inputs.Add(new LinqTestInput("IndexOf char", b => getQuery(b).Select(doc => doc.StringField.IndexOf('c'))));
            inputs.Add(new LinqTestInput("IndexOf string", b => getQuery(b).Select(doc => doc.StringField.IndexOf("str"))));
            inputs.Add(new LinqTestInput("IndexOf char w/ startIndex", b => getQuery(b).Select(doc => doc.StringField.IndexOf('c', 0))));
            inputs.Add(new LinqTestInput("IndexOf string w/ startIndex", b => getQuery(b).Select(doc => doc.StringField.IndexOf("str", 0))));
            // Count
            inputs.Add(new LinqTestInput("Count", b => getQuery(b).Select(doc => doc.StringField.Count())));
            // ToLower
            inputs.Add(new LinqTestInput("ToLower", b => getQuery(b).Select(doc => doc.StringField.ToLower())));
            // TrimStart
            inputs.Add(new LinqTestInput("TrimStart", b => getQuery(b).Select(doc => doc.StringField.TrimStart())));
            // Replace
            inputs.Add(new LinqTestInput("Replace char", b => getQuery(b).Select(doc => doc.StringField.Replace('c', 'a'))));
            inputs.Add(new LinqTestInput("Replace string", b => getQuery(b).Select(doc => doc.StringField.Replace("str", "str2"))));
            // TrimEnd
            inputs.Add(new LinqTestInput("TrimEnd", b => getQuery(b).Select(doc => doc.StringField.TrimEnd())));
            //StartsWith
            inputs.Add(new LinqTestInput("StartsWith", b => getQuery(b).Select(doc => doc.StringField.StartsWith("str"))));
            inputs.Add(new LinqTestInput("String constant StartsWith", b => getQuery(b).Select(doc => "str".StartsWith(doc.StringField))));
            // Substring
            inputs.Add(new LinqTestInput("Substring", b => getQuery(b).Select(doc => doc.StringField.Substring(0, 1))));
            // ToUpper
            inputs.Add(new LinqTestInput("ToUpper", b => getQuery(b).Select(doc => doc.StringField.ToUpper())));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestArrayFunctions()
        {
            const int Records = 100;
            const int MaxAbsValue = 10;
            const int MaxArraySize = 50;
            Func<Random, DataObject> createDataObj = (random) => {
                var obj = new DataObject();
                obj.ArrayField = new int[random.Next(MaxArraySize)];
                for (int i = 0; i < obj.ArrayField.Length; ++i)
                {
                    obj.ArrayField[i] = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                }
                obj.EnumerableField = new List<int>();
                for (int i = 0; i < random.Next(MaxArraySize); ++i)
                {
                    obj.EnumerableField.Add(random.Next(MaxAbsValue * 2) - MaxAbsValue);
                }
                obj.NumericField = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<int> emptyList = new List<int>();
            List<int> constantList = new List<int>() { 1, 2, 3 };
            int[] constantArray = new int[] { 1, 2, 3 };

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            // Concat
            inputs.Add(new LinqTestInput("Concat another array", b => getQuery(b).Select(doc => doc.ArrayField.Concat(new int[] { 1, 2, 3 }))));
            inputs.Add(new LinqTestInput("Concat constant list", b => getQuery(b).Select(doc => doc.ArrayField.Concat(constantList))));
            inputs.Add(new LinqTestInput("Concat w/ ArrayField itself", b => getQuery(b).Select(doc => doc.ArrayField.Concat(doc.ArrayField))));
            inputs.Add(new LinqTestInput("Concat enumerable w/ constant list", b => getQuery(b).Select(doc => doc.EnumerableField.Concat(constantList))));
            // Contains
            inputs.Add(new LinqTestInput("ArrayField contains", b => getQuery(b).Select(doc => doc.ArrayField.Contains(1))));
            inputs.Add(new LinqTestInput("EnumerableField contains", b => getQuery(b).Select(doc => doc.EnumerableField.Contains(1))));
            inputs.Add(new LinqTestInput("EnumerableField not contains", b => getQuery(b).Select(doc => !doc.EnumerableField.Contains(1))));
            // Contains with constants should be IN
            inputs.Add(new LinqTestInput("Constant list contains numeric field", b => getQuery(b).Select(doc => constantList.Contains((int)doc.NumericField))));
            inputs.Add(new LinqTestInput("Constant array contains numeric field", b => getQuery(b).Select(doc => constantArray.Contains((int)doc.NumericField))));
            inputs.Add(new LinqTestInput("Constant list contains numeric field in filter", b => getQuery(b).Select(doc => doc.NumericField).Where(number => constantList.Contains((int)number))));
            inputs.Add(new LinqTestInput("Constant array contains numeric field in filter", b => getQuery(b).Select(doc => doc.NumericField).Where(number => constantArray.Contains((int)number))));
            // NOT IN
            inputs.Add(new LinqTestInput("Constant list not contains", b => getQuery(b).Select(doc => !constantList.Contains((int)doc.NumericField))));
            inputs.Add(new LinqTestInput("Constant array not contains", b => getQuery(b).Select(doc => !constantArray.Contains((int)doc.NumericField))));
            inputs.Add(new LinqTestInput("Filter constant list not contains", b => getQuery(b).Select(doc => doc.NumericField).Where(number => !constantList.Contains((int)number))));
            inputs.Add(new LinqTestInput("Filter constant array not contains", b => getQuery(b).Select(doc => doc.NumericField).Where(number => !constantArray.Contains((int)number))));
            // Empty list
            inputs.Add(new LinqTestInput("Empty list contains", b => getQuery(b).Select(doc => emptyList.Contains((int)doc.NumericField))));
            inputs.Add(new LinqTestInput("Empty list not contains", b => getQuery(b).Select(doc => !emptyList.Contains((int)doc.NumericField))));
            // Count
            inputs.Add(new LinqTestInput("Count ArrayField", b => getQuery(b).Select(doc => doc.ArrayField.Count())));
            inputs.Add(new LinqTestInput("Count EnumerableField", b => getQuery(b).Select(doc => doc.EnumerableField.Count())));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSpatialFunctions()
        {
            // The spatial functions are not supported on the client side.
            // Therefore these methods are verified with baselines only.
            List<DataObject> data = new List<DataObject>();
            var query = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution : true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? query : data.AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            // Distance
            inputs.Add(new LinqTestInput("Point distance", b => getQuery(b).Select(doc => doc.Point.Distance(new Point(20.1, 20)))));
            // Within
            Polygon polygon = new Polygon(
                new[]
                    {
                        new Position(10, 10),
                        new Position(30, 10),
                        new Position(30, 30),
                        new Position(10, 30),
                        new Position(10, 10),
                    });
            inputs.Add(new LinqTestInput("Point within polygon", b => getQuery(b).Select(doc => doc.Point.Within(polygon))));
            // Intersects
            inputs.Add(new LinqTestInput("Point intersects with polygon", b => getQuery(b).Where(doc => doc.Point.Intersects(polygon))));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSpecialMethods()
        {
            const int Records = 100;
            const int MaxStringLength = 20;
            const int MaxArrayLength = 10;
            Func<Random, DataObject> createDataObj = (random) => {
                var obj = new DataObject();
                obj.StringField = random.NextDouble() < 0.1 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.EnumerableField = new List<int>();
                for (int i = 0; i < random.Next(MaxArrayLength - 1) + 1; ++i)
                {
                    obj.EnumerableField.Add(random.Next());
                }
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            // Equals
            inputs.Add(new LinqTestInput("Equals", b => getQuery(b).Select(doc => doc.StringField.Equals("str"))));
            // ToString
            inputs.Add(new LinqTestInput("ToString", b => getQuery(b).Select(doc => doc.StringField.ToString())));
            // get_item
            inputs.Add(new LinqTestInput("get_item", b => getQuery(b).Select(doc => doc.EnumerableField[0])));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestConditional()
        {
            const int Records = 100;
            const int MaxStringLength = 20;
            const int MaxArrayLength = 10;
            const int MaxAbsValue = 10;
            Func<Random, DataObject> createDataObj = (random) => {
                var obj = new DataObject();
                obj.StringField = random.NextDouble() < 0.1 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.NumericField = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                obj.ArrayField = new int[random.Next(MaxArrayLength)];
                for (int i = 0; i < obj.ArrayField.Length; ++i)
                {
                    obj.ArrayField[i] = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                }
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Filter w/ ternary conditional ?", b => getQuery(b).Where(doc => doc.NumericField < 3 ? true : false).Select(doc => doc.StringField)));
            inputs.Add(new LinqTestInput("Filter w/ ternary conditional ? and contains", b => getQuery(b).Where(doc => doc.NumericField == (doc.ArrayField.Contains(1) ? 1 : 5)).Select(doc => doc.StringField)));
            inputs.Add(new LinqTestInput("Filter w/ ternary conditional ? and contains #2", b => getQuery(b).Where(doc => doc.NumericField == (doc.StringField == "str" ? 1 : doc.ArrayField.Contains(1) ? 3 : 4)).Select(doc => doc.StringField)));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestCoalesce()
        {
            const int Records = 100;
            const int MaxStringLength = 20;
            Func<Random, DataObject> createDataObj = (random) => {
                var obj = new DataObject();
                obj.StringField = random.NextDouble() < 0.1 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.StringField2 = random.NextDouble() < 0.1 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.NumericField = random.Next();
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Coalesce", b => getQuery(b).Select(doc => doc.StringField ?? "str")));
            inputs.Add(new LinqTestInput("Filter with coalesce comparison", b => getQuery(b).Where(doc => doc.StringField == (doc.StringField2 ?? "str")).Select(doc => doc.NumericField)));
            inputs.Add(new LinqTestInput("Filter with coalesce comparison #2", b => getQuery(b).Select(doc => doc.StringField).Where(str => (str ?? "str") == "str2")));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [TestCategory("Ignore")]
        public void TestStringCompareTo()
        {
            var testQuery = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution : true);
            
            const int Records = 100;
            const int MaxStringLength = 20;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                var obj = new DataObject();
                obj.StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.StringField2 = random.NextDouble() < 0.5 ? obj.StringField : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            // projected compare
            inputs.Add(new LinqTestInput("Projected CompareTo ==", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) == 0)));
            inputs.Add(new LinqTestInput("Projected CompareTo >", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) > 0)));
            inputs.Add(new LinqTestInput("Projected CompareTo >=", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) >= 0)));
            inputs.Add(new LinqTestInput("Projected CompareTo <", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) < 0)));
            inputs.Add(new LinqTestInput("Projected CompareTo <=", b => getQuery(b).Select(doc => doc.StringField.CompareTo(doc.StringField2) <= 0)));
            // static strings
            inputs.Add(new LinqTestInput("CompareTo static string ==", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") == 0)));
            inputs.Add(new LinqTestInput("CompareTo static string >", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") > 0)));
            inputs.Add(new LinqTestInput("CompareTo static string >=", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") >= 0)));
            inputs.Add(new LinqTestInput("CompareTo static string <", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") < 0)));
            inputs.Add(new LinqTestInput("CompareTo static string <=", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") <= 0)));
            // reverse operands
            inputs.Add(new LinqTestInput("Projected CompareTo == reverse operands", b => getQuery(b).Select(doc => 0 == doc.StringField.CompareTo(doc.StringField2))));
            inputs.Add(new LinqTestInput("Projected CompareTo < reverse operands", b => getQuery(b).Select(doc => 0 < doc.StringField.CompareTo(doc.StringField2))));
            inputs.Add(new LinqTestInput("Projected CompareTo <= reverse operands", b => getQuery(b).Select(doc => 0 <= doc.StringField.CompareTo(doc.StringField2))));
            inputs.Add(new LinqTestInput("Projected CompareTo > reverse operands", b => getQuery(b).Select(doc => 0 > doc.StringField.CompareTo(doc.StringField2))));
            inputs.Add(new LinqTestInput("Projected CompareTo >= reverse operands", b => getQuery(b).Select(doc => 0 >= doc.StringField.CompareTo(doc.StringField2))));
            // errors Invalid compare value
            inputs.Add(new LinqTestInput("CompareTo > 1", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") > 1), ClientResources.StringCompareToInvalidConstant));
            inputs.Add(new LinqTestInput("CompareTo == 1", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") == 1), ClientResources.StringCompareToInvalidConstant));
            inputs.Add(new LinqTestInput("CompareTo == -1", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") == -1), ClientResources.StringCompareToInvalidConstant));
            // errors Invalid operator
            inputs.Add(new LinqTestInput("CompareTo | 0", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") | 0), ClientResources.StringCompareToInvalidOperator));
            inputs.Add(new LinqTestInput("CompareTo & 0", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") & 0), ClientResources.StringCompareToInvalidOperator));
            inputs.Add(new LinqTestInput("CompareTo ^ 0", b => getQuery(b).Select(doc => doc.StringField.CompareTo("str") ^ 0), ClientResources.StringCompareToInvalidOperator));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        [TestCategory("Ignore")]
        public void TestUDFs()
        {
            // The UDFs invokation are not supported on the client side.
            // Therefore these methods are verified with baselines only.
            List<DataObject> data = new List<DataObject>();
            var query = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution : true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? query : data.AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("No param", b => getQuery(b).Select(f => UserDefinedFunctionProvider.Invoke("NoParameterUDF"))));
            inputs.Add(new LinqTestInput("Single param", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("SingleParameterUDF", doc.NumericField))));
            inputs.Add(new LinqTestInput("Single param w/ array", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("SingleParameterUDFWithArray", doc.ArrayField))));
            inputs.Add(new LinqTestInput("Multi param", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("MultiParamterUDF", doc.NumericField, doc.StringField, doc.Point))));
            inputs.Add(new LinqTestInput("Multi param w/ array", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("MultiParamterUDWithArrayF", doc.ArrayField, doc.NumericField, doc.Point))));
            inputs.Add(new LinqTestInput("ArrayCount", b => getQuery(b).Where(doc => (int)UserDefinedFunctionProvider.Invoke("ArrayCount", doc.ArrayField) > 2)));
            inputs.Add(new LinqTestInput("ArrayCount && SomeBooleanUDF", b => getQuery(b).Where(doc => (int)UserDefinedFunctionProvider.Invoke("ArrayCount", doc.ArrayField) > 2 && (bool)UserDefinedFunctionProvider.Invoke("SomeBooleanUDF"))));
            inputs.Add(new LinqTestInput("expression", b => getQuery(b).Where(doc => (int)UserDefinedFunctionProvider.Invoke("SingleParameterUDF", doc.NumericField) + 2 == 4)));
            // UDF with constant parameters
            inputs.Add(new LinqTestInput("Single constant param", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("SingleParameterUDF", 1))));
            inputs.Add(new LinqTestInput("Single constant int array param", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("SingleParameterUDFWithArray", new int[] { 1, 2, 3 }))));
            inputs.Add(new LinqTestInput("Single constant string array param", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("SingleParameterUDFWithArray", new string[] { "1", "2" }))));
            inputs.Add(new LinqTestInput("Multi constant params", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("MultiParamterUDF", 1, "str", true))));
            inputs.Add(new LinqTestInput("Multi constant array params", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("MultiParamterUDWithArrayF", new int[] { 1, 2, 3 }, 1, "str"))));
            inputs.Add(new LinqTestInput("ArrayCount with constant param", b => getQuery(b).Where(doc => (int)UserDefinedFunctionProvider.Invoke("ArrayCount", new int[] { 1, 2, 3 }) > 2)));
            // regression (different type parameters including objects)
            inputs.Add(new LinqTestInput("different type parameters including objects", b => getQuery(b).Where(doc => (bool)UserDefinedFunctionProvider.Invoke("MultiParamterUDF2", doc.Point, "str", 1))));
            // errors
            inputs.Add(new LinqTestInput("Null udf name", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke(null)), ClientResources.UdfNameIsNullOrEmpty));
            inputs.Add(new LinqTestInput("Empty udf name", b => getQuery(b).Select(doc => UserDefinedFunctionProvider.Invoke("")), ClientResources.UdfNameIsNullOrEmpty));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestClausesOrderVariations()
        {
            const int Records = 100;
            const int MaxStringLength = 20;
            const int MaxAbsValue = 5;
            const int MaxCoordinateValue = 200;
            Func<Random, DataObject> createDataObj = (random) => {
                var obj = new DataObject();
                obj.StringField = random.NextDouble() < 0.5 ? "str" : LinqTestsCommon.RandomString(random, random.Next(MaxStringLength));
                obj.NumericField = random.Next(MaxAbsValue * 2) - MaxAbsValue;
                var coordinates = new List<double>();
                coordinates.Add(random.NextDouble() < 0.5 ? 10 : random.Next(MaxCoordinateValue));
                coordinates.Add(random.NextDouble() < 0.5 ? 5 : random.Next(MaxCoordinateValue));
                coordinates.Add(random.NextDouble() < 0.5 ? 20 : random.Next(MaxCoordinateValue));
                obj.Point = new Point(new Position(coordinates));
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };
            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            inputs.Add(new LinqTestInput("Where -> Select",
                b => getQuery(b).Where(doc => doc.StringField == "str")
                .Select(doc => doc.NumericField)));
            inputs.Add(new LinqTestInput("Select -> Where",
                b => getQuery(b).Select(doc => doc.NumericField)
                .Where(number => number == 0)));
            inputs.Add(new LinqTestInput("Select -> Multiple Where",
                b => getQuery(b).Select(doc => doc.Point)
                .Where(point => point.Position.Latitude == 100)
                .Where(point => point.Position.Longitude == 50)
                .Where(point => point.Position.Altitude == 20)
                .Where(point => point.Position.Coordinates[0] == 100)
                .Where(point => point.Position.Coordinates[1] == 50)));
            inputs.Add(new LinqTestInput("Multiple Where -> Select",
                b => getQuery(b).Where(doc => doc.Point.Position.Latitude == 100)
                .Where(doc => doc.Point.Position.Longitude == 50)
                .Where(doc => doc.Point.Position.Altitude == 20)
                .Where(doc => doc.Point.Position.Coordinates[0] == 100)
                .Where(doc => doc.Point.Position.Coordinates[1] == 50)
                .Select(doc => doc.Point)));
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestSelectTop()
        {
            var generatedData = CreateDataTestSelectTop();
            var seed = generatedData.Item1;
            var data = generatedData.Item2;

            var query = testContainer.GetItemLinqQueryable<DataObject>(allowSynchronousQueryExecution : true);
            Func<bool, IQueryable<DataObject>> getQuery = useQuery => useQuery ? query : data.AsQueryable();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            // Take
            inputs.Add(new LinqTestInput("Take 0", b => getQuery(b).Take(0)));
            inputs.Add(new LinqTestInput("Select -> Take", b => getQuery(b).Select(doc => doc.NumericField).Take(1)));
            inputs.Add(new LinqTestInput("Filter -> Take", b => getQuery(b).Where(doc => doc.NumericField > 100).Take(2)));
            inputs.Add(new LinqTestInput("Select -> Filter -> Take", b => getQuery(b).Select(doc => doc.NumericField).Where(number => number > 100).Take(7)));
            inputs.Add(new LinqTestInput("Filter -> Select -> Take", b => getQuery(b).Where(doc => doc.NumericField > 100).Select(doc => doc.NumericField).Take(8)));
            inputs.Add(new LinqTestInput("Fitler -> OrderBy -> Select -> Take", b => getQuery(b).Where(doc => doc.NumericField > 100).OrderBy(doc => doc.NumericField).Select(doc => doc.NumericField).Take(9)));
            inputs.Add(new LinqTestInput("Take -> Filter", b => getQuery(b).Take(3).Where(doc => doc.NumericField > 100), ErrorMessages.TopInSubqueryNotSupported));
            inputs.Add(new LinqTestInput("Take -> Filter -> Select", b => getQuery(b).Take(4).Where(doc => doc.NumericField > 100).Select(doc => doc.NumericField), ErrorMessages.TopInSubqueryNotSupported));
            inputs.Add(new LinqTestInput("Take -> Select -> Filter", b => getQuery(b).Take(5).Select(doc => doc.NumericField).Where(number => number > 100), ErrorMessages.TopInSubqueryNotSupported));
            inputs.Add(new LinqTestInput("Select -> Take -> Filter", b => getQuery(b).Select(doc => doc.NumericField).Take(6).Where(number => number > 100), ErrorMessages.TopInSubqueryNotSupported));
            inputs.Add(new LinqTestInput("Take -> Filter -> OrderBy -> Select", b => getQuery(b).Take(10).Where(doc => doc.NumericField > 100).OrderByDescending(doc => doc.NumericField).Select(doc => doc.NumericField), ErrorMessages.TopInSubqueryNotSupported));
            // multiple takes
            inputs.Add(new LinqTestInput("Take 10 -> Take 5", b => getQuery(b).Take(10).Take(5)));
            inputs.Add(new LinqTestInput("Take 5 -> Take 10", b => getQuery(b).Take(5).Take(10)));
            inputs.Add(new LinqTestInput("Take 10 -> Select -> Take 1", b => getQuery(b).Take(10).Select(doc => doc.NumericField).Take(1)));
            inputs.Add(new LinqTestInput("Take 10 -> Filter -> Take 2", b => getQuery(b).Take(10).Where(doc => doc.NumericField > 100).Take(2), ErrorMessages.TopInSubqueryNotSupported));
            // negative value
            inputs.Add(new LinqTestInput("Take -1 -> Take 5", b => getQuery(b).Take(-1).Take(5), ErrorMessages.ExpressionMustBeNonNegativeInteger));
            inputs.Add(new LinqTestInput("Take -2 -> Select", b => getQuery(b).Take(-2).Select(doc => doc.NumericField), ErrorMessages.ExpressionMustBeNonNegativeInteger));
            inputs.Add(new LinqTestInput("Filter -> Take -3", b => getQuery(b).Where(doc => doc.NumericField > 100).Take(-3), ErrorMessages.ExpressionMustBeNonNegativeInteger));
            this.ExecuteTestSuite(inputs);
        }

        private Tuple<int, List<DataObject>> CreateDataTestSelectTop()
        {
            const int Records = 100;
            const int NumAbsMax = 500;
            List<DataObject> data = new List<DataObject>();
            int seed = DateTime.UtcNow.Millisecond;
            Random random = new Random(seed);
            for (int i = 0; i < Records; ++i)
            {
                var obj = new DataObject();
                obj.NumericField = random.Next(NumAbsMax * 2) - NumAbsMax;
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                data.Add(obj);
            }

            foreach (DataObject obj in data)
            {
                testContainer.CreateItemAsync(obj).Wait();
            }

            return Tuple.Create(seed, data);
        }

        private Func<bool, IQueryable<DataObject>> CreateDataTestSelectManyWithFilters()
        {
            const int Records = 10;
            const int ListSizeMax = 20;
            const int NumAbsMax = 10000;
            Func<Random, DataObject> createDataObj = (random) =>
            {
                var obj = new DataObject();
                obj.EnumerableField = new List<int>();
                int listSize = random.Next(ListSizeMax);
                for (int j = 0; j < listSize; ++j)
                {
                    obj.EnumerableField.Add(random.Next(NumAbsMax * 2) - NumAbsMax);
                }
                obj.NumericField = random.Next(NumAbsMax * 2) - NumAbsMax;
                obj.StringField = LinqTestsCommon.RandomString(random, random.Next(listSize));
                obj.Id = Guid.NewGuid().ToString();
                obj.Pk = "Test";
                return obj;
            };

            var getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);
            return getQuery;
        }

        [TestMethod]
        public void TestSelectManyWithFilters()
        {
            var getQuery = CreateDataTestSelectManyWithFilters();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            // Filter outer query
            inputs.Add(new LinqTestInput("SelectMany(Where -> Select)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Where(number => doc.NumericField > 0)
                    .Select(number => number))));
            inputs.Add(new LinqTestInput("Where -> SelectMany(Select)",
                b => getQuery(b).Where(doc => doc.NumericField > 0)
                .SelectMany(doc => doc.EnumerableField
                    .Select(number => number))));
            // Filter inner query
            inputs.Add(new LinqTestInput("SelectMany(Where -> Select)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Where(number => number > 0)
                    .Select(number => number))));
            inputs.Add(new LinqTestInput("SelectMany(Select -> Where)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Select(number => number)
                    .Where(number => number > 0))));
            // Filter both
            inputs.Add(new LinqTestInput("SelectMany(Where1 -> Where2 -> Select)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Where(number => doc.NumericField > 0)
                    .Where(number => number > 10)
                    .Select(number => number))));
            inputs.Add(new LinqTestInput("SelectMany(Where2 -> Where1 -> Select)",
                b => getQuery(b).SelectMany(doc => doc.EnumerableField
                    .Where(number => number > 10)
                    .Where(number => doc.NumericField > 0)
                    .Select(number => number))));
            inputs.Add(new LinqTestInput("Where -> SelectMany(Where -> Select)",
                b => getQuery(b).Where(doc => doc.NumericField > 0)
                .SelectMany(doc => doc.EnumerableField
                    .Where(number => number > 10)
                    .Select(number => number))));
            // OrderBy + Take
            inputs.Add(new LinqTestInput("Where -> OrderBy -> Take -> SelectMany(Where -> Select)",
                b => getQuery(b).Where(doc => doc.NumericField > 0)
                .OrderBy(doc => doc.StringField)
                .Take(10)
                .SelectMany(doc => doc.EnumerableField
                    .Where(number => number > 10)
                    .Select(number => number)), ErrorMessages.OrderByInSubqueryNotSuppported));
            this.ExecuteTestSuite(inputs);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }
    }
}