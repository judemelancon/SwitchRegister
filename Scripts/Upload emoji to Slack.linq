<Query Kind="Program">
  <Output>DataGrids</Output>
  <Reference>&lt;RuntimeDirectory&gt;\System.Net.Http.dll</Reference>
  <NuGetReference>Nito.AsyncEx</NuGetReference>
  <Namespace>Nito.AsyncEx.Synchronous</Namespace>
  <Namespace>System.Collections.ObjectModel</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Net.Http.Headers</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
  <Namespace>System.Collections.Immutable</Namespace>
</Query>

// This is can be run in LINQPad ( http://www.linqpad.net/ ) in C# Program mode.
// It uses the NuGet feature that requires a Developer or Premium license.
// Alternatively, it could be translated to a console program easily enough.

// Primary Configuration:
private const string SlackInstanceDomain = "FIXME.slack.com";
private static IReadOnlyDictionary<string, string> Cookies = new Dictionary<string, string> {
                                                                                              // This seems to be the only cookie needed at present.
                                                                                              { "d", "FIXME" }
                                                                                            };
private static readonly string QueuedDirectoryName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Emoji", "To " + SlackInstanceDomain);
private static readonly string UploadedDirectoryName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Emoji", "Sent to " + SlackInstanceDomain);
// If this is false, images with names that are not valid for Slack emoji after lowercasing and dropping extensions are silently ignored.
private const bool ErrorForInvalidImageFilename = true;
private const bool DeleteEmptySubdirectories = true;
private const bool DeleteSourcingMarkdown = true;
private const bool MoveFullSizeEmoji = true;
// End of Primary Configuration

// Additional Configuration
private const int EmojiAnnouncementLimit = JumbojiLimit - 1;
private const string AddedEmojiName = "added_emoji";
private const string FullSizeEmojiSuffix = " - full";
private static readonly Regex FullSizeEmojiNamePattern = new Regex(@"[-_a-z0-9]+" + FullSizeEmojiSuffix, RegexOptions.Compiled);
// Please don't set this to 0 and annoy Slack into making this task harder.
private static TimeSpan WaitAfterUpload = new TimeSpan(0, 0, 0, 1, 125);
// End of Additional Configuration

// constants based on Slack's behavior and documentation
private const int JumbojiLimit = 23;
private const HttpStatusCode TooManyRequests = (HttpStatusCode)429;
private static TimeSpan WaitAfterTooManyRequests = new TimeSpan(0, 0, 5, 0, 5);
private static readonly Uri TokenAddress = new Uri("https://" + SlackInstanceDomain + "/customize/emoji");
private static readonly Uri UploadAddress = new Uri("https://" + SlackInstanceDomain + "/api/emoji.add");
private const string UploadSuccessResponse = "{\"ok\":true}";
private static readonly Regex EmojiNamePattern = new Regex(@"[-_a-z0-9]+", RegexOptions.Compiled);
private static readonly Regex TokenPattern = new Regex(@"\s*""api_token"":\s*""(?<value>xoxs-[0-9]+-[0-9]+-[0-9]+-[0-9a-f]+)"",\s*",
                                                       RegexOptions.Compiled | RegexOptions.CultureInvariant);

public static HttpClient Client { get; } = ConstructClient();
public static string Token { get; } = GetToken();

public static void Main() {
    Directory.CreateDirectory(QueuedDirectoryName);
    Directory.CreateDirectory(UploadedDirectoryName);

    IReadOnlyCollection<(string Name, string FilePath)> listed = ListAndDumpEmoji();
    if (listed == null)
        return;

    $"Uploading {listed.Count} emoji...".Dump();
    foreach ((string name, string filePath) in listed) {
        UploadAndRecordEmojiAsync(name, filePath).WaitAndUnwrapException();
    }

    if (DeleteSourcingMarkdown) {
        foreach (string sourcingMarkdownFilename in Directory.EnumerateFiles(QueuedDirectoryName, "Sourcing.md", SearchOption.AllDirectories))
            File.Delete(sourcingMarkdownFilename);
    }

    if (DeleteEmptySubdirectories) {
        foreach (string subdirectoryName in Directory.EnumerateDirectories(QueuedDirectoryName, "*", SearchOption.AllDirectories)
                                                     .OrderByDescending(s => s.Length)) {
            if (!Directory.EnumerateFileSystemEntries(subdirectoryName).Any())
                Directory.Delete(subdirectoryName);
        }
    }
}


