﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using TriggersTools.DiscordBots.Modules;
using TriggersTools.DiscordBots.Services;
using TriggersTools.DiscordBots.TriggerChan.Services;
using TriggersTools.DiscordBots.Utils;
using TriggersTools.DiscordBots.Commands;
using TriggersTools.DiscordBots.Reactions;
using TriggersTools.DiscordBots.Extensions;
using TriggersTools.DiscordBots.SpoilerBot.Services;
using TriggersTools.DiscordBots.TriggerChan.Commands;
using System.IO;
using TriggersTools.DiscordBots.TriggerChan.Database;
using Microsoft.EntityFrameworkCore;
using System.Runtime.InteropServices;
using System.Reflection;
using TriggersTools.Build;

namespace TriggersTools.DiscordBots.TriggerChan.Modules {
	[Name("Help")]
	[Summary("Commands for help with using the bot")]
	[AllowBots(false)]
	[IsLockable(false)]
	public class HelpModule : TriggerModule {

		private readonly ISpoilerService spoilers;
		private readonly ReactionService reactions;
		private readonly EmotePreviewService emotePreview;
		private readonly HelpService help;
		private readonly ConfigParserService configParser;

		public HelpModule(TriggerServiceContainer services,
						  ISpoilerService spoilers,
						  ReactionService reactions,
						  EmotePreviewService emotePreview,
						  HelpService help,
						  ConfigParserService configParser)
			: base(services)
		{
			this.spoilers = spoilers;
			this.reactions = reactions;
			this.emotePreview = emotePreview;
			this.help = help;
			this.configParser = configParser;
		}

		[Name("help")]
		[Group("help"), Alias("h", "?")]
		[Usage("[commandName|module <moduleName>]")]
		[Summary("Display information about the bot's commands and modules")]
		public class HelpGroup : DiscordBotModule {

			private readonly HelpService help;

			public HelpGroup(DiscordBotServiceContainer services,
							 HelpService help)
				: base(services)
			{
				this.help = help;
			}

			[Name("help")]
			[Command("")]
			[Priority(0)]
			[Example("Show a list of all commands and modules")]
			public async Task HelpList() {
				await ReplyAsync(embed: await help.BuildHelpListAsync(Context, Commands.CommandSet).ConfigureAwait(false)).ConfigureAwait(false);
			}

			[Name("help <commandName>")]
			[Command("")]
			[Priority(0)]
			[Example("spoiler", "Explain the usage of the `spoiler` command")]
			public async Task HelpSearchCommand([Remainder] string commandName) {
				commandName = string.Join(" ", commandName.Trim().Split(' '));
				CommandDetails command = Commands.CommandSet.FindCommand(commandName, Context);
				if (command == null)
					await ReplyAsync($"No command with the name `{commandName}` exists").ConfigureAwait(false);
				else
					await ReplyAsync(embed: await help.BuildCommandHelpAsync(Context, command).ConfigureAwait(false)).ConfigureAwait(false);
			}

			[Name("help module <moduleName>")]
			[Command("module")]
			[Priority(1)]
			[Example("management", "Summarize the `Management` command module")]
			public async Task HelpSearchGroup([Remainder] string moduleName) {
				moduleName = string.Join(" ", moduleName.Trim().Split(' '));
				ModuleDetails module = Commands.CommandSet.FindModule(moduleName, Context);
				if (module == null)
					await ReplyAsync($"No module with the name `{moduleName}` exists").ConfigureAwait(false);
				else
					await ReplyAsync(embed: await help.BuildModuleHelpAsync(Context, module, false).ConfigureAwait(false)).ConfigureAwait(false);
			}
		}

		[Name("prefix")]
		[Command("prefix")]
		[Priority(0)]
		[Summary("Gets the bot's command prefix")]
		[Usage("Displays the current bot command prefix")]
		public async Task GetPrefix() {
			string prefix = await Contexting.GetPrefixAsync(Context).ConfigureAwait(false);
			await ReplyAsync($"**Prefix:** `{Format.Sanitize(prefix)}`").ConfigureAwait(false);
		}

		[Name("reactions")]
		[Command("reactions")]
		[Summary("List and explain how all reactions are used by the bot")]
		[Example("Display the bot's reaction list")]
		public Task Reactions() {
			return ReplyAsync(embed: help.BuildReactionList());
		}

		private struct Stats {

			public int Guilds { get; set; }
			public long Members { get; set; }

			public long Spoilers { get; set; }
			public long SpoiledUsers { get; set; }
			public long MALUsers { get; set; }
			public long AniListUsers { get; set; }
			public long KitsuUsers { get; set; }
			public long VNDbUsers { get; set; }
			public long MFCUsers { get; set; }
			public long TimeZones { get; set; }

