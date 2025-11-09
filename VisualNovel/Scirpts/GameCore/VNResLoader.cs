using Godot;
using System.Collections.Generic;

namespace VisualNovel
{
    public static class VNResloader
    {
        private static Dictionary<string, WeakRef> _textureCache = [];
        private static Dictionary<string, WeakRef> _audioCache = [];
        public static int CleanupThreshold { get; set; } = 17;

        /// <summary>
        /// 加载 Texture2D 资源。如果缓存中存在有效弱引用，则返回该资源；否则加载新资源并缓存其弱引用。
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <returns>加载的 Texture2D 或 null</returns>
        public static Texture2D LoadTexture2D(string path)
        {
            CleanupInvalidRefs();
            if (_textureCache.TryGetValue(path, out var weakRef))
            {
                // 获取弱引用的实际对象，并尝试转换为目标类型。
                var cachedTex = weakRef.GetRef().As<Texture2D>();

                if (cachedTex != null)
                {
                    return cachedTex;
                }

                // 缓存无效（引用已释放或类型不匹配），从缓存中移除。
                _textureCache.Remove(path);
            }

            var newTex = GD.Load<Texture2D>(path);

            if (newTex != null)
            {
                // 创建一个新的弱引用并缓存它。
                _textureCache[path] = GodotObject.WeakRef(newTex);
            }

            return newTex;
        }

        /// <summary>
        /// 清理指定 Texture2D 资源的缓存条目。如果该资源在缓存中且有效，则释放它并移除缓存。
        /// </summary>
        /// <param name="target">要释放的 Texture2D 资源</param>
        public static void Dispose(Texture2D target)
        {
            // 遍历缓存，查找匹配的路径。
            // 注意：在循环中移除字典条目需要特殊处理，但由于这里使用了 break，所以是安全的。
            string keyToRemove = null;
            foreach (var kvp in _textureCache)
            {
                // 简化类型检查
                var cachedObj = kvp.Value.GetRef();
                if (cachedObj.As<Texture2D>() == target)
                {
                    // 释放资源。
                    target.Free();
                    keyToRemove = kvp.Key;
                    break;
                }
            }

            if (keyToRemove != null)
            {
                // 移除缓存条目。
                _textureCache.Remove(keyToRemove);
            }
        }

        /// <summary>
        /// 加载 AudioStream 资源。如果缓存中存在有效弱引用，则返回该资源；否则加载新资源并缓存其弱引用。
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <returns>加载的 AudioStream 或 null</returns>
        public static AudioStream LoadAudio(string path)
        {
            CleanupInvalidRefs();
            if (_audioCache.TryGetValue(path, out var weakRef))
            {
                var cachedAudio = weakRef.GetRef().As<AudioStream>();

                if (cachedAudio != null)
                {
                    return cachedAudio;
                }

                // 移除无效的弱引用。
                _audioCache.Remove(path);
            }

            var newAudio = GD.Load<AudioStream>(path);

            if (newAudio != null)
            {
                _audioCache[path] = GodotObject.WeakRef(newAudio);
            }

            return newAudio;
        }

        /// <summary>
        /// 清理所有音频资源的缓存。遍历缓存，释放有效资源并清空缓存。
        /// </summary>
        public static void DisposeAllAudio()
        {
            // 遍历所有音频缓存条目。
            foreach (var kvp in _audioCache)
            {
                var cachedObj = kvp.Value.GetRef();
                var audio = cachedObj.As<AudioStream>();
                if (audio != null)
                {
                    // 使用 Free() 释放 Godot 资源。
                    audio.Free();
                }
            }

            // 清空整个音频缓存字典。
            _audioCache.Clear();
        }
    
        public static void CleanupInvalidRefs()
        {
            if (_textureCache.Count + _audioCache.Count < CleanupThreshold) return;

            var keysToRemove = new List<string>();
            foreach (var texKey in _textureCache)
            {
                var texObj = texKey.Value.GetRef();
                if (texObj.As<Texture2D>() == null)
                {
                    keysToRemove.Add(texKey.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _textureCache.Remove(key);
            }

            var audioKeysToRemove = new List<string>();
            foreach (var audioKey in _audioCache)
            {
                var audioObj = audioKey.Value.GetRef();
                if (audioObj.As<AudioStream>() == null)
                {
                    audioKeysToRemove.Add(audioKey.Key);
                }
            }

            foreach (var key in audioKeysToRemove)
            {
                _audioCache.Remove(key);
            }
        }
    }
}