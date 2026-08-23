using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using SKSSL.Serializing;

namespace SKSSL;

public readonly struct SoundCue
{
    private readonly List<Handle> _sounds = [];

    public SoundCue(params Handle[] sounds)
    {
        _sounds = [..sounds];
    }

    public void Add(Handle sound)
    {
        // Avoid duplicates. All sounds play equally likely, at the moment. No variations.
        // TODO: Add chances?
        if (!_sounds.Contains(sound))
        {
            _sounds.Add(sound);
        }
    }

    public Handle Select()
    {
        if (_sounds.Count == 0)
            throw new InvalidOperationException("Sound cue contains no sounds.");

        return _sounds[Random.Shared.Next(_sounds.Count)];
    }
}