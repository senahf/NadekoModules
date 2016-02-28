using System;
using System.Linq;
using Discord.Modules;
using Discord.Commands;
using Discord;
using NadekoBot.Extensions;
using System.Collections.Concurrent;
using NadekoBot.Classes.Music;
using Timer = System.Timers.Timer;
using System.Threading.Tasks;
using NadekoBot.Classes;
using Discord.Audio;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using System.IO;

namespace NadekoBot.Modules {
    class Music : DiscordModule {

        public static ConcurrentDictionary<Server, MusicControls> musicPlayers = new ConcurrentDictionary<Server, MusicControls>();
        public static ConcurrentDictionary<ulong, float> musicVolumes = new ConcurrentDictionary<ulong, float>();
        public string musicGlobal = "";

        internal static string GetMusicStats() {
            var stats = musicPlayers.Where(kvp => kvp.Value?.SongQueue.Count > 0 || kvp.Value?.CurrentSong != null);
            int cnt;
            return $"Playing {cnt = stats.Count()} songs".SnPl(cnt) + $", {stats.Sum(kvp => kvp.Value?.SongQueue?.Count ?? 0)} queued.";
        }

        public Music() : base() {
        }
        public static string file_get_contents(string fileName)
        {
            string sContents = string.Empty;
            if (fileName.ToLower().IndexOf("http:") > -1)
            { // URL 
                System.Net.WebClient wc = new System.Net.WebClient();
                wc.Headers.Add("User-Agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322)");
                byte[] response = wc.DownloadData(fileName);
                sContents = System.Text.Encoding.ASCII.GetString(response);
            }
            else
            {
                // Regular Filename 
                System.IO.StreamReader sr = new System.IO.StreamReader(fileName);
                sContents = sr.ReadToEnd();
                sr.Close();
            }
            return sContents;
        }

