using System;
using System.Collections.Generic;
using System.IO;

namespace Conecting.Core
{
    public class HistoryItem
    {
        public string Id { get; set; }
        public string Hostname { get; set; }
        public string Alias { get; set; }
        public DateTime LastConnected { get; set; }
    }

    /// <summary>
    /// Connection History Storage and Persistence Engine.
    /// Manages recently connected workstations in local JSON format.
    /// </summary>
    public static class ConnectionHistoryManager
    {
        private static readonly string AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ConnectingNodes"
        );
        private static readonly string HistoryFilePath = Path.Combine(AppDataDirectory, "history.json");

        public static List<HistoryItem> GetRecentSessions()
        {
            List<HistoryItem> items = new List<HistoryItem>();
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    string json = File.ReadAllText(HistoryFilePath);
                    items = ParseHistoryJson(json);
                }
            }
            catch { }

            items.Sort((a, b) => b.LastConnected.CompareTo(a.LastConnected));
            return items;
        }

        public static void SaveRecentSession(string nodeId, string hostname)
        {
            if (string.IsNullOrEmpty(nodeId) || nodeId.Length != 9) return;
            try
            {
                List<HistoryItem> items = GetRecentSessions();
                HistoryItem existing = items.Find(x => x.Id == nodeId);
                if (existing != null)
                {
                    existing.LastConnected = DateTime.Now;
                    if (!string.IsNullOrEmpty(hostname)) existing.Hostname = hostname;
                }
                else
                {
                    items.Add(new HistoryItem
                    {
                        Id = nodeId,
                        Hostname = string.IsNullOrEmpty(hostname) ? "PC-REMOTO" : hostname,
                        Alias = "",
                        LastConnected = DateTime.Now
                    });
                }

                if (items.Count > 20)
                {
                    items.RemoveRange(20, items.Count - 20);
                }

                SaveHistoryList(items);
            }
            catch { }
        }

        public static void UpdateAlias(string nodeId, string newAlias)
        {
            try
            {
                List<HistoryItem> items = GetRecentSessions();
                HistoryItem existing = items.Find(x => x.Id == nodeId);
                if (existing != null)
                {
                    existing.Alias = newAlias == null ? "" : newAlias.Trim();
                    SaveHistoryList(items);
                }
            }
            catch { }
        }

        public static void RemoveSession(string nodeId)
        {
            try
            {
                List<HistoryItem> items = GetRecentSessions();
                items.RemoveAll(x => x.Id == nodeId);
                SaveHistoryList(items);
            }
            catch { }
        }

        public static void ClearAll()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    File.Delete(HistoryFilePath);
                }
            }
            catch { }
        }

        private static void SaveHistoryList(List<HistoryItem> items)
        {
            try
            {
                if (!Directory.Exists(AppDataDirectory)) Directory.CreateDirectory(AppDataDirectory);
                string json = SerializeHistoryJson(items);
                File.WriteAllText(HistoryFilePath, json);
            }
            catch { }
        }

        private static string SerializeHistoryJson(List<HistoryItem> items)
        {
            List<string> jsonItems = new List<string>();
            foreach (var item in items)
            {
                string safeAlias = item.Alias == null ? "" : item.Alias.Replace("\"", "\\\"");
                string safeHost = item.Hostname == null ? "PC-REMOTO" : item.Hostname.Replace("\"", "\\\"");
                jsonItems.Add(string.Format("{{\"id\":\"{0}\",\"host\":\"{1}\",\"alias\":\"{2}\",\"date\":\"{3}\"}}", 
                    item.Id, safeHost, safeAlias, item.LastConnected.ToString("o")));
            }
            return "[" + string.Join(",", jsonItems.ToArray()) + "]";
        }

        private static List<HistoryItem> ParseHistoryJson(string json)
        {
            List<HistoryItem> list = new List<HistoryItem>();
            if (string.IsNullOrEmpty(json) || !json.StartsWith("[")) return list;

            try
            {
                string inner = json.Trim('[', ']');
                if (string.IsNullOrEmpty(inner)) return list;

                string[] blocks = inner.Split(new string[] { "},{" }, StringSplitOptions.None);
                foreach (string b in blocks)
                {
                    string clean = b.Trim('{', '}');
                    string[] kvPairs = clean.Split(',');
                    HistoryItem item = new HistoryItem { LastConnected = DateTime.Now, Hostname = "PC-REMOTO", Alias = "" };
                    foreach (string kv in kvPairs)
                    {
                        string[] parts = kv.Split(new char[] { ':' }, 2);
                        if (parts.Length == 2)
                        {
                            string key = parts[0].Trim('"', ' ');
                            string val = parts[1].Trim('"', ' ');
                            if (key == "id") item.Id = val;
                            else if (key == "host") item.Hostname = val;
                            else if (key == "alias") item.Alias = val;
                            else if (key == "date")
                            {
                                DateTime dt;
                                if (DateTime.TryParse(val, out dt)) item.LastConnected = dt;
                            }
                        }
                    }
                    if (!string.IsNullOrEmpty(item.Id)) list.Add(item);
                }
            }
            catch { }

            return list;
        }
    }
}
