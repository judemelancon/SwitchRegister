<Query Kind="Expression" />

// This is can be run in LINQPad ( http://www.linqpad.net/ ) in C# Expression mode.
// Alternatively, it could be translated to a console program easily enough.
Enumerable.SelectMany(new[] { "*.png", "*.gif", "*.jpg", "*.jpeg" },
                      s => Directory.EnumerateFiles(Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), ".."), s, SearchOption.AllDirectories))
          .Select(Path.GetFileNameWithoutExtension)
          .ToHashSet(StringComparer.InvariantCultureIgnoreCase)
          .Count
