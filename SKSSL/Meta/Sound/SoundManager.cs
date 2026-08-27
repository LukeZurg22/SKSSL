using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using SKSSL.Extensions;
using SKSSL.Serializing;

namespace SKSSL.Sound;

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
    
    // WIP: Improve this.
    private readonly Dictionary<Handle, SoundEffect> _soundEffectCache = new();

    /// <summary>
    /// Categorize sounds by custom inserted enum types. Keyed by category names.
    /// </summary>
    private readonly Dictionary<string, List<Handle>> _soundCategories = [];

    private readonly Dictionary<Handle, SoundCue> _soundCues = [];
    private readonly Dictionary<Handle, List<SoundEffectInstance>> _instances = [];

    private static float _targetVolume = 1f;
    private static float _currentVolume = 1f;
    private static bool _playMusic = true;

    #region Registration

    /// <summary>
    /// Sounds have different root directories (aka, 
    /// </summary>
    /// <param name="handle">Relative path of a file; shortname for organization.</param>
    /// <param name="info">File info to a sound's handle.</param>
    /// <param name="parent">Used to handle Cue creation order.</param>
    /// <param name="category">Allows interacting with multiple sounds in one category. Always lowercased.</param>
    public void RegisterSound(Handle handle, FileInfo info, Handle? parent, string? category)
    {
        // Put to the general registry. Assume handle is normalized.
        _handleToFile[handle] = info;

        // Handle the categorization of this handle.
        if (category != null)
        {
            // Set category to lower-case.
            category = category.ToLowerInvariant();

            // Ensure the category is present.
            if (!_soundCategories.ContainsKey(category))
                _soundCategories.Add(category, []);

            // Prevent a handle from being added more than once.
            if (!_soundCategories[category].Contains(handle))
                _soundCategories[category].Add(handle);
        }
        else _soundCategories[string.Empty].Add(handle); // Add the handle to a blank category anyway ig.


        // Add all relevant sub-handles to handle. Sound Cue handle is still top-brass. Each has a pointer to a file.
        // The file is assumed valid by now. Else, why was it read and put here with a handle?

        // Handle relational handling between parent and child handles with assurances that a cue is made.
        if (parent != null && _soundCues.TryGetValue(parent.Value, out SoundCue cue))
            cue.Add(handle); // Has a parent handle, and so must be assigned as a child entry.
        else if (parent == null && !_soundCues.ContainsKey(handle))
            _soundCues.Add(handle, new SoundCue(handle)); // No parent means this handle IS the parent.
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
        handle = handle.AsNormalizedPath();
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
        handle = handle.AsNormalizedPath();
        foreach (SoundEffectInstance instance in _instances[handle])
            instance.Stop();
    }

    /// <summary>
    /// Stop all sound.
    /// </summary>
    public void StopAll(string category = "")
    {
        // If no category provided, assume stopping ALL sounds.
        if (string.IsNullOrEmpty(category))
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
        handle = handle.AsNormalizedPath();

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
    /// <param name="handle">
    /// A (non)normalized handle. This is auto-normalized if not.
    /// Default library-provided classes auto-normalize handles in registries that use handles for objects with
    /// filepaths.
    /// </param>
    /// <returns></returns>
    private SoundEffect LoadSound(Handle handle)
    {
        handle = handle.Value.NormalizePath(); // Normalize the handle.

        // If Handle contains sub-sounds, then randomly select one and use that instead,
        // which includes this handle. Otherwise, it assumes the handle alone is fine.
        if (_soundCues.TryGetValue(handle, out SoundCue instances))
            handle = instances.Select();

        // All handles, even variants are stored in the Handle->File storage, so this will always work.
        using FileStream stream = _handleToFile[handle].OpenRead();
        SoundEffect? sound = SoundEffect.FromStream(stream);
        return sound;
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
        foreach (var instances in _instances.Values)
            Clean(instances);
    }

    /// <summary>
    /// Clean all instances of a specific handle. Does not Stop existing instances.
    /// </summary>
    /// <param name="handle"></param>
    private void CleanupInstances(Handle handle)
    {
        if (!_instances.TryGetValue(handle, out var instances))
            return;
        Clean(instances);
        if (instances.Count == 0)
            _instances.Remove(handle);
    }

    private static void Clean(List<SoundEffectInstance> instances)
    {
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            if (instances[i].State != SoundState.Stopped)
                continue;
            instances[i].Dispose();
            instances.RemoveAt(i);
        }
    }

    public bool Enabled { get; internal set; }
    public int UpdateOrder => 0;
    public event EventHandler<EventArgs>? EnabledChanged;
    public event EventHandler<EventArgs>? UpdateOrderChanged;
}