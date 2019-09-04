﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosDatabaseTests
    {
        protected CosmosClient cosmosClient = null;
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellationToken;

        [TestInitialize]
        public void TestInit()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.cosmosClient = TestCommon.CreateCosmosClient();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            this.cancellationTokenSource?.Cancel();
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task DatabaseContractTest()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();
            Assert.IsNotNull(response);
            Assert.IsTrue(response.RequestCharge > 0);
            Assert.IsNotNull(response.Headers);
            Assert.IsNotNull(response.Headers.ActivityId);

            DatabaseProperties databaseSettings = response.Resource;
            Assert.IsNotNull(databaseSettings.Id);
            Assert.IsNotNull(databaseSettings.ResourceId);
            Assert.IsNotNull(databaseSettings.ETag);
            Assert.IsTrue(databaseSettings.LastModified.HasValue);
            Assert.IsTrue(databaseSettings.LastModified.Value > new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), databaseSettings.LastModified.Value.ToString());

            DatabaseCore databaseCore = response.Database as DatabaseCore;
            Assert.IsNotNull(databaseCore);
            Assert.IsNotNull(databaseCore.LinkUri);
            Assert.IsFalse(databaseCore.LinkUri.ToString().StartsWith("/"));

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropDatabase()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [TestMethod]
        public async Task StreamCrudTestAsync()
        {
            Cosmos.Database database = await this.CreateDatabaseStreamHelper();

            using (ResponseMessage response = await database.ReadStreamAsync())
            {
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }

            using (ResponseMessage response = await database.DeleteStreamAsync())
            {
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }
        }

        [TestMethod]
        public async Task StreamCreateConflictTestAsync()
        {
            DatabaseProperties databaseSettings = new DatabaseProperties()
            {
                Id = Guid.NewGuid().ToString()
            };

            using (ResponseMessage response = await this.cosmosClient.CreateDatabaseStreamAsync(databaseSettings))
            {
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }

            // Stream operations do not throw exceptions.
            using (ResponseMessage response = await this.cosmosClient.CreateDatabaseStreamAsync(databaseSettings))
            {
                Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }

            using (ResponseMessage response = await this.cosmosClient.GetDatabase(databaseSettings.Id).DeleteStreamAsync())
            {
                Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
                Assert.IsNotNull(response.Headers);
                Assert.IsTrue(response.Headers.RequestCharge > 0);
            }
        }

        [TestMethod]
        public async Task CreateConflict()
        {
            DatabaseResponse response = await this.CreateDatabaseHelper();
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            try
            {
                response = await this.CreateDatabaseHelper(response.Resource.Id);
                Assert.Fail($"Unexpected success status code {response.StatusCode}");
            }
            catch (CosmosException hre)
            {
                DefaultTrace.TraceInformation(hre.ToString());
            }

            response = await response.Database.DeleteAsync(cancellationToken: this.cancellationToken);
            Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        }

        [TestMethod]
        public async Task ImplicitConversion()
        {
            DatabaseResponse cosmosDatabaseResponse = await this.CreateDatabaseHelper();
            Cosmos.Database cosmosDatabase = cosmosDatabaseResponse;
            DatabaseProperties cosmosDatabaseSettings = cosmosDatabaseResponse;
            Assert.IsNotNull(cosmosDatabase);
            Assert.IsNotNull(cosmosDatabaseSettings);

            cosmosDatabaseResponse = await cosmosDatabase.DeleteAsync(cancellationToken: this.cancellationToken);
            cosmosDatabase = cosmosDatabaseResponse;
            cosmosDatabaseSettings = cosmosDatabaseResponse;
            Assert.IsNotNull(cosmosDatabase);
            Assert.IsNull(cosmosDatabaseSettings);
        }

        [TestMethod]
        public async Task DropNonExistingDatabase()
        {
            try
            {
                DatabaseResponse response = await this.cosmosClient.GetDatabase(Guid.NewGuid().ToString()).DeleteAsync(cancellationToken: this.cancellationToken);
                Assert.Fail();
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReadDatabase()
        {
            DatabaseResponse createResponse = await this.CreateDatabaseHelper();
            DatabaseResponse readResponse = await createResponse.Database.ReadAsync(cancellationToken: this.cancellationToken);

            Assert.AreEqual(createResponse.Database.Id, readResponse.Database.Id);
            Assert.AreEqual(createResponse.Resource.Id, readResponse.Resource.Id);
            Assert.AreNotEqual(createResponse.ActivityId, readResponse.ActivityId);
            ValidateHeaders(readResponse);
            await createResponse.Database.DeleteAsync(cancellationToken: this.cancellationToken);
        }

        [TestMethod]
        public async Task CreateIfNotExists()
        {
            DatabaseResponse createResponse = await this.CreateDatabaseHelper();
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            createResponse = await this.CreateDatabaseHelper(createResponse.Resource.Id, databaseExists: true);
            Assert.AreEqual(HttpStatusCode.OK, createResponse.StatusCode);
        }

        [TestMethod]
        public async Task NoThroughputTests()
        {
            string databaseId = Guid.NewGuid().ToString();
            DatabaseResponse createResponse = await this.CreateDatabaseHelper(databaseId, databaseExists: false);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            Cosmos.Database cosmosDatabase = createResponse;
            try
            {
                int? readThroughput = await ((DatabaseCore)cosmosDatabase).ReadThroughputAsync();
                Assert.Fail("Should through not found exception as throughput is not configured");
            }
            catch (CosmosException exception)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            }

            await cosmosDatabase.DeleteAsync();
        }

        [TestMethod]
        public async Task SharedThroughputTests()
        {
            string databaseId = Guid.NewGuid().ToString();
            int throughput = 10000;
            DatabaseResponse createResponse = await this.CreateDatabaseHelper(databaseId, databaseExists: false, throughput: throughput);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            Cosmos.Database cosmosDatabase = createResponse;
            int? readThroughput = await cosmosDatabase.ReadThroughputAsync();
            Assert.AreEqual(throughput, readThroughput);

            string containerId = Guid.NewGuid().ToString();
            string partitionPath = "/users";
            ContainerResponse containerResponse = await cosmosDatabase.CreateContainerAsync(containerId, partitionPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

            Container container = containerResponse;
            try
            {
                readThroughput = await ((ContainerCore)container).ReadThroughputAsync();
                Assert.Fail("Should through not found exception as throughput is not configured");
            } catch (CosmosException exception)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            }

            await container.DeleteContainerAsync();
            await cosmosDatabase.DeleteAsync();
        }

        [TestMethod]
        public async Task ReadReplaceThroughputResponseTests()
        {
            string databaseId = Guid.NewGuid().ToString();
            int throughput = 10000;
            DatabaseResponse createResponse = await this.CreateDatabaseHelper(databaseId, databaseExists: false, throughput: throughput);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            Cosmos.Database cosmosDatabase = createResponse;
            ThroughputResponse readThroughputResponse = await cosmosDatabase.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(readThroughputResponse);
            Assert.IsNotNull(readThroughputResponse.Resource);
            Assert.IsNotNull(readThroughputResponse.MinThroughput);
            Assert.IsNotNull(readThroughputResponse.Resource.Throughput);
            Assert.AreEqual(throughput, readThroughputResponse.Resource.Throughput.Value);

            // Implicit
            ThroughputProperties throughputProperties = await cosmosDatabase.ReadThroughputAsync(new RequestOptions());
            Assert.IsNotNull(throughputProperties);
            Assert.AreEqual(throughput, throughputProperties.Throughput);

            // Simple API 
            int? readThroughput = await cosmosDatabase.ReadThroughputAsync();
            Assert.AreEqual(throughput, readThroughput);

            string containerId = Guid.NewGuid().ToString();
            string partitionPath = "/users";
            ContainerResponse containerResponse = await cosmosDatabase.CreateContainerAsync(containerId, partitionPath);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);

            ThroughputResponse replaceThroughputResponse = await cosmosDatabase.ReplaceThroughputAsync(readThroughputResponse.Resource.Throughput.Value + 1000);
            Assert.IsNotNull(replaceThroughputResponse);
            Assert.IsNotNull(replaceThroughputResponse.Resource);
            Assert.AreEqual(readThroughputResponse.Resource.Throughput.Value + 1000, replaceThroughputResponse.Resource.Throughput.Value);

            Container container = containerResponse;
            try
            {
                readThroughputResponse = await container.ReadThroughputAsync(new RequestOptions());
                Assert.Fail("Should through not found exception as throughput is not configured");
            }
            catch (CosmosException exception)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            }

            await container.DeleteContainerAsync();
            await cosmosDatabase.DeleteAsync();
        }

        [TestMethod]
        public async Task DatabaseIterator()
        {
            List<Cosmos.Database> deleteList = new List<Cosmos.Database>();
            HashSet<string> databaseIds = new HashSet<string>();
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    DatabaseResponse createResponse = await this.CreateDatabaseHelper();
                    deleteList.Add(createResponse.Database);
                    databaseIds.Add(createResponse.Resource.Id);
                }

                FeedIterator<DatabaseProperties> feedIterator = this.cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>(
                    queryDefinition: null,
                    continuationToken: null,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 2 });

                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<DatabaseProperties> iterator =
                        await feedIterator.ReadNextAsync(this.cancellationToken);
                    foreach (DatabaseProperties databaseSettings in iterator)
                    {
                        if (databaseIds.Contains(databaseSettings.Id))
                        {
                            databaseIds.Remove(databaseSettings.Id);
                        }
                    }
                }
            }
            finally
            {
                foreach (Cosmos.Database database in deleteList)
                {
                    await database.DeleteAsync(cancellationToken: this.cancellationToken);
                }
            }

            Assert.AreEqual(0, databaseIds.Count);
        }

        [TestMethod]
        public async Task DatabaseQueryIterator()
        {
            List<Cosmos.Database> deleteList = new List<Cosmos.Database>();
            try
            {
                string firstDb = "Abcdefg";
                string secondDb = "Bcdefgh";
                string thirdDb = "Zoo";

                DatabaseResponse createResponse2 = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(secondDb);
                deleteList.Add(createResponse2.Database);
                DatabaseResponse createResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(firstDb);
                deleteList.Add(createResponse.Database);
                DatabaseResponse createResponse3 = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(thirdDb);
                deleteList.Add(createResponse3.Database);
                 
                FeedIterator<DatabaseProperties> feedIterator =
                    this.cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>(
                        new QueryDefinition("select c.id From c where c.id = @id ")
                        .WithParameter("@id", createResponse.Database.Id),
                        requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });

                FeedResponse<DatabaseProperties> iterator = await feedIterator.ReadNextAsync(this.cancellationToken);
                Assert.AreEqual(1, iterator.Resource.Count());
                Assert.AreEqual(firstDb, iterator.First().Id);

                Assert.IsFalse(feedIterator.HasMoreResults);
            }
            finally
            {
                foreach (Cosmos.Database database in deleteList)
                {
                    await database.DeleteAsync(cancellationToken: this.cancellationToken);
                }
            }
        }

        private Task<DatabaseResponse> CreateDatabaseHelper()
        {
            return this.CreateDatabaseHelper(Guid.NewGuid().ToString(), databaseExists: false);
        }

        private async Task<DatabaseResponse> CreateDatabaseHelper(
            string databaseId,
            int? throughput = null,
            bool databaseExists = false)
        {
            DatabaseResponse response = null;
            if (databaseExists)
            {
                response = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                    databaseId,
                    throughput,
                    cancellationToken: this.cancellationToken);
            }
            else
            {
                response = await this.cosmosClient.CreateDatabaseAsync(
                    databaseId,
                    throughput,
                    cancellationToken: this.cancellationToken);
            }

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Database);
            Assert.IsNotNull(response.Resource);
            Assert.AreEqual(databaseId, response.Resource.Id);
            Assert.AreEqual(databaseId, response.Database.Id);
            ValidateHeaders(response);

            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK || (response.StatusCode == HttpStatusCode.Created && !databaseExists));

            return response;
        }

        private async Task<Cosmos.Database> CreateDatabaseStreamHelper(
            string databaseId = null,
            int? throughput = null,
            bool databaseExists = false)
        {
            if (string.IsNullOrEmpty(databaseId))
            {
                databaseId = Guid.NewGuid().ToString();
            }

            DatabaseProperties databaseSettings = new DatabaseProperties() { Id = databaseId };
            ResponseMessage response = await this.cosmosClient.CreateDatabaseStreamAsync(
                databaseSettings,
                throughput: 400);

            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Headers.RequestCharge);
            Assert.IsNotNull(response.Headers.ActivityId);

            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK || (response.StatusCode == HttpStatusCode.Created && !databaseExists));

            return this.cosmosClient.GetDatabase(databaseId);
        }

        private void ValidateHeaders(DatabaseResponse cosmosDatabaseResponse)
        {
            // Test emulator is regression and commented out to unblock
            // Assert.IsNotNull(cosmosDatabaseResponse.MaxResourceQuota);
            // Assert.IsNotNull(cosmosDatabaseResponse.CurrentResourceQuotaUsage);
        }
    }
}
