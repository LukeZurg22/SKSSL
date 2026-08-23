using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using SKSSL.Serializing;

namespace SKSSL;

/// <summary>
/// Using FMOD requires a display of the FMOD logo to stay legal.
/// </summary>
/// <references>
/// This furry has made things significantly easier for me. Why is it that furries make for excellent programmers,
/// I do not know.
/// https://github.com/Martenfur/FmodForFoxes
/// </references>
public class SoundManager : IUpdateable
{
    ///private static Channel FoxChannel = new();
    ///private static ChannelGroup FoxChannelGroup = new("Default");
    ///private const double timerMilli = 0.005;
    ///private const float tolerance = 0.01f;
    //private static SoundInstance? _currentSong = new();
    //private static readonly Dictionary<Handle, List<SoundInstance>> _playingSounds = new();
    private SoundEffectInstance? _currentMusic;

    private Handle? _currentMusicHandle;

    /// <summary>
    /// For storing handles to functional sound files.
    /// </summary>
    private readonly Dictionary<Handle, FileInfo> _handleToFile = new();

    /// <summary>
    /// Categorize sounds by custom inserted enum types.
    /// </summary>
    private readonly Dictionary<Enum, List<Handle>> _soundCategories = [];

    private readonly Dictionary<Handle, SoundCue> _soundCues = [];
    private readonly Dictionary<Handle, List<SoundEffectInstance>> _instances = [];

    private static float _targetVolume = 1f;
    private static float _currentVolume = 1f;
    private static bool _playMusic = true;

    #region Registration

    /// <summary>
    /// Sounds have different root directories (aka, 
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="info"></param>
    // TODO: Accompany this with some extra file info which should be paired with the sfx.
    //  If not present, then assume it's alone by default?
    public void RegisterSound(Handle handle, FileInfo info)
    {
        _handleToFile[handle] = info;
    }

    /// <summary>
    /// Assigns a sound as the Cue of another sound, such that it may be played if the parent is attempted to play.
    /// </summary>
    /// <param name="current"></param>
    /// <param name="parent"></param>
    // WARN: There's no way for the end-user to assign these sounds directly.
    //  Some kind of file processing and dictating will be needed so the end-user can deal with this..
    public void AssignSoundAsSubCue(Handle current, Handle parent)
    {
        _soundCues[parent].Add(current);
    }

    /// <summary>
    /// Allows one to categorize sounds to Stop-All, later.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="category"></param>
    // TODO: This should probably also be in file information, but the end-user does  NOT programatically know of
    //  any enums. The developer *does*, though, and can probably share the categories on their own. Then the
    //  files can be serialized ENUM values similar to prototypes.
    public void LabelSoundAsCategory(Handle handle, Enum category)
    {
        if (!_soundCategories.TryGetValue(category, out var instances))
            _soundCategories[category] = instances = [];

        if (instances.Contains(handle))
        {
            Log(new Exception($"Attempted to add existing Sound handle \'{handle}\' is the \'{category.ToString()}\' " +
                              $"sound categories list."));
            return;
        }

        instances.Add(handle);
    }

    #endregion


    public void Play(
        Handle handle,
        bool allowMultiple = true,
        bool loop = false,
        float volume = 1.0f,
        double startTimeDelay = 0.0,
        double fadeInTime = 0.0)
    {
        CleanupInstances(handle);

        // Prevent duplicates.
        if (!allowMultiple && _instances.ContainsKey(handle))
            return;

        SoundEffect sound = LoadSound(handle);

        SoundEffectInstance instance = sound.CreateInstance();
        instance.IsLooped = loop;
        instance.Volume = fadeInTime > 0 ? 0f : volume;
        instance.Play();

        if (!_instances.TryGetValue(handle, out var instances))
            _instances[handle] = instances = [];

        instances.Add(instance);
    }

    /// <summary>
    /// Stop all instances of a handle.
    /// </summary>
    /// <param name="handle"></param>
    public void Stop(Handle handle)
    {
        foreach (SoundEffectInstance instance in _instances[handle])
            instance.Stop();
    }

    /// <summary>
    /// Stop all sound.
    /// </summary>
    public void StopAll(Enum? category = null)
    {
        // If no category provided, assume stopping ALL sounds.
        if (category == null)
        {
            foreach (var instanceKVP in _instances)
            {
                StopAll(instanceKVP.Value);
                CleanupInstances(instanceKVP.Key);
            }

            return;
        }

        // If category provided, stop all sounds within that category. Easy-peasy.
        foreach (Handle sound in _soundCategories[category])
        {
            if (!_instances.TryGetValue(sound, out var instanceList))
                continue;
            StopAll(instanceList);
            CleanupInstances(sound);
        }

        MediaPlayer.Stop();
    }

    /// <summary>
    /// Stop all active instances in a list.
    /// </summary>
    /// <param name="instances"></param>
    private static void StopAll(List<SoundEffectInstance> instances)
    {
        foreach (SoundEffectInstance instance in instances) instance.Stop();
    }

    public void PlayMusic(Handle handle, bool loop = true, float volume = 1f)
    {
        // TODO: Ease out the current music if it's there, ease in the new music.
        //  May require an additional song.
        //  All songs loop, unless they don't.
        _currentMusic?.Stop();
        _currentMusic?.Dispose();

        SoundEffect sound = LoadSound(handle);

        _currentMusic = sound.CreateInstance();
        _currentMusic.IsLooped = loop;
        _currentMusic.Volume = volume;
        _currentMusic.Play();

        _currentMusicHandle = handle;
    }

    /// <summary>
    /// Loads a sound effect from a handle, using a Cue's sounds at random if necessary.
    /// </summary>
    /// <param name="handle"></param>
    /// <returns></returns>
    private SoundEffect LoadSound(Handle handle)
    {
        if (!_soundCues.TryGetValue(handle, out SoundCue cue))
        {
            using FileStream stream = _handleToFile[handle].OpenRead();
            SoundEffect? sound = SoundEffect.FromStream(stream);
            cue = new SoundCue(sound);
            _soundCues.Add(handle, cue);
            return cue.Select();
        }

        return cue.Select();
    }

    // TODO: add things like
    //  randomization,
    //  cooldowns,
    //  concurrency limits,
    //  attenuation,
    //  fade-in/out,
    //  extra parameters

    public void Update(GameTime gameTime)
    {
        if (!Enabled)
            return;
        //@formatter:off
        foreach (var instances in _instances.Values)
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            if (instances[i].State != SoundState.Stopped)
                continue;
            instances[i].Dispose();
            instances.RemoveAt(i);
        }
        //@formatter:on
    }

    private void CleanupInstances(Handle handle)
    {
        if (!_instances.TryGetValue(handle, out var instances))
            return;

        for (int i = instances.Count - 1; i >= 0; i--)
        {
            if (instances[i].State != SoundState.Stopped)
                continue;
            instances[i].Dispose();
            instances.RemoveAt(i);
        }

        if (instances.Count == 0)
            _instances.Remove(handle);
    }

    public bool Enabled { get; internal set; }
    public int UpdateOrder => 0;
    public event EventHandler<EventArgs>? EnabledChanged;
    public event EventHandler<EventArgs>? UpdateOrderChanged;
}