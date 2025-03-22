using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace MapSearchApp
{
    public partial class Form1 : Form
    {
        private WebView2 webView;
        private Dictionary<string, List<string>> graph;
        private Dictionary<string, (double lat, double lng)> coordinates;
        private string? searchMethod;
        private string? selectedDestination;
        private const double DistanceThreshold = 1.0; // Adjust this value as needed (in kilometers)

        public Form1()
        {
            InitializeComponent();
            webView = new WebView2();
            graph = new Dictionary<string, List<string>>();
            coordinates = GetUserDefinedCoordinates();
            LoadGraph();
            AskSearchMethod();
            InitializeWebView();
        }

        private Dictionary<string, (double, double)> GetUserDefinedCoordinates()
        {
            return new Dictionary<string, (double, double)>
            {
                    { "A", (9.301199, 123.29617) },
                    { "B", (9.294393, 123.294144) },
                    { "C", (9.307714, 123.304092) },
                    { "D", (9.304323, 123.298001) },
                    { "E", (9.304994, 123.309263) },
                    { "F", (9.303039, 123.309612) },
                    { "G", (9.310496, 123.30648) },
                    { "H", (9.29658, 123.297849) },
                    { "I", (9.304659, 123.29566) },
                    { "J", (9.296035, 123.31152) },
                    { "K", (9.294138, 123.311514) },
                    { "L", (9.309136, 123.302479) },
                    { "M", (9.310213, 123.304725) },
                    { "N", (9.293316, 123.299999) },
                    { "O", (9.294654, 123.309478) },
                    { "P", (9.30684, 123.304742) },
                    { "Q", (9.300373, 123.302151) },
                    { "R", (9.291963, 123.30754) },
                    { "S", (9.310988, 123.300073) },
                    { "T", (9.306884, 123.305691) },
                    { "U", (9.294843, 123.296034) },
                    { "V", (9.308279, 123.304715) },
                    { "W", (9.301037, 123.310136) },
                    { "X", (9.306476, 123.29354) },
                    { "Y", (9.299136, 123.305627) },
                    { "Z", (9.305215, 123.308054) },
                    { "AA", (9.310077, 123.310511) },
                    { "AB", (9.298886, 123.309786) },
                    { "AC", (9.292617, 123.311936) },
                    { "AD", (9.307633, 123.30127) }
            };
        }

        private void LoadGraph()
        {
            foreach (var loc in coordinates.Keys)
                graph[loc] = new List<string>();

            foreach (var loc1 in coordinates.Keys)
            {
                foreach (var loc2 in coordinates.Keys)
                {
                    if (loc1 != loc2 && CalculateDistance(coordinates[loc1], coordinates[loc2]) <= DistanceThreshold)
                    {
                        graph[loc1].Add(loc2);
                    }
                }
            }
        }

        private double CalculateDistance((double lat, double lng) coord1, (double lat, double lng) coord2)
        {
            double R = 6371; // Radius of the Earth in kilometers
            double lat1 = coord1.lat * Math.PI / 180;
            double lat2 = coord2.lat * Math.PI / 180;
            double dLat = (coord2.lat - coord1.lat) * Math.PI / 180;
            double dLng = (coord2.lng - coord1.lng) * Math.PI / 180;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double distance = R * c;

            return distance;
        }

        private void AskSearchMethod()
        {
            var result = MessageBox.Show("Choose search method:\nYes = DFS, No = BFS", "Search Method", MessageBoxButtons.YesNo);
            searchMethod = result == DialogResult.Yes ? "DFS" : "BFS";
        }

        private async void InitializeWebView()
        {
            webView.Dock = DockStyle.Fill;
            this.Controls.Add(webView);
            await webView.EnsureCoreWebView2Async();
            webView.NavigationCompleted += WebView_NavigationCompleted;
            LoadMapWithPins();
        }

        private void LoadMapWithPins()
        {
            string script = GenerateMapScript();
            webView.NavigateToString(script);
        }

        private string GenerateMapScript()
        {
            string script = "<html><head>"
                + "<link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css' />"
                + "<script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>"
                + "</head><body>"
                + "<div id='map' style='width:100%; height:100vh;'></div>"
                + "<script>"
                + "var map = L.map('map').setView([9.301698619415394, 123.30338631858156], 14);"
                + "L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {"
                + "   attribution: '&copy; OpenStreetMap contributors'"
                + "}).addTo(map);";

            foreach (var loc in coordinates)
            {
                script += $"L.marker([{loc.Value.lat}, {loc.Value.lng}]).addTo(map).bindPopup('{loc.Key}').on('click', function() {{ window.chrome.webview.postMessage('{loc.Key}'); }});";
            }

            script += "</script></body></html>";
            return script;
        }

        private void WebView_NavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
        }

        private void WebView_WebMessageReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
        {
            string message = e.WebMessageAsJson.Trim('"');
            if (!string.IsNullOrEmpty(message))
            {
                selectedDestination = message;
                SearchPath();
            }
        }

        private void SearchPath()
        {
            if (selectedDestination == null || !graph.ContainsKey(selectedDestination))
            {
                MessageBox.Show("Invalid destination");
                return;
            }

            List<string> path = searchMethod == "DFS" ? DepthFirstSearch("A", selectedDestination) : BreadthFirstSearch("A", selectedDestination);
            MessageBox.Show($"{searchMethod} Path: {string.Join(" -> ", path)}\nTotal steps: {path.Count}");
        }

        private List<string> DepthFirstSearch(string start, string goal)
        {
            Stack<string> stack = new Stack<string>();
            HashSet<string> visited = new HashSet<string>();
            Dictionary<string, string> parent = new Dictionary<string, string>();
            stack.Push(start);

            while (stack.Count > 0)
            {
                string node = stack.Pop();
                if (visited.Contains(node)) continue;
                visited.Add(node);

                if (node == goal)
                    return ReconstructPath(parent, start, goal);

                foreach (var neighbor in graph[node])
                {
                    if (!visited.Contains(neighbor))
                    {
                        stack.Push(neighbor);
                        parent[neighbor] = node;
                    }
                }
            }
            return new List<string> { "No Path Found" };
        }

        private List<string> BreadthFirstSearch(string start, string goal)
        {
            Queue<string> queue = new Queue<string>();
            HashSet<string> visited = new HashSet<string>();
            Dictionary<string, string> parent = new Dictionary<string, string>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                string node = queue.Dequeue();
                if (visited.Contains(node)) continue;
                visited.Add(node);

                if (node == goal)
                    return ReconstructPath(parent, start, goal);

                foreach (var neighbor in graph[node])
                {
                    if (!visited.Contains(neighbor))
                    {
                        queue.Enqueue(neighbor);
                        parent[neighbor] = node;
                    }
                }
            }
            return new List<string> { "No Path Found" };
        }

        private List<string> ReconstructPath(Dictionary<string, string> parent, string start, string goal)
        {
            List<string> path = new List<string>();
            string current = goal;
            while (current != start)
            {
                path.Add(current);
                if (!parent.ContainsKey(current)) return new List<string> { "No Path Found" };
                current = parent[current];
            }
            path.Add(start);
            path.Reverse();
            return path;
        }
    }
}