			public string OS { get; set; }
			public string Framework { get; set; }
			public long MemoryUsage { get; set; }

			public TimeSpan Uptime { get; set; }
			public TimeSpan TotalUptime { get; set; }

			public override string ToString() {
				return	$"{ServerStats()}\n" +
						$"{SpoilerStats()}\n" +
						$"{ProfileStats()}"/*\n" +
						$"{SystemStats()}"*/;
			}
			public string UptimeStats() {
				int d = (int) Uptime.TotalDays;
				int h = Uptime.Hours;
				int m = Uptime.Minutes;
				int s = Uptime.Seconds;
				int d2 = (int) TotalUptime.TotalDays;
				int h2 = TotalUptime.Hours;
				int m2 = TotalUptime.Minutes;
				int s2 = TotalUptime.Seconds;
				return $"Current: " +
						$"**{d}** day{Plural(d)}, " +
						$"**{h}** hour{Plural(h)}, " +
						$"**{m}** minute{Plural(m)}, and " +
						$"**{s}** second{Plural(s)}\n" +
						$"Total: " +
						$"**{d2}** day{Plural(d2)}, " +
						$"**{h2}** hour{Plural(h2)}, " +
						$"**{m2}** minute{Plural(m2)}, and " +
						$"**{s2}** second{Plural(s2)}";
			}

			public string ServerStats() {
				return	$"I am active on **{Guilds}** server{Plural(Guilds)} " +
						$"with **{Members}** member{Plural(Members)}";
			}

			public string SpoilerStats() {
				return	$"**{Spoilers}** spoiler{Plural(Spoilers)} ha{Plural(Spoilers, "ve", "s")} been revealed " +
						$"**{SpoiledUsers}** time{Plural(SpoiledUsers)}";
			}

			public string ProfileStats() {
				return	$"I have registered:\n" +
						$"**{MALUsers}** [MyAnimeList](https://myanimelist.net/) user{Plural(MALUsers)}\n" +
						$"**{AniListUsers}** [AniList](https://anilist.co/) user{Plural(AniListUsers)}\n" +
						//$"**{KitsuUsers}** [Kitsu](https://kitsu.io/) user{Plural(KitsuUsers)}\n" +
						//$"**{VNDbUsers}** [VNDb](https://vndb.org/) user{Plural(VNDbUsers)}\n" +
						$"**{MFCUsers}** [MyFigureCollection](https://myfigurecollection.net/) user{Plural(MFCUsers)}\n" +
						$"**{TimeZones}** timezone{Plural(TimeZones)}";
			}

			public string SystemStats() {
				return  $"I am running on:\n" +
						$"*{OS}*\n" +
						$"*{Framework}*\n" +
						$"with **{FormatBytes.Format(MemoryUsage)}** of memory usage";
			}

			private string Plural(long count, string plural = "s", string single = "") {
				return (count != 1 ? plural : single);
			}
		}

		private async Task<Stats> GetStatsAsync() {
			foreach (var guild in Client.Guilds)
				await guild.DownloadUsersAsync().ConfigureAwait(false);
			Stats stats = new Stats {
				Guilds = Client.Guilds.Count,
				Members = Client.Guilds.Sum(g => g.Users.Count),
				OS = RuntimeInformation.OSDescription.Trim(),
				Framework = RuntimeInformation.FrameworkDescription.Trim(),
				MemoryUsage = Process.GetCurrentProcess().WorkingSet64,
				Uptime = DiscordBot.Uptime,
				TotalUptime = DiscordBot.TotalUptime,
			};
			using (var db = GetDb<TriggerDbContext>()) {
				foreach (var profile in db.UserProfiles) {
					if (!string.IsNullOrEmpty(profile.MALUsername)) stats.MALUsers++;
					if (!string.IsNullOrEmpty(profile.AniListUsername)) stats.AniListUsers++;
					//if (!string.IsNullOrEmpty(profile.KitsuUsername)) stats.KitsuUsers++;
					//if (!string.IsNullOrEmpty(profile.VNdbUsername)) stats.VNDbUsers++;
					if (!string.IsNullOrEmpty(profile.MFCUsername)) stats.MFCUsers++;
					if (profile.TimeZone != null) stats.TimeZones++;
				}
				stats.Spoilers = await db.Spoilers.LongCountAsync().ConfigureAwait(false);
				stats.SpoiledUsers = await db.SpoiledUsers.LongCountAsync().ConfigureAwait(false);
			}
			return stats;
		}

