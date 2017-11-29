using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using NAudio.Wave;

namespace DiscordBotTesting
{
    [RequireUserPermissions(Permissions.ViewAuditLog)]
    public class Commands
    {
        //TODO: command to move one song to another location in the playlist
        //TODO: Command to add another directory to the current playlist

        #region Fields and methods
        public Playlist playlist = new Playlist();

        public bool PauseRequested = false;
        public bool StopRequested = false;
        public bool SkipRequested = false;
        public bool PrevRequested = false;
        public bool RestartRequested = false;

        //TODO: try to avoid mid line splits
        //TODO: print as Discord embed
        public async Task PrintLong(CommandContext ctx, string[] s)
        {
            string str = string.Join(Environment.NewLine, s);
            await PrintLong(ctx, str);
        }
        public async Task PrintLong(CommandContext ctx, string str)
        {
            for (int i = 0; i <= str.ToString().Length / 1993; i++)
            {
                if (str.ToString().Substring(i * 1993).Length < 1993)
                    await ctx.RespondAsync("```" + str.ToString().Substring(i * 1993) + "```");
                else
                    await ctx.RespondAsync("```" + str.ToString().Substring(i * 1993, 1993) + "```");
            }
        }
        public async Task<VoiceNextConnection> GetVnc(CommandContext ctx, bool send)
        {
            var vnext = ctx.Client.GetVoiceNextClient();
            if (vnext == null)
            {
                if (send) { await ctx.RespondAsync("VNext is not enabled or configured."); }
                return null;
            }

            var vnc = vnext.GetConnection(ctx.Guild);
            if (vnc == null)
            {
                if (send) { await ctx.RespondAsync("Not connected in this guild."); }
                return null;
            }

            return vnc;
        }
        #endregion

        #region Music
        [Command("play"), Description("Play an audio file.")]
        [Aliases("resume")]
        public async Task Play(CommandContext ctx, string index = null)
        {
            if (this.playlist.Length <= 0)
            {
                await ctx.RespondAsync("No songs loaded into playlist.");
                return;
            }

            //TODO: play the selected song if already playing music

            bool isCompleted = false;

            do
            {
                var vnc = await GetVnc(ctx, true);
                if (vnc == null)
                    return;

                if (index != null)
                {
                    if (int.TryParse(index, out int tmp))
                    {
                        if (tmp < 1 || tmp > this.playlist.Length)
                        {
                            await ctx.RespondAsync($"Please enter a number between 1 and {this.playlist.Length}.");
                            return;
                        }
                        else
                        {
                            await this.playlist.Seek(tmp - 1);
                        }
                    }
                    else
                    {
                        await ctx.RespondAsync($"Please enter a number.");
                        return;
                    }
                }

                if (!File.Exists(@playlist.Current.Path))
                {
                    await ctx.RespondAsync($"File `{@playlist.Current.Path}` does not exist.");
                    return;
                }

                // skip command if already playing
                if (vnc.IsPlaying)
                {
                    await ctx.RespondAsync("Already playing music.");
                    return;
                }

                // play
                Exception exc = null;
                await ctx.Message.RespondAsync($"Playing `{this.playlist.CurrentToString()}`");
                await ctx.Client.UpdateStatusAsync(new DiscordGame(this.playlist.CurrentToString()));
                await vnc.SendSpeakingAsync(true);
                try
                {
                    var outFormat = new WaveFormat(48000, 16, 2);

                    using (var mp3Reader = new Mp3FileReader($"{@playlist.Current.Path}"))
                    using (var resampler = new MediaFoundationResampler(mp3Reader, outFormat))
                    {
                        resampler.ResamplerQuality = 60;
                        int blockSize = outFormat.AverageBytesPerSecond / 50;
                        byte[] buffer = new byte[blockSize];
                        int byteCount;

                        if (this.playlist.Position >= this.playlist.Length - 1)
                            isCompleted = true;

                        if (this.playlist.Current.Position > 0)
                            mp3Reader.Position = this.playlist.Current.Position;

                        while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                        {
                            if (this.StopRequested)
                            {
                                this.playlist.Current.Position = 0;
                                return;
                            }

                            if (this.PauseRequested)
                            {
                                this.playlist.Current.Position = mp3Reader.Position;
                                return;
                            }

                            if (this.SkipRequested)
                            {
                                this.playlist.Current.Position = 0;
                                await this.playlist.Next();
                                break;
                            }

                            if (this.PrevRequested)
                            {
                                this.playlist.Current.Position = 0;
                                await this.playlist.Previous();
                                break;
                            }

                            if (this.RestartRequested)
                            {
                                this.playlist.Current.Position = 0;
                                break;
                            }

                            if (byteCount < blockSize)
                            {
                                for (int g = byteCount; g < blockSize; g++)
                                    buffer[g] = 0;
                            }

                            await vnc.SendAsync(buffer, 20);
                        }
                    }
                    
                    if (!this.PrevRequested && !this.SkipRequested && !this.RestartRequested)
                    {
                        this.playlist.Current.Position = 0;
                        await this.playlist.Next();
                    }
                }
                catch (Exception ex) { exc = ex; }
                finally
                {
                    index = null;
                    this.PauseRequested = false;
                    this.StopRequested = false;
                    this.SkipRequested = false;
                    this.PrevRequested = false;
                    this.RestartRequested = false;
                    await vnc.SendSpeakingAsync(false);
                    await ctx.Client.UpdateStatusAsync();
                }

                if (exc != null)
                    await ctx.RespondAsync($"An exception occured during playback: `{exc.GetType()}: {exc.Message}`");
            } while (!isCompleted);
        }

