using System.Text.Json;

namespace ILSight;

/// <summary>Reads the value of a key at the top of a theme file, the reader on that value, which it must
/// move past.</summary>
public delegate void TopLevelKeyReader(ref Utf8JsonReader reader, string key, int line);
