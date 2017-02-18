﻿namespace ArcGIS.Test.Integration
{
    using ArcGIS.ServiceModel;
    using ArcGIS.ServiceModel.Common;
    using ArcGIS.ServiceModel.Operation;
    using ArcGIS.ServiceModel.Serializers;
    using ServiceStack.Text;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Abstractions;

    public class ArcGISGateway : PortalGateway
    {
        public ArcGISGateway(ISerializer serializer = null)
            : this(@"http://sampleserver3.arcgisonline.com/ArcGIS/", null, serializer)
        { }

        public ArcGISGateway(string root, ITokenProvider tokenProvider, ISerializer serializer = null)
            : base(root, serializer, tokenProvider)
        { }

        public Task<QueryResponse<T>> QueryAsPost<T>(Query queryOptions) where T : IGeometry
        {
            return Post<QueryResponse<T>, Query>(queryOptions, CancellationToken.None);
        }

        public Task<AgsObject> GetAnything(ArcGISServerEndpoint endpoint)
        {
            return Get<AgsObject>(endpoint, CancellationToken.None);
        }

        internal readonly static Dictionary<string, Func<Type>> TypeMap = new Dictionary<string, Func<Type>>
            {
                { GeometryTypes.Point, () => typeof(Point) },
                { GeometryTypes.MultiPoint, () => typeof(MultiPoint) },
                { GeometryTypes.Envelope, () => typeof(Extent) },
                { GeometryTypes.Polygon, () => typeof(Polygon) },
                { GeometryTypes.Polyline, () => typeof(Polyline) }
            };

        public async Task<FindResponse> DoFind(Find findOptions)
        {
            var response = await Find(findOptions);

            foreach (var result in response.Results.Where(r => r.Geometry != null))
            {
                result.Geometry = JsonSerializer.DeserializeFromString(result.Geometry.SerializeToString(), TypeMap[result.GeometryType]());
            }
            return response;
        }
    }

    public class AgsObject : JsonObject, IPortalResponse
    {
        [DataMember(Name = "error")]
        public ArcGISError Error { get; set; }

        [DataMember(Name = "links")]
        public List<Link> Links { get; set; }
    }

    public class ArcGISGatewayTests : IClassFixture<IntegrationTestFixture>
    {
        public ArcGISGatewayTests(IntegrationTestFixture fixture, ITestOutputHelper output)
        {
            fixture.SetTestOutputHelper(output);
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/", "/Earthquakes/EarthquakesFromLastSevenDays/MapServer")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis", "Generic/newzealand/MapServer")]
        public async Task CanGetAnythingFromServer(string rootUrl, string relativeUrl)
        {
            var gateway = new ArcGISGateway(rootUrl, null, new ServiceStackSerializer());
            var endpoint = new ArcGISServerEndpoint(relativeUrl);
            var response = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.GetAnything(endpoint);
            });

            Assert.Null(response.Error);
            Assert.True(response.ContainsKey("capabilities"));
            Assert.True(response.ContainsKey("mapName"));
            Assert.True(response.ContainsKey("layers"));
            Assert.True(response.ContainsKey("documentInfo"));
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/")]
        [InlineData("https://services.arcgisonline.co.nz/arcgis")]
        public async Task CanPingServer(string rootUrl)
        {
            var gateway = new PortalGateway(rootUrl);
            var endpoint = new ArcGISServerEndpoint("/");

            var response = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Ping(endpoint);
            });

            Assert.Null(response.Error);
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/")]
        [InlineData("https://services.arcgisonline.co.nz/arcgis")]
        [InlineData("https://services.arcgisonline.com/arcgis")]
        public async Task CanGetServerInfo(string rootUrl)
        {
            var gateway = new PortalGateway(rootUrl);

            var response = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Info();
            });

            Assert.Null(response.Error);
            Assert.NotNull(response.CurrentVersion);
            Assert.True(response.CurrentVersion > 9.0);
            Assert.NotNull(response.AuthenticationInfo);
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis")]
        public async Task CanDescribeSite(string rootUrl)
        {
            var gateway = new PortalGateway(rootUrl);
            var response = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.DescribeSite();
            });

            Assert.NotNull(response);
            Assert.True(response.Version > 0);

            foreach (var resource in response.ArcGISServerEndpoints)
            {
                var ping = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
                {
                    return gateway.Ping(resource);
                });
                Assert.Null(ping.Error);
            }
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis")]
        public async Task CanDescribeSiteServices(string rootUrl)
        {
            var gateway = new PortalGateway(rootUrl);
            var siteResponse = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.DescribeSite();
            });

            Assert.NotNull(siteResponse);
            Assert.True(siteResponse.Version > 0);

            var response = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.DescribeServices(siteResponse);
            });

            foreach (var serviceDescription in response)
            {
                Assert.NotNull(serviceDescription);
                Assert.Null(serviceDescription.Error);
                Assert.NotNull(serviceDescription.ServiceDescription);
            }
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/", "Petroleum/KSWells/MapServer/0")]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS/", "Petroleum/KSWells/MapServer/1")]
        [InlineData("http://services.arcgisonline.co.nz/arcgis", "Canvas/Light/MapServer/0")]
        public async Task CanDescribeLayer(string rootUrl, string layerUrl)
        {
            var gateway = new PortalGateway(rootUrl);
            var layerResponse = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.DescribeLayer(layerUrl.AsEndpoint());
            });

            Assert.NotNull(layerResponse);
            Assert.Null(layerResponse.Error);
            Assert.NotNull(layerResponse.GeometryType);
        }

        [Fact]
        public async Task GatewayDoesAutoPost()
        {
            var gateway = new ArcGISGateway() { IncludeHypermediaWithResponse = true };

            var longWhere = new StringBuilder("region = '");
            for (var i = 0; i < 3000; i++)
                longWhere.Append(i);

            var query = new Query(@"/Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint()) { Where = longWhere + "'" };

            var result = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(query);
            });
            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.NotNull(result.SpatialReference);
            Assert.False(result.Features.Any());
            Assert.NotNull(result.Links);
            Assert.Equal("POST", result.Links.First().Method);
        }

        [Theory]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS", "/Earthquakes/EarthquakesFromLastSevenDays/MapServer/0")]
        [InlineData("http://sampleserver3.arcgisonline.com/ArcGIS", "Earthquakes/Since_1970/MapServer/0")]
        public async Task QueryCanReturnPointFeatures(string rootUrl, string relativeUrl)
        {
            var gateway = new PortalGateway(rootUrl);

            var query = new Query(relativeUrl.AsEndpoint());
            var result = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(query);
            });

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.NotNull(result.SpatialReference);
            Assert.True(result.Features.Any());
            Assert.Null(result.Links);
            Assert.True(result.Features.All(i => i.Geometry != null));
        }

        [Fact]
        public async Task QueryCanReturnDifferentGeometryTypes()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint()) { Where = "magnitude > 4.5" };
            var resultPoint = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.QueryAsPost<Point>(queryPoint);
            });

            Assert.True(resultPoint.Features.Any());
            Assert.True(resultPoint.Features.All(i => i.Geometry != null));

            var queryPolyline = new Query(@"Hydrography/Watershed173811/MapServer/1".AsEndpoint()) { OutFields = new List<string> { "lengthkm" } };
            var resultPolyline = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Polyline>(queryPolyline);
            });

            Assert.True(resultPolyline.Features.Any());
            Assert.True(resultPolyline.Features.All(i => i.Geometry != null));

            gateway = new ArcGISGateway(new JsonDotNetSerializer());

            var queryPolygon = new Query(@"/Hydrography/Watershed173811/MapServer/0".AsEndpoint()) { Where = "areasqkm = 0.012", OutFields = new List<string> { "areasqkm" } };
            var resultPolygon = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.QueryAsPost<Polygon>(queryPolygon);
            });

            Assert.True(resultPolygon.Features.Any());
            Assert.True(resultPolygon.Features.All(i => i.Geometry != null));
        }

        [Fact]
        public async Task QueryCanReturnNoGeometry()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint()) { ReturnGeometry = false };
            var resultPoint = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(queryPoint);
            });

            Assert.True(resultPoint.Features.Any());
            Assert.True(resultPoint.Features.All(i => i.Geometry == null));

            var queryPolyline = new Query(@"Hydrography/Watershed173811/MapServer/1".AsEndpoint()) { OutFields = new List<string> { "lengthkm" }, ReturnGeometry = false };
            var resultPolyline = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.QueryAsPost<Polyline>(queryPolyline);
            });

            Assert.True(resultPolyline.Features.Any());
            Assert.True(resultPolyline.Features.All(i => i.Geometry == null));
        }

        [Fact]
        public async Task QueryCanUseWhereClause()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/Since_1970/MapServer/0".AsEndpoint())
            {
                ReturnGeometry = false,
                Where = "UPPER(Name) LIKE UPPER('New Zea%')"
            };
            queryPoint.OutFields.Add("Name");
            var resultPoint = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(queryPoint);
            });

            Assert.True(resultPoint.Features.Any());
            Assert.True(resultPoint.Features.All(i => i.Geometry == null));
            Assert.True(resultPoint.Features.All(i => i.Attributes["Name"].ToString().StartsWithIgnoreCase("New Zea")));
        }

        [Fact]
        public async Task QueryObjectIdsAreHonored()
        {
            var gateway = new ArcGISGateway();

            var queryPoint = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint()) { ReturnGeometry = false };
            var resultPoint = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(queryPoint);
            });

            Assert.True(resultPoint.Features.Any());
            Assert.True(resultPoint.Features.All(i => i.Geometry == null));

            var queryPointByOID = new Query(@"Earthquakes/EarthquakesFromLastSevenDays/MapServer/0".AsEndpoint())
            {
                ReturnGeometry = false,
                ObjectIds = resultPoint.Features.Take(10).Select(f => long.Parse(f.Attributes["objectid"].ToString())).ToList()
            };
            var resultPointByOID = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(queryPointByOID);
            });

            Assert.True(resultPointByOID.Features.Any());
            Assert.True(resultPointByOID.Features.All(i => i.Geometry == null));
            Assert.True(resultPoint.Features.Count() > 10);
            Assert.True(resultPointByOID.Features.Count() == 10);
            Assert.False(queryPointByOID.ObjectIds.Except(resultPointByOID.Features.Select(f => f.ObjectID)).Any());
        }

        [Fact]
        public async Task QueryOutFieldsAreHonored()
        {
            var gateway = new ArcGISGateway();

            var queryPolyline = new Query(@"Hydrography/Watershed173811/MapServer/1".AsEndpoint()) { OutFields = new List<string> { "lengthkm" }, ReturnGeometry = false };
            var resultPolyline = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Polyline>(queryPolyline);
            });

            Assert.True(resultPolyline.Features.Any());
            Assert.True(resultPolyline.Features.All(i => i.Geometry == null));
            Assert.True(resultPolyline.Features.All(i => i.Attributes != null && i.Attributes.Count == 1));

            var queryPolygon = new Query(@"/Hydrography/Watershed173811/MapServer/0".AsEndpoint())
            {
                Where = "areasqkm = 0.012",
                OutFields = new List<string> { "areasqkm", "elevation", "resolution", "reachcode" }
            };
            var resultPolygon = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Polygon>(queryPolygon);
            });

            Assert.True(resultPolygon.Features.Any());
            Assert.True(resultPolygon.Features.All(i => i.Geometry != null));
            Assert.True(resultPolygon.Features.All(i => i.Attributes != null && i.Attributes.Count == 4));
        }

        [Theory]
        [InlineData("http://sampleserver6.arcgisonline.com/arcgis", "911CallsHotspot/MapServer/1", "INC_NO")]
        public async Task QueryOrderByIsHonored(string rootUrl, string relativeUrl, string orderby)
        {
            var gateway = new PortalGateway(rootUrl);

            var query = new Query(relativeUrl.AsEndpoint())
            {
                OrderBy = new List<string> { orderby },
                ReturnGeometry = false
            };
            var result = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(query);
            });

            var queryDesc = new Query(relativeUrl.AsEndpoint())
            {
                OrderBy = new List<string> { orderby + " DESC" },
                ReturnGeometry = false
            };
            var resultDesc = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(queryDesc);
            });

            Assert.True(result.Features.Any());
            Assert.True(resultDesc.Features.Any());
            Assert.NotEqual(result.Features, resultDesc.Features);
        }

        [Theory]
        [InlineData("http://services.arcgis.com/hMYNkrKaydBeWRXE/arcgis", "TestReturnExtentOnly/FeatureServer/0")]
        public async Task CanQueryExtent(string rootUrl, string relativeUrl)
        {
            var gateway = new PortalGateway(rootUrl);

            var query = new QueryForExtent(relativeUrl.AsEndpoint());
            var result = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.QueryForExtent(query);
            });

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.True(result.NumberOfResults > 0);
            Assert.NotNull(result.Extent);
            Assert.NotNull(result.Extent.SpatialReference);
            Assert.NotNull(result.Extent.GetCenter());
        }

        [Theory]
        [InlineData("http://services.arcgis.com/hMYNkrKaydBeWRXE/arcgis", "TestReturnExtentOnly/FeatureServer/0", 1, 2)]
        public async Task CanPagePointQuery(string rootUrl, string relativeUrl, int start, int numberToReturn)
        {
            var gateway = new PortalGateway(rootUrl);

            var queryCount = new QueryForCount(relativeUrl.AsEndpoint());
            var resultCount = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.QueryForCount(queryCount);
            });

            Assert.NotNull(resultCount);
            Assert.Null(resultCount.Error);
            Assert.NotEqual(numberToReturn, resultCount.NumberOfResults);
            Assert.True(numberToReturn < resultCount.NumberOfResults);

            var query = new Query(relativeUrl.AsEndpoint()) { ResultOffset = start, ResultRecordCount = numberToReturn };
            var result = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(query);
            });

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.True(result.Features.Any());
            Assert.Equal(numberToReturn, result.Features.Count());
        }

        [Fact]
        public async Task CanQueryForCount()
        {
            var gateway = new PortalGateway("http://services.arcgisonline.com/arcgis/");

            var query = new QueryForCount(@"/Specialty/Soil_Survey_Map/MapServer/2".AsEndpoint());
            var result = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.QueryForCount(query);
            });

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.True(result.NumberOfResults > 0);
        }

        [Fact]
        public async Task CanQueryForIds()
        {
            var gateway = new PortalGateway("http://services.arcgisonline.com/arcgis/");

            var query = new QueryForIds(@"/Specialty/Soil_Survey_Map/MapServer/2".AsEndpoint());
            var result = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.QueryForIds(query);
            });

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.NotNull(result.ObjectIds);
            Assert.True(result.ObjectIds.Any());

            var queryFiltered = new QueryForIds(@"/Specialty/Soil_Survey_Map/MapServer/2".AsEndpoint())
            {
                ObjectIds = result.ObjectIds.Take(100).ToList()
            };
            var resultFiltered = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.QueryForIds(queryFiltered);
            });

            Assert.NotNull(resultFiltered);
            Assert.Null(resultFiltered.Error);
            Assert.NotNull(resultFiltered.ObjectIds);
            Assert.True(resultFiltered.ObjectIds.Any());
            Assert.True(resultFiltered.ObjectIds.Count() == queryFiltered.ObjectIds.Count);
        }

        /// <summary>
        /// Performs unfiltered query, then filters by Extent and Polygon to SE quadrant of globe and verifies both filtered
        /// results contain same number of features as each other, and that both filtered resultsets contain fewer features than unfiltered resultset.
        /// </summary>
        /// <param name="serviceUrl"></param>
        /// <returns></returns>
        public async Task QueryGeometryCriteriaHonored(string serviceUrl)
        {
            int countAllResults = 0;
            int countExtentResults = 0;
            int countPolygonResults = 0;

            var gateway = new ArcGISGateway(new JsonDotNetSerializer());

            var queryPointAllResults = new Query(serviceUrl.AsEndpoint());

            var resultPointAllResults = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(queryPointAllResults);
            });

            var queryPointExtentResults = new Query(serviceUrl.AsEndpoint())
            {
                Geometry = new Extent { XMin = 0, YMin = 0, XMax = 180, YMax = -90, SpatialReference = SpatialReference.WGS84 }, // SE quarter of globe
                OutputSpatialReference = SpatialReference.WebMercator
            };
            var resultPointExtentResults = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(queryPointExtentResults);
            });

            var rings = new Point[]
            {
                new Point { X = 0, Y = 0 },
                new Point { X = 180, Y = 0 },
                new Point { X = 180, Y = -90 },
                new Point { X = 0, Y = -90 },
                new Point { X = 0, Y = 0 }
            }.ToPointCollectionList();

            var queryPointPolygonResults = new Query(serviceUrl.AsEndpoint())
            {
                Geometry = new Polygon { Rings = rings }
            };
            var resultPointPolygonResults = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Query<Point>(queryPointPolygonResults);
            });

            countAllResults = resultPointAllResults.Features.Count();
            countExtentResults = resultPointExtentResults.Features.Count();
            countPolygonResults = resultPointPolygonResults.Features.Count();

            Assert.Equal(resultPointExtentResults.SpatialReference.Wkid, queryPointExtentResults.OutputSpatialReference.Wkid);
            Assert.True(countAllResults > countExtentResults);
            Assert.True(countPolygonResults == countExtentResults);
        }

        /// <summary>
        /// Test geometry query against MapServer
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task QueryMapServerGeometryCriteriaHonored()
        {
            await QueryGeometryCriteriaHonored(@"/Earthquakes/EarthquakesFromLastSevenDays/MapServer/0");
        }

        /// <summary>
        /// Test geometry query against FeatureServer
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task QueryFeatureServerGeometryCriteriaHonored()
        {
            await QueryGeometryCriteriaHonored(@"/Earthquakes/EarthquakesFromLastSevenDays/FeatureServer/0");
        }

        [Fact]
        public async Task CanAddUpdateAndDelete()
        {
            var gateway = new ArcGISGateway();

            var feature = new Feature<Point>();
            feature.Attributes.Add("type", 0);
            feature.Geometry = new Point { SpatialReference = new SpatialReference { Wkid = SpatialReference.WebMercator.Wkid }, X = -13073617.8735768, Y = 4071422.42978062 };

            var adds = new ApplyEdits<Point>(@"Fire/Sheep/FeatureServer/0".AsEndpoint())
            {
                Adds = new List<Feature<Point>> { feature }
            };
            var resultAdd = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.ApplyEdits(adds);
            });

            Assert.True(resultAdd.Adds.Any());
            Assert.True(resultAdd.Adds.First().Success);
            Assert.Equal(resultAdd.ExpectedAdds, resultAdd.ActualAdds);
            Assert.Equal(resultAdd.ActualAdds, resultAdd.ActualAddsThatSucceeded);

            var id = resultAdd.Adds.First().ObjectId;

            feature.Attributes.Add("description", "'something'"); // problem with serialization means we need single quotes around string values
            feature.Attributes.Add("objectId", id);

            var updates = new ApplyEdits<Point>(@"Fire/Sheep/FeatureServer/0".AsEndpoint())
            {
                Updates = new List<Feature<Point>> { feature }
            };
            var resultUpdate = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.ApplyEdits(updates);
            });

            Assert.True(resultUpdate.Updates.Any());
            Assert.True(resultUpdate.Updates.First().Success);
            Assert.Equal(resultUpdate.ExpectedUpdates, resultUpdate.ActualUpdates);
            Assert.Equal(resultUpdate.ActualUpdates, resultUpdate.ActualUpdatesThatSucceeded);
            Assert.Equal(resultUpdate.Updates.First().ObjectId, id);

            var deletes = new ApplyEdits<Point>(@"Fire/Sheep/FeatureServer/0".AsEndpoint())
            {
                Deletes = new List<long> { id }
            };
            var resultDelete = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.ApplyEdits(deletes);
            });

            Assert.True(resultDelete.Deletes.Any());
            Assert.True(resultDelete.Deletes.First().Success);
            Assert.Equal(resultDelete.ExpectedDeletes, resultDelete.ActualDeletes);
            Assert.Equal(resultDelete.ActualDeletes, resultDelete.ActualDeletesThatSucceeded);
            Assert.Equal(resultDelete.Deletes.First().ObjectId, id);
        }

        [Fact]
        public async Task FindCanReturnResultsAndGeometry()
        {
            var gateway = new ArcGISGateway();

            var find = new Find(@"/Network/USA/MapServer".AsEndpoint())
            {
                SearchText = "route",
                LayerIdsToSearch = new List<int> { 1, 2, 3 },
                SearchFields = new List<string> { "Name", "RouteName" }
            };
            var result = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Find(find);
            });

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.True(result.Results.Any());
            Assert.True(result.Results.All(i => i.Geometry != null));
        }

        [Fact]
        public async Task FindCanReturnResultsAndNoGeometry()
        {
            var gateway = new ArcGISGateway();

            var find = new Find(@"/Network/USA/MapServer".AsEndpoint())
            {
                SearchText = "route",
                LayerIdsToSearch = new List<int> { 1, 2, 3 },
                SearchFields = new List<string> { "Name", "RouteName" },
                ReturnGeometry = false
            };
            var result = await IntegrationTestFixture.TestPolicy.ExecuteAsync(() =>
            {
                return gateway.Find(find);
            });

            Assert.NotNull(result);
            Assert.Null(result.Error);
            Assert.True(result.Results.Any());
            Assert.True(result.Results.All(i => i.Geometry == null));
        }
    }
}
