using PASSWORD_LIS_Client.Properties;
using System;
using System.Windows.Media;

namespace PASSWORD_LIS_Client.Services
{
    public class BackgroundMusicService : IDisposable
    {
        private readonly MediaPlayer player;
        private const int MinimumVolume = 0;
        private const int MaximumVolume = 1;
        private bool disposed;


        public double Volume
        {
            get => player.Volume;
            set
            {
                double clamped;
                if (value < MinimumVolume)
                {
                    clamped = MinimumVolume;
                }
                else if (value > MaximumVolume)
                {
                    clamped = MaximumVolume;
                }
                else
                {
                    clamped = value;
                }
                player.Volume = clamped;
                Settings.Default.MusicVolume = clamped;
                Settings.Default.Save();
            }
        }

        public bool IsPlaying { get; private set; }

        public BackgroundMusicService()
        {
            var uriBackGroundMusic = new Uri("Resources/BackgroundMusic.mp3", UriKind.Relative);
            player = new MediaPlayer();
            player.Open(uriBackGroundMusic);
            player.MediaEnded += OnMediaEnded;

            player.Volume = Settings.Default.MusicVolume;
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            player.Position = TimeSpan.Zero;
            player.Play();
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

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                player.MediaEnded -= OnMediaEnded;
                try
                {
                    if (IsPlaying)
                    {
                        player.Stop();
                        IsPlaying = false;
                    }
                }
                finally
                {
                    player.Close();
                }
                
            }

            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