		private async Task AddAboutFields(EmbedBuilder embed) {
			var stats = await GetStatsAsync().ConfigureAwait(false);

			/*embed.AddField("Stats", $"**{spoilers}** spoiler{Plural(spoilers)} have been revealed " +
									$"**{spoiledUsers}** time{Plural(spoiledUsers)}");*/
			embed.AddField("Stats", stats.ToString());
			embed.AddField("Uptime", stats.UptimeStats());
			embed.AddField("System", stats.SystemStats());
			/*embed.AddField("Servers", $"I am active on **{guilds}** server{Plural(guilds)} with " +
									  $"**{members}** member{Plural(members)}");*/

			embed.WithFooter($"{configParser.Nickname} {configParser.Version}, built on");// {configParser.BuildDate}");
			embed.WithTimestamp(Assembly.GetEntryAssembly().GetUtcBuildTime());

			//embed.WithImageUrl(@"https://cdn.discordapp.com/attachments/436949335947870240/506494327250223104/VJMgPcQ.png");
			embed.WithImageUrl(@"https://raw.githubusercontent.com/trigger-death/TriggerChan/master/DiscordBots.MyBots/TriggerChan/Resources/UrlImages/triggerchan_wide.png");
		}

		[Name("stats")]
		[Command("stats"), Alias("statistics")]
		[Summary("Get some of my statistics such as spoilers, profiles, and servers")]
		[Example("Display the statistics message")]
		public async Task GetStats() {
			var stats = await GetStatsAsync().ConfigureAwait(false);
			var embed = new EmbedBuilder {
				Color = configParser.EmbedColor,
				Title = $"{configParser.EmbedPrefix} {configParser.Nickname} Statistics",
				//Description = stats.ToString(),
			};
			embed.AddField("Servers", stats.ServerStats());
			embed.AddField("Spoilers", stats.SpoilerStats());
			embed.AddField("Profiles", stats.ProfileStats());
			embed.AddField("Uptime", stats.UptimeStats());
			embed.AddField("System", stats.SystemStats());

			embed.WithFooter($"{configParser.Nickname} {configParser.Version}, built on");// {configParser.BuildDate}");
			embed.WithTimestamp(Assembly.GetEntryAssembly().GetUtcBuildTime());

			await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
			/*embed.AddField("Stats", $"**{spoilers}** spoiler{Plural(spoilers)} have been revealed " +
									$"**{spoiledUsers}** time{Plural(spoiledUsers)}");*/
			//embed.AddField("Stats", stats.ToString());
		}

		[Name("about")]
		[Command("about")]
		[Summary("Give a detailed explanation of the bot along with general information and statistics")]
		[Example("Display the about message")]
		public async Task About() {
			var embed = await help.BuildAboutEmbedAsync(Context, AddAboutFields).ConfigureAwait(false);
			await ReplyAsync(embed: embed).ConfigureAwait(false);
			
			/*var embed = new EmbedBuilder {
				Color = ColorUtils.Parse(Config["embed_color"]),
				Title = Config["about_title"] ?? $"{Client.CurrentUser.Username}: About",
			};
			embed.WithThumbnailUrl(Client.CurrentUser.GetAvatarUrl());
			StringBuilder str = new StringBuilder();
			var links = Config.GetSection("about_links");
			List<string> linkList = new List<string>();
			if (links != null) {
				foreach (var child in links.GetChildren()) {
					string name = child["name"];
					string url = child["url"];
					if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url)) {
						linkList.Add($"[{name}]({url})");
					}
				}
			}
			if (linkList.Count != 0)
				embed.Description = string.Join(" | ", linkList);
			if (Config["about_description"] != null)
				embed.AddField("About", Config["about_description"]);

			string prefix = await Contexting.GetPrefixAsync(Context).ConfigureAwait(false);
			embed.AddField("Prefix", $"The command prefix is `{prefix}`");// is present on **{Client.Guilds.Count}** servers!");

			TimeSpan uptime = DiscordBot.Uptime;
			int d = (int) uptime.TotalDays;
			int h = uptime.Hours;
			int m = uptime.Minutes;
			int s = uptime.Seconds;
			uptime = DiscordBot.TotalUptime;
			int d2 = (int) uptime.TotalDays;
			int h2 = uptime.Hours;
			int m2 = uptime.Minutes;
			int s2 = uptime.Seconds;
			int guilds = Client.Guilds.Count;
			long spoilers = await this.spoilers.GetSpoilerCountAsync().ConfigureAwait(false);
			long spoiledUsers = await this.spoilers.GetSpoiledUserCountAsync().ConfigureAwait(false);
			long members = Client.Guilds.Sum(g => g.Users.Count);

			embed.AddField("Stats", $"**{spoilers}** spoiler{Plural(spoilers)} have been revealed " +
									$"**{spoiledUsers}** time{Plural(spoiledUsers)}");
			embed.AddField("Uptime", $"`Current:` " +
									 $"**{d}** day{Plural(d)}, " +
									 $"**{h}** hour{Plural(h)}, " +
									 $"**{m}** minute{Plural(m)}, and " +
									 $"**{s}** second{Plural(s)}\n" +
									 $"`Total:` " +
									 $"**{d2}** day{Plural(d)}, " +
									 $"**{h2}** hour{Plural(h)}, " +
									 $"**{m2}** minute{Plural(m)}, and " +
									 $"**{s2}** second{Plural(s)}");
			embed.AddField("Servers", $"Active on **{guilds}** server{Plural(guilds)} with " +
									  $"**{members}** member{Plural(members)}");

			await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);*/
		}