        [Command("pause"), Description("Pause the current song.")]
        public async Task Pause(CommandContext ctx)
        {
            var vnc = await GetVnc(ctx, true);
            if (vnc.IsPlaying)
            {
                this.PauseRequested = true;
                await ctx.RespondAsync("Song paused.");
            }
        }

        [Command("stop"), Description("Stop playback.")]
        [Aliases("end")]
        public async Task Stop(CommandContext ctx)
        {
            var vnc = await GetVnc(ctx, true);
            if (vnc.IsPlaying)
            {
                this.StopRequested = true;
                await ctx.RespondAsync("Playback stopped.");
            }
        }

        [Command("next"), Description("Go to the next track in the playlist.")]
        [Aliases("skip", "forward")]
        public async Task Next(CommandContext ctx)
        {
            var vnc = await GetVnc(ctx, false);
            if (vnc != null)
            {
                if (vnc.IsPlaying && this.playlist.HasNext)
                {
                    this.SkipRequested = true;
                    return;
                }
            }

            if (this.playlist.Length > 0 && this.playlist.HasNext)
            {
                this.playlist.Current.Position = 0;
                await this.playlist.Next();
                await ctx.RespondAsync($"Raising position in playlist to `{this.playlist.CurrentToString()}`.");
            }

            //TODO: if loop is enabled go to first song.
        }

        [Command("previous"), Description("Go to the last track in the playlist.")]
        [Aliases("prev", "back")]
        public async Task Previous(CommandContext ctx)
        {
            var vnc = await GetVnc(ctx, false);
            if (vnc != null)
            {
                if (vnc.IsPlaying && this.playlist.Position > 0)
                {
                    this.PrevRequested = true;
                    return;
                }
            }

            if (this.playlist.Length > 0 && this.playlist.Position > 0)
            {
                this.playlist.Current.Position = 0;
                await this.playlist.Previous();
                await ctx.RespondAsync($"Lowering position in playlist to `{this.playlist.CurrentToString()}`.");
            }
        }

        [Command("restart"), Description("Restart the current song.")]
        [Aliases("rs")]
        public async Task Restart(CommandContext ctx)
        {
            var vnc = await GetVnc(ctx, true);
            if (vnc.IsPlaying)
            {
                this.RestartRequested = true;
            }
        }
        #endregion

        #region Playlist
        [Command("playlist"), Description("Load all the song in a directory.")]
        [Aliases("list", "pl")]
        public async Task Playlist(CommandContext ctx, [RemainingText, Description("Path to the read dir.")]string path = null)
        {
            await ctx.TriggerTypingAsync();

            if (string.IsNullOrEmpty(path))
            {
                if (this.playlist.Length > 0)
                {
                    await ctx.RespondAsync($"The following {this.playlist.Length} songs are loaded into the playlist");
                    await PrintLong(ctx, this.playlist.ToString());
                }
                else
                {
                    await ctx.RespondAsync($"There are currently no songs loaded into the playlist.");
                }
            }
            else if (Directory.Exists(@path))
            {
                string[] files = Directory.GetFiles(@path, "*.mp3", SearchOption.TopDirectoryOnly);
                if (files != null)
                {
                    this.playlist = new Playlist(files);
                    await ctx.RespondAsync($"{this.playlist.Length} songs loaded into playlist.");
                    await PrintLong(ctx, this.playlist.ToString());
                }
                else
                {
                    await ctx.RespondAsync("No music files found.");
                }
            }
            else
            {
                await ctx.RespondAsync("Path not found.");
            }
        }

