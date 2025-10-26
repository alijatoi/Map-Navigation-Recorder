using MapNavigationRecorder.Models;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace MapNavigationRecorder.Services
{
    internal static class RouteExportService
    {
        // Define commonly used XML namespaces
        private static readonly XNamespace XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

        /// <summary>
        /// Export route to GPX format (GPS Exchange Format)
        /// </summary>
        public static string ExportToGpx(SavedRoute route)
        {
            var gpx = new XElement("gpx",
                new XAttribute("version", "1.1"),
                new XAttribute("creator", "MapNavigationRecorder"),
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace.NamespaceName),
                new XAttribute(XsiNamespace + "schemaLocation", "http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/1/1/gpx.xsd"),
                new XElement("metadata",
                    new XElement("name", route.Name),
                    new XElement("time", route.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                ),
                new XElement("trk",
                    new XElement("name", route.Name),
                    new XElement("trkseg",
                        route.Points.Select(p =>
                            new XElement("trkpt",
                                new XAttribute("lat", p.Latitude.ToString(CultureInfo.InvariantCulture)),
                                new XAttribute("lon", p.Longitude.ToString(CultureInfo.InvariantCulture)),
                                new XElement("time", p.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                            )
                        )
                    )
                )
            );

            return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{gpx}";
        }

        /// <summary>
        /// Export route to KML format (Keyhole Markup Language - Google Earth)
        /// </summary>
        public static string ExportToKml(SavedRoute route)
        {
            var coordinates = string.Join("\n",
                route.Points.Select(p => $"{p.Longitude.ToString(CultureInfo.InvariantCulture)},{p.Latitude.ToString(CultureInfo.InvariantCulture)},0"));

            var kml = new XElement(XNamespace.Get("http://www.opengis.net/kml/2.2") + "kml",
                new XElement("Document",
                    new XElement("name", route.Name),
                    new XElement("Placemark",
                        new XElement("name", route.Name),
                        new XElement("description", $"Recorded on {route.Timestamp:yyyy-MM-dd HH:mm:ss}"),
                        new XElement("Style",
                            new XElement("LineStyle",
                                new XElement("color", "ff0000ff"),
                                new XElement("width", "4")
                            )
                        ),
                        new XElement("LineString",
                            new XElement("tessellate", "1"),
                            new XElement("coordinates", coordinates)
                        )
                    )
                )
            );

            return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{kml}";
        }

        /// <summary>
        /// Export route to GeoJSON format
        /// </summary>
        public static string ExportToGeoJson(SavedRoute route)
        {
            var coordinates = route.Points.Select(p =>
                $"[{p.Longitude.ToString(CultureInfo.InvariantCulture)},{p.Latitude.ToString(CultureInfo.InvariantCulture)}]");

            var geoJson = @$"{{
  ""type"": ""FeatureCollection"",
  ""features"": [
    {{
      ""type"": ""Feature"",
      ""properties"": {{
        ""name"": ""{route.Name}"",
        ""timestamp"": ""{route.Timestamp:yyyy-MM-ddTHH:mm:ssZ}"",
        ""points"": {route.Points.Count}
      }},
      ""geometry"": {{
        ""type"": ""LineString"",
        ""coordinates"": [
          {string.Join(",\n          ", coordinates)}
        ]
      }}
    }}
  ]
}}";
            return geoJson;
        }

        /// <summary>
        /// Export route to CSV format (comma-separated values)
        /// </summary>
        public static string ExportToCsv(SavedRoute route)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Latitude,Longitude,Timestamp");

            foreach (var point in route.Points)
            {
                sb.AppendLine($"{point.Latitude.ToString(CultureInfo.InvariantCulture)},{point.Longitude.ToString(CultureInfo.InvariantCulture)},{point.Timestamp:yyyy-MM-dd HH:mm:ss}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Export route to TCX format (Training Center XML - Garmin)
        /// </summary>
        public static string ExportToTcx(SavedRoute route)
        {
            var ns = XNamespace.Get("http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2");

            var tcx = new XElement(ns + "TrainingCenterDatabase",
                new XAttribute(XNamespace.Xmlns + "xsi", XsiNamespace.NamespaceName),
                new XAttribute(XsiNamespace + "schemaLocation",
                    "http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2 http://www.garmin.com/xmlschemas/TrainingCenterDatabasev2.xsd"),
                new XElement(ns + "Courses",
                    new XElement(ns + "Course",
                        new XElement(ns + "Name", route.Name),
                        new XElement(ns + "Lap",
                            new XElement(ns + "TotalTimeSeconds", "0"),
                            new XElement(ns + "DistanceMeters", "0"),
                            new XElement(ns + "Intensity", "Active")
                        ),
                        new XElement(ns + "Track",
                            route.Points.Select(p =>
                                new XElement(ns + "Trackpoint",
                                    new XElement(ns + "Time", p.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                                    new XElement(ns + "Position",
                                        new XElement(ns + "LatitudeDegrees", p.Latitude.ToString(CultureInfo.InvariantCulture)),
                                        new XElement(ns + "LongitudeDegrees", p.Longitude.ToString(CultureInfo.InvariantCulture))
                                    )
                                )
                            )
                        )
                    )
                )
            );

            return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n{tcx}";
        }

        /// <summary>
        /// Get file extension for the specified format
        /// </summary>
        public static string GetFileExtension(string format)
        {
            return format.ToLower() switch
            {
                "gpx" => ".gpx",
                "kml" => ".kml",
                "geojson" => ".geojson",
                "csv" => ".csv",
                "tcx" => ".tcx",
                _ => ".txt"
            };
        }

        /// <summary>
        /// Export route to the specified format
        /// </summary>
        public static string ExportRoute(SavedRoute route, string format)
        {
            return format.ToLower() switch
            {
                "gpx" => ExportToGpx(route),
                "kml" => ExportToKml(route),
                "geojson" => ExportToGeoJson(route),
                "csv" => ExportToCsv(route),
                "tcx" => ExportToTcx(route),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };
        }
    }
}