		private string Plural(long count) {
			return (count != 1 ? "s" : "");
		}

		/*[Command("ping")]
		[Summary("Ping the bot and see the latency")]
		public async Task Ping() {
			await ReplyAsync($"**Pong:** `{(int) Client.Latency}ms`");
		}*/


		[Name("uptime")]
		[Command("uptime")]
		[Summary("Displays the time the bot has been running for")]
		public async Task Uptime() {
			TimeSpan uptime = DiscordBot.Uptime;
			int d = (int) uptime.TotalDays;
			int h = uptime.Hours;
			int m = uptime.Minutes;
			int s = uptime.Seconds;
			uptime = DiscordBot.TotalUptime;
			int d2 = (int) uptime.TotalDays;
			int h2 = uptime.Hours;
			int m2 = uptime.Minutes;
			int s2 = uptime.Seconds;
			
			var embed = new EmbedBuilder {
				Color = configParser.EmbedColor,
				Title = $"{configParser.EmbedPrefix}Uptime",
				Description = $"Current: " +
							  $"**{d}** day{Plural(d)}, " +
							  $"**{h}** hour{Plural(h)}, " +
							  $"**{m}** minute{Plural(m)}, and " +
							  $"**{s}** second{Plural(s)}\n" +
							  $"Total: " +
							  $"**{d2}** day{Plural(d2)}, " +
							  $"**{h2}** hour{Plural(h2)}, " +
							  $"**{m2}** minute{Plural(m2)}, and " +
							  $"**{s2}** second{Plural(s2)}",
			};
			/*embed.AddField("Current", $"**{d}** day{Plural(d)}, " +
									  $"**{h}** hour{Plural(h)}, " +
									  $"**{m}** minute{Plural(m)}, and " +
									  $"**{s}** second{Plural(s)}");
			embed.AddField("Total", $"**{d2}** day{Plural(d2)}, " +
									$"**{h2}** hour{Plural(h2)}, " +
									$"**{m2}** minute{Plural(m2)}, and " +
									$"**{s2}** second{Plural(s2)}");*/
			await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
			/*return ReplyAsync($"`Current:` " +
							  $"**{d}** day{Plural(d)}, " +
							  $"**{h}** hour{Plural(h)}, " +
							  $"**{m}** minute{Plural(m)}, and " +
							  $"**{s}** second{Plural(s)}\n" +
							  $"`Total:` " +
							  $"**{d2}** day{Plural(d)}, " +
							  $"**{h2}** hour{Plural(h)}, " +
							  $"**{m2}** minute{Plural(m)}, and " +
							  $"**{s2}** second{Plural(s)}");*/
		}

		[Name("ping")]
		[Command("ping")]
		[Summary("Returns the current estimated round-trip latency over WebSocket and REST")]
		public async Task PingAsync() {
			await help.PingResultAsync(Context).ConfigureAwait(false);
		}

		[Name("emotes [columns]")]
		[Command("emotes"), Alias("emoji")]
		[Summary("Lists all emotes for this server")]
		[Remarks("Sadly, this will not play animated emotes")]
		[Usage("[columns]")]
		[Example("Display all emotes within a reasonable column to row ratio")]
		[Example("4", "Display all emotes within 4 columns or less")]
		[IsLockable(true)]
		public async Task<RuntimeResult> ListEmotes(int? columns = null) {
			if (columns.HasValue && columns.Value < 1) {
				return EmoteResults.FromInvalidArgument();
			}
			else if (Context.Guild.Emotes.Count == 0) {
				await ReplyAsync("This server has no emotes").ConfigureAwait(false);
			}
			using (var typing = Context.Channel.EnterTypingState()) {
				List<GuildEmote> emotes = new List<GuildEmote>(Context.Guild.Emotes);
				emotes.Sort((a, b) => string.Compare(a.Name, b.Name, true));

				using (var bitmap = emotePreview.Draw(emotes, columns))
					await ReplyBitmapAsync(bitmap, "GuildEmotes.png").ConfigureAwait(false);
			}
			return NormalResult.FromSuccess();
		}

