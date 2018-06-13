﻿using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Maya.Music.Youtube;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TriggersTools.DiscordBots.TriggerChan.Info;
using TriggersTools.DiscordBots.TriggerChan.Util;

namespace TriggersTools.DiscordBots.TriggerChan.Services {
	public class AudioService : BotServiceBase {

		private enum AudioState {
			Playing,
			Stopped,
			Downloading,
		}

		private class AudioInfo {
			public IGuild Guild => Channel.Guild;
			public IVoiceChannel Channel { get; }
			public IAudioClient Client { get; set; }
			public CancellationTokenSource Token { get; set; }
			public bool IsPlaying { get; set; }
			public bool HasPlayed { get; set; }
			public AudioState LastState { get; set; }
			public SongInfo LastSong { get; set; }
			public bool IsSameAudioState {
				get {
					if (!IsPlaying && LastState == AudioState.Stopped)
						return true;
					else if (IsPlaying) {
						if (LastSong == CurrentSong &&
							LastState == AudioState.Downloading &&
							CurrentSong is SongDownloadInfo download &&
							download.DownloadStatus == DownloadStatus.InProgress)
						{
							return true;
						}
					}
					return false;
				}
			}

			public void UpdateLastAudioState() {
				LastSong = CurrentSong;
				if (IsPlaying) {
					if (CurrentSong is SongDownloadInfo download &&
						download.DownloadStatus == DownloadStatus.InProgress)
						LastState = AudioState.Downloading;
					else
						LastState = AudioState.Playing;
				}
				else {
					LastState = AudioState.Stopped;
				}
			}

			public IUserMessage StatusMessage { get; set; }

			public AudioInfo(IVoiceChannel channel, IAudioClient client) {
				Channel = channel;
				Client = client;
				Token = new CancellationTokenSource();
				IsPlaying = false;
			}

			public DateTime StartTime { get; set; }

			public SongInfo CurrentSong { get; set; }

			public int QueuedCount => QueuedSongs.Count;

			public List<SongInfo> QueuedSongs { get; } = new List<SongInfo>();

			public Timer StatusTimer { get; set; }
		}

		private readonly ConcurrentDictionary<ulong, AudioInfo> connections;
		private readonly Regex YouTubeRegex;
		private readonly Regex YouTubeCodeRegex;

		public AudioService() {
			connections = new ConcurrentDictionary<ulong, AudioInfo>();
			const string pattern = @"^https?:\/\/\w*.?(youtube\.com|youtu\.be)";
			RegexOptions options = RegexOptions.IgnoreCase;
			YouTubeRegex = new Regex(pattern, options);
			RegexOptions optionsCode = RegexOptions.IgnoreCase;// | RegexOptions.ExplicitCapture;
			const string patternCode = @"(?:https?:\/\/\w*.?(?:youtube\.com\/watch\?.*v=|youtu\.be\/))((\-|\w)+?)(?:$|&)";
			YouTubeCodeRegex = new Regex(patternCode, optionsCode);
		}


		public bool IsYouTubeUrl(string url, out string finalUrl) {
			Match match = YouTubeCodeRegex.Match(url);
			bool success = match.Success && match.Groups.Count >= 2;
			if (success)
				finalUrl = @"https://www.youtube.com/watch?v=" + match.Groups[1].Value;
			else
				finalUrl = null;
			return success;
		}

		protected override async void OnInitialized(ServiceProvider services) {
			base.OnInitialized(services);
			foreach (SocketGuild guild in Client.Guilds) {
				foreach (var channel in guild.VoiceChannels) {
					if (channel.Users.Any(u => u.Id == Client.CurrentUser.Id)) {
						await JoinAudio(guild, channel, true);
					}
				}
			}
		}

		public async Task JoinAudio(SocketCommandContext context, bool rejoin = false) {
			IVoiceChannel channel = (context.User as IVoiceState).VoiceChannel;
			await JoinAudio(context.Guild, channel, rejoin);
		}

