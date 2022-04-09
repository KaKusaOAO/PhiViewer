using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ManagedBass;
using ManagedBass.Fx;
using Timer = System.Timers.Timer;

namespace Phi.Viewer.Audio
{
    public class AudioPlayer : IDisposable
    {
        static AudioPlayer()
        {
            Bass.Init();
        }

        private int _channelHandle;
        private int _compressorHandle;

        private float _playbackRate = 1;
        private float _playbackPitch;
        private float _volume = 1;
        private bool _playing;

        private float _preStartTime;
        private bool _isWaitingForPreStart;
        private Stopwatch _preStartStopwatch = new Stopwatch();
        private Timer _preStartTimer;

        public float PlaybackRate
        {
            get => _playbackRate;
            set => SetPlaybackRate(value);
        }

        public float PlaybackPitch
        {
            get => _playbackPitch;
            set => SetPlaybackPitch(value);
        }

        public float Volume
        {
            get => _volume;
            set => SetVolume(value);
        }

        public bool IsPlaying => _playing;

        public bool SyncSpeedAndPitch { get; set; }
        
        public float Duration =>
            (float) Bass.ChannelBytes2Seconds(_channelHandle, Bass.ChannelGetLength(_channelHandle)) * 1000f;

        public float PlaybackTime => InternalGetPlayTime();

        private float InternalGetPlayTime()
        {
            if (_isWaitingForPreStart)
                return (float) _preStartStopwatch.Elapsed.TotalMilliseconds + _preStartTime;
            return (float) Bass.ChannelBytes2Seconds(_channelHandle, Bass.ChannelGetPosition(_channelHandle)) * 1000f;
        }
        
        public void LoadFromPath(string path)
        {
            var stream = new FileStream(path, FileMode.Open);
            LoadFromStream(stream);
        }
        
        public void LoadFromStream(Stream stream)
        {
            Bass.StreamFree(_channelHandle);

            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, (int) stream.Length);
            
            var audio = Bass.CreateStream(buffer, 0, stream.Length, BassFlags.Decode);
            var err = Bass.LastError;
            if (err != Errors.OK)
            {
                throw new Exception("Failed to create audio stream! " + err);
            }
            
            var fx = BassFx.TempoCreate(audio, BassFlags.FxFreeSource);

            err = Bass.LastError;
            if (err != Errors.OK)
            {
                throw new Exception("Failed to create audio stream! " + err);
            }
            
            _channelHandle = fx;
            SetPlaybackPitch(_playbackPitch);
            SetPlaybackRate(_playbackRate);
            SetVolume(_volume);
        }

        public void EnableCompressor()
        {
            if (_compressorHandle == 0)
            {
                _compressorHandle = Bass.ChannelSetFX(_channelHandle, EffectType.Compressor, 0);
                var param = new CompressorParameters();
                Bass.FXGetParameters(_compressorHandle, param);
                param.fAttack = 0;
                Bass.FXSetParameters(_compressorHandle, param);
            }
        }

        public void DisableCompressor()
        {
            Bass.ChannelRemoveFX(_channelHandle, _compressorHandle);
            _compressorHandle = 0;
        }

        public void Seek(float time)
        {
            var pos = Bass.ChannelSeconds2Bytes(_channelHandle, time / 1000);
            Bass.ChannelSetPosition(_channelHandle, pos);
        }
        
        public void Play(float from = 0)
        {
            if (from < 0)
            {
                _isWaitingForPreStart = true;
                _preStartStopwatch.Restart();
                _preStartTime = from;
                
                var timer = new Timer();
                timer.Interval = -from;
                timer.Elapsed += (o, e) =>
                {
                    _isWaitingForPreStart = false;
                    timer.Stop();
                    Play();
                };
                timer.Start();
                _preStartTimer = timer;

                _playing = true;
                return;
            }
            Seek(from);
            Bass.ChannelPlay(_channelHandle);

            _playing = true;
        }

        public void Stop()
        {
            if (_isWaitingForPreStart)
            {
                _preStartTimer?.Stop();
                _preStartTimer = null;
            }
            Bass.ChannelStop(_channelHandle);
            _playing = false;
        }

        public void SetPlaybackRate(float rate)
        {
            Bass.ChannelSetAttribute(_channelHandle, ChannelAttribute.Tempo, (rate - 1) * 100);
            _playbackRate = rate;

            if (SyncSpeedAndPitch)
            {
                var semitones = MathF.Log(rate, 2) * 12;
                Bass.ChannelSetAttribute(_channelHandle, ChannelAttribute.Pitch, semitones);
                _playbackPitch = semitones;
            }
        }
        
        public void SetVolume(float volume)
        {
            Bass.ChannelSetAttribute(_channelHandle, ChannelAttribute.Volume, volume);
            _volume = volume;
        }

        public void SetPlaybackPitch(float semitones)
        {
            Bass.ChannelSetAttribute(_channelHandle, ChannelAttribute.Pitch, semitones);
            _playbackPitch = semitones;
            
            if (SyncSpeedAndPitch)
            {
                var rate = MathF.Pow(2, semitones / 12);
                Bass.ChannelSetAttribute(_channelHandle, ChannelAttribute.Tempo, (rate - 1) * 100);
                _playbackRate = rate;
            }
        }

        public void Dispose()
        {
            Bass.StreamFree(_channelHandle);
        }
    }
}