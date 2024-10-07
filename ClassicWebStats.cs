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

using MCGalaxy;
using MCGalaxy.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ClassicWebStats
{
    public static class Stats
    {
        public static int PlayerCount { get; set; }
        public static JsonArray Players { get; set; }
        public static string Started { get; set; }
        public static string Uptime { get; set; }
        public static JsonObject MainMap { get; set; }
    }

    public class CStatsWebServer
    {
        private static readonly HttpListener listener = new HttpListener();
        public static string pluginName = "";
        public static string pluginCreator = "";
        public static string pluginVersion = "";

        private static JsonArray GetPlayersStats(List<Player> players)
        {
            JsonArray playerArray = new JsonArray();
            foreach (var player in players)
            {
                TimeSpan joinTime = DateTime.UtcNow.Subtract(player.SessionStartTime);
                playerArray.Add(new JsonObject {
                    { "Name", new JsonObject {
                        { "Name", player.name },
                        { "FullName", player.FullName },
                        { "DisplayName", player.DisplayName },
                        { "ColoredDisplayName", player.ColoredName },
                        { "Prefix", player.prefix },
                        { "Title", player.title },
                        { "TitleColor", player.titlecolor }
                    }},
                    { "AFK", player.IsAfk },
                    { "LastActionTime", player.LastAction.ToUnixTime().ToString() },
                    { "Group", player.group.Name },
                    { "GroupColored", player.group.ColoredName },
                    { "Rank", player.Rank.ToString() },
                    { "Client", player.Session.ClientName() },
                    { "Skin", player.SkinName },
                    { "JoinTime", player.SessionStartTime.ToUnixTime().ToString() },
                    { "OnlineTime", string.Format("{0:D2}:{1:D2}:{2:D2}", joinTime.Hours, joinTime.Minutes, joinTime.Seconds) },
                    { "Map", player.level.name },
                    { "Coordinates", new JsonObject {
                        { "Block", new JsonObject {
                            { "X", player.Pos.FeetBlockCoords.X },
                            { "Y", player.Pos.FeetBlockCoords.Y },
                            { "Z", player.Pos.FeetBlockCoords.Z }
                        }},
                        { "Unit", new JsonObject {
                            { "X", player.Pos.X },
                            { "Y", player.Pos.Y },
                            { "Z", player.Pos.Z }
                        }}
                    }},
                    { "HeldBlock", new JsonObject {
                        { "Name", Block.GetName(player, player.ClientHeldBlock) },
                        { "Id", (int)player.ClientHeldBlock }
                    }},
                });
            };
            return playerArray;
        }

        private static void GetCurrentStats()
        {
            // get players online
            Player fakePlayer = new Player("Test");
            Stats.Players = GetPlayersStats(PlayerInfo.GetOnlineCanSee(fakePlayer, LevelPermission.Guest).ToList());
            // get player count
            Stats.PlayerCount = PlayerInfo.Online.Count;
            Stats.Started = Server.StartTime.ToUnixTime().ToString();
            TimeSpan uptime = DateTime.UtcNow.Subtract(Server.StartTime);
            Stats.Uptime = string.Format("{0:D2}:{1:D2}:{2:D2}", uptime.Hours, uptime.Minutes, uptime.Seconds);
            Stats.MainMap = new JsonObject
            {
                { "Name", Server.mainLevel.name },
                { "Players", GetPlayersStats(Server.mainLevel.players) },
                { "Bounds", new JsonObject {
                    { "Width", (int)Server.mainLevel.Width },
                    { "Length", (int)Server.mainLevel.Length },
                    { "Height", (int)Server.mainLevel.Height }
                }},
                { "SpawnPos", new JsonObject {
                    { "Block", new JsonObject {
                        { "X", Server.mainLevel.SpawnPos.FeetBlockCoords.X },
                        { "Y", Server.mainLevel.SpawnPos.FeetBlockCoords.Y },
                        { "Z", Server.mainLevel.SpawnPos.FeetBlockCoords.Z }
                    }},
                    { "Unit", new JsonObject {
                        { "X", Server.mainLevel.SpawnPos.X },
                        { "Y", Server.mainLevel.SpawnPos.Y },
                        { "Z", Server.mainLevel.SpawnPos.Z }
                    }}
                }}
            };
        }

        public static void StartWebServer()
        {
            // TODO: NEEDS CLEANUP
            string url = "http://localhost:8081/";
            listener.Prefixes.Add(url);
            listener.Start();
            Logger.Log(LogType.Debug, "Listening on {0}", url);
            Task.Run(() =>
            {
                while (listener.IsListening)
                {

                    HttpListenerContext context = listener.GetContext();
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;
                    response.Headers.Add("Server", pluginName + " " + pluginVersion + " on " + Server.SoftwareNameVersioned + ";");


                    if (request.Url.AbsolutePath.StartsWith("/level/"))
                    {
                        string rem = request.Url.AbsolutePath.Substring("/level/".Length);
                        if (rem.Contains("/map"))
                        {
                            string mapName = rem.Substring(0, rem.IndexOf("/map"));
                            if (!LevelInfo.MapExists(mapName))
                            {
                                var error = new JsonObject
                                {
                                    {  "Error", "Map does not exist." },
                                };
                                var json = Encoding.UTF8.GetBytes(Json.SerialiseObject(error));

                                response.ContentType = "application/json";
                                response.ContentLength64 = json.Length;
                                response.OutputStream.Write(json, 0, json.Length);
                                response.OutputStream.Close();
                                continue;
                            }

                            byte[] buffer = ClassicMap.GetTopBlocks(Level.Load(mapName));

                            response.ContentType = "application/octet-stream";
                            response.AddHeader("Content-Disposition", "attachment; filename=\"" + mapName + "_map.dat\"");
                            response.ContentLength64 = buffer.Length;
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                            response.OutputStream.Close();
                        }
                        else if (rem.Contains("/bounds"))
                        {
                            string mapName = rem.Substring(0, rem.IndexOf("/bounds"));
                            if (!LevelInfo.MapExists(mapName))
                            {
                                var error = new JsonObject
                                {
                                    {  "Error", "Map does not exist." },
                                };
                                var errorJson = Encoding.UTF8.GetBytes(Json.SerialiseObject(error));

                                response.ContentType = "application/json";
                                response.ContentLength64 = errorJson.Length;
                                response.OutputStream.Write(errorJson, 0, errorJson.Length);
                                response.OutputStream.Close();
                                continue;
                            }

                            var bounds = ClassicMap.GetBounds(Level.Load(mapName));
                            var msg = new JsonObject
                            {
                                { "Width", (int)bounds["Width"] },
                                { "Length", (int)bounds["Length"] },
                                { "Height", (int)bounds["Height"] }
                            };

                            var boundsJson = Encoding.UTF8.GetBytes(Json.SerialiseObject(msg));
                            response.ContentType = "application/json";
                            response.ContentLength64 = boundsJson.Length;
                            response.OutputStream.Write(boundsJson, 0, boundsJson.Length);
                            response.OutputStream.Close();
                        } else
                        {
                            Logger.Log(LogType.UserActivity, "Unknown page:", request.Url.AbsolutePath);
                        }

                    }
                    else
                    {
                        GetCurrentStats();
                        var stats = new JsonObject
                        {
                            {  "PlayerCount", Stats.PlayerCount },
                            {  "Started", Stats.Started },
                            {  "Uptime", Stats.Uptime },
                            {  "Players", Stats.Players },
                            {  "MainMap", Stats.MainMap },
                        };
                        string json = Json.SerialiseObject(stats);
                        byte[] buffer = Encoding.UTF8.GetBytes(json);

                        response.ContentType = "application/json";
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                        response.OutputStream.Close();
                    }

                }
            });
        }

        public static void StopWebServer()
        {
            if (listener.IsListening)
            {
                listener.Stop();
                listener.Close();
            }
        }

    }

    public static class ClassicMap
    {
        // modified version of danilwhale's code from when we were testing map image gen
        public static byte[] GetTopBlocks(Level level)
        {
            var blocks = new byte[level.Width * level.Length];

            for (var x = (ushort)0; x < level.Width; x++)
            {
                for (var z = (ushort)0; z < level.Length; z++)
                {
                    for (var y = level.Height - 1; y >= 0; y--)
                    {
                        var block = Block.ToRaw(level.GetBlock(x, (ushort)y, z));
                        if (block == Block.Air) continue;
                        blocks[x + Server.mainLevel.Width * z] = (byte)block;
                        break;
                    }
                }
            }
            return blocks;
        }

        public static Dictionary<string, ushort> GetBounds(Level level)
        {
            return new Dictionary<string, ushort>
            {
                { "Width", level.Width },
                { "Length", level.Length },
                { "Height", level.Height }
            };
        }
    }

    public sealed class ClassicWebStats : Plugin
    {
        public override string name { get { return "Classic Web Stats"; } }
        public override string creator { get { return "DexrnZacAttack"; } }
        public override string MCGalaxy_Version { get { return "1.2.0.0"; } }

        public override void Load(bool startup)
        {
            CStatsWebServer.pluginName = name;
            CStatsWebServer.pluginCreator = creator;
            CStatsWebServer.pluginVersion = MCGalaxy_Version;
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
