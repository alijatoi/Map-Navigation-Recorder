using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MauiApp1
{
    public partial class MainPage : ContentPage
    {

        readonly List<GpsPoint> _points = new();
        CancellationTokenSource? _cts;
        readonly HttpClient _http = new();

        public MainPage()
        {
            InitializeComponent();
            var html = LoadMapHtml();
            MapWebView.Source = new HtmlWebViewSource { Html = html };
        }

        string LoadMapHtml()
        {
            try
            {
                var task = FileSystem.OpenAppPackageFileAsync("map.html");
                task.Wait();
                using var stream = task.Result;
                using var reader = new System.IO.StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch
            {
                return "<html><body><h3>Map failed to load</h3></body></html>";
            }
        }

        async void OnRecordClicked(object? sender, EventArgs e)
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                await DisplayAlert("Permission", "Location permission required.", "OK");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            RecordButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusLabel.Text = "Recording...";

            _ = Task.Run(() => RecordingLoopAsync(_cts.Token));
        }

        async Task RecordingLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    double lat = 0, lon = 0;
                    bool gotLocation = false;

                    try
                    {
                        var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                        var location = await Geolocation.GetLocationAsync(request, token);
                        if (location != null)
                        {
                            lat = location.Latitude;
                            lon = location.Longitude;
                            gotLocation = true;
                        }
                    }
                    catch { }

#if WINDOWS
                    // Fallback to IP-based location
                    if (!gotLocation)
                    {
                        var ipLoc = await GetLocationFromIpAsync();
                        if (ipLoc != null)
                        {
                            lat = ipLoc.Latitude;
                            lon = ipLoc.Longitude;
                            gotLocation = true;
                        }
                    }
#endif

                    if (gotLocation)
                    {
                        AddPointIfSignificant(lat, lon);
                    }

                    await Task.Delay(2000, token);
                }
            }
            catch (OperationCanceledException) { }
        }

#if WINDOWS
        // IP-based location record
        public record IpLocation(double Latitude, double Longitude);
        public record IpLocationResponse(double latitude, double longitude);

        async Task<IpLocation?> GetLocationFromIpAsync()
        {
            try
            {
                var result = await _http.GetFromJsonAsync<IpLocationResponse>("https://ipapi.co/json/");
                if (result != null) return new IpLocation(result.latitude, result.longitude);
            }
            catch { }
            return null;
        }