		[Name("usercount")]
		[Command("usercount")]
		[Summary("Counts the total number of users in the server, including those that are offline")]
		[RequiresHomeGuild]
		public async Task UserCountHome() {
			await Context.Guild.DownloadUsersAsync().ConfigureAwait(false);
			int triggerNPCs = 0;
			int otherNPCs = 0;
			int playerEntities = 0;
			int brushEntities = 0;
			int total = Context.Guild.Users.Count;
			int offline = 0;
			int bots = 0;
			ulong triggerNPCId = ulong.Parse(Home["roles:trigger_npc"]);
			ulong otherNPCId = ulong.Parse(Home["roles:other_npc"]);
			ulong BrushEntityId = ulong.Parse(Home["roles:brush_entity"]);
			ulong playerEntityId = ulong.Parse(Home["roles:player_entity"]);
			foreach (IGuildUser user in Context.Guild.Users) {
				foreach (ulong roleId in user.RoleIds) {
					if (roleId == triggerNPCId) {
						triggerNPCs++;
						break;
					}
					if (roleId == otherNPCId) {
						otherNPCs++;
						break;
					}
					if (roleId == playerEntityId)
						playerEntities++;
					if (roleId == BrushEntityId)
						brushEntities++;
				}
				if (user.IsBot)
					bots++;
				if (user.Status == UserStatus.Offline)
					offline++;
			}
			int online = total - offline;
			StringBuilder str1 = new StringBuilder();
			str1.AppendLine($"<:trigger_npc:527156203130716162> Trigger NPCs: **{triggerNPCs}**");
			str1.AppendLine($"<:other_npc:527156216330190848> Other NPCs: **{otherNPCs}**");
			str1.AppendLine($"<:player:527156180427079682> Player Entities: **{playerEntities}**");
			str1.AppendLine($"<:trigger:527156338418122767> Brush Entities: **{brushEntities}**");

			StringBuilder str2 = new StringBuilder();
			str2.AppendLine($"<:online:527164546755067904> Online: **{online}**");
			str2.AppendLine($"<:offline:527164561879465984> Offline: **{offline}**");
			str2.AppendLine($"<:empty:527165168942317589> Bots: **{bots}**");
			str2.AppendLine($"<:empty:527165168942317589> Total: **{total}**");
			var embed = new EmbedBuilder {
				Color = ColorUtils.Parse(Config["home_embed_color"]),// configParser.EmbedColor,
				//Description = $"Users in {Context.Guild.Name}",
				//Title = $"Users in {Context.Guild.Name}",
				/*Description =  $"Online: **{online}**\n" +
							   $"Offline: **{offline}**\n" +
							   $"Bots: **{bots}**\n" +
							   $"Total: **{total}**\n",*/
			};
			embed.WithAuthor($"Users in {Context.Guild.Name}", Context.Guild.IconUrl);
			embed.AddField("User Types", str1.ToString());
			embed.AddField("User Statuses", str2.ToString());
			await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
		}

		[Name("usercount")]
		[Command("usercount")]
		[Summary("Counts the total number of users in the server, including those that are offline")]
		[RequiresNotHomeGuild]
		public async Task UserCount() {
			await Context.Guild.DownloadUsersAsync().ConfigureAwait(false);
			int total = Context.Guild.Users.Count;
			int offline = 0;
			int bots = 0;
			foreach (IGuildUser user in Context.Guild.Users) {
				if (user.IsBot)
					bots++;
				if (user.Status == UserStatus.Offline)
					offline++;
			}
			int online = total - offline;
			var embed = new EmbedBuilder {
				Color = configParser.EmbedColor,
				//Description = $"Users in {Context.Guild.Name}",
				Title = $"Users in {Context.Guild.Name}",
				Description =  $"Online: **{online}**\n" +
							   $"Offline: **{offline}**\n" +
							   $"Bots: **{bots}**\n" +
							   $"Total: **{total}**\n",
			};
			await ReplyAsync(embed: embed.Build()).ConfigureAwait(false);
		}
	}
}
