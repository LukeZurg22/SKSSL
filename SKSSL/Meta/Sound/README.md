Sounds are inserted into your game's / mod's "sounds" or "sound" folder.

This folder may contain sub-directories. Each of these directories contains sound files, and/or other
sub-directories.

Every sub-directory may contain a meta file (default is `meta.yml`) that will dictate the category and meta
information of the files they are paired with.
> Though this isn't required, it is recommended for sounds with variations and dedicated categories.
>
> Those two properties interact with SKSSL's sound manager, and make sound management much easier.

```yaml
# Example of a Meta File
- sound: my_sound_handle
  category: "my_custom_category"
  variants:
    - "other_handle_A"
    - "other_handle_B"
    - "other_handle_C"
- sound: my_other_handle
  category: "other_category"
  variants:
    - "other_handle_D"
...
```

### Limitations

1. Variants _must_ be handles of sound files within the current folder. As of current, they do not work with nested
   directories even if a path is provided.

2. Categories will default to the name of folder the file is contained-in if not provided.

3. All variants defined below a sound will assume the parent's category.

4. Sound handles must be in lower case, and normalized (`/` instead of `\\`). The normalization is automatically
   applied on load, and in later callbacks.

5. Sounds defined as variants do not need to—and should never—be redefined in new sound entries.
   This may cause unexpected behaviour.