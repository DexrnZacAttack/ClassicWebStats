/*
 * Copyright (c) 2024 DexrnZacAttack
 * This file is part of ClassicWebStats.
 * https://github.com/DexrnZacAttack/ClassicWebStats
 *
 * Licensed under the MIT License. See LICENSE file for details.
*/

//reference System.dll
//reference System.Runtime.Serialization.dll
//reference System.Core.dll

using System;
using System.Linq;
using System.Net;
using System.Text;
using MCGalaxy;
using MCGalaxy.Config;

namespace ClassicWebStats
{
    public static class Stats
    {
        public static int PlayerCount { get; set; }
        public static JsonArray Players { get; set; }
        public static string Started { get; set; }
        public static string Uptime { get; set; }
        public static string MainMap { get; set; }
    }

    public class CStatsWebServer
    {
        private static HttpListener listener = new System.Net.HttpListener();
        private static bool isRunning = false;

        private static void GetCurrentStats()
        {
            // get players online
            Player fakePlayer = new Player("Test");
            Stats.Players = new JsonArray();
            foreach (var player in PlayerInfo.GetOnlineCanSee(fakePlayer, LevelPermission.Guest).Select(player => player.DisplayName).ToList())
            {
                Stats.Players.Add(player);
            };
            // get player count
            Stats.PlayerCount = PlayerInfo.Online.Count;
            Stats.Started = Server.StartTime.ToUnixTime().ToString();
            TimeSpan uptime = DateTime.UtcNow.Subtract(MCGalaxy.Server.StartTime);
            Stats.Uptime = string.Format("{0:D2}:{1:D2}:{2:D2}", uptime.Hours, uptime.Minutes, uptime.Seconds);
            Stats.MainMap = Server.mainLevel.name;
        }

        public static void StartWebServer()
        {
            string url = "http://localhost:8081/";
            listener.Prefixes.Add(url);
            listener.Start();
            isRunning = true;
            Logger.Log(LogType.Debug, "Listening on {0}", url);
            while (isRunning)
            {
                GetCurrentStats();
                var stats = new JsonObject
                {
                    {  "PlayerCount", Stats.PlayerCount },
                    {  "Players", Stats.Players },
                    {  "Started", Stats.Started },
                    {  "Uptime", Stats.Uptime },
                    {  "MainMapName", Stats.MainMap },
                };

                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;
                response.ContentType = "application/json";

                string json = Json.SerialiseObject(stats);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        }

        public static void StopWebServer()
        {
            if (listener.IsListening)
            {
                isRunning = false;
                listener.Stop();
            }
                
        }

    }

    public sealed class ClassicWebStats : Plugin
    {
        public override string name { get { return "Classic Web Stats"; } }
        public override string creator { get { return "DexrnZacAttack"; } }
        public override string MCGalaxy_Version { get { return "1.0.0.0"; } }

        public override void Load(bool startup)
        {
            CStatsWebServer.StartWebServer();
            Logger.Log(LogType.Debug, "{0} {1} loaded.", name, MCGalaxy_Version);
        }

        public override void Unload(bool shutdown)
        {
            CStatsWebServer.StopWebServer();
            Logger.Log(LogType.Debug, "{0} {1} unloaded.", name, MCGalaxy_Version);
        }
    }
}