        public static string GetStringInBetween(string strBegin, string strEnd, string strSource, bool includeBegin, bool includeEnd)
        {
            string[] result = { string.Empty, string.Empty };
            int iIndexOfBegin = strSource.IndexOf(strBegin);

            if (iIndexOfBegin != -1)
            {
                // include the Begin string if desired 
                if (includeBegin)
                    iIndexOfBegin -= strBegin.Length;

                strSource = strSource.Substring(iIndexOfBegin + strBegin.Length);

                int iEnd = strSource.IndexOf(strEnd);
                if (iEnd != -1)
                {
                    // include the End string if desired 
                    if (includeEnd)
                        iEnd += strEnd.Length;
                    result[0] = strSource.Substring(0, iEnd);
                    // advance beyond this segment 
                    if (iEnd + strEnd.Length < strSource.Length)
                        result[1] = strSource.Substring(iEnd + strEnd.Length);
                }
            }
            else
                // stay where we are 
                result[1] = strSource;
            return result[0];
        }
        public bool playing = false;
        private async void getPlaying(CommandEventArgs e)
        {
            do
            {
                var client = NadekoBot.client;
                Server server = e.Server;
                if (!playing) return;
                if (musicPlayers.ContainsKey(server) == false) { return; }
                else
                {
                    var player = musicPlayers[server];
                    try
                    {
                        if (player.CurrentSong.LinkType == MusicType.Radio)
                        {
                            var res = file_get_contents(player.CurrentSong.Title);
                            if (res.Contains("SHOUTcast"))
                            {
                                String song = GetStringInBetween("Current Song: </font></td><td><font class=default><b>", "</b></td>", res, false, false);
                                using (StreamReader sr = new StreamReader("rawr.txt"))
                                {
                                    
                                    if (sr.ReadToEnd().Contains(song))
                                    {
                                        // do nothing   
                                        sr.Close();

                                    }
                                    else
                                    {
                                        sr.Close();
                                        if (song.Contains("f(x)"))
                                        {
                                            await e.Channel.SendMessage($"{song}\r\n{e.Channel.GetUser(133118499923951616)?.Mention}");
                                        }
                                        else if (song.Contains("Girls' Generation"))
                                        {
                                            await e.Channel.SendMessage($"{song}\r\n{e.Channel.GetUser(133118499923951616)?.Mention}");
                                        }
                                        else
                                        {
                                            await e.Channel.SendMessage(song);
                                        }
                                        using (var fs = new FileStream("rawr.txt", FileMode.Truncate))
                                        {
                                            fs.Close();
                                        }
                                        using (StreamWriter file = new StreamWriter("rawr.txt", true))
                                        {
                                            await file.WriteLineAsync(song);
                                            file.Close();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex) { Console.WriteLine(ex); }
                }
                Thread.Sleep(7000);
            } while (playing);
        }
        private void setMusic()
        {
            do
            {
                var client = NadekoBot.client;
                Server server = client.GetServer(133389185988952064);
                if (!playing) return; 
                if (musicPlayers.ContainsKey(server) == false) { playing = false; client.SetGame("No Song Playing"); }
                else
                {
                    var player = musicPlayers[server];
                    try
                    {
                        if (player.CurrentSong.LinkType == MusicType.Radio)
                        {
                            var res = file_get_contents(player.CurrentSong.Title);
                            if (res.Contains("SHOUTcast"))
                            {
                                client.SetGame(GetStringInBetween("Current Song: </font></td><td><font class=default><b>", "</b></td>", res, false, false));
                            }
                            else { client.SetGame("Radio Not Supported"); }
                        }
                        else
                        {

                            client.SetGame(player.CurrentSong.FullPrettyName);
                        }
                    }
                    catch (Exception ex) { Console.WriteLine(ex); }
                }
                Thread.Sleep(7000);
            } while (playing);
        }
        public override void Install(ModuleManager manager) {
            var client = NadekoBot.client;

            manager.CreateCommands("!m", cgb => {

                cgb.AddCheck(Classes.Permissions.PermissionChecker.Instance);

                commands.ForEach(cmd => cmd.Init(cgb));

                cgb.CreateCommand("n")
                    .Alias("next")
                    .Description("Goes to the next song in the queue.")
                    .Do(async e => {
                        if (musicPlayers.ContainsKey(e.Server) == false) return;
                        await musicPlayers[e.Server].LoadNextSong();
                    });

                cgb.CreateCommand("s")
                    .Alias("stop")
                    .Description("Completely stops the music, unbinds the bot from the channel, and cleans up files.")
                    .Do(e => {
                        if (musicPlayers.ContainsKey(e.Server) == false) return;
                        musicPlayers[e.Server].Stop(true);
                    });

                cgb.CreateCommand("p")
                    .Alias("pause")
                    .Description("Pauses or Unpauses the song.")
                    .Do(async e => {
                        if (musicPlayers.ContainsKey(e.Server) == false) return;
                        if (musicPlayers[e.Server].TogglePause())
                            await e.Channel.SendMessage("🎵`Music player paused.`");
                        else
                            await e.Channel.SendMessage("🎵`Music player unpaused.`");

                    });

                cgb.CreateCommand("q")
                    .Alias("yq")
                    .Description("Queue a song using keywords or a link. Bot will join your voice channel. **You must be in a voice channel**.\n**Usage**: `!m q Dream Of Venice`")
                    .Parameter("query", ParameterType.Unparsed)
                    .Do(async e => {
                        
                        await QueueSong(e, e.GetArg("query"));
                        if (!playing) { playing = true; new Thread(x => getPlaying(e)).Start();  new Thread(x => setMusic()).Start(); }

                    });

                cgb.CreateCommand("lq")
                    .Alias("ls").Alias("lp")
                    .Description("Lists up to 10 currently queued songs.")
                    .Do(async e => {
                        if (musicPlayers.ContainsKey(e.Server) == false) {
                            await e.Channel.SendMessage("🎵 No active music player.");
                            return;
                        }
                        var player = musicPlayers[e.Server];
                        string toSend = "🎵 **" + player.SongQueue.Count + "** `videos currently queued.` ";
                        if (player.SongQueue.Count >= 50)
                            toSend += "**Song queue is full!**\n";
                        await e.Channel.SendMessage(toSend);
                        int number = 1;
                        await e.Channel.SendMessage(string.Join("\n", player.SongQueue.Take(10).Select(v => $"`{number++}.` {v.FullPrettyName}")));
                    });

                cgb.CreateCommand("np")
                 .Alias("playing")
                 .Description("Shows the song currently playing.")
                 .Do(async e => {
                     if (musicPlayers.ContainsKey(e.Server) == false) return;
                     var player = musicPlayers[e.Server];
                     try {
                         if (player.CurrentSong.LinkType == MusicType.Radio)
                         {
                         
                             var res = file_get_contents(player.CurrentSong.Title);
                             if (res.Contains("SHOUTcast"))
                             {
                                 await e.Channel.SendMessage(GetStringInBetween("Current Song: </font></td><td><font class=default><b>", "</b></td>", res, false, false));
                             } else { await e.Channel.SendMessage("`np` command for this radio not supported."); }
                         }
                         else
                         {

                             await e.Channel.SendMessage($"🎵`Now Playing` {player.CurrentSong.FullPrettyName}");
                         }
                     } catch (Exception ex) { Console.WriteLine(ex);  }
                 });

                cgb.CreateCommand("vol")
                  .Description("Sets the music volume 0-150%")
                  .Parameter("val", ParameterType.Required)
                  .Do(async e => {
                      if (musicPlayers.ContainsKey(e.Server) == false) return;
                      var player = musicPlayers[e.Server];
                      var arg = e.GetArg("val");
                      int volume;
                      if (!int.TryParse(arg, out volume)) {
                          await e.Channel.SendMessage("Volume number invalid.");
                          return;
                      }
                      volume = player.SetVolume(volume);
                      await e.Channel.SendMessage($"🎵 `Volume set to {volume}%`");
                  });

                cgb.CreateCommand("dv")
                    .Alias("defvol")
                    .Description("Sets the default music volume when music playback is started (0-100). Does not persist through restarts.\n**Usage**: !m dv 80")
                    .Parameter("val", ParameterType.Required)
                    .Do(async e => {
                        var arg = e.GetArg("val");
                        float volume;
                        if (!float.TryParse(arg, out volume) || volume < 0 || volume > 100) {
                            await e.Channel.SendMessage("Volume number invalid.");
                            return;
                        }
                        musicVolumes.AddOrUpdate(e.Server.Id, volume / 100, (key, newval) => volume / 100);
                        await e.Channel.SendMessage($"🎵 `Default volume set to {volume}%`");
                    });

                cgb.CreateCommand("min").Alias("mute")
                    .Description("Sets the music volume to 0%")
                    .Do(e => {
                        if (musicPlayers.ContainsKey(e.Server) == false) return;
                        var player = musicPlayers[e.Server];
                        player.SetVolume(0);
                    });

                cgb.CreateCommand("max")
                  .Description("Sets the music volume to 100% (real max is actually 150%).")
                  .Do(e => {
                      if (musicPlayers.ContainsKey(e.Server) == false) return;
                      var player = musicPlayers[e.Server];
                      player.SetVolume(100);
                  });

                cgb.CreateCommand("half")
                  .Description("Sets the music volume to 50%.")
                  .Do(e => {
                      if (musicPlayers.ContainsKey(e.Server) == false) return;
                      var player = musicPlayers[e.Server];
                      player.SetVolume(50);
                  });

                cgb.CreateCommand("sh")
                    .Description("Shuffles the current playlist.")
                    .Do(async e => {
                        if (musicPlayers.ContainsKey(e.Server) == false) return;
                        var player = musicPlayers[e.Server];
                        if (player.SongQueue.Count < 2) {
                            await e.Channel.SendMessage("Not enough songs in order to perform the shuffle.");
                            return;
                        }

                        player.SongQueue.Shuffle();
                        await e.Channel.SendMessage("🎵 `Songs shuffled.`");
                    });

                Timer setgameTimer = new Timer();
                setgameTimer.Interval = 20000;
                setgameTimer.Elapsed += (s, e) => {
                    int num = musicPlayers.Where(kvp => kvp.Value.CurrentSong != null).Count();
                    NadekoBot.client.SetGame($"{num} songs".SnPl(num) + $", {musicPlayers.Sum(kvp => kvp.Value.SongQueue.Count())} queued");
                };
                /*cgb.CreateCommand("setgame")
                    .Description("Sets the game of the bot to the number of songs playing.**Owner only**")
                    .Do(async e => {
                        if (NadekoBot.OwnerID != e.User.Id)
                            return;
                        setgameEnabled = !setgameEnabled;
                        if (setgameEnabled)
                            setgameTimer.Start();
                        else
                            setgameTimer.Stop();

                        await e.Channel.SendMessage("`Music status " + (setgameEnabled ? "enabled`" : "disabled`"));
                    });*/

                cgb.CreateCommand("pl")
                    .Description("Queues up to 25 songs from a youtube playlist specified by a link, or keywords.")
                    .Parameter("playlist", ParameterType.Unparsed)
                    .Do(async e => {
                        if (e.User.VoiceChannel?.Server != e.Server) {
                            await e.Channel.SendMessage("💢 You need to be in a voice channel on this server.\n If you are already in a voice channel, try rejoining it.");
                            return;
                        }
                        var ids = await SearchHelper.GetVideoIDs(await SearchHelper.GetPlaylistIdByKeyword(e.GetArg("playlist")));
                        //todo TEMPORARY SOLUTION, USE RESOLVE QUEUE IN THE FUTURE
                        var msg = await e.Channel.SendMessage($"🎵 `Attempting to queue {ids.Count} songs".SnPl(ids.Count)+"...`");
                        foreach (var id in ids) {
                            Task.Run(async () => await QueueSong(e, id, true)).ConfigureAwait(false);
                            await Task.Delay(150);
                        }
                        msg?.Edit("🎵 `Playlist queue complete.`");
                    });

                cgb.CreateCommand("lopl")
                  .Description("Queues up to 50 songs from a directory.")
                  .Parameter("directory", ParameterType.Unparsed)
                  .AddCheck(Classes.Permissions.SimpleCheckers.OwnerOnly())
                  .Do(async e => {
                      var arg = e.GetArg("directory");
                      if(string.IsNullOrWhiteSpace(e.GetArg("directory")))
                        return;
                      try {
                          var fileEnum = System.IO.Directory.EnumerateFiles(e.GetArg("directory")).Take(50);
                          foreach (var file in fileEnum) {
                              await Task.Run(async() => await QueueSong(e, file, true, MusicType.Local)).ConfigureAwait(false);
                          }
                          await e.Channel.SendMessage("🎵 `Directory queue complete.`");
                      }
                      catch { }
                  });

                cgb.CreateCommand("radio").Alias("ra")
                    .Description("Queues a direct radio stream from a link.")
                    .Parameter("radio_link", ParameterType.Required)
                    .Do(async e => {
                        if (e.User.VoiceChannel?.Server != e.Server) {
                            await e.Channel.SendMessage("💢 You need to be in a voice channel on this server.\n If you are already in a voice channel, try rejoining it.");
                            return;
                        }
                        
                        await QueueSong(e, e.GetArg("radio_link"), musicType: MusicType.Radio);
                        if (!playing) { playing = true; new Thread(x => getPlaying(e)).Start(); new Thread(x => setMusic()).Start(); }

                    });

                cgb.CreateCommand("lo")
                  .Description("Queues a local file by specifying a full path. BOT OWNER ONLY.")
                  .Parameter("path", ParameterType.Unparsed)
                  .AddCheck(Classes.Permissions.SimpleCheckers.OwnerOnly())
                  .Do(async e => {
                      var arg = e.GetArg("path");
                      if (string.IsNullOrWhiteSpace(arg))
                          return;
                      await QueueSong(e, e.GetArg("path"), musicType: MusicType.Local);
                  });

                cgb.CreateCommand("mv")
                  .Description("Moves the bot to your voice channel. (works only if music is already playing)")
                  .Do(async e => {
                      MusicControls mc;
                      if (e.User.VoiceChannel == null || e.User.VoiceChannel.Server != e.Server || !musicPlayers.TryGetValue(e.Server,out mc))
                          return;
                      mc.VoiceChannel = e.User.VoiceChannel;
                      mc.VoiceClient = await mc.VoiceChannel.JoinAudio();
                  });

                cgb.CreateCommand("rm")
                    .Description("Remove a song by its # in the queue, or 'all' to remove whole queue.")
                    .Parameter("num",ParameterType.Required)
                    .Do(async e => {
                        var arg = e.GetArg("num");
                        MusicControls mc;
                        if (!musicPlayers.TryGetValue(e.Server, out mc)) {
                            return;
                        }
                        if (arg?.ToLower() == "all") {
                            mc.SongQueue?.Clear();
                            await e.Channel.SendMessage($"🎵`Queue cleared!`");
                            return;
                        }
                        int num;
                        if (!int.TryParse(arg, out num)) {
                            return;
                        }
                        if (num <= 0 || num > mc.SongQueue.Count)
                            return;

                        mc.SongQueue.RemoveAt(num - 1);
                        await e.Channel.SendMessage($"🎵**Track at position `#{num}` has been removed.**");
                    });

                cgb.CreateCommand("debug")
                    .Description("Writes some music data to console. **BOT OWNER ONLY**")
                    .Do(e => {
                        if (NadekoBot.OwnerID != e.User.Id)
                            return;
                        var output = "SERVER_NAME---SERVER_ID-----USERCOUNT----QUEUED\n" +
                            string.Join("\n", musicPlayers.Select(kvp => kvp.Key.Name + "--" + kvp.Key.Id + " --" + kvp.Key.Users.Count() + "--" + kvp.Value.SongQueue.Count));
                        Console.WriteLine(output);
                    });
            });
        }

        private async Task QueueSong(CommandEventArgs e, string query, bool silent = false, MusicType musicType = MusicType.Normal) {
            if (e.User.VoiceChannel?.Server != e.Server) {
                if(!silent)
                    await e.Channel.SendMessage("💢 You need to be in a voice channel on this server.\n If you are already in a voice channel, try rejoining.");
                return;
            }
            
            if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
                return;

            query = query.Trim();
            if (musicType != MusicType.Local && IsRadioLink(query)) {
                musicType = MusicType.Radio;
                musicGlobal = "radio";
                query = await HandleStreamContainers(query) ?? query;
            }

            if (musicPlayers.ContainsKey(e.Server) == false) {
                float? vol = null;
                float throwAway;
                if (musicVolumes.TryGetValue(e.Server.Id, out throwAway))
                    vol = throwAway;

                if (!musicPlayers.TryAdd(e.Server, new MusicControls(e.User.VoiceChannel, e, vol))) {
                    await e.Channel.SendMessage("Failed to create a music player for this server.");
                    return;
                }
            }

            var player = musicPlayers[e.Server];

            if (player.SongQueue.Count >= 50) return;

            try {
                var sr = new StreamRequest(e, query, player, musicType);

                if (sr == null)
                    throw new NullReferenceException("StreamRequest is null.");

                Message qmsg = null;
                Message msg = null;
                if (!silent) {
                    qmsg = await e.Channel.SendMessage("🎵 `Searching / Resolving...`");
                    sr.OnResolvingFailed += async (err) => {
                        await qmsg?.Edit($"💢 🎵 `Resolving failed` for **{query}**");
                    };
                    sr.OnQueued += async () => {
                        await qmsg?.Edit($"🎵`Queued`{sr.FullPrettyName}");
                    };
                }
                sr.OnCompleted += async () => {
                    MusicControls mc;
                    if (musicPlayers.TryGetValue(e.Server, out mc)) {
                        if (mc.SongQueue.Count == 0)
                            mc.Stop();
                            musicGlobal = "";
                    }
                    await e.Channel.SendMessage($"🎵`Finished`{sr.FullPrettyName}");
                };
                sr.OnStarted += async () => {
                    var msgTxt = $"🎵`Playing`{sr.FullPrettyName} `Vol: {(int)(player.Volume * 100)}%`";
                    if (msg == null)
                        await e.Channel.SendMessage(msgTxt);
                    else
                        await msg.Edit(msgTxt);
                    qmsg?.Delete();
                };
                sr.OnBuffering += async () => {
                    msg = await e.Channel.SendMessage($"🎵`Buffering...`{sr.FullPrettyName}");
                };
                await sr.Resolve();
            } catch (Exception ex) {
                Console.WriteLine();
                await e.Channel.SendMessage($"💢 {ex.Message}");
                return;
            }
        }

        private bool IsRadioLink(string query) =>
            (query.StartsWith("http") ||
            query.StartsWith("ww")) 
            &&
            (query.Contains(".pls") ||
            query.Contains(".m3u") ||
            query.Contains(".asx") ||
            query.Contains(".xspf"));

        private async Task<string> HandleStreamContainers(string query) {
            string file = null;
            try {
                 file = await SearchHelper.GetResponseAsync(query);
            }
            catch {
                return query;
            }
            if (query.Contains(".pls")) {
                //File1=http://armitunes.com:8000/
                //Regex.Match(query)
                try {
                    var m = Regex.Match(file, "File1=(?<url>.*?)\\n");
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch {
                    Console.WriteLine($"Failed reading .pls:\n{file}");
                    return null;
                }
            }
            else if (query.Contains(".m3u")) {
                /* 
                    # This is a comment
                   C:\xxx4xx\xxxxxx3x\xx2xxxx\xx.mp3
                   C:\xxx5xx\x6xxxxxx\x7xxxxx\xx.mp3
                */
                try {
                    var m = Regex.Match(file, "(?<url>^[^#].*)", RegexOptions.Multiline);
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch {
                    Console.WriteLine($"Failed reading .m3u:\n{file}");
                    return null;
                }

            }
            else if (query.Contains(".asx")) {
                //<ref href="http://armitunes.com:8000"/>
                try {
                    var m = Regex.Match(file, "<ref href=\"(?<url>.*?)\"");
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch {
                    Console.WriteLine($"Failed reading .asx:\n{file}");
                    return null;
                }
            }
            else if (query.Contains(".xspf")) {
                /*
                <?xml version="1.0" encoding="UTF-8"?>
                    <playlist version="1" xmlns="http://xspf.org/ns/0/">
                        <trackList>
                            <track><location>file:///mp3s/song_1.mp3</location></track>
                */
                try {
                    var m = Regex.Match(file, "<location>(?<url>.*?)</location>");
                    var res = m.Groups["url"]?.ToString();
                    return res?.Trim();
                }
                catch {
                    Console.WriteLine($"Failed reading .xspf:\n{file}");
                    return null;
                }
            }

            return query;
        }
    }
}
