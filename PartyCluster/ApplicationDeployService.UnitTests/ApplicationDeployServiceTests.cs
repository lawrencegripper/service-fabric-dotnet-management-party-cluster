﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace ApplicationDeployService.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using Domain;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Mocks;

    [TestClass]
    public class ApplicationDeployServiceTests
    {
        [TestMethod]
        public async Task QueueApplicationDeploymentSuccessful()
        {
            MockReliableStateManager stateManager = new MockReliableStateManager();
            MockApplicationOperator applicationOperator = new MockApplicationOperator();
            ApplicationDeployService target = new ApplicationDeployService(stateManager, applicationOperator, this.CreateServiceParameters());

            target.ApplicationPackages = new List<ApplicationPackageInfo>()
            {
                new ApplicationPackageInfo("type1", "1.0", "path/to/type1", "", "", "", ""),
                new ApplicationPackageInfo("type2", "2.0", "path/to/type2", "", "", "", "")
            };

            IEnumerable<Guid> result = await target.QueueApplicationDeploymentAsync("localhost", 19000);

            Assert.AreEqual(2, result.Count());

            foreach (Guid actual in result)
            {
                Assert.AreEqual<ApplicationDeployStatus>(ApplicationDeployStatus.Copy, await target.GetStatusAsync(actual));
            }
        }

        [TestMethod]
        public async Task ProcessApplicationDeploymentCopy()
        {
            string type = "type1";
            string version = "1.0";
            string expected = "type1_1.0";

            MockReliableStateManager stateManager = new MockReliableStateManager();
            MockApplicationOperator applicationOperator = new MockApplicationOperator
            {
                CopyPackageToImageStoreAsyncFunc = (cluster, appPackage, appType, appVersion) => Task.FromResult(appType + "_" + appVersion)
            };

            ApplicationDeployService target = new ApplicationDeployService(stateManager, applicationOperator, this.CreateServiceParameters());
            ApplicationDeployment appDeployment = new ApplicationDeployment("", ApplicationDeployStatus.Copy, "", type, version, "", "", DateTimeOffset.UtcNow);
            ApplicationDeployment actual = await target.ProcessApplicationDeployment(appDeployment, CancellationToken.None);

            Assert.AreEqual(expected, actual.ImageStorePath);
            Assert.AreEqual(ApplicationDeployStatus.Register, actual.Status);
        }

        [TestMethod]
        public async Task ProcessApplicationDeploymentRegister()
        {
            MockReliableStateManager stateManager = new MockReliableStateManager();
            MockApplicationOperator applicationOperator = new MockApplicationOperator
            {
                CopyPackageToImageStoreAsyncFunc = (cluster, appPackage, appType, appVersion) => Task.FromResult(appType + "_" + appVersion)
            };

            ApplicationDeployService target = new ApplicationDeployService(stateManager, applicationOperator, this.CreateServiceParameters());
            ApplicationDeployment appDeployment = new ApplicationDeployment(
                "",
                ApplicationDeployStatus.Register,
                "",
                "type1",
                "1.0.0",
                "",
                "",
                DateTimeOffset.UtcNow);
            ApplicationDeployment actual = await target.ProcessApplicationDeployment(appDeployment, CancellationToken.None);

            Assert.AreEqual(ApplicationDeployStatus.Create, actual.Status);
        }

        [TestMethod]
        public async Task ProcessApplicationDeploymentCreate()
        {
            MockReliableStateManager stateManager = new MockReliableStateManager();
            MockApplicationOperator applicationOperator = new MockApplicationOperator
            {
                CopyPackageToImageStoreAsyncFunc = (cluster, appPackage, appType, appVersion) => Task.FromResult(appType + "_" + appVersion)
            };

            ApplicationDeployService target = new ApplicationDeployService(stateManager, applicationOperator, this.CreateServiceParameters());
            ApplicationDeployment appDeployment = new ApplicationDeployment(
                "",
                ApplicationDeployStatus.Create,
                "",
                "type1",
                "1.0.0",
                "",
                "",
                DateTimeOffset.UtcNow);
            ApplicationDeployment actual = await target.ProcessApplicationDeployment(appDeployment, CancellationToken.None);

            Assert.AreEqual(ApplicationDeployStatus.Complete, actual.Status);
        }

        [TestMethod]
        public async Task GetServiceEndpointWithDomain()
        {
            string expectedDomain = "test.cloudapp.azure.com";
            string serviceAddress = "http://23.45.67.89/service/api";
            string expected = "http://test.cloudapp.azure.com:80/service/api";

            MockReliableStateManager stateManager = new MockReliableStateManager();
            MockApplicationOperator applicationOperator = new MockApplicationOperator()
            {
                GetServiceEndpointFunc = (cluster, service) => Task.FromResult(serviceAddress)
            };

            ApplicationDeployService target = new ApplicationDeployService(stateManager, applicationOperator, this.CreateServiceParameters());
            target.ApplicationPackages = new List<ApplicationPackageInfo>()
            {
                new ApplicationPackageInfo("type1", "1.0", "path/to/type1", "fabric:/app/service", "", "description", "")
            };

            IEnumerable<ApplicationView> actual = await target.GetApplicationDeploymentsAsync(expectedDomain, 19000);

            Assert.AreEqual(expected, actual.First().EntryServiceInfo.Address); 
        }

        private StatefulServiceParameters CreateServiceParameters()
        {
            return new StatefulServiceParameters(null, null, Guid.NewGuid(), null, null, 0);
        }
    }
}