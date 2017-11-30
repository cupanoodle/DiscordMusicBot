﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using System.IO;
using TagLib;

namespace DiscordBotTesting
{
    public class Playlist
    {
        public List<PlaylistItem> Songs { get; private set; }

        //if looping position%length
        public int Position { get; private set; }

        public int Length => Songs.Count;
        public PlaylistItem Current => this.Length > 0 ? (this.Position >= this.Length || this.Position < 0 ? null : this.Songs[Position]) : null;
        public bool HasNext => this.Position < this.Length - 1;

        public Playlist()
        {
            this.Songs = new List<PlaylistItem>();
        }

        public Playlist(string[] files)
        {
            this.Songs = new List<PlaylistItem>();

            for (int i = 0; i < files.Length; i++)
                this.Songs.Add(new PlaylistItem(files[i]));
        }

        public async Task Next()
        {
            if (this.HasNext)
                this.Position++;

            await Task.CompletedTask;
        }

        public async Task Previous()
        {
            if (this.Position > 0)
                this.Position--;

            await Task.CompletedTask;
        }

        public async Task Seek(int SongIndex)
        {
            if (SongIndex >= 0 && SongIndex < this.Length)
                this.Position = SongIndex;

            await Task.CompletedTask;
        }

        public async Task Shuffle()
        {
            if (this.Songs.Count > 0)
            {
                PlaylistItem temp = this.Current;

                Random r = new Random();

                this.Songs[0] = temp;

                for (int i = 1; i < this.Songs.Count; i++)
                {
                    int j = r.Next(i, this.Songs.Count);

                    this.Songs.Swap(i, j);
                }

                this.Position = this.Songs.IndexOf(temp);
            }

            await Task.CompletedTask;
        }

        //TODO: append 0s to index if > 10, 100 etc
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            var longest = this.Songs.Select(x => x.Duration).Max();

            for (int i = 0; i < this.Songs.Count; i++)
            {

                //string duration = string.Format();
                string index = string.Format($"{{0,{this.Length / 10 + 1}:#}}/{this.Length}", (i + 1));

                sb.AppendLine($"{(this.Position == i ? ">> " : "   ")}{index} - {this.Songs[i].ToString()}");
            }

            return sb.ToString();
        }

        public string CurrentToString()
        {
            return $"{ this.Position + 1}/{ this.Length} - { this.Current.ToString()}";
        }

        public string Peek()
        {
            StringBuilder sb = new StringBuilder();

            if (this.Length > 0)
            {
                int startIndex = 0;
                int endIndex = 0;

                if (this.Position >= 3)
                {
                    startIndex = this.Position - 2;
                }

                int remaining = this.Length - (this.Position + 1);
                if (remaining >= 5)
                {
                    endIndex = this.Position + 5;
                }
                else
                {
                    endIndex = this.Position + remaining;
                }

                for (int i = startIndex; i <= endIndex; i++)
                {
                    //string duration = string.Format();
                    string index = string.Format($"{{0,{this.Length / 10 + 1}:#}}/{this.Length}", (i + 1));

                    sb.AppendLine($"{(this.Position == i ? ">> " : "   ")}{index} - {this.Songs[i].ToString()}");
                }
            }

            return sb.ToString();
        }
    }

    public class PlaylistItem
    {
        public string Path;
        public Tag Tag;
        public long Position = 0;
        public TimeSpan Duration;

        public PlaylistItem(string path)
        {
            this.Path = path;

            if (System.IO.File.Exists(@path))
            {
                TagLib.File f = TagLib.File.Create(@path);

                if (f?.Tag != null)
                {
                    this.Tag = f.Tag;
                }

                this.Duration = GetMediaDuration(this.Path);
            }
        }

        public override string ToString()
        {
            string artist = this.Tag?.AlbumArtists?.FirstOrDefault() ?? this.Tag?.Performers?.FirstOrDefault();
            if (artist != null)
                artist += " - ";

            return artist != null || this.Tag?.Title != null ? $"{artist}{this.Tag.Title ?? string.Empty}" : this.Path.Split('\\').Last();
        }

        TimeSpan GetMediaDuration(string MediaFilename)
        {
            double duration = 0.0;
            using (FileStream fs = System.IO.File.OpenRead(MediaFilename))
            {
                Mp3Frame frame = Mp3Frame.LoadFromStream(fs);

                while (frame != null)
                {
                    duration += (double)frame.SampleCount / (double)frame.SampleRate;
                    frame = Mp3Frame.LoadFromStream(fs);
                }
            }
            return TimeSpan.FromSeconds(duration);
        }
    }
}