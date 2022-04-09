using System.IO;
using Phi.Viewer.Audio;

namespace Phi.Viewer
{
    public class OneShotAudio : AnimatedObject
    {
        private AudioPlayer _audioPlayer;
        
        public OneShotAudio(Stream audioStream)
        {
            _audioPlayer = new AudioPlayer();
            audioStream.Seek(0, SeekOrigin.Begin);
            _audioPlayer.LoadFromStream(audioStream);
            _audioPlayer.EnableCompressor();
            _audioPlayer.Play();
        }
        
        public override void Update()
        {
            if (!(_audioPlayer.PlaybackTime >= _audioPlayer.Duration)) return;
            
            _audioPlayer.Stop();
            _audioPlayer.Dispose();
            NotNeeded = true;
        }
    }
}