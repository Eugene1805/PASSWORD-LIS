using PASSWORD_LIS_Client.Properties;
using System;
using System.Windows.Media;

namespace PASSWORD_LIS_Client.Services
{
    public class BackgroundMusicService : IDisposable
    {
        private readonly MediaPlayer mediaPlayer;
        private bool disposed;
        private const int MinimumVolume = 0;
        private const int MaximumVolume = 1;
        public double Volume
        {
            get => mediaPlayer.Volume;
            set
            {
                double clampedVolume;
                if (value < MinimumVolume)
                {
                    clampedVolume = MinimumVolume;
                }
                else if (value > MaximumVolume)
                {
                    clampedVolume = MaximumVolume;
                }
                else
                {
                    clampedVolume = value;
                }
                mediaPlayer.Volume = clampedVolume;
                Settings.Default.MusicVolume = clampedVolume;
                Settings.Default.Save();
            }
        }

        public bool IsPlaying { get; private set; }

        public BackgroundMusicService()
        {
            var uriBackGroundMusic = new Uri("Resources/BackgroundMusic.mp3", UriKind.Relative);
            mediaPlayer = new MediaPlayer();
            mediaPlayer.Open(uriBackGroundMusic);
            mediaPlayer.MediaEnded += OnMediaEnded;

            mediaPlayer.Volume = Settings.Default.MusicVolume;
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            mediaPlayer.Position = TimeSpan.Zero;
            mediaPlayer.Play();
        }

        public void Play()
        {
            if (!IsPlaying)
            {
                mediaPlayer.Play();
                IsPlaying = true;
            }
        }

        public void Pause()
        {
            if (IsPlaying)
            {
                mediaPlayer.Pause();
                IsPlaying = false;
            }
        }

        public void Stop()
        {
            mediaPlayer.Stop();
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
                mediaPlayer.MediaEnded -= OnMediaEnded;
                try
                {
                    if (IsPlaying)
                    {
                        mediaPlayer.Stop();
                        IsPlaying = false;
                    }
                }
                finally
                {
                    mediaPlayer.Close();
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
