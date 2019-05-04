<Query Kind="Program">
  <NuGetReference>Markdig</NuGetReference>
  <Namespace>Markdig</Namespace>
  <Namespace>Markdig.Syntax</Namespace>
  <Namespace>Markdig.Syntax.Inlines</Namespace>
</Query>

// This is can be run in LINQPad ( http://www.linqpad.net/ ) in C# Program mode.
// It uses the NuGet feature that requires a Developer or Premium license.
// Alternatively, it could be translated to a console program easily enough.

private static readonly string EmojiRootDirectory = Path.Combine(Path.GetDirectoryName(Util.CurrentQueryPath), "../Emoji");
private const string DocumentationFilename = "README.md";
private static readonly IReadOnlyCollection<string> EmojiPatterns = new[] { "*.png", "*.gif", "*.jpg", "*.jpeg" };

public void Main() {
    foreach (string subdirectoryName in Directory.EnumerateDirectories(EmojiRootDirectory, "*", SearchOption.AllDirectories)) {
        string documentationFilePath = Path.Combine(subdirectoryName, DocumentationFilename);
        (bool table, ISet<string> referenced) = Extract(documentationFilePath);
        ISet<string> existent = EmojiPatterns.SelectMany(s => Directory.EnumerateFiles(subdirectoryName, s))
                                             .Select(Path.GetFileName)
                                             .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        ISet<string> missingFromDocumentation = new HashSet<string>(existent);
        missingFromDocumentation.ExceptWith(referenced);
        AppendMissing(documentationFilePath, table,  missingFromDocumentation);
        ISet<string> missingFromSubdirectory = new HashSet<string>(referenced);
        missingFromSubdirectory.ExceptWith(existent);
        DumpMissing(documentationFilePath, missingFromSubdirectory);
    }
}


private static (bool Table, ISet<string> ReferencedImages) Extract(string filename) {
    if (File.Exists(filename)) {
        string raw = File.ReadAllText(filename);
        MarkdownDocument markdown = Markdown.Parse(raw);
        ISet<string> referencedImages = markdown.Descendants<ParagraphBlock>()
                                                .SelectMany(pb => pb.Inline.Descendants<LinkInline>())
                                                .Where(li => li.IsImage)
                                                .Select(li => li.Url)
                                                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        return (raw.Contains('|'), referencedImages);
    }
    else {
        return (true, new HashSet<string>(0));
    }
}


private static void AppendMissing(string documentationFilePath, bool table, ICollection<string> missing) {
    if (!missing.Any())
        return;
    string addendum = missing.Aggregate((new StringBuilder()).AppendLine()
                                                             .AppendLine(table ? "Emoji|Notes" : "# Unclassified")
                                                             .AppendLine(table ? "-----|-----" : string.Empty),
                                        (sb, s) => sb.Append("![")
                                                     .Append(GetFriendlyName(s))
                                                     .Append("](")
                                                     .Append(s)
                                                     .AppendLine(table ? ")|TODO" :  ")"),
                                        sb => sb.ToString());
    File.AppendAllText(documentationFilePath, addendum, Encoding.UTF8);
}


private void DumpMissing(string documentationFilePath, ICollection<string> missingFromSubdirectory) {
    foreach (string missingFilename in missingFromSubdirectory)
        $"{documentationFilePath} refers to {missingFilename}, which doesn't exist in that directory".Dump();
}


private static string GetFriendlyName(string filename) {
    bool capitalize = true;
    char GetFriendlyCharacter(char c) {
        if (c == '_') {
            capitalize = true;
            return ' ';
        }
        c = capitalize ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c);
        capitalize = false;
        return c;
    }
    return new string(Path.GetFileNameWithoutExtension(filename).Select(GetFriendlyCharacter).ToArray());
}