        [Command("peek"), Description("Peek at the current location in the playlist.")]
        public async Task Peek(CommandContext ctx)
        {
            if (this.playlist.Length > 0)
            {
                await ctx.RespondAsync($"```{this.playlist.Peek()}```");
            }
            else
            {
                await ctx.RespondAsync("No songs loaded into playlist.");
            }
        }

        [Command("select"), Description("Queue up the next song in the playlist.")]
        [Aliases("queue")]
        public async Task Select(CommandContext ctx, [Description("1-based index to the song in the playlist to select.")]int index = -1)
        {
            if (this.playlist.Length > 0)
            {
                if (index < 1 || index > this.playlist.Length)
                {
                    await ctx.RespondAsync($"Please enter a number between 1 and {this.playlist.Length}.");
                    return;
                }
                else
                {
                    await this.playlist.Seek(index - 1);
                    await ctx.RespondAsync($"```{this.playlist.Peek()}```");
                }
            }
            else
            {
                await ctx.RespondAsync("No songs loaded into playlist.");
            }
        }

        [Command("lookup"), Description("Find a song in the current playlist.")]
        [Aliases("find", "grep", "seek")]
        public async Task Lookup(CommandContext ctx) { await Task.CompletedTask; }

        [Command("shuffle")]
        [Aliases("shuf")]
        public async Task Shuffle(CommandContext ctx)
        {
            if (this.playlist.Length > 0)
            {
                await ctx.TriggerTypingAsync();

                await this.playlist.Shuffle();
                await ctx.RespondAsync("The playlist was shuffled.");
                await PrintLong(ctx, this.playlist.ToString());
            }
            else
            {
                await ctx.RespondAsync("No songs loaded into playlist.");
            }
        }

        [Command("loop"), Description("Set the playlist to loop.")]
        public async Task Loop(CommandContext ctx) { await Task.CompletedTask; }

        [Command("clear")]
        public async Task Clear(CommandContext ctx)
        {
            var vnc = await GetVnc(ctx, false);
            if (vnc != null)
            {
                if (vnc.IsPlaying)
                {
                    this.StopRequested = true;
                }
            }

            this.playlist.Songs.Clear();
            await ctx.RespondAsync("The playlist was cleared.");
        }
        #endregion

        #region Admin
        [Command("join"), Description("Join a voice channel.")]
        [Aliases("connect")]
        public async Task Join(CommandContext ctx, DiscordChannel chn = null)
        {
            // check whether VNext is enabled
            VoiceNextClient vnext = ctx.Client.GetVoiceNextClient();
            if (vnext == null)
            {
                // not enabled
                await ctx.RespondAsync("VNext is not enabled or configured.");
                return;
            }

            // check whether we aren't already connected
            VoiceNextConnection vnc = vnext.GetConnection(ctx.Guild);
            if (vnc != null)
            {
                // already connected
                await ctx.RespondAsync("Already connected in this guild.");
                return;
            }

            // get member's voice state
            var vstat = ctx.Member?.VoiceState;
            if (vstat?.Channel == null && chn == null)
            {
                // they did not specify a channel and are not in one
                await ctx.RespondAsync("You are not in a voice channel.");
                return;
            }

            // channel not specified, use user's
            if (chn == null)
                chn = vstat.Channel;

            // connect
            vnc = await vnext.ConnectAsync(chn);
            await ctx.RespondAsync($"Connected to `{chn.Name}`");
        }


        //TODO: fix this method, the delay does not seem to work
        [Command("leave"), Description("Leave a voice channel.")]
        [Aliases("part")]
        public async Task Leave(CommandContext ctx)
        {
            var vnc = await GetVnc(ctx, true);
            if (vnc != null)
            {
                if (vnc.IsPlaying)
                {
                    this.StopRequested = true;
                    await Task.Delay(60);
                }
            }

            // disconnect
            vnc.Disconnect();
            await ctx.RespondAsync("Disconnected");
        }

        [RequireOwner, Hidden]
        [Command("dir"), Description("Get a directory listing.")]
        public async Task Dir(CommandContext ctx, [RemainingText, Description("Path to the read dir.")]string path)
        {
            if (Directory.Exists(@path))
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("Directories");
                string[] dirs = Directory.GetDirectories(@path, "*", SearchOption.TopDirectoryOnly);
                foreach (string dir in dirs)
                {
                    sb.AppendLine(dir);
                }

                sb.AppendLine();
                sb.AppendLine("Files");
                string[] files = Directory.GetFiles(@path, "*", SearchOption.TopDirectoryOnly);
                foreach (string file in files)
                {
                    sb.AppendLine(file);
                }

                await PrintLong(ctx, sb.ToString());
            }
            else
                await ctx.RespondAsync("Path not found.");
        }
        #endregion
    }
}