#endif

        void AddPointIfSignificant(double lat, double lon)
        {
            lock (_points)
            {
                if (_points.Count == 0)
                {
                    _points.Add(new GpsPoint(lat, lon, DateTime.UtcNow));
                    SendPointToMap(lat, lon);
                }
                else
                {
                    var last = _points[^1];
                    var dist = HaversineDistanceMeters(last.Latitude, last.Longitude, lat, lon);
                    if (dist >= 8.0)
                    {
                        _points.Add(new GpsPoint(lat, lon, DateTime.UtcNow));
                        SendPointToMap(lat, lon);
                    }
                }
            }
        }

        void SendPointToMap(double lat, double lon)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var js = $"window.addPoint({lat.ToString(CultureInfo.InvariantCulture)},{lon.ToString(CultureInfo.InvariantCulture)});";
                    await MapWebView.EvaluateJavaScriptAsync(js);
                }
                catch { }
            });
        }

        double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            static double ToRad(double deg) => deg * Math.PI / 180.0;
            var R = 6371000.0;
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        async void OnStopClicked(object? sender, EventArgs e)
        {
            _cts?.Cancel();
            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusLabel.Text = "Stopped";

            List<GpsPoint> pointsCopy;
            lock (_points) pointsCopy = _points.ToList();

            if (pointsCopy.Count >= 2)
            {
                StatusLabel.Text = "Snapping to roads...";
                var success = await TrySnapToRoadsAndRender(pointsCopy);
                StatusLabel.Text = success ? "Route shown (snapped)" : "Route shown (raw)";
                if (!success) await RenderRawTrack(pointsCopy);
            }

            if (_points.Count >= 1)
            {
                bool save = await DisplayAlert("Save Route?", "Do you want to save this route?", "Yes", "No");
                if (save)
                {
                    string routeName = await DisplayPromptAsync("Save Route", "Enter a name for the route:", initialValue: $"Route {DateTime.Now:yyyy-MM-dd HH-mm}");
                    if (!string.IsNullOrWhiteSpace(routeName))
                    {
                        var route = new SavedRoute
                        {
                            Name = routeName,
                            Timestamp = DateTime.Now,
                            Points = _points.ToList()
                        };

                        await RouteStorageService.SaveRouteAsync(route);
                        await DisplayAlert("Saved", $"Route '{routeName}' saved.", "OK");
                    }
                }
            }
            else if (pointsCopy.Count == 1)
            {
                var p = pointsCopy[0];
                var geojson = $"{{\"type\":\"FeatureCollection\",\"features\":[{{\"type\":\"Feature\",\"geometry\":{{\"type\":\"LineString\",\"coordinates\":[[{p.Longitude.ToString(CultureInfo.InvariantCulture)},{p.Latitude.ToString(CultureInfo.InvariantCulture)}]]}}}}]}}";
                await MapWebView.EvaluateJavaScriptAsync($"window.setRoute({geojson});");
                StatusLabel.Text = "Single point recorded";
            }
        }

        async void OnSavedRoutesClicked(object sender, EventArgs e)
        {
            var routes = await RouteStorageService.LoadRoutesAsync();
            if (routes.Count == 0)
            {
                await DisplayAlert("No Routes", "No saved routes found.", "OK");
                return;
            }

            string[] options = routes.Select(r => r.Name).ToArray();
            string selected = await DisplayActionSheet("Saved Routes", "Cancel", null, options);

            if (!string.IsNullOrWhiteSpace(selected) && selected != "Cancel")
            {
                var route = routes.First(r => r.Name == selected);
                await DisplayRouteOnMap(route);
                await ShowRouteOptions(route);
            }
        }


        async Task DisplayRouteOnMap(SavedRoute route)
        {
            var coordsArr = route.Points.Select(p => new[] { p.Longitude, p.Latitude }).ToArray();
            var geojsonObj = new
            {
                type = "FeatureCollection",
                features = new[]
                {
            new { type = "Feature", geometry = new { type = "LineString", coordinates = coordsArr } }
        }
            };

            var json = JsonSerializer.Serialize(geojsonObj);
            await MapWebView.EvaluateJavaScriptAsync($"window.setRoute({json});");
            StatusLabel.Text = $"Showing: {route.Name}";
        }

        async Task ShowRouteOptions(SavedRoute route)
        {
            string action = await DisplayActionSheet($"Options for '{route.Name}'", "Cancel", null, "Rename", "Delete");
            if (action == "Rename")
            {
                string newName = await DisplayPromptAsync("Rename Route", "Enter new name:", initialValue: route.Name);
                if (!string.IsNullOrWhiteSpace(newName))
                    await RouteStorageService.RenameRouteAsync(route, newName);
            }
            else if (action == "Delete")
            {
                RouteStorageService.DeleteRoute(route);
                await DisplayAlert("Deleted", $"Route '{route.Name}' deleted.", "OK");
            }
        }

        async Task<bool> TrySnapToRoadsAndRender(List<GpsPoint> points)
        {
            try
            {
                var coords = string.Join(";", points.Select(p => $"{p.Longitude.ToString(CultureInfo.InvariantCulture)},{p.Latitude.ToString(CultureInfo.InvariantCulture)}"));
                var url = $"https://router.project-osrm.org/route/v1/driving/{coords}?overview=full&geometries=geojson&steps=false";
                using var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return false;

                using var stream = await resp.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);
                if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0) return false;
                var route = routes[0];
                if (!route.TryGetProperty("geometry", out var geometry)) return false;

                var geometryRaw = geometry.GetRawText();
                var geojson = $"{{\"type\":\"FeatureCollection\",\"features\":[{{\"type\":\"Feature\",\"geometry\":{geometryRaw}}}]}}";

                await MapWebView.EvaluateJavaScriptAsync($"window.setRoute({geojson});");
                return true;
            }
            catch
            {
                return false;
            }
        }

        async Task RenderRawTrack(List<GpsPoint> points)
        {
            var coordsArr = points.Select(p => new[] { p.Longitude, p.Latitude }).ToArray();
            var geojsonObj = new { type = "FeatureCollection", features = new[] { new { type = "Feature", geometry = new { type = "LineString", coordinates = coordsArr } } } };
            var json = JsonSerializer.Serialize(geojsonObj);
            await MapWebView.EvaluateJavaScriptAsync($"window.setRoute({json});");
        }

        async void OnClearClicked(object? sender, EventArgs e)
        {
            _cts?.Cancel();
            lock (_points) _points.Clear();
            RecordButton.IsEnabled = true;
            StopButton.IsEnabled = false;
            StatusLabel.Text = "Cleared";
            try { await MapWebView.EvaluateJavaScriptAsync("window.clearRoute();"); } catch { }
        }
    }
}