		public async Task JoinAudio(IGuild guild, IVoiceChannel channel, bool rejoin = false) {
			AudioInfo info;
			if (connections.TryGetValue(guild.Id, out info)) {
				if (channel.Id != info.Channel.Id) {
					if (info.StatusMessage != null) {
						try {
							await info.StatusMessage.DeleteAsync();
						}
						catch { }
					}
					info.StatusTimer.Dispose();
					connections.TryRemove(guild.Id, out info);
				}
				else {
					if (rejoin) {
						await info.Client.StopAsync();
						info.Token = new CancellationTokenSource();
						info.Client = await channel.ConnectAsync();
						info.IsPlaying = false;
					}
					return;
				}
			}
			if (channel.Guild.Id != guild.Id) {
				return;
			}

			info = new AudioInfo(channel, await channel.ConnectAsync());
			if (rejoin) {
				Thread.Sleep(100);
				await info.Client.StopAsync();
				info.Client = await channel.ConnectAsync();
			}
			info.StatusTimer = new Timer(OnUpdateStatus, info, 5000, 5000);

			if (connections.TryAdd(guild.Id, info)) {
				// If you add a method to log happenings from this service,
				// you can uncomment these commented lines to make use of that.
				//await Log(LogSeverity.Info, $"Connected to voice on {guild.Name}.");
			}
			else {
				Console.WriteLine("Faield to add connection");
				info.StatusTimer.Dispose();
			}
		}

		public async void OnUpdateStatus(object state) {
			AudioInfo info = (AudioInfo) state;
			if (connections.TryGetValue(info.Guild.Id, out AudioInfo info2)) {
				if (info != info2) {
					info.StatusTimer.Dispose();
				}
			}
			try {
				await UpdateMusicStatus(info);
			}
			catch { }
		}

		private void StartDownload(ulong guildId, SongDownloadInfo download) {
			lock (download) {
				if (download.DownloadTask != null)
					return;
				string name = string.Join("_", download.Title.Split(Path.GetInvalidFileNameChars()));
				download.FileName = BotResources.GetMusic(guildId, name, download.Extension);
				download.DownloadTask = Task.Run(async () => { await MusicDownloader.Download(download); });
			}
		}

		/*public async Task<bool> StopAudio(SocketCommandContext context) {
			IVoiceChannel channel = (context.User as IVoiceState).VoiceChannel;
			AudioInfo info;
			if (connections.TryGetValue(context.Guild.Id, out info)) {
				bool sameChannel = (info.Channel.Id == channel.Id);
				if (sameChannel) {
					info.Token.Cancel();
					info.IsPlaying = false;
					await JoinAudio(context.Guild, info.Channel, true);
				}
				return sameChannel;
				//await Log(LogSeverity.Info, $"Disconnected from voice on {guild.Name}.");
			}
			return false;
		}*/


		public async Task<bool> LeaveAudio(SocketCommandContext context) {
			IVoiceChannel channel = (context.User as IVoiceState).VoiceChannel;
			if (connections.TryRemove(context.Guild.Id, out AudioInfo info)) {
				bool sameChannel = (info.Channel.Id == channel.Id);
				if (sameChannel) {
					if (info.StatusMessage != null) {
						try {
							await info.StatusMessage.DeleteAsync();
						}
						catch { }
					}
					info.StatusTimer.Dispose();
					await info.Client.StopAsync();
				}
				return sameChannel;
				//await Log(LogSeverity.Info, $"Disconnected from voice on {guild.Name}.");
			}
			return false;
		}

		public bool IsPlaying(SocketCommandContext context) {
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				return info.IsPlaying;
			}
			return false;
		}

