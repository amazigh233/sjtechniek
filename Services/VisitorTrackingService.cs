using System.Collections.Concurrent;

namespace SjTechniek.Services;

public class VisitorVisit
{
    public string SessionId { get; set; } = "";
    public string Page { get; set; } = "/";
    public string IpAddress { get; set; } = "";
    public DateTime LastSeen { get; set; }
    public DateTime FirstSeen { get; set; }
}

public class PageViewStat
{
    public string Page { get; set; } = "";
    public int Views { get; set; }
}

public class VisitorTrackingService
{
    private readonly ConcurrentDictionary<string, VisitorVisit> _sessions = new();
    private readonly ConcurrentDictionary<string, int> _pageViews = new();
    private readonly List<(DateTime Time, string Page, string Ip)> _recentActivity = new();
    private int _todayVisitors = 0;
    private DateTime _lastReset = DateTime.Today;
    private readonly object _activityLock = new();

    public void TrackVisit(string sessionId, string page, string ip)
    {
        CheckDayReset();

        bool isNew = !_sessions.ContainsKey(sessionId);
        var firstSeen = isNew ? DateTime.Now : _sessions[sessionId].FirstSeen;

        _sessions[sessionId] = new VisitorVisit
        {
            SessionId = sessionId,
            Page = page,
            IpAddress = ip,
            LastSeen = DateTime.Now,
            FirstSeen = firstSeen
        };

        if (isNew) Interlocked.Increment(ref _todayVisitors);

        _pageViews.AddOrUpdate(page, 1, (_, v) => v + 1);

        lock (_activityLock)
        {
            _recentActivity.Insert(0, (DateTime.Now, page, ip));
            if (_recentActivity.Count > 100) _recentActivity.RemoveAt(_recentActivity.Count - 1);
        }
    }

    public int GetActiveVisitorCount()
    {
        var threshold = DateTime.Now.AddMinutes(-30);
        return _sessions.Values.Count(v => v.LastSeen > threshold);
    }

    public IReadOnlyList<VisitorVisit> GetActiveVisitors()
    {
        var threshold = DateTime.Now.AddMinutes(-30);
        return _sessions.Values
            .Where(v => v.LastSeen > threshold)
            .OrderByDescending(v => v.LastSeen)
            .ToList();
    }

    public int GetTodayVisitorCount() => _todayVisitors;

    public IReadOnlyList<PageViewStat> GetPageViewStats() =>
        _pageViews.Select(kv => new PageViewStat { Page = kv.Key, Views = kv.Value })
            .OrderByDescending(s => s.Views)
            .ToList();

    public IReadOnlyList<(DateTime Time, string Page, string Ip)> GetRecentActivity()
    {
        lock (_activityLock) { return _recentActivity.Take(20).ToList(); }
    }

    private void CheckDayReset()
    {
        if (DateTime.Today <= _lastReset) return;
        _lastReset = DateTime.Today;
        Interlocked.Exchange(ref _todayVisitors, 0);
    }
}
