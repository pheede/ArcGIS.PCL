﻿namespace ArcGIS.ServiceModel
{
    using ArcGIS.ServiceModel.Common;
    using ArcGIS.ServiceModel.Operation;
    using ArcGIS.ServiceModel.Operation.Admin;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// ArcGIS Server gateway
    /// </summary>
    public class PortalGateway : PortalGatewayBase
    {
        public PortalGateway(string rootUrl, ISerializer serializer = null, ITokenProvider tokenProvider = null, Func<HttpClient> httpClientFunc = null)
            : base(rootUrl, serializer, tokenProvider, httpClientFunc)
        { }

        /// <summary>
        /// Recursively parses an ArcGIS Server site and discovers the resources available
        /// </summary>
        /// <param name="ct">Optional cancellation token to cancel pending request</param>
        /// <returns>An ArcGIS Server site hierarchy</returns>
        public virtual async Task<SiteDescription> DescribeSite(CancellationToken ct = default(CancellationToken))
        {
            var result = new SiteDescription();

            result.Resources.AddRange(await DescribeEndpoint("/".AsEndpoint(), ct).ConfigureAwait(false));

            return result;
        }

        async Task<List<SiteFolderDescription>> DescribeEndpoint(IEndpoint endpoint, CancellationToken ct = default(CancellationToken))
        {
            SiteFolderDescription folderDescription = null;
            var result = new List<SiteFolderDescription>();
            try
            {
                folderDescription = await Get<SiteFolderDescription>(endpoint, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                // don't have access to the folder
                result.Add(new SiteFolderDescription
                {
                    Error = new ArcGISError
                    {
                        Message = "HttpRequestException for Get SiteFolderDescription at path " + endpoint.RelativeUrl,
                        Details = new[] { ex.ToString() }
                    }
                });
                return result;
            }
            catch (System.Runtime.Serialization.SerializationException ex)
            {
                // don't have access to the folder
                result.Add(new SiteFolderDescription
                {
                    Error = new ArcGISError
                    {
                        Message = "SerializationException for Get SiteFolderDescription at path " + endpoint.RelativeUrl,
                        Details = new[] { ex.ToString() }
                    }
                });
                return result;
            }
            catch (Exception ex)
            {
                result.Add(new SiteFolderDescription
                {
                    Error = new ArcGISError
                    {
                        Message = "Exception for Get SiteFolderDescription at path " + endpoint.RelativeUrl,
                        Details = new[] { ex.ToString() }
                    }
                });
                return result;
            }
            if (ct.IsCancellationRequested) return result;

            folderDescription.Path = endpoint.RelativeUrl;
            result.Add(folderDescription);

            if (folderDescription.Folders != null)
                foreach (var folder in folderDescription.Folders)
                {
                    if (ct.IsCancellationRequested) return result;
                    result.AddRange(await DescribeEndpoint((endpoint.RelativeUrl + folder).AsEndpoint(), ct).ConfigureAwait(false));
                }

            return result;
        }

        /// <summary>
        /// Return the service description details for the matched services in the site description
        /// </summary>
        /// <param name="siteDescription"></param>
        /// <param name="ct">An optional cancellation token</param>
        /// <returns>A collection of service description details</returns>
        public virtual Task<List<ServiceDescriptionDetailsResponse>> DescribeServices(SiteDescription siteDescription, CancellationToken ct = default(CancellationToken))
        {
            Guard.AgainstNullArgument(nameof(siteDescription), siteDescription);
            Guard.AgainstNullArgumentProperty(nameof(siteDescription), nameof(siteDescription.Services) , siteDescription.Services);

            return DescribeServices(siteDescription.Services.ToList(), ct);
        }

        /// <summary>
        /// Return the service description details for the matched services
        /// </summary>
        /// <param name="services"></param>
        /// <param name="ct">An optional cancellation token</param>
        /// <returns>A collection of service description details</returns>
        public virtual async Task<List<ServiceDescriptionDetailsResponse>> DescribeServices(List<ServiceDescription> services, CancellationToken ct = default(CancellationToken))
        {
            Guard.AgainstNullArgument(nameof(services), services);

            var result = new List<ServiceDescriptionDetailsResponse>();

            foreach (var serviceDescription in services)
            {
                result.Add(await DescribeService(serviceDescription.ArcGISServerEndpoint, ct).ConfigureAwait(false));
            }

            return result;
        }

        /// <summary>
        ///  Return the service description details for the requested endpoint
        /// </summary>
        /// <param name="serviceEndpoint"></param>
        /// <param name="ct">An optional cancellation token</param>
        /// <returns>The service description details</returns>
        public virtual Task<ServiceDescriptionDetailsResponse> DescribeService(IEndpoint serviceEndpoint, CancellationToken ct = default(CancellationToken))
        {
            Guard.AgainstNullArgument(nameof(serviceEndpoint), serviceEndpoint);

            return Get<ServiceDescriptionDetailsResponse>(new ServiceDescriptionDetails(serviceEndpoint), ct);
        }

        /// <summary>
        /// Return the layer description details for the requested endpoint
        /// </summary>
        /// <param name="layerEndpoint"></param>
        /// <param name="ct"></param>
        /// <returns>The layer description details</returns>
        public virtual Task<ServiceLayerDescriptionResponse> DescribeLayer(IEndpoint layerEndpoint, CancellationToken ct = default(CancellationToken))
        {
            Guard.AgainstNullArgument(nameof(layerEndpoint), layerEndpoint);

            return Get<ServiceLayerDescriptionResponse>(new ServiceLayerDescription(layerEndpoint), ct);
        }

        /// <summary>
        /// Admin operation used to get all services for the ArcGIS Server and their reports
        /// </summary>
        /// <param name="ct">Optional cancellation token to cancel pending request</param>
        /// <param name="path">The starting path (folder). If omitted then this will start at the root and get all sub folders too</param>
        /// <returns>All discovered services for the site</returns>
        public virtual async Task<SiteReportResponse> SiteReport(string path = "", CancellationToken ct = default(CancellationToken))
        {
            var folders = new List<string>();

            if (string.IsNullOrWhiteSpace(path))
            {
                var folderDescription = await Get<SiteFolderDescription>("/".AsEndpoint(), ct).ConfigureAwait(false);
                folders.Add("/");
                folders.AddRange(folderDescription.Folders);
            }
            else
                folders.Add(path);

            var result = new SiteReportResponse();
            foreach (var folder in folders)
            {
                var folderReport = await Get<FolderReportResponse>(new ServiceReport(folder), ct).ConfigureAwait(false);

                result.Resources.Add(folderReport);

                if (ct.IsCancellationRequested) return result;
            }
            return result;
        }

        /// <summary>
        /// Returns the expected and actual status of a service
        /// </summary>
        /// <param name="serviceDescription">Service description usually generated from a previous call to DescribeSite</param>
        /// <param name="ct">Optional cancellation token to cancel pending request</param>
        /// <returns>The expected and actual status of the service</returns>
        public virtual Task<ServiceStatusResponse> ServiceStatus(ServiceDescription serviceDescription, CancellationToken ct = default(CancellationToken))
        {
            return Get<ServiceStatusResponse>(new ServiceStatus(serviceDescription), ct);
        }

        /// <summary>
        /// Start the service
        /// </summary>
        /// <param name="serviceDescription">Service description usually generated from a previous call to DescribeSite</param>
        /// <param name="ct">Optional cancellation token to cancel pending request</param>
        /// <returns>Standard response object</returns>
        public virtual Task<StartStopServiceResponse> StartService(ServiceDescription serviceDescription, CancellationToken ct = default(CancellationToken))
        {
            return Post<StartStopServiceResponse, StartService>(new StartService(serviceDescription), ct);
        }

        /// <summary>
        /// Stop the service
        /// </summary>
        /// <param name="serviceDescription">Service description usually generated from a previous call to DescribeSite</param>
        /// <param name="ct">Optional cancellation token to cancel pending request</param>
        /// <returns>Standard response object</returns>
        public virtual Task<StartStopServiceResponse> StopService(ServiceDescription serviceDescription, CancellationToken ct = default(CancellationToken))
        {
            return Post<StartStopServiceResponse, StopService>(new StopService(serviceDescription), ct);
        }

        /// <summary>
        /// Call the reverse geocode operation.
        /// </summary>
        /// <param name="reverseGeocode"></param>
        /// <param name="ct">Optional cancellation token to cancel pending request</param>
        /// <returns></returns>
        public virtual Task<ReverseGeocodeResponse> ReverseGeocode(ReverseGeocode reverseGeocode, CancellationToken ct = default(CancellationToken))
        {
            return Get<ReverseGeocodeResponse, ReverseGeocode>(reverseGeocode, ct);
        }

        /// <summary>
        /// Call the single line geocode operation.
        /// </summary>
        /// <param name="geocode"></param>
        /// <param name="ct">Optional cancellation token to cancel pending request</param>
        /// <returns></returns>
        public virtual Task<SingleInputGeocodeResponse> Geocode(SingleInputGeocode geocode, CancellationToken ct = default(CancellationToken))
        {
            return Get<SingleInputGeocodeResponse, SingleInputGeocode>(geocode, ct);
        }

        /// <summary>
        /// Call the suggest geocode operation.
        /// </summary>
        /// <param name="suggestGeocode"></param>
        /// <param name="ct">Optional cancellation token to cancel pending request</param>
        /// <returns></returns>
        public virtual Task<SuggestGeocodeResponse> Suggest(SuggestGeocode suggestGeocode, CancellationToken ct = default(CancellationToken))
        {
            return Get<SuggestGeocodeResponse, SuggestGeocode>(suggestGeocode, ct);
        }

        /// <summary>
        /// Call the find operation, note that since this can return more than one geometry type you will need to deserialize
        /// the geometry string on the result set e.g.
        /// foreach (var result in response.Results.Where(r => r.Geometry != null))
        /// {
        ///     result.Geometry = ServiceStack.Text.JsonSerializer.DeserializeFromString(result.Geometry.SerializeToString(), TypeMap[result.GeometryType]());
        /// }
        /// </summary>
        /// <param name="findOptions"></param>
        /// <param name="ct">Optional cancellation token to cancel pending request</param>
        /// <returns></returns>
        public virtual Task<FindResponse> Find(Find findOptions, CancellationToken ct = default(CancellationToken))
        {
            return Get<FindResponse, Find>(findOptions, ct);
        }
    }
}
