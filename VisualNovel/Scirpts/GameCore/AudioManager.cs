using Godot;
using System;

namespace VisualNovel
{
    [GlobalClass]
    public partial class AudioManager : Node
    {
        public static AudioManager Instance { get; private set; }
        [Signal] public delegate void VoiceCompleteEventHandler();
        private const string MASTER_BUS = "Master";
        private const string BGM_BUS = "BGM";
        private const string SFX_BUS = "SFX";
        private const string VOICE_BUS = "Voice";

        private AudioStreamPlayer _bgmPlayer;
        private AudioStreamPlayer2D _voicePlayer;
        private Tween _bgmTween;

        private const float PAN_SPREAD_DISTANCE = 600f;

        [ExportGroup("Volume Settings")]
        [Export(PropertyHint.Range, "-80,0,0.1,slider")] public float MasterVolume { get; set; } = 0f;
        [Export(PropertyHint.Range, "-80,0,0.1,slider")] public float BGMVolume { get; set; } = 0f;
        [Export(PropertyHint.Range, "-80,0,0.1,slider")] public float SFXVolume { get; set; } = 0f;
        [Export(PropertyHint.Range, "-80,0,0.1,slider")] public float VoiceVolume { get; set; } = 0f;

        private string _currentBgmPath = string.Empty;

        public override void _Ready()
        {
            if (Instance != null && Instance != this) { QueueFree(); return; }
            Instance = this;

            _bgmPlayer = new AudioStreamPlayer { Name = "BGMPlayer", Bus = BGM_BUS };
            AddChild(_bgmPlayer);

            _voicePlayer = new AudioStreamPlayer2D { 
                Name = "VoicePlayer", 
                Bus = VOICE_BUS,
                MaxDistance = 20000f,
                Attenuation = 0f,
                PanningStrength = 1f 
            };
            AddChild(_voicePlayer);
            _voicePlayer.Finished += () => EmitSignal(SignalName.VoiceComplete);

            ApplyVolumes();
        }

        public void ApplyVolumes()
        {
            SetBusDb(MASTER_BUS, MasterVolume);
            SetBusDb(BGM_BUS, BGMVolume);
            SetBusDb(SFX_BUS, SFXVolume);
            SetBusDb(VOICE_BUS, VoiceVolume);
        }

        private void SetBusDb(string busName, float db)
        {
            int index = AudioServer.GetBusIndex(busName);
            if (index != -1) AudioServer.SetBusVolumeDb(index, db);
        }

        public void PlaySFX(string resourcePath, float volumeOffset = 0f, float pitchScale = 1f)
        {
            var stream = GD.Load<AudioStream>(resourcePath);
            if (stream == null) return;

            var sfxPlayer = new AudioStreamPlayer {
                Stream = stream,
                Bus = SFX_BUS,
                VolumeDb = volumeOffset,
                PitchScale = pitchScale
            };

            sfxPlayer.Finished += sfxPlayer.QueueFree;
            AddChild(sfxPlayer);
            sfxPlayer.Play();
        }

        //set audio loop in AudioStream resource.
        public void PlayVoice(string resourcePath, float balance = 0f, float volumeOffset = 0f)
        {
            if (_voicePlayer.Playing) _voicePlayer.Stop();

            var stream = GD.Load<AudioStream>(resourcePath);
            
            if (stream == null) return;

            // 根据 balance 计算位置，实现声道偏移
            var center = GetViewport().GetVisibleRect().Size / 2f;
            _voicePlayer.GlobalPosition = center + new Vector2(balance * PAN_SPREAD_DISTANCE, 0);
            
            _voicePlayer.Stream = stream;
            _voicePlayer.VolumeDb = volumeOffset;
            _voicePlayer.Play();
        }

        public void StopVoice()
        {
            if (_voicePlayer.Playing) _voicePlayer.Stop();
        }

        public void PlayBGM(string resourcePath, float fadeDuration = 1.0f)
        {
            if (_currentBgmPath == resourcePath) return;
            if (_bgmPlayer.Playing) StopBGM(fadeDuration, () => PlayBGM(resourcePath, fadeDuration));

            var stream = GD.Load<AudioStream>(resourcePath);
            if (stream == null) return;

            if (_bgmTween != null && _bgmTween.IsValid()) _bgmTween.Kill();

            _bgmPlayer.Stream = stream;
            _bgmPlayer.VolumeDb = -80f;
            _bgmPlayer.Play();
            _currentBgmPath = resourcePath;

            _bgmTween = CreateTween();
            _bgmTween.TweenProperty(_bgmPlayer, "volume_db", 0f, fadeDuration);
        }

        public void StopBGM(float fadeDuration = 1.0f, Action callback = null)
        {
            if (_bgmTween != null && _bgmTween.IsValid()) _bgmTween.Kill();
            _bgmTween = CreateTween();
            _bgmTween.TweenProperty(_bgmPlayer, "volume_db", -80f, fadeDuration);
            _bgmTween.TweenCallback(Callable.From(() => 
            { 
                _bgmPlayer.Stop(); 
                _currentBgmPath = string.Empty; 
                callback?.Invoke();     
            }));
        }
    }
}