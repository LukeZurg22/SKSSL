using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using FMOD;
using FmodForFoxes;
using Microsoft.Xna.Framework;
using SKSSL.Serializing;
using Channel = FmodForFoxes.Channel;
using ChannelGroup = FmodForFoxes.ChannelGroup;
using Sound = FmodForFoxes.Sound;

namespace SKSSL;

/// <summary>
/// Using FMOD requires a display of the FMOD logo to stay legal.
/// </summary>
/// <references>
/// This furry has made things significantly easier for me. Why is it that furries make for excellent programmers,
/// I do not know.
/// https://github.com/Martenfur/FmodForFoxes
/// </references>
public class SoundManager
{
    private static Channel FoxChannel = new();
    private static ChannelGroup FoxChannelGroup = new ChannelGroup("");
    private const double timerMilli = 0.005;
    private const float tolerance = 0.01f;

    private static SoundInstance? _currentSong = new();
    private static readonly Dictionary<handle, List<SoundInstance>> _playingSounds = new();
    private readonly Dictionary<handle, (FileInfo, string relative)> _handleToFile = new();

    #region Registration

    /// <summary>
    /// Sounds have different root directories (aka, 
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="info"></param>
    /// <param name="build"></param>
    public void RegisterSound(handle handle, FileInfo info, string build)
    {
        _handleToFile[handle] = (info, build);
    }

    #endregion

    public void PlaySound(
        handle handle,
        bool allowMultiple = true,
        bool loop = false,
        float volume = 1.0f,
        double startTimeDelay = 0.0,
        double fadeInTime = 0.0)
    {
        if (!allowMultiple && _playingSounds.TryGetValue(handle, out var value) && value.Count > 0)
        {
            // If multiple instances are not allowed and the sound is already playing, do nothing
            return;
        }

        // TODO: Implement caching.

        var build = _handleToFile[handle].relative;
        FileLoader.RootDirectory = build;
        Stream? stream = TitleContainer.OpenStream(Path.Combine(FileLoader.RootDirectory, handle));
        var buffer = FileLoader.LoadFileAsBuffer(stream);
        Sound sound = CoreSystem.LoadStreamedSound(buffer);
        sound.Play(FoxChannelGroup, true);

        // Set looping
        FoxChannelGroup.Mode = loop ? MODE.LOOP_NORMAL : MODE.LOOP_OFF;

        // Track the playing sound
        if (!_playingSounds.TryGetValue(handle, out var value1))
        {
            value1 = [];
            _playingSounds[handle] = value1;
        }

        SoundInstance instance = CreateSoundInstance(FoxChannel, volume, startTimeDelay, fadeInTime);
        value1.Add(instance);
    }

    public void PlaySong(
        handle Handle,
        float volume = 1.0f,
        double startTimeDelay = 0.0,
        double fadeInTime = 0.0)
    {
        _currentSong?.Channel.Stop();

        // TODO: Implement caching.
        FileInfo info = _handleToFile[Handle].Item1;
        Sound sound = CoreSystem.LoadStreamedSound(info.FullName);
        sound.Play(FoxChannelGroup, true);
        _currentSong = CreateSoundInstance(FoxChannel, volume, startTimeDelay, fadeInTime);
    }

    public static void StopSound(handle Handle, double fadeOutTimeMilli = 0)
    {
        if (!_playingSounds.TryGetValue(Handle, value: out var sound))
            return;
        foreach (SoundInstance soundInstance in sound)
            ApplyFadeOutOrStop(soundInstance, fadeOutTimeMilli);
    }

    public static void StopSong(double fadeOutTimeMilli = 0)
    {
        if (_currentSong != null) ApplyFadeOutOrStop(_currentSong, fadeOutTimeMilli);
    }

    public void StopAllSounds()
    {
        foreach (var pair in _playingSounds)
        foreach (SoundInstance sound in pair.Value)
        {
            sound.Channel.Stop();
        }

        _playingSounds.Clear();
    }

    public void Update()
    {
        UpdatePlayingSounds(timerMilli);
        UpdateCurrentSong(timerMilli);
    }

    private static void UpdatePlayingSounds(double currentTimeMilli)
    {
        foreach (handle Handle in _playingSounds.Keys.ToList())
        {
            _playingSounds[Handle].RemoveAll(inst => HandleSoundInstance(inst, currentTimeMilli));
            if (_playingSounds[Handle].Count == 0)
            {
                _playingSounds.Remove(Handle);
            }
        }
    }

    private static void UpdateCurrentSong(double currentTimeMilli)
    {
        if (_currentSong == null) return;
        if (HandleSoundInstance(_currentSong, currentTimeMilli)) _currentSong = null;
    }

    private SoundInstance CreateSoundInstance(
        Channel channel,
        float volume,
        double startTimeDelay,
        double fadeInTime)
    {
        var instance = new SoundInstance
        {
            Channel = channel,
            StartTime = timerMilli + startTimeDelay,
            FadeInTime = fadeInTime,
            TargetVolume = volume,
            FadeStartTime = timerMilli,
            HasPlayed = false
        };

        channel.Volume = 0;
        channel.Paused = true;

        return instance;
    }

    private static void ApplyFadeOutOrStop(SoundInstance instance, double fadeOutTimeMilli)
    {
        if (fadeOutTimeMilli > tolerance)
        {
            instance.FadeOutTime = fadeOutTimeMilli;
            instance.FadeStartTime = timerMilli;
        }
        else
        {
            instance.Channel.Stop();
        }
    }

    private static bool HandleSoundInstance(SoundInstance instance, double currentTimeMilli)
    {
        var isPlaying = instance.Channel.IsPlaying;
        if (instance.HasPlayed)
        {
            if (isPlaying)
                return instance.FadeOutTime > tolerance && ApplyFadeOut(instance, currentTimeMilli);
            instance.Channel.Stop();
            return true;
        }

        if (!(currentTimeMilli >= instance.StartTime))
            return false;
        if (instance.FadeInTime > tolerance)
        {
            double elapsedTime = currentTimeMilli - instance.StartTime;
            if (elapsedTime < instance.FadeInTime)
            {
                float newVolume = instance.TargetVolume * (float)(elapsedTime / instance.FadeInTime);
                instance.Channel = instance.Channel with { Volume = (newVolume) };
            }
            else
            {
                instance.Channel = instance.Channel with { Volume = (instance.TargetVolume) };
                instance.HasPlayed = true;
                instance.Channel = instance.Channel with { Paused = false };
            }
        }
        else
        {
            instance.Channel = instance.Channel with { Volume = (instance.TargetVolume) };
            instance.HasPlayed = true;
            instance.Channel = instance.Channel with { Paused = false };
        }

        return false;
    }

    private static bool ApplyFadeOut(SoundInstance instance, double currentTimeMilli)
    {
        double elapsedTime = currentTimeMilli - instance.FadeStartTime;
        if (!(elapsedTime < instance.FadeOutTime))
            return true;
        float newVolume = instance.TargetVolume * (float)(1.0 - elapsedTime / instance.FadeOutTime);
        instance.Channel = instance.Channel with { Volume = (newVolume) };
        return false;
    }

    [SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
    public class SoundInstance
    {
        public Channel Channel { get; set; }
        public double StartTime { get; set; }
        public bool HasPlayed { get; set; }
        public double FadeInTime { get; set; }
        public double FadeOutTime { get; set; }
        public double FadeStartTime { get; set; }
        public float TargetVolume { get; set; }
    }
}