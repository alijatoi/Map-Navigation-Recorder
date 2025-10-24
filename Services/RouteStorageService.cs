using MapNavigationRecorder.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace MapNavigationRecorder.Services
{
    internal static class RouteStorageService
    {
        // Save a route as a separate JSON file
        public static async Task SaveRouteAsync(SavedRoute route)
        {
            var filePath = Path.Combine(FileSystem.AppDataDirectory, $"{route.Id}.json");
            var json = JsonSerializer.Serialize(route);
            await File.WriteAllTextAsync(filePath, json);
        }

        // Load all saved routes
        public static async Task<List<SavedRoute>> LoadRoutesAsync()
        {
            var routes = new List<SavedRoute>();
            var files = Directory.GetFiles(FileSystem.AppDataDirectory, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var route = JsonSerializer.Deserialize<SavedRoute>(json);
                    if (route != null)
                        routes.Add(route);
                }
                catch
                {
                    // Ignore corrupted files
                }
            }

            return routes.OrderByDescending(r => r.Timestamp).ToList();
        }

        // Delete a route file
        public static void DeleteRoute(SavedRoute route)
        {
            var filePath = Path.Combine(FileSystem.AppDataDirectory, $"{route.Id}.json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        // Rename a route (update Name property and resave)
        public static async Task RenameRouteAsync(SavedRoute route, string newName)
        {
            route.Name = newName;
            await SaveRouteAsync(route);
        }
    }
}
