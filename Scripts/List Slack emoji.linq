<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Net.Http.dll</Reference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Converters</Namespace>
  <Namespace>Newtonsoft.Json.Serialization</Namespace>
  <Namespace>System.Collections.ObjectModel</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Net.Http</Namespace>
  <Namespace>System.Net.Http.Headers</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

// This is can be run in LINQPad ( http://www.linqpad.net/ ) in C# Program mode.
// It uses the NuGet feature, which requires a Developer or Premium license.
// Alternatively, it could be translated to a console program easily enough.

private const string SlackInstanceDomain = "FIXME.slack.com";

// Additional Configuration:
private const int PageSize = 500;
// Please don't set this to 0 and annoy Slack into making this task harder.
private static TimeSpan WaitAfterPage = new TimeSpan(0, 0, 0, 1, 500);
// End of Additional Configuration

// This seems to be the only cookie needed at present.
private static IReadOnlyCollection<string> CookieNames = new ReadOnlyCollection<string>(new[] { "d" });
private const HttpStatusCode TooManyRequests = (HttpStatusCode)429;
private static TimeSpan WaitAfterTooManyRequests = new TimeSpan(0, 0, 5, 0, 5);
private static readonly Uri TokenAddress = new Uri("https://" + SlackInstanceDomain + "/customize/emoji");
private static readonly Uri UploadAddress = new Uri("https://" + SlackInstanceDomain + "/api/emoji.adminList");
private static readonly Regex TokenPattern = new Regex(@"\s*""api_token"":\s*""(?<value>[a-z]+-[0-9]+-[0-9]+-[0-9]+-[0-9a-f]+)"",\s*",
                                                       RegexOptions.Compiled | RegexOptions.CultureInvariant);

private static readonly SemaphoreSlim RequestBottleneck = new SemaphoreSlim(1);

public static HttpClient Client { get; } = ConstructClient();
public static string Token { get; } = GetToken();

public static void Main() {
    IReadOnlyCollection<Emoji> emoji = Util.Cache(() => ListEmojiAsync().GetAwaiter().GetResult(), $"{nameof(emoji)} for {SlackInstanceDomain}", out bool fromCache);
    if (fromCache)
        "Found cached emoji".Dump();
    emoji.Count.Dump("Total");
    emoji.Count(e => e.IsAlias).Dump("Aliases");
    emoji.Count(e => e.CanDelete).Dump("Deletable");
    emoji.Where(e => e.IsBad).Dump("Bad Emoji");
    ILookup<string, Emoji> emojiByUser = emoji.ToLookup(e => e.UserDisplayName);
    emojiByUser.Select(ig => new { User = ig.Key, CreatedCount = ig.Count() }).Dump();
}


private static async Task<List<Emoji>> ListEmojiAsync() {
    EmojiPage firstPage = await GetEmojiPageAsync(1);
    firstPage.Dump(nameof(firstPage));
    IEnumerable<IReadOnlyCollection<Emoji>> remainingPages = await ListEmojiFromPagesAsync(2, firstPage.Paging.Pages);
    return firstPage.Emoji.Concat(remainingPages.SelectMany(iroce => iroce)).ToList();
}


private static Task<IReadOnlyCollection<Emoji>[]> ListEmojiFromPagesAsync(int startingPage, int endingPage)
    => Task.WhenAll(Enumerable.Range(startingPage, endingPage - startingPage + 1).Select(ListEmojiFromPageAsync));


private static async Task<IReadOnlyCollection<Emoji>> ListEmojiFromPageAsync(int pageNumber) => (await GetEmojiPageRateLimitedAsync(pageNumber)).Emoji;


private static async Task<EmojiPage> GetEmojiPageRateLimitedAsync(int pageNumber) {
    EmojiPage page;
    await RequestBottleneck.WaitAsync();
    try {
        page = await GetEmojiPageAsync(pageNumber);
        await Task.Delay(WaitAfterPage);
    }
    finally {
        RequestBottleneck.Release();
    }
    return page;
}


private static async Task<EmojiPage> GetEmojiPageAsync(int pageNumber, bool recursing = false) {
    HttpContent content = ConstructContent(pageNumber);
    HttpResponseMessage result = await Client.PostAsync(UploadAddress, content);
    switch (result.StatusCode) {
        case HttpStatusCode.OK:
            string body = await result.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<EmojiPage>(body);
        case TooManyRequests:
            if (recursing)
                throw new Exception("Failed again");
            $"Failed once; waiting for {WaitAfterTooManyRequests}...".Dump();
            await Task.Delay(WaitAfterTooManyRequests);
            $"Retrying...".Dump();
            return await GetEmojiPageAsync(pageNumber, recursing: true);
        default:
            throw new Exception($"Failed: {result.StatusCode}: {result.ReasonPhrase}");
    }
}


private static HttpContent ConstructContent(int pageNumber) {
    MultipartFormDataContent content = new MultipartFormDataContent();
    content.AddContentEntry("page", pageNumber.ToString());
    content.AddContentEntry("count", PageSize.ToString());
    content.AddContentEntry("token", Token);
    return content;
}


private static HttpClient ConstructClient() {
    if (SlackInstanceDomain.StartsWith("FIXME", StringComparison.InvariantCultureIgnoreCase)) {
        string message = $"{nameof(SlackInstanceDomain)} needs to be configured";
        Util.Highlight(message).Dump();
        throw new Exception(message);
    }

    HttpClientHandler handler = new HttpClientHandler() { CookieContainer = new CookieContainer() };

    if (!CookieNames.Any())
        Util.Highlight($"No cookies").Dump();
    foreach (string cookieName in CookieNames) {
        string cookieValue = Util.GetPassword($"{SlackInstanceDomain} Cookie {cookieName}");
        if (string.IsNullOrWhiteSpace(cookieValue))
            Util.Highlight($"Implausible cookie '{cookieName}'").Dump();
        handler.CookieContainer.Add(new Cookie(cookieName, cookieValue) { Domain = SlackInstanceDomain });
    }

    return new HttpClient(handler);
}


private static string GetToken() {
    string uploadPage = Client.GetStringAsync(TokenAddress).GetAwaiter().GetResult();
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


[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public sealed class EmojiPage {
    public bool OK { get; set; }
    public Emoji[] Emoji { get; set; }
    public Dictionary<string, Emoji> DisabledEmoji { get; set; }
    public int CustomEmojiTotalCount { get; set; }
    public EmojiPagingData Paging { get; set; }
}


[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
public sealed class EmojiPagingData {
    public int Count { get; set; }
    public int Total { get; set; }
    public int Page { get; set; }
    public int Pages { get; set; }
}


[JsonObject(NamingStrategyType = typeof(SnakeCaseNamingStrategy))]
[Serializable]
public sealed class Emoji {
    public string Name { get; set; }
    public bool IsAlias { get; set; }
    public string AliasFor { get; set; }
    public string Url { get; set; }
    [JsonConverter(typeof(UnixDateTimeConverter))]
    public DateTime Created { get; set; }
    public string TeamId { get; set; }
    public string UserId { get; set; }
    public string UserDisplayName { get; set; }
    public string AvatarHash { get; set; }
    public bool CanDelete { get; set; }
    public bool IsBad { get; set; }
    public string[] Synonyms { get; set; }
}
