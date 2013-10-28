﻿using System;
using System.Runtime.Serialization;
using ArcGIS.ServiceModel.Common;

namespace ArcGIS.ServiceModel.Operation
{
    [DataContract]
    public abstract class GeocodeOperation : ArcGISServerOperation
    {
        protected GeocodeOperation(ArcGISServerEndpoint endpoint, String operationPath)
            : base(endpoint, operationPath)
        { }

        /// <summary>
        /// Defines an origin point location that is used with the distance parameter to sort geocoding candidates based upon their proximity to the location. 
        /// The distance parameter specifies the radial distance from the location in meters. 
        /// The priority of candidates within this radius is boosted relative to those outside the radius.
        /// The location parameter can be specified without specifying a distance. If distance is not specified, it defaults to 2000 meters.
        /// </summary>
        [DataMember(Name = "location")]
        public Point Location { get; set; }

        /// <summary>
        /// Specifies the radius of an area around a point location which is used to boost the rank of geocoding candidates so that candidates closest to the location are returned first. The distance value is in meters. 
        /// If the distance parameter is specified, then the location parameter must be specified as well.
        /// It is important to note that unlike the bbox parameter, the location/distance parameters allow searches to extend beyond the specified search radius. 
        /// They are not used to filter results, but rather to rank resulting candidates based on their distance from a location. 
        /// You must pass a bbox value in addition to location/distance if you want to confine the search results to a specific area.
        /// </summary>
        [DataMember(Name = "distance")]
        public double? Distance { get; set; }        
    }

    /// <summary>
    /// Reverse geocoding is useful for applications in which a user will click a location in a map and expect the address of that location to be returned.
    /// It involves passing the coordinates of a point location to the geocoding service, which returns the address closest to the location.
    /// </summary>
    /// <remarks>If no distance value is specified then the value is assumed to be 100 meters. </remarks>
    [DataContract]
    public class ReverseGeocode : GeocodeOperation
    {
        public ReverseGeocode(ArcGISServerEndpoint endpoint)
            : base(endpoint, Operations.ReverseGeocode)
        {
            Distance = 100;
        }

        /// <summary>
        /// The spatial reference of the x/y coordinates returned by a geocode request. 
        /// This is useful for applications using a map with a spatial reference different than that of the geocode service. 
        /// If the outSR is not specified, the spatial reference of the output locations is the same as that of the service. 
        /// The world geocoding service spatial reference is WGS84 (WKID = 4326). 
        /// </summary>
        [DataMember(Name = "outSR")]
        public SpatialReference OutputSpatialReference { get; set; }
    }

    [DataContract]
    public class ReverseGeocodeResponse : PortalResponse
    {
        /// <summary>
        /// Complete matching address returned for findAddressCandidates and geocodeAddresses geocode requests. 
        /// </summary>
        [DataMember(Name = "address")]
        public Address Address { get; set; }

        /// <summary>
        /// The point coordinates of the output match location, as specified by the x and y properties. 
        /// The spatial reference of the x and y coordinates is defined by the spatialReference output field. 
        /// Always returned by default for findAddressCandidates and geocodeAddresses geocode requests only.
        /// </summary>
        [DataMember(Name = "location")]
        public Point Location { get; set; }
    }

    [DataContract]
    public class Address
    {
        [DataMember(Name = "Address")]
        public String AddressText { get; set; }

        [DataMember(Name = "Neighborhood")]
        public String Neighborhood { get; set; }

        [DataMember(Name = "City")]
        public String City { get; set; }

        [DataMember(Name = "Subregion")]
        public String Subregion { get; set; }

        [DataMember(Name = "Region")]
        public String Region { get; set; }

        [DataMember(Name = "Postal")]
        public String Postal { get; set; }

        [DataMember(Name = "PostalExt")]
        public String PostalExt { get; set; }

        [DataMember(Name = "CountryCode")]
        public String CountryCode { get; set; }

        /// <summary>
        /// The name of the component locator used to return a particular match result. 
        /// It is a combination of the 3-digit ISO country code for the country within which the match is located and the address locator style, such as StreetAddress. 
        /// Example: USA.StreetAddress
        /// </summary>
        /// <remarks>The Loc_name field is used internally by ArcGIS software and is not intended for use by client applications.</remarks>
        [DataMember(Name = "Loc_name")]
        public String LocatorName { get; set; }
    }
}
