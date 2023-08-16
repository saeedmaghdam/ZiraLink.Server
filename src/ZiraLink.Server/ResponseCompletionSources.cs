﻿using System.Collections.Concurrent;

namespace ZiraLink.Server
{
    public class ResponseCompletionSources
    {
        private ConcurrentDictionary<string, TaskCompletionSource<HttpResponseModel>> _responseCompletionSources;

        public ConcurrentDictionary<string, TaskCompletionSource<HttpResponseModel>> Sources { get { return _responseCompletionSources; } private set { _responseCompletionSources = value; } }

        public ResponseCompletionSources()
        {
            _responseCompletionSources = new ConcurrentDictionary<string, TaskCompletionSource<HttpResponseModel>>();
        }
    }
}
