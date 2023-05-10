﻿using System.Collections.Generic;
using System.Net;
using CYQ.Data.Cache;

namespace System.Web
{
    /// <summary>
    /// 分布式Session
    /// </summary>
    public class HttpSessionState
    {
        public static readonly HttpSessionState Instance = new HttpSessionState();

        protected Microsoft.AspNetCore.Http.HttpContext context
        {
            get
            {
                return HttpContext.contextAccessor.HttpContext;
            }
        }

        internal HttpSessionState()
        {
            Timeout = 20;
        }
        /// <summary>
        /// 超时时间，默认20分钟
        /// </summary>
        public int Timeout { get; set; }
        List<string> keys = new List<string>();
        CacheManage cache = CacheManage.Instance;
        public string SessionID
        {
            get
            {
                if (context.Items.ContainsKey("HttpSessionID"))
                {
                    return context.Items["HttpSessionID"] as String;
                }
                string sessionID = context.Request.Cookies["CYQ.SessionID"];
                if (string.IsNullOrEmpty(sessionID))
                {
                    sessionID = DateTime.Now.ToString("HHmmss") + Guid.NewGuid().GetHashCode();
                    context.Response.Cookies.Append("CYQ.SessionID", sessionID);
                }
                context.Items.Add("HttpSessionID", sessionID);
                return sessionID;
            }
        }

        private string GetName(string name)
        {
            return name + "_" + SessionID;
        }

        public object this[int index]
        {
            get
            {
                if (index < keys.Count)
                {
                    return this[keys[index]];
                }
                return null;
            }
            set
            {
                if (index < keys.Count)
                {
                    this[keys[index]] = value;
                }
            }
        }

        public object this[string name]
        {
            get
            {
                return cache.Get(GetName(name));
            }
            set
            {
                Add(name, value);
            }
        }
        public void Add(string name, object value)
        {
            cache.Set(GetName(name), value, Timeout);
            if (keys.Contains(name))
            {
                keys.Add(name);
            }
        }
        public void Remove(string key)
        {
            cache.Remove(GetName(key));
        }
        public bool IsAvailable => true;

        public IEnumerable<string> Keys
        {
            get
            {
                return keys;
            }
        }

        /// <summary>
        /// 清空当前会话的所有数据。
        /// </summary>
        public void Clear()
        {
            foreach (string key in keys)
            {
                Remove(key);
            }
        }
    }
}