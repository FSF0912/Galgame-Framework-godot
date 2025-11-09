using Godot;
using System.Collections.Generic;

namespace VisualNovel
{
    [GlobalClass]
    public partial class AudioManager : Node
    {
        // 单例实例
        public static AudioManager Instance { get; private set; }

        // 音频总线名称
        private const string MASTER_BUS = "Master";
        private const string BGM_BUS = "BGM";
        private const string SFX_BUS = "SFX";
        private const string VOICE_BUS = "Voice";

        // 音频玩家节点
        private AudioStreamPlayer _bgmPlayer;
        private readonly List<AudioStreamPlayer> _sfxPlayers = new(); // SFX池
        private AudioStreamPlayer2D _voicePlayer;

        [ExportGroup("Volume Settings")]
        // 音量控制（-80到0 dB）
        [Export(PropertyHint.Range, "-80,0,0.1,slider")] public float MasterVolume { get; set; } = 0f;
        [Export(PropertyHint.Range, "-80,0,0.1,slider")] public float BGMVolume { get; set; } = 0f;
        [Export(PropertyHint.Range, "-80,0,0.1,slider")] public float SFXVolume { get; set; } = 0f;
        [Export(PropertyHint.Range, "-80,0,0.1,slider")] public float VoiceVolume { get; set; } = 0f;

        [ExportGroup("SFX Pool Settings")]
        // 池大小配置
        [Export(PropertyHint.Range, "1,10,1,slider")] public int SFXPoolSize { get; set; } = 5;

        // 当前BGM路径
        private string _currentBgmPath = string.Empty;