		public void NextSong(SocketCommandContext context) {
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				NextSong(info);
			}
		}

		private void NextSong(AudioInfo info) {
			lock (info) {
				info.Token.Cancel();
			}
		}

		public void Stop(SocketCommandContext context) {
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				lock (info) {
					info.Token.Cancel();
					info.QueuedSongs.Clear();
				}
			}
		}

		public int GetQueuedCount(SocketCommandContext context) {
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				return info.QueuedCount;
			}
			return 0;
		}

		public IEnumerable<SongInfo> GetQueuedSongs(SocketCommandContext context) {
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				return info.QueuedSongs;
			}
			return Enumerable.Empty<SongInfo>();
		}

		public async Task AddSong(SocketCommandContext context, SongInfo song) {
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				lock (info.QueuedSongs) {
					info.QueuedSongs.Add(song);
					//if (info.QueuedSongs.Count == 1 && song is SongDownloadInfo download && info.IsPlaying)
					//	StartDownload(context.Guild.Id, download);
				}
				await NewMusicStatus(context);
				lock (info) {
					if (!info.IsPlaying) {
						info.IsPlaying = true;
						Task.Run(async () => { await PlayQueue(info); });
					}
				}
			}
		}

		public async Task<bool> InsertSong(SocketCommandContext context, SongInfo song, int index) {
			bool sucess = false;
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				lock (info.QueuedSongs) {
					if (info.QueuedSongs.Count >= index) {
						//if (index == 0 && song is SongDownloadInfo download && info.IsPlaying)
						//	StartDownload(context.Guild.Id, download);
						info.QueuedSongs.Insert(index, song);
						sucess = true;
					}
				}
				if (sucess) {
					await NewMusicStatus(context);
					lock (info) {
						if (!info.IsPlaying) {
							info.IsPlaying = true;
							Task.Run(async () => { await PlayQueue(info); });
						}
					}
				}
			}
			return sucess;
		}

		public async Task<bool> MoveSong(SocketCommandContext context, int index, int distance) {
			bool sucess = false;
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				lock (info.QueuedSongs) {
					int newIndex = index + distance;
					if (index >= 0 && info.QueuedSongs.Count > index &&
						newIndex >= 0 && info.QueuedSongs.Count > newIndex)
					{
						var song = info.QueuedSongs[index];
						info.QueuedSongs.RemoveAt(index);
						info.QueuedSongs.Insert(newIndex, song);
						sucess = true;
					}
				}
				if (sucess)
					await NewMusicStatus(context);
			}
			return sucess;
		}

		public async Task<bool> RemoveSong(SocketCommandContext context, int index) {
			bool sucess = false;
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				lock (info.QueuedSongs) {
					if (info.QueuedSongs.Count > index) {
						info.QueuedSongs.RemoveAt(index);
						sucess = true;
					}
				}
				if (sucess) 
					await NewMusicStatus(context);
			}
			return sucess;
		}

		private async Task PlayQueue(AudioInfo info) {
			int queuedCount;
			lock (info) {
				queuedCount = info.QueuedCount;
			}
			while (info.QueuedCount > 0) {
				SongInfo song;
				lock (info) {
					lock (info.QueuedSongs) {
						song = info.QueuedSongs[0];
						info.QueuedSongs.RemoveAt(0);
						info.CurrentSong = song;
						info.Token = new CancellationTokenSource();
					}
				}
				bool exists = true;
				if (song is SongDownloadInfo download) {
					await UpdateMusicStatus(info);
					StartDownload(info.Guild.Id, download);
					if (download.DownloadTask != null && !download.DownloadTask.IsCompleted) {
						download.DownloadTask.Wait();
					}
					if (download.DownloadStatus == DownloadStatus.Failed) {
						exists = false;
						Console.WriteLine(download.DownloadError.ToString());
						await download.Channel.SendMessageAsync($"Failed to download video `{download.Title}`\n" +
							$"Reason: {download.DownloadError.Message}");
					}
				}
				if (exists) {
					lock (info) {
						info.StartTime = DateTime.UtcNow;
					}
					await UpdateMusicStatus(info);
					await PlayAudio(info, song);
				}
				lock (info) {
					queuedCount = info.QueuedCount;
					if (queuedCount == 0) {
						info.IsPlaying = false;
						info.CurrentSong = null;
					}
				}
				if (queuedCount == 0)
					await UpdateMusicStatus(info);
			}
		}

		private async Task PlayAudio(AudioInfo info, SongInfo song) {
			// Your task: Get a full path to the file if the value of 'path' is only a filename.
			string path = song.FileName;
			/*if (!File.Exists(path)) {
				await context.Channel.SendMessageAsync($"File `{Path.GetFileName(path)}` does not exist.");
				return;
			}*/
			//AudioInfo info;
			if (connections.TryGetValue(info.Guild.Id, out info)) {
				if (info.HasPlayed) {
					info.Token = new CancellationTokenSource();
					info.Client = await info.Channel.ConnectAsync();
				}
				info.HasPlayed = true;
				using (var ffmpeg = CreateStream(path))
				using (var audio = info.Client.CreatePCMStream(AudioApplication.Music)) {
					await Task.Delay(400);
					if (ffmpeg.HasExited && ffmpeg.ExitCode != 0) {
						await song.Channel.SendMessageAsync($"FFmpeg failed to play song `{song.Title}`");
					}
					else {
						try {
							lock (info) {
								info.StartTime = DateTime.UtcNow;
							}
							await ffmpeg.StandardOutput.BaseStream.CopyToAsync(audio, 81920, info.Token.Token);
						}
						catch (OperationCanceledException) {
							await audio.FlushAsync();
						}
						finally {
							await audio.FlushAsync();
						}
					}
				}
			}
			if (song.IsTemporary) {
				try {
					File.Delete(path);
				}
				catch (Exception) { }
			}
		}

		public async Task NewMusicStatus(SocketCommandContext context) {
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				IUserMessage message;
				lock (info) {
					message = info.StatusMessage;
				}
				if (message != null) {
					try {
						await message.DeleteAsync();
					}
					catch {
						return;
					}
				}
				message = await context.Channel.SendMessageAsync("", false, await MakeMusicStatus(info));
				lock (info) {
					info.StatusMessage = message;
					info.UpdateLastAudioState();
				}
			}
		}

		private async Task UpdateMusicStatus(AudioInfo info) {
			IUserMessage message = info.StatusMessage;

			lock (info) {
				if (info.IsSameAudioState)
					return;
				info.UpdateLastAudioState();
			}
			if (message != null) {
				await message.ModifyAsync(async (p) => {
					p.Embed = await MakeMusicStatus(info);
				});
			}
		}

		private async Task<Embed> MakeMusicStatus(AudioInfo info) {
			var embed = new EmbedBuilder() {
				Title = "Not Playing",
			};
			if (info.CurrentSong != null) {
				SongInfo song = info.CurrentSong;
				string title = "";
				string description = $"{await song.Owner.GetName(info.Guild)}'s Choice\n" +
						$"{song.Title}\n";
				if (info.CurrentSong is SongDownloadInfo download &&
					download.DownloadStatus == DownloadStatus.InProgress) {
					title = "Downloading";
					if (download.Proxy != null) {
						title += " though proxy";
					}
				}
				else {
					title = "Playing";
					TimeSpan position = DateTime.UtcNow - info.StartTime;
					description += $"{position.ToPlayString()} / {song.Duration.ToPlayString()}";
				}
				embed.WithTitle(title);
				embed.WithDescription(description);
			}
			string queuedList = "";
			int count = 0;
			foreach (SongInfo song in info.QueuedSongs) {
				count++;
				queuedList += $"{count}) {song.Title}";
				if (song.Duration != TimeSpan.Zero) {
					queuedList += $" [{song.Duration.ToPlayString()}]";
				}
				queuedList += "\n";
			}
			if (string.IsNullOrEmpty(queuedList))
				queuedList = "No queued songs";
			embed.AddField($"Queued Songs: {count}", queuedList);
			return embed.Build();
		}
		

		private Process CreateStream(string path) {
			return Process.Start(new ProcessStartInfo {
				FileName = @"C:\Users\Onii-chan\Downloads\Discord.Net-Example-1.0\Discord.Net-Example-1.0\src\bin\Debug\netcoreapp2.0\ffmpeg.exe",
				Arguments = $"-hide_banner -xerror -loglevel quiet -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
				//Arguments = $"-hide_banner -i \"{path}\" -sample_fmt s16 -ar 48000 -ac 2 -acodec libopus -b:a 192k -vbr on -compression_level 10 -map 0:a -f data pipe:1",
				UseShellExecute = false,
				RedirectStandardOutput = true,
			});
		}

		public bool IsInVoiceSameChannel(SocketCommandContext context) {
			IVoiceChannel channel = (context.User as IVoiceState).VoiceChannel;
			if (connections.TryGetValue(context.Guild.Id, out AudioInfo info)) {
				return (channel != null && info.Channel.Id == channel.Id);
			}
			return false;
		}
	}
}
