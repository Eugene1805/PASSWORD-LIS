using PASSWORD_LIS_Client.Properties;
using System;
using System.Windows.Media;

namespace PASSWORD_LIS_Client.Services
{
    public class BackgroundMusicService : IDisposable
    {
        private readonly MediaPlayer player;
        private bool disposed;

        public double Volume
        {
            get => player.Volume;
            set
            {
                double clamped;
                if (value < 0)
                {
                    clamped = 0;
                }
                else if (value > 1)
                {
                    clamped = 1;
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
            player = new MediaPlayer();
            player.Open(new Uri("Resources/BackgroundMusic.mp3", UriKind.Relative));
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
                // unsubscribe events and release managed resources
                player.MediaEnded -= OnMediaEnded;
                try
                {
                    if (IsPlaying)
                    {
                        player.Stop();
                        IsPlaying = false;
                    }
                }
                catch
                {
                    // Ignore exceptions during shutdown/cleanup
                }
                player.Close();
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
