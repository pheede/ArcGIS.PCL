﻿using ArcGIS.ServiceModel;
using ArcGIS.ServiceModel.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace ArcGIS.ServiceModel.Operation
{
    [DataContract]
    public class GeometryOperationResponse<T> : PortalResponse where T : IGeometry
    {
        [DataMember(Name = "geometries")]
        public List<T> Geometries { get; set; }
    }

    [DataContract]
    public class SimplifyGeometry<T> : ArcGISServerOperation where T : IGeometry
    {
        public SimplifyGeometry(IEndpoint endpoint, List<Feature<T>> features = null, SpatialReference spatialReference = null)
        {
            if (endpoint == null) throw new ArgumentNullException("endpoint");
            Endpoint = (endpoint is AbsoluteEndpoint) ?
               (IEndpoint) new AbsoluteEndpoint(endpoint.RelativeUrl.Trim('/') + "/" + Operations.Simplify)
             : (IEndpoint) new ArcGISServerEndpoint(endpoint.RelativeUrl.Trim('/') + "/" + Operations.Simplify);

            Geometries = new GeometryCollection<T> { Geometries = features == null ? null : features.Select(f => f.Geometry).ToList() };
            SpatialReference = spatialReference;
        }

        [DataMember(Name = "geometries")]
        public GeometryCollection<T> Geometries { get; set; }

        [DataMember(Name = "sr")]
        public SpatialReference SpatialReference { get; set; }
    }

    [DataContract]
    public class BufferGeometry<T> : GeometryOperation<T> where T : IGeometry
    {
        public BufferGeometry(IEndpoint endpoint, List<Feature<T>> features, SpatialReference spatialReference, double distance)
            : base(endpoint, features, spatialReference, Operations.Buffer)
        {
            Geometries.Geometries.First().SpatialReference = spatialReference;
            BufferSpatialReference = spatialReference;
            Distances = new List<double> { distance };
        }

        [DataMember(Name = "bufferSR")]
        public SpatialReference BufferSpatialReference { get; set; }

        public List<double> Distances { get; set; }

        [DataMember(Name = "distances")]
        public string DistancesCSV
        {
            get
            {
                string strDistances = "";
                foreach (double distance in Distances)
                {
                    strDistances += distance.ToString("0.000") + ", ";
                }

                if (strDistances.Length >= 2)
                {
                    strDistances = strDistances.Substring(0, strDistances.Length - 2);
                }

                return strDistances;
            }
        }

        /// <summary>
        /// See http://resources.esri.com/help/9.3/ArcGISDesktop/ArcObjects/esriGeometry/esriSRUnitType.htm and http://resources.esri.com/help/9.3/ArcGISDesktop/ArcObjects/esriGeometry/esriSRUnit2Type.htm
        /// If not specified, derived from bufferSR, or inSR.
        /// </summary>
        [DataMember(Name = "unit")]
        public string Unit { get; set; }

        [DataMember(Name = "unionResults")]
        public bool UnionResults { get; set; }
    }

    [DataContract]
    public class ProjectGeometry<T> : GeometryOperation<T> where T : IGeometry
    {
        public ProjectGeometry(IEndpoint endpoint, List<Feature<T>> features, SpatialReference outputSpatialReference)
            : base(endpoint, features, outputSpatialReference, Operations.Project)
        { }
    }

    [DataContract]
    public class GeometryCollection<T> where T : IGeometry
    {
        [DataMember(Name = "geometryType")]
        public String GeometryType
        {
            get
            {
                return Geometries == null
                    ? String.Empty
                    : GeometryTypes.TypeMap[Geometries.First().GetType()]();
            }
        }

        [DataMember(Name = "geometries")]
        public List<T> Geometries { get; set; }
    }

    public abstract class GeometryOperation<T> : ArcGISServerOperation where T : IGeometry
    {
        public GeometryOperation(IEndpoint endpoint,
            List<Feature<T>> features,
            SpatialReference outputSpatialReference,
            String operation)
        {
            if (endpoint == null) throw new ArgumentNullException("endpoint");
            Endpoint = (endpoint is AbsoluteEndpoint) ?
               (IEndpoint)new AbsoluteEndpoint(endpoint.RelativeUrl.Trim('/') + "/" + operation)
             : (IEndpoint)new ArcGISServerEndpoint(endpoint.RelativeUrl.Trim('/') + "/" + operation);

            if (features.Any())
            {
                Geometries = new GeometryCollection<T> { Geometries = new List<T>(features.Select(f => f.Geometry)) };

                if (Geometries.Geometries.First().SpatialReference == null && features.First().Geometry.SpatialReference != null)
                    Geometries.Geometries.First().SpatialReference = new SpatialReference { Wkid = features.First().Geometry.SpatialReference.Wkid };
            }
            OutputSpatialReference = outputSpatialReference;
        }

        [DataMember(Name = "geometries")]
        public GeometryCollection<T> Geometries { get; protected set; }

        /// <summary>
        /// Taken from the spatial reference of the first geometry, if that is null then assumed to be using Wgs84
        /// </summary>
        [DataMember(Name = "inSR")]
        public SpatialReference InputSpatialReference { get { return Geometries.Geometries.First().SpatialReference ?? SpatialReference.WGS84; } }

        /// <summary>
        /// The spatial reference of the returned geometry. 
        /// If not specified, the geometry is returned in the spatial reference of the input.
        /// </summary>
        [DataMember(Name = "outSR")]
        public SpatialReference OutputSpatialReference { get; protected set; }
    }
}