/// <returns>null for problems</returns>
private static IReadOnlyCollection<(string Name, string FilePath)> ListAndDumpEmoji() {
    IReadOnlyCollection<(string Name, string FilePath)> listed = ListEmoji();

    if (listed == null)
        return null;

    foreach (IEnumerable<string> currentNames in listed.Select(t => t.Name)
                                                       .Chunk(EmojiAnnouncementLimit))
        (":" + AddedEmojiName + ": :" + string.Join(": :", currentNames) + ":").Dump();

    return listed;
}


/// <returns>null for problems</returns>
private static IReadOnlyCollection<(string Name, string FilePath)> ListEmoji() {
    // uniqueness check
    ILookup<string, string> found = Directory.EnumerateFiles(QueuedDirectoryName, "*.png", SearchOption.AllDirectories)
                                                         .Concat(Directory.EnumerateFiles(QueuedDirectoryName, "*.gif", SearchOption.AllDirectories))
                                                         .Concat(Directory.EnumerateFiles(QueuedDirectoryName, "*.jpg", SearchOption.AllDirectories))
                                                         .Concat(Directory.EnumerateFiles(QueuedDirectoryName, "*.jpeg", SearchOption.AllDirectories))
                                                         .ToLookup(s => Path.GetFileNameWithoutExtension(s).Trim().ToLowerInvariant());
    if (!found.Any()) {
        Util.Highlight("Found no emoji to upload").Dump();
        return null;
    }

    IReadOnlyList<string> duplicateNames = found.Where(ig => ig.Count() > 1)
                                                 .Select(ig => ig.Key)
                                                 .ToList();
    if (duplicateNames.Any()) {
        foreach (string duplicateName in duplicateNames)
            Util.Highlight(duplicateName + " duplicated: " + string.Join(", ", found[duplicateName].Select(s => $"'{s}'"))).Dump();
        return null;
    }

    if (ErrorForInvalidImageFilename) {
        IReadOnlyList<string> invalidNames = found.Select(kvp => kvp.Key)
                                                  .Where(s => !EmojiNamePattern.IsCoveringMatch(s))
                                                  .Where(s => !(MoveFullSizeEmoji && FullSizeEmojiNamePattern.IsCoveringMatch(s)))
                                                  .ToList();
        if (invalidNames.Any()) {
            Util.Highlight("Found invalid emoji name" + (invalidNames.Count != 1 ? "s" : string.Empty) + ": " + string.Join(", ", invalidNames.Select(s => $"\"{s}\""))).Dump();
            return null;
        }
    }

    return found.Where(kvp => EmojiNamePattern.IsCoveringMatch(kvp.Key))
                  .Select(kvp => (Name: kvp.Key, FilePath: kvp.Single()))
                  .OrderByDescending(t => t.Name == AddedEmojiName)
                  .ThenBy(t => t.FilePath)
                  .ToImmutableList();
}


private static async Task UploadAndRecordEmojiAsync(string name, string filePath) {
    string uploadedPath = Path.Combine(UploadedDirectoryName, Path.GetFileName(filePath));
    if (File.Exists(uploadedPath)) {
        Util.Highlight($"{name} skipped: existed in uploaded directory ({UploadedDirectoryName})").Dump();
        return;
    }

    byte[] emojiData = File.ReadAllBytes(filePath);
    bool uploaded = await UploadEmojiAsync(name, emojiData);
    await Task.Delay(WaitAfterUpload);
    if (!uploaded)
        return;

    File.Move(filePath, uploadedPath);
    if (MoveFullSizeEmoji) {
        string fullSizePath = Path.Combine(QueuedDirectoryName, Path.GetFileNameWithoutExtension(filePath) + FullSizeEmojiSuffix + Path.GetExtension(filePath));
        if (File.Exists(fullSizePath)) {
            string uploadedFullSizePath = Path.Combine(UploadedDirectoryName, Path.GetFileName(fullSizePath));
            if (File.Exists(uploadedFullSizePath)) {
                Util.Highlight($"Anomaly: '{uploadedFullSizePath}' exists when '{uploadedPath}' did not; '{fullSizePath}' is being left in place.").Dump();
            }
            else {
                File.Move(fullSizePath, uploadedFullSizePath);
            }
        }
    }
}


