using System.Net.WebSockets;
using Microsoft.Extensions.Caching.Memory;
using ZiraLink.Server.Framework.Services;
using ZiraLink.Server.Models;

namespace ZiraLink.Server.Services
{
    public class Cache : ICache
    {
        private readonly IMemoryCache _memoryCache;

        public Cache(IMemoryCache memoryCache) => _memoryCache = memoryCache;

        public Project SetProject(string host, Project value) => _memoryCache.Set($"project:{host}", value);
        public bool TryGetProject(string host, out Project value) => _memoryCache.TryGetValue($"project:{host}", out value);
        public AppProject SetAppProject(string username, int port, AppProject value) => _memoryCache.Set($"appProject:username:{username}:port:{port}", value);
        public bool TryGetAppProject(string username, int port, out AppProject value) => _memoryCache.TryGetValue($"appProject:username:{username}:port:{port}", out value);
        public AppProject SetAppProject(Guid viewId, AppProject value) => _memoryCache.Set($"appProject:viewId:{viewId}", value);
        public bool TryGetAppProject(Guid viewId, out AppProject value) => _memoryCache.TryGetValue($"appProject:viewId:{viewId}", out value);
        public IWebSocket SetWebSocket(string host, IWebSocket value) => _memoryCache.Set($"ws:{host}", value);
        public bool TryGetWebSocket(string host, out IWebSocket value) => _memoryCache.TryGetValue($"ws:{host}", out value);
        public void RemoveWebSocket(string host) => _memoryCache.Remove($"ws:{host}");
    }
}
