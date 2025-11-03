# Hex-Serializer
### IMPORTANT: This tool was made for [Dual Attorneys](https://discord.gg/phECHVHCDe). It's only built to handle what we needed for the game.
**ALSO REMEMBER: This is mostly a portfolio piece rather than a general use library.**

This serializer turns the contents of a binary file into an instance of a class (and viceversa).

It's made to be the easiest possible solution to handle the types we need for our save file and dialogue system. As such, only `bool`, `int`, `float` and `string` are supported.

Also, the save file is meant to be relatively small (in the range of a few MBs at most), being the reason streams are currently not used.

### Objective
Since this is originally meant to be used in a game, it's designed so it does the most amount of "slow" work possible in the same time frame (a loading screen).

`CachedProperties` exposes `_cachedProperties` to allow integration with our dialogue system.

### How to use
All you need to do is define a new class to hold your data. 

To all properties you want to serialize/deserialize, simply attach the `[HexOrder(int)]` attribute. Unmarked properties are ignored.

Example:
```cs
public sealed class Data
{
  [HexOrder(1)]
  public int SomeIntValue { get; private set; } = 100;

  public float SomeFloatValue { get; private set; }  // <--- This will get ignored

  [HexOrder(2)]
  public bool SomeBoolValue { get; private set; } = true;
}
```
Output:

<img width="722" height="87" alt="image" src="https://github.com/user-attachments/assets/c69f8bb8-4d3d-4224-ba11-eae2fc54d846" />

<img width="1417" height="99" alt="image" src="https://github.com/user-attachments/assets/aab4a43a-2f30-4657-a926-ce40a8e7650d" />

### Some suggestions
The best way to work in a group using this, is to assign each person a range of indexes they can use so you don't overwrite one another. That's the way we split gameplay and dialogue variables.
