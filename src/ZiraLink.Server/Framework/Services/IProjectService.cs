﻿using ZiraLink.Server.Models;

namespace ZiraLink.Server.Framework.Services
{
    public interface IProjectService
    {
        Project GetByHost(string host);
        Task InitializeAsync(CancellationToken cancellationToken);
    }
}