        public override void _Ready()
        {
            if (Instance != null && Instance != this)
            {
                QueueFree();
                return;
            }
            Instance = this;

            
            _bgmPlayer = new AudioStreamPlayer();
            _bgmPlayer.Bus = BGM_BUS;
            _bgmPlayer.Name = "BGMPlayer";
            AddChild(_bgmPlayer);

            
            for (int i = 0; i < SFXPoolSize; i++)
            {
                var player = new AudioStreamPlayer();
                player.Bus = SFX_BUS;
                player.Name = $"SFXPlayer_{i}";
                AddChild(player);
                _sfxPlayers.Add(player);
            }

            
            _voicePlayer = new AudioStreamPlayer2D();
            _voicePlayer.Bus = VOICE_BUS;
            _voicePlayer.Name = "VoicePlayer";
            AddChild(_voicePlayer);

            
            ApplyVolumes();

            
            _bgmPlayer.Finished += OnBgmFinished;
            _voicePlayer.Finished += OnVoiceFinished;
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        // 应用所有音量到总线
        private void ApplyVolumes()
        {
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(MASTER_BUS), MasterVolume);
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(BGM_BUS), BGMVolume);
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(SFX_BUS), SFXVolume);
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(VOICE_BUS), VoiceVolume);
        }

        public void SetMasterVolume(float volumeDb)
        {
            MasterVolume = Mathf.Clamp(volumeDb, -80f, 0f);
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(MASTER_BUS), MasterVolume);
        }

        public void SetBGMVolume(float volumeDb)
        {
            BGMVolume = Mathf.Clamp(volumeDb, -80f, 0f);
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(BGM_BUS), BGMVolume);
        }

        public void SetSFXVolume(float volumeDb)
        {
            SFXVolume = Mathf.Clamp(volumeDb, -80f, 0f);
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(SFX_BUS), SFXVolume);
        }

        public void SetVoiceVolume(float volumeDb)
        {
            VoiceVolume = Mathf.Clamp(volumeDb, -80f, 0f);
            AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(VOICE_BUS), VoiceVolume);
        }

        public void PlayBGM(string resourcePath, float duration = 1.0f, bool loop = true)
        {
            if (_currentBgmPath == resourcePath && _bgmPlayer.Playing)
            {
                return;
            }

            duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;

            if (_bgmPlayer.Playing)
            {
                StopBGM(duration);
                return;
            }

            var stream = VNResloader.LoadAudio(resourcePath);
            if (stream == null)
            {
                GD.PrintErr($"BGM resource not found: {resourcePath}");
                return;
            }

            _bgmPlayer.Stream = stream;
            _bgmPlayer.Autoplay = false;
            _currentBgmPath = resourcePath;

            if (loop)
            {
                _bgmPlayer.Finished += () => _bgmPlayer.Play();
            }
            else
            {
                _bgmPlayer.Finished -= OnBgmFinished;
            }

            if (duration > 0)
            {
                Tween tween = CreateTween();
                _bgmPlayer.VolumeDb = -80f;
                _bgmPlayer.Play();
                tween.TweenProperty(_bgmPlayer, "volume_db", BGMVolume, duration);
            }
            else
            {
                _bgmPlayer.VolumeDb = BGMVolume;
                _bgmPlayer.Play();
            }
        }

        public void StopBGM(float duration = 0f)
        {
            duration = duration <= 0 ? GlobalSettings.AnimationDefaultTime : duration;
            if (!_bgmPlayer.Playing) return;

            if (duration > 0)
            {
                Tween tween = CreateTween();
                tween.TweenProperty(_bgmPlayer, "volume_db", -80f, duration);
                tween.TweenCallback(Callable.From(() => _bgmPlayer.Stop()));
            }
            else
            {
                _bgmPlayer.Stop();
            }
            _currentBgmPath = string.Empty;
        }

        private void OnBgmFinished()
        {
            GD.Print("BGM播放结束");
        }

        // 播放SFX（从池获取可用玩家，支持并发）
        public void PlaySFX(string resourcePath, float volumeDb = 0f, float pitchScale = 1f)
        {
            var player = GetAvailablePlayer(_sfxPlayers);
            if (player == null)
            {
                GD.PrintErr("SFX pool is full, cannot play more sound effects");
                return;
            }

            var stream = VNResloader.LoadAudio(resourcePath);
            if (stream == null)
            {
                GD.PrintErr($"SFX resource not found: {resourcePath}");
                return;
            }

            player.Stream = stream;
            player.VolumeDb = volumeDb + SFXVolume;
            player.PitchScale = pitchScale;
            player.Play();

            player.Finished += () => RecyclePlayer(player, _sfxPlayers);
        }

        // 获取可用player
        private AudioStreamPlayer GetAvailablePlayer(List<AudioStreamPlayer> pool)
        {
            foreach (var player in pool)
            {
                if (!player.Playing)
                {
                    return player;
                }
            }
            return null;
        }

        // 回收player
        private void RecyclePlayer(AudioStreamPlayer player, List<AudioStreamPlayer> pool)
        {
            player.Stop();
            player.Stream = null;
            player.PitchScale = 1f; // 重置
            player.Finished -= () => RecyclePlayer(player, pool);
        }

        // PlayVoice（单一空间节点，默认中心位置；传 Vector2 指定 [-1,1] 位置，不变）
        public void PlayVoice(string resourcePath, Vector2? normalizedPosition = null, float volumeDb = 0f)
        {
            if (_voicePlayer.Playing)
            {
                GD.PushWarning($"VoicePlayer is buzy, ignore audio {resourcePath}");
                return;
            }

            var stream = VNResloader.LoadAudio(resourcePath);
            if (stream == null)
            {
                GD.PrintErr($"Voice资源未找到: {resourcePath}");
                return;
            }

            // 位置：默认中心 (0,0)，或指定
            _voicePlayer.GlobalPosition = normalizedPosition.HasValue 
                ? MapNormalizedToWorld(normalizedPosition.Value) 
                : Vector2.Zero;

            _voicePlayer.Stream = stream;
            _voicePlayer.VolumeDb = volumeDb + VoiceVolume;
            _voicePlayer.Play();
        }

        private void OnVoiceFinished()
        {
            _voicePlayer.Stop();
            _voicePlayer.Stream = null;
            _voicePlayer.GlobalPosition = Vector2.Zero; // 重置位置
        }

        // 私有：位置映射（不变）
        private Vector2 MapNormalizedToWorld(Vector2 normPos)
        {
            var viewport = GetViewport();
            var rect = viewport.GetVisibleRect();
            var size = rect.Size;

            float x = Mathf.Lerp(-size.X / 2f, size.X / 2f, (normPos.X + 1f) / 2f);
            float y = Mathf.Lerp(-size.Y / 2f, size.Y / 2f, (normPos.Y + 1f) / 2f);

            return new Vector2(x, y);
        }

        // 暂停所有音频
        public void PauseAll()
        {
            _bgmPlayer.StreamPaused = true;
            foreach (var player in _sfxPlayers) player.StreamPaused = true;
            _voicePlayer.StreamPaused = true;
        }

        // 恢复所有音频
        public void ResumeAll()
        {
            _bgmPlayer.StreamPaused = false;
            foreach (var player in _sfxPlayers) player.StreamPaused = false;
            _voicePlayer.StreamPaused = false;
        }

        // 停止所有SFX
        public void StopAllSFX()
        {
            foreach (var player in _sfxPlayers)
            {
                player.Stop();
                RecyclePlayer(player, _sfxPlayers);
            }
        }

        // 停止所有Voice
        public void StopVoice()
        {
            _voicePlayer.Stop();
            OnVoiceFinished();
        }
    }
}