using System.Collections.Concurrent;
using BrainBackend.Models;

namespace BrainBackend.Services;

/// <summary>
/// In-memory LRU cache cho AnalysisResult theo FEN hash.
/// Max 500 entries, TTL 5 phút.
/// </summary>
public class FenCacheService
{
    private readonly int _maxSize;
    private readonly TimeSpan _ttl;

    private readonly record struct CacheEntry(AnalysisResult Result, DateTime ExpiresAt);
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private readonly LinkedList<string> _lruList = new();
    private readonly Dictionary<string, LinkedListNode<string>> _nodeMap = new();
    private readonly object _cacheLock = new();

    public FenCacheService(IConfiguration config)
    {
        _maxSize = config.GetValue("Cache:MaxSize", 500);
        _ttl = TimeSpan.FromMinutes(config.GetValue("Cache:TtlMinutes", 5));
    }

    public bool TryGet(string fenHash, out AnalysisResult? result)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(fenHash, out var entry))
            {
                if (entry.ExpiresAt > DateTime.UtcNow)
                {
                    // LRU: move to front
                    _lruList.Remove(_nodeMap[fenHash]);
                    _nodeMap[fenHash] = _lruList.AddFirst(fenHash);
                    result = entry.Result;
                    return true;
                }
                // Expired — remove
                Evict(fenHash);
            }
        }
        result = null;
        return false;
    }

    public void Set(string fenHash, AnalysisResult result)
    {
        lock (_cacheLock)
        {
            if (_cache.ContainsKey(fenHash))
            {
                _lruList.Remove(_nodeMap[fenHash]);
                _nodeMap.Remove(fenHash);
            }
            else if (_cache.Count >= _maxSize)
            {
                // Evict LRU (tail)
                var lruKey = _lruList.Last!.Value;
                Evict(lruKey);
            }

            var node = _lruList.AddFirst(fenHash);
            _nodeMap[fenHash] = node;
            _cache[fenHash] = new CacheEntry(result, DateTime.UtcNow.Add(_ttl));
        }
    }

    private void Evict(string key)
    {
        if (_nodeMap.TryGetValue(key, out var node))
        {
            _lruList.Remove(node);
            _nodeMap.Remove(key);
        }
        _cache.Remove(key);
    }

    public int Count
    {
        get { lock (_cacheLock) return _cache.Count; }
    }

    /// <summary>Clear all cache entries (called on new game).</summary>
    public void Clear()
    {
        lock (_cacheLock)
        {
            _cache.Clear();
            _lruList.Clear();
            _nodeMap.Clear();
        }
    }
}
