﻿using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Globalization;
using System.IO;
using Discord;
using Discord.WebSocket;
using PoE.Bot.Attributes;
using PoE.Bot.Commands;
using PoE.Bot.Commands.Permissions;
using CsvHelper;
using CsvHelper.Configuration;

namespace PoE.Bot.Plugin.Leaderboard
{
    public class LeaderboardCommands : ICommandModule
    {
        public string Name { get { return "PoE.Bot.Plugin.Leaderboard Module"; } }

        [Command("addleaderboard", "Adds an Leaderboard variant to a specified channel.", CheckerId = "CoreAdminChecker", CheckPermissions = true, RequiredPermission = Permission.Administrator)]
        public async Task AddRss(CommandContext ctx,
            [ArgumentParameter("Mention of the channel to add the Leaderboard to.", true)] ITextChannel channel,
            [ArgumentParameter("Leaderboard variant.", true)] string variant,
            [ArgumentParameter("Set if the Leaderboard is enabled for the variant.", true)] bool enabled)
        {
            var chf = channel as SocketTextChannel;
            if (chf == null)
                throw new ArgumentException("Invalid channel specified.");
            if (string.IsNullOrEmpty(variant))
                throw new ArgumentException("No Variant specified.");

            variant = WebUtility.UrlEncode(variant);

            LeaderboardPlugin.Instance.AddLeaderboard(variant, chf.Id, enabled);
            var embed = this.PrepareEmbed("Success", "Leaderboard was added successfully.", EmbedType.Success);
            embed.AddField("Details", string.Concat("Leaderboard pointing to <", variant, ">", " was added to ", chf.Mention, " and is ", (enabled ? "" : "not "), "Enabled."))
                .WithAuthor(ctx.User)
                .WithThumbnailUrl(string.IsNullOrEmpty(ctx.User.GetAvatarUrl()) ? ctx.User.GetDefaultAvatarUrl() : ctx.User.GetAvatarUrl());

            await ctx.Channel.SendMessageAsync("", false, embed.Build());
        }

        [Command("rmleaderboard", "Removes an Leaderboard variant from a specified channel.", CheckerId = "CoreAdminChecker", CheckPermissions = true, RequiredPermission = Permission.Administrator)]
        public async Task RemoveRss(CommandContext ctx,
            [ArgumentParameter("Mention of the channel to remove the Leaderboard from.", true)] ITextChannel channel,
            [ArgumentParameter("Leaderboard variant.", true)] string variant)
        {
            var chf = channel as SocketTextChannel;
            if (chf == null)
                throw new ArgumentException("Invalid channel specified.");
            if (string.IsNullOrEmpty(variant))
                throw new ArgumentException("No Variant specified.");

            variant = WebUtility.UrlEncode(variant);

            LeaderboardPlugin.Instance.RemoveLeaderboard(variant, chf.Id);
            var embed = this.PrepareEmbed("Success", "Leaderboard was removed successfully.", EmbedType.Success);
            embed.AddField("Details", string.Concat("Leaderboard pointing to <", variant, ">", " was removed from ", chf.Mention, "."))
               .WithAuthor(ctx.User)
               .WithThumbnailUrl(string.IsNullOrEmpty(ctx.User.GetAvatarUrl()) ? ctx.User.GetDefaultAvatarUrl() : ctx.User.GetAvatarUrl());

            await ctx.Channel.SendMessageAsync("", false, embed.Build());
        }