private static async Task<bool> UploadEmojiAsync(string name, byte[] emojiData, bool recursing = false) {
    HttpContent content = ConstructContent(name, emojiData);
    HttpResponseMessage result = await Client.PostAsync(UploadAddress, content).ConfigureAwait(false);
    switch (result.StatusCode) {
        case HttpStatusCode.OK:
            string body = result.Content.ReadAsStringAsync().Result;
            if (string.Equals(body, UploadSuccessResponse)) {
                name.Dump();
                return true;
            }
            Util.Highlight($"{name} failed: {body}").Dump();
            return false;
        case TooManyRequests:
            Util.Highlight($"{name} failed {(recursing ? "again" : "once")}: {result.StatusCode}: {result.ReasonPhrase}").Dump();
            $"Waiting for {WaitAfterTooManyRequests}...".Dump();
            await Task.Delay(WaitAfterTooManyRequests);
            if (recursing) {
                return false;
            }
            else {
                $"{name} retrying...".Dump();
                // one more try if this was the first
                return await UploadEmojiAsync(name, emojiData, recursing: true);
            }
        default:
            Util.Highlight($"{name} failed: {result.StatusCode}: {result.ReasonPhrase}").Dump();
            return false;
    }
}


private static HttpContent ConstructContent(string name, byte[] emojiData) {
    MultipartFormDataContent content = new MultipartFormDataContent();
    content.AddContentEntry("add", "1");
    content.AddContentEntry("token", Token);
    content.AddContentEntry("name", name.ToLowerInvariant());
    content.AddContentEntry("mode", "data");
    {
        ByteArrayContent imageContent = new ByteArrayContent(emojiData);
        imageContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data");
        imageContent.Headers.ContentDisposition.Name = "image";
        imageContent.Headers.ContentDisposition.FileName = name + ".png";
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        content.Add(imageContent);
    }
    return content;
}


private static HttpClient ConstructClient() {
    HttpClientHandler handler = new HttpClientHandler();
    handler.CookieContainer = new CookieContainer();
    if (!Cookies.Any())
        Util.Highlight($"No cookies").Dump();
    foreach (KeyValuePair<string, string> cookie in Cookies) {
        if (string.IsNullOrWhiteSpace(cookie.Value) || StringComparer.InvariantCultureIgnoreCase.Equals(cookie.Value, "FIXME") || StringComparer.InvariantCultureIgnoreCase.Equals(cookie.Value, "TODO"))
            Util.Highlight($"Suspect cookie '{cookie.Key}': '{cookie.Value}'").Dump();
        handler.CookieContainer.Add(new Cookie(cookie.Key, cookie.Value) { Domain = SlackInstanceDomain });
    }
    return new HttpClient(handler);
}


private static string GetToken() {
    string uploadPage = Client.GetStringAsync(TokenAddress).WaitAndUnwrapException();
    MatchCollection matches = TokenPattern.Matches(uploadPage);
    switch (matches.Count) {
        case 0:
            Util.Highlight($"Token pattern did not match").Dump();
            if (uploadPage.Contains("You need to sign in to see this page."))
                Util.Highlight("Attempt to retrieve the upload page appears to have received login page; are the cookies set correctly?").Dump();
            break;
        case 1:
            return matches.OfType<Match>()
                          .Single()
                          .Groups["value"]
                          .Value;
        default:
            Util.Highlight($"Token pattern matched {matches.Count} times").Dump();
            break;
    }
    // This can be helpful, but the menu bar is disorienting.
    //Util.RawHtml(uploadPage).Dump("Upload Page?");
    throw new Exception("Could not retrieve token");
}


public static class MultipartFormDataContentExtensions {
    public static void AddContentEntry(this MultipartFormDataContent self, string name, byte[] value) {
        ByteArrayContent entry = new ByteArrayContent(value);
        entry.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data");
        entry.Headers.ContentDisposition.Name = name;
        self.Add(entry);
    }

    public static void AddContentEntry(this MultipartFormDataContent self, string name, string value) => self.AddContentEntry(name, Encoding.UTF8.GetBytes(value));
}


public static class EnumerableExtensions {
    public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> self, int chunkSize) {
        // The specification says that repeated calls to MoveNext() after it returns false are legal.

        IEnumerable<T> TakeChunk(IEnumerator<T> enumerator) {
            int count = 0;
            do {
                yield return enumerator.Current;
            } while (++count < chunkSize && enumerator.MoveNext());
        }

        if (self == null)
            throw new ArgumentNullException(nameof(self));
        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize));

        using (IEnumerator<T> enumerator = self.GetEnumerator()) {
            while (enumerator.MoveNext())
                yield return TakeChunk(enumerator);
        }
    }
}


public static class RegexExtensions {
    public static bool IsCoveringMatch(this Regex self, string input) {
        if (self == null)
            throw new ArgumentNullException(nameof(self));
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        return self.Match(input).Length == input.Length;
    }
}