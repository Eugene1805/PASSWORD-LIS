using PASSWORD_LIS_Client.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PASSWORD_LIS_Client.Services
{
    public class BackgroundMusicService : IDisposable
    {
        private readonly MediaPlayer player;

        public double Volume
        {
            get => player.Volume;
            set
            {
                var clamped = value < 0 ? 0 : (value > 1 ? 1 : value);
                player.Volume = clamped;
                Settings.Default.MusicVolume = clamped;
                Settings.Default.Save();
            }
        }

        public bool IsPlaying { get; private set; }

        public BackgroundMusicService()
        {
            player = new MediaPlayer();
            player.Open(new Uri("Resources/BackgroundMusic.mp3", UriKind.Relative));
            player.MediaEnded += (s, e) =>
            {
                player.Position = TimeSpan.Zero;
                player.Play();
            };

            player.Volume = Settings.Default.MusicVolume;
        }

        public void Play()
        {
            if (!IsPlaying)
            {
                player.Play();
                IsPlaying = true;
            }
        }

        public void Pause()
        {
            if (IsPlaying)
            {
                player.Pause();
                IsPlaying = false;
            }
        }

        public void Stop()
        {
            player.Stop();
            IsPlaying = false;
        }

        public void Dispose()
        {
            player.Close();
        }
    }
}