        [Command("listleaderboards", "Lists Leaderboards active in the current guild.", CheckerId = "CoreAdminChecker", CheckPermissions = true, RequiredPermission = Permission.Administrator)]
        public async Task ListRss(CommandContext ctx)
        {
            var gld = ctx.Guild as SocketGuild;
            var lbs = LeaderboardPlugin.Instance.GetLeaderboards(gld.Channels.Select(xch => xch.Id).ToArray());

            var sb = new StringBuilder();
            foreach (var lb in lbs)
            {
                var xch = gld.GetChannel(lb.ChannelId) as SocketTextChannel;
                var roles = new StringBuilder();

                sb.Append("```");
                sb.AppendFormat("Variant: {0}\nChannel: #{1}\nEnabled: {2}", WebUtility.UrlDecode(lb.Variant), xch.Name, lb.Enabled).AppendLine();
                sb.Append("```");

                sb.AppendLine("---------");
            }

            var embed = this.PrepareEmbed(EmbedType.Info);
            embed.AddField("Leaderboards", sb.ToString());
            await ctx.Channel.SendMessageAsync("", false, embed.Build());
        }

        [Command("getleaderboard", "Gets and posts all active Leaderboards, only doing the first index.", CheckerId = "CoreAdminChecker", CheckPermissions = true, RequiredPermission = Permission.AddReactions)]
        public async Task GetLeaderboard(CommandContext ctx)
        {
            var baseURL = "https://www.pathofexile.com/public/ladder/Path_of_Exile_Xbox_{0}_league_export.csv";
            var gld = ctx.Guild as SocketGuild;
            var lbs = LeaderboardPlugin.Instance.GetLeaderboards(gld.Channels.Select(xch => xch.Id).ToArray());

            foreach(var lb in lbs)
            {
                if (lb.Enabled)
                {
                    var fullURL = string.Format(baseURL, lb.Variant);
                    List<LeaderboardData> racers = new List<LeaderboardData>();

                    using (HttpClient client = new HttpClient())
                    {
                        using (HttpResponseMessage response = client.GetAsync(fullURL, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                        {
                            if (response.IsSuccessStatusCode)
                            {
                                using (Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                                {
                                    using (TextReader reader = new StreamReader(stream))
                                    {
                                        using (CsvReader csv = new CsvReader(reader))
                                        {
                                            csv.Configuration.RegisterClassMap<LeaderboardDataMap>();
                                            await csv.ReadAsync();
                                            csv.ReadHeader();

                                            while (await csv.ReadAsync())
                                                racers.Add(csv.GetRecord<LeaderboardData>());
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (racers.Count > 0)
                    {
                        var sb = new StringBuilder();
                        var rSlayers = racers.FindAll(x => x.Class == AscendancyClass.Slayer);
                        var rGladiators = racers.FindAll(x => x.Class == AscendancyClass.Gladiator);
                        var rChampions = racers.FindAll(x => x.Class == AscendancyClass.Champion);
                        var rAssassins = racers.FindAll(x => x.Class == AscendancyClass.Assassin);
                        var rSaboteurs = racers.FindAll(x => x.Class == AscendancyClass.Saboteur);
                        var rTricksters = racers.FindAll(x => x.Class == AscendancyClass.Trickster);
                        var rJuggernauts = racers.FindAll(x => x.Class == AscendancyClass.Juggernaut);
                        var rBerserkers = racers.FindAll(x => x.Class == AscendancyClass.Berserker);
                        var rChieftains = racers.FindAll(x => x.Class == AscendancyClass.Chieftain);
                        var rNecromancers = racers.FindAll(x => x.Class == AscendancyClass.Necromancer);
                        var rElementalists = racers.FindAll(x => x.Class == AscendancyClass.Elementalist);
                        var rOccultists = racers.FindAll(x => x.Class == AscendancyClass.Occultist);
                        var rDeadeyes = racers.FindAll(x => x.Class == AscendancyClass.Deadeye);
                        var rRaiders = racers.FindAll(x => x.Class == AscendancyClass.Raider);
                        var rPathfinders = racers.FindAll(x => x.Class == AscendancyClass.Pathfinder);
                        var rInquisitors = racers.FindAll(x => x.Class == AscendancyClass.Inquisitor);
                        var rHierophants = racers.FindAll(x => x.Class == AscendancyClass.Hierophant);
                        var rGuardians = racers.FindAll(x => x.Class == AscendancyClass.Guardian);
                        var rAscendants = racers.FindAll(x => x.Class == AscendancyClass.Ascendant);
                        var rDuelists = racers.FindAll(x => x.Class == AscendancyClass.Duelist);
                        var rShadows = racers.FindAll(x => x.Class == AscendancyClass.Shadow);
                        var rMarauders = racers.FindAll(x => x.Class == AscendancyClass.Marauder);
                        var rWitchs = racers.FindAll(x => x.Class == AscendancyClass.Witch);
                        var rRangers = racers.FindAll(x => x.Class == AscendancyClass.Ranger);
                        var rTemplars = racers.FindAll(x => x.Class == AscendancyClass.Templar);
                        var rScions = racers.FindAll(x => x.Class == AscendancyClass.Scion);

                        if (rSlayers.Count() > 0)
                            sb.AppendLine($"Slayers      : {rSlayers.Count().ToString("##,##0")}");
                        if (rGladiators.Count() > 0)
                            sb.AppendLine($"Gladiators   : {rGladiators.Count().ToString("##,##0")}");
                        if (rChampions.Count() > 0)
                            sb.AppendLine($"Champions    : {rChampions.Count().ToString("##,##0")}");
                        if (rAssassins.Count() > 0)
                            sb.AppendLine($"Assassins    : {rAssassins.Count().ToString("##,##0")}");
                        if (rSaboteurs.Count() > 0)
                            sb.AppendLine($"Saboteurs    : {rSaboteurs.Count().ToString("##,##0")}");
                        if (rTricksters.Count() > 0)
                            sb.AppendLine($"Tricksters   : {rTricksters.Count().ToString("##,##0")}");
                        if (rJuggernauts.Count() > 0)
                            sb.AppendLine($"Juggernauts  : {rJuggernauts.Count().ToString("##,##0")}");
                        if (rBerserkers.Count() > 0)
                            sb.AppendLine($"Berserkers   : {rBerserkers.Count().ToString("##,##0")}");
                        if (rChieftains.Count() > 0)
                            sb.AppendLine($"Chieftains   : {rChieftains.Count().ToString("##,##0")}");
                        if (rNecromancers.Count() > 0)
                            sb.AppendLine($"Necromancers : {rNecromancers.Count().ToString("##,##0")}");
                        if (rElementalists.Count() > 0)
                            sb.AppendLine($"Elementalists: {rElementalists.Count().ToString("##,##0")}");
                        if (rOccultists.Count() > 0)
                            sb.AppendLine($"Occultists   : {rOccultists.Count().ToString("##,##0")}");
                        if (rDeadeyes.Count() > 0)
                            sb.AppendLine($"Deadeyes     : {rDeadeyes.Count().ToString("##,##0")}");
                        if (rRaiders.Count() > 0)
                            sb.AppendLine($"Raiders      : {rRaiders.Count().ToString("##,##0")}");
                        if (rPathfinders.Count() > 0)
                            sb.AppendLine($"Pathfinders  : {rPathfinders.Count().ToString("##,##0")}");
                        if (rInquisitors.Count() > 0)
                            sb.AppendLine($"Inquisitors  : {rInquisitors.Count().ToString("##,##0")}");
                        if (rHierophants.Count() > 0)
                            sb.AppendLine($"Hierophants  : {rHierophants.Count().ToString("##,##0")}");
                        if (rGuardians.Count() > 0)
                            sb.AppendLine($"Guardians    : {rGuardians.Count().ToString("##,##0")}");
                        if (rAscendants.Count() > 0)
                            sb.AppendLine($"Ascendants   : {rAscendants.Count().ToString("##,##0")}");
                        if (rDuelists.Count() > 0)
                            sb.AppendLine($"Duelists     : {rDuelists.Count().ToString("##,##0")}");
                        if (rShadows.Count() > 0)
                            sb.AppendLine($"Shadows      : {rShadows.Count().ToString("##,##0")}");
                        if (rMarauders.Count() > 0)
                            sb.AppendLine($"Marauders    : {rMarauders.Count().ToString("##,##0")}");
                        if (rWitchs.Count() > 0)
                            sb.AppendLine($"Witchs       : {rWitchs.Count().ToString("##,##0")}");
                        if (rRangers.Count() > 0)
                            sb.AppendLine($"Rangers      : {rRangers.Count().ToString("##,##0")}");
                        if (rTemplars.Count() > 0)
                            sb.AppendLine($"Templars     : {rTemplars.Count().ToString("##,##0")}");
                        if (rScions.Count() > 0)
                            sb.AppendLine($"Scions       : {rScions.Count().ToString("##,##0")}");

                        var embed = new EmbedBuilder();
                        embed.WithTitle($"{WebUtility.UrlDecode(lb.Variant).Replace("_", " ")} Leaderboard")
                            .WithDescription($"Retrieved {racers.Count().ToString("##,##0")} records, Rank is overall and not by Ascendancy, below is the total of Ascendancy classes:\n```{sb.ToString()}```")
                            .WithColor(new Color(0, 127, 255))
                            .WithCurrentTimestamp();

                        embed.AddField("Top 10 Characters of each Class Ascendancy", "Rank is overall and not by Ascendancy.");

                        var cDuelists = racers.FindAll(x => x.Class == AscendancyClass.Duelist || x.Class == AscendancyClass.Slayer || x.Class == AscendancyClass.Gladiator || x.Class == AscendancyClass.Champion);
                        var cShadows = racers.FindAll(x => x.Class == AscendancyClass.Shadow || x.Class == AscendancyClass.Saboteur || x.Class == AscendancyClass.Assassin || x.Class == AscendancyClass.Trickster);
                        var cMarauders = racers.FindAll(x => x.Class == AscendancyClass.Marauder || x.Class == AscendancyClass.Juggernaut || x.Class == AscendancyClass.Chieftain || x.Class == AscendancyClass.Berserker);
                        var cWitchs = racers.FindAll(x => x.Class == AscendancyClass.Witch || x.Class == AscendancyClass.Necromancer || x.Class == AscendancyClass.Occultist || x.Class == AscendancyClass.Elementalist);
                        var cRangers = racers.FindAll(x => x.Class == AscendancyClass.Ranger || x.Class == AscendancyClass.Raider || x.Class == AscendancyClass.Deadeye || x.Class == AscendancyClass.Pathfinder);
                        var cTemplars = racers.FindAll(x => x.Class == AscendancyClass.Templar || x.Class == AscendancyClass.Inquisitor || x.Class == AscendancyClass.Hierophant || x.Class == AscendancyClass.Guardian);
                        var cScions = racers.FindAll(x => x.Class == AscendancyClass.Scion || x.Class == AscendancyClass.Ascendant);

                        cDuelists.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        cShadows.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        cMarauders.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        cWitchs.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        cRangers.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        cTemplars.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        cScions.Sort((p, q) => p.Rank.CompareTo(q.Rank));

                        if (cDuelists.Count > 0)
                        {
                            var tDuelists = cDuelists.GetRange(0, (cDuelists.Count() < 10 ? cDuelists.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tDuelists)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | {racer.Class.ToString(),14}{(racer.Dead ? " | X" : null)}");
                            embed.AddField("Duelists, Slayers, Champions, Gladiators", $"```{sb.ToString()}```");
                        }

                        if (cShadows.Count > 0)
                        {
                            var tShadows = cShadows.GetRange(0, (cShadows.Count() < 10 ? cShadows.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tShadows)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | {racer.Class.ToString(),14}{(racer.Dead ? " | X" : null)}");
                            embed.AddField("Shadows, Saboteurs, Assassins, Tricksters", $"```{sb.ToString()}```");
                        }

                        if (cMarauders.Count > 0)
                        {
                            var tMarauders = cMarauders.GetRange(0, (cMarauders.Count() < 10 ? cMarauders.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tMarauders)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | {racer.Class.ToString(),14}{(racer.Dead ? " | X" : null)}");
                            embed.AddField("Marauders, Juggernauts, Chieftains, Berserkers", $"```{sb.ToString()}```");
                        }

                        if (cWitchs.Count > 0)
                        {
                            var tWitchs = cWitchs.GetRange(0, (cWitchs.Count() < 10 ? cWitchs.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tWitchs)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | {racer.Class.ToString(),14}{(racer.Dead ? " | X" : null)}");
                            embed.AddField("Witches, Necromancers, Occultists, Elemantalists", $"```{sb.ToString()}```");
                        }

                        if (cRangers.Count > 0)
                        {
                            var tRangers = cRangers.GetRange(0, (cRangers.Count() < 10 ? cRangers.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tRangers)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | {racer.Class.ToString(),14}{(racer.Dead ? " | X" : null)}");
                            embed.AddField("Rangers, Pathfinders, Raiders, Deadeyes", $"```{sb.ToString()}```");
                        }

                        if (cTemplars.Count > 0)
                        {
                            var tTemplars = cTemplars.GetRange(0, (cTemplars.Count() < 10 ? cTemplars.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tTemplars)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | {racer.Class.ToString(),14}{(racer.Dead ? " | X" : null)}");
                            embed.AddField("Templars, Guardians, Inquisitors, Hierophants", $"```{sb.ToString()}```");
                        }

                        if (cScions.Count > 0)
                        {
                            var tScions = cScions.GetRange(0, (cScions.Count() < 10 ? cScions.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tScions)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | {racer.Class.ToString(),14}{(racer.Dead ? " | X" : null)}");
                            embed.AddField("Scions, Ascendants", $"```{sb.ToString()}```");
                        }

                        var embedClasses = new EmbedBuilder();
                        embedClasses.WithTitle("Top 10 Characters of each Class")
                            .WithDescription("Rank is overall and not by Class.")
                            .WithColor(new Color(0, 127, 255))
                            .WithCurrentTimestamp();

                        rDuelists.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rShadows.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rMarauders.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rWitchs.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rRangers.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rTemplars.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rScions.Sort((p, q) => p.Rank.CompareTo(q.Rank));

                        if (rDuelists.Count > 0)
                        {
                            var tDuelists = rDuelists.GetRange(0, (rDuelists.Count() < 10 ? rDuelists.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tDuelists)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedClasses.AddField("Duelists", $"```{sb.ToString()}```");
                        }

                        if (rShadows.Count > 0)
                        {
                            var tShadows = rShadows.GetRange(0, (rShadows.Count() < 10 ? rShadows.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tShadows)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedClasses.AddField("Shadows", $"```{sb.ToString()}```");
                        }

                        if (rMarauders.Count > 0)
                        {
                            var tMarauders = rMarauders.GetRange(0, (rMarauders.Count() < 10 ? rMarauders.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tMarauders)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedClasses.AddField("Marauders", $"```{sb.ToString()}```");
                        }

                        if (rWitchs.Count > 0)
                        {
                            var tWitchs = rWitchs.GetRange(0, (rWitchs.Count() < 10 ? rWitchs.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tWitchs)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedClasses.AddField("Witches", $"```{sb.ToString()}```");
                        }

                        if (rRangers.Count > 0)
                        {
                            var tRangers = rRangers.GetRange(0, (rRangers.Count() < 10 ? rRangers.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tRangers)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedClasses.AddField("Rangers", $"```{sb.ToString()}```");
                        }

                        if (rTemplars.Count > 0)
                        {
                            var tTemplars = rTemplars.GetRange(0, (rTemplars.Count() < 10 ? rTemplars.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tTemplars)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedClasses.AddField("Templars", $"```{sb.ToString()}```");
                        }

                        if (rScions.Count > 0)
                        {
                            var tScions = rScions.GetRange(0, (rScions.Count() < 10 ? rScions.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tScions)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedClasses.AddField("Scions", $"```{sb.ToString()}```");
                        }

                        var embedAscendancy = new EmbedBuilder();
                        embedAscendancy.WithTitle("Top 10 Characters of each Ascendancy")
                            .WithDescription("Rank is overall and not by Ascendancy.")
                            .WithColor(new Color(0, 127, 255))
                            .WithCurrentTimestamp();

                        rSlayers.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rGladiators.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rChampions.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rAssassins.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rSaboteurs.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rTricksters.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rJuggernauts.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rBerserkers.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rChieftains.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rNecromancers.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rElementalists.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rOccultists.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rDeadeyes.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rRaiders.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rPathfinders.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rInquisitors.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rHierophants.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rGuardians.Sort((p, q) => p.Rank.CompareTo(q.Rank));
                        rAscendants.Sort((p, q) => p.Rank.CompareTo(q.Rank));

                        if (rSlayers.Count > 0)
                        {
                            var tSlayers = rSlayers.GetRange(0, (rSlayers.Count() < 10 ? rSlayers.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tSlayers)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancy.AddField("Slayers", $"```{sb.ToString()}```");
                        }

                        if (rChampions.Count > 0)
                        {
                            var tChampions = rChampions.GetRange(0, (rChampions.Count() < 10 ? rChampions.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tChampions)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancy.AddField("Champions", $"```{sb.ToString()}```");
                        }

                        if (rGladiators.Count > 0)
                        {
                            var tGladiators = rGladiators.GetRange(0, (rGladiators.Count() < 10 ? rGladiators.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tGladiators)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancy.AddField("Gladiators", $"```{sb.ToString()}```");
                        }

                        if (rAssassins.Count > 0)
                        {
                            var tAssassins = rAssassins.GetRange(0, (rAssassins.Count() < 10 ? rAssassins.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tAssassins)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancy.AddField("Assassins", $"```{sb.ToString()}```");
                        }

                        if (rSaboteurs.Count > 0)
                        {
                            var tSaboteurs = rSaboteurs.GetRange(0, (rSaboteurs.Count() < 10 ? rSaboteurs.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tSaboteurs)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancy.AddField("Saboteurs", $"```{sb.ToString()}```");
                        }

                        if (rTricksters.Count > 0)
                        {
                            var tTricksters = rTricksters.GetRange(0, (rTricksters.Count() < 10 ? rTricksters.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tTricksters)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancy.AddField("Tricksters", $"```{sb.ToString()}```");
                        }

                        if (rJuggernauts.Count > 0)
                        {
                            var tJuggernauts = rJuggernauts.GetRange(0, (rJuggernauts.Count() < 10 ? rJuggernauts.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tJuggernauts)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancy.AddField("Juggernauts", $"```{sb.ToString()}```");
                        }

                        if (rBerserkers.Count > 0)
                        {
                            var tBerserkers = rBerserkers.GetRange(0, (rBerserkers.Count() < 10 ? rBerserkers.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tBerserkers)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancy.AddField("Berserkers", $"```{sb.ToString()}```");
                        }

                        if (rChieftains.Count > 0)
                        {
                            var tChieftains = rChieftains.GetRange(0, (rChieftains.Count() < 10 ? rChieftains.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tChieftains)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancy.AddField("Chieftains", $"```{sb.ToString()}```");
                        }

                        var embedAscendancyCont = new EmbedBuilder();
                        embedAscendancyCont.WithTitle("Top 10 Characters of each Ascendancy")
                            .WithDescription("Rank is overall and not by Ascendancy.")
                            .WithColor(new Color(0, 127, 255))
                            .WithCurrentTimestamp();

                        if (rNecromancers.Count > 0)
                        {
                            var tNecromancers = rNecromancers.GetRange(0, (rNecromancers.Count() < 10 ? rNecromancers.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tNecromancers)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Necromancers", $"```{sb.ToString()}```");
                        }

                        if (rElementalists.Count > 0)
                        {
                            var tElementalists = rElementalists.GetRange(0, (rElementalists.Count() < 10 ? rElementalists.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tElementalists)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Elemantalists", $"```{sb.ToString()}```");
                        }

                        if (rOccultists.Count > 0)
                        {
                            var tOccultists = rOccultists.GetRange(0, (rOccultists.Count() < 10 ? rOccultists.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tOccultists)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Occultists", $"```{sb.ToString()}```");
                        }

                        if (rDeadeyes.Count > 0)
                        {
                            var tDeadeyes = rDeadeyes.GetRange(0, (rDeadeyes.Count() < 10 ? rDeadeyes.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tDeadeyes)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Deadeyes", $"```{sb.ToString()}```");
                        }

                        if (rRaiders.Count > 0)
                        {
                            var tRaiders = rRaiders.GetRange(0, (rRaiders.Count() < 10 ? rRaiders.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tRaiders)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Raiders", $"```{sb.ToString()}```");
                        }

                        if (rPathfinders.Count > 0)
                        {
                            var tPathfinders = rPathfinders.GetRange(0, (rPathfinders.Count() < 10 ? rPathfinders.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tPathfinders)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Pathfinders", $"```{sb.ToString()}```");
                        }

                        if (rInquisitors.Count > 0)
                        {
                            var tInquisitors = rInquisitors.GetRange(0, (rInquisitors.Count() < 10 ? rInquisitors.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tInquisitors)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Inquisitors", $"```{sb.ToString()}```");
                        }

                        if (rHierophants.Count > 0)
                        {
                            var tHierophants = rHierophants.GetRange(0, (rHierophants.Count() < 10 ? rHierophants.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tHierophants)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Hierophants", $"```{sb.ToString()}```");
                        }

                        if (rGuardians.Count > 0)
                        {
                            var tGuardians = rGuardians.GetRange(0, (rGuardians.Count() < 10 ? rGuardians.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tGuardians)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Guardians", $"```{sb.ToString()}```");
                        }

                        if (rAscendants.Count > 0)
                        {
                            var tAscendants = rAscendants.GetRange(0, (rAscendants.Count() < 10 ? rAscendants.Count() : 10));
                            sb = new StringBuilder();
                            foreach (var racer in tAscendants)
                                sb.AppendLine($"{racer.Character.PadRight(24)}R:{racer.Rank,5} | L:{racer.Level,3} | E:{racer.Experience,10}{(racer.Dead ? " | X" : null)}");
                            embedAscendancyCont.AddField("Ascendants", $"```{sb.ToString()}```");
                        }

                        IMessageChannel chan = gld.GetChannel(lb.ChannelId) as IMessageChannel;
                        await chan.SendMessageAsync("", false, embed.Build());

                        if (embedClasses.Fields.Count > 0)
                            await chan.SendMessageAsync("", false, embedClasses.Build());

                        if (embedAscendancy.Fields.Count > 0)
                            await chan.SendMessageAsync("", false, embedAscendancy.Build());

                        if (embedAscendancyCont.Fields.Count > 0)
                            await chan.SendMessageAsync("", false, embedAscendancyCont.Build());
                    }
                }
            }
        }

        private EmbedBuilder PrepareEmbed(EmbedType type)
        {
            var embed = new EmbedBuilder();
            embed.WithCurrentTimestamp();
            switch (type)
            {
                case EmbedType.Info:
                    embed.Color = new Color(0, 127, 255);
                    break;

                case EmbedType.Success:
                    embed.Color = new Color(127, 255, 0);
                    break;

                case EmbedType.Warning:
                    embed.Color = new Color(255, 255, 0);
                    break;

                case EmbedType.Error:
                    embed.Color = new Color(255, 127, 0);
                    break;

                default:
                    embed.Color = new Color(255, 255, 255);
                    break;
            }
            return embed;
        }

        private EmbedBuilder PrepareEmbed(string title, string desc, EmbedType type)
        {
            var embed = this.PrepareEmbed(type);
            embed.Title = title;
            embed.Description = desc;
            embed.WithCurrentTimestamp();
            return embed;
        }

        private enum EmbedType : uint
        {
            Unknown,
            Success,
            Error,
            Warning,
            Info
        }
    }